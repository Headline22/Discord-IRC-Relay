﻿using System;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Discord;
using Discord.Commands;
using Discord.Net.Providers.WS4Net;
using Discord.WebSocket;

using IRCRelay.Logs;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Collections.Generic;

namespace IRCRelay
{
    class Discord : IDisposable
    {
        private Session session;

        private DiscordSocketClient client;
        private CommandService commands;
        private IServiceProvider services;
        private dynamic config;

        public DiscordSocketClient Client { get => client; }

        public Discord(dynamic config, Session session)
        {
            this.config = config;
            this.session = session;

            var socketConfig = new DiscordSocketConfig
            {
                WebSocketProvider = WS4NetProvider.Instance,
                LogLevel = LogSeverity.Verbose
            };

            client = new DiscordSocketClient(socketConfig);
            commands = new CommandService();

            client.Log += Log;

            services = new ServiceCollection().BuildServiceProvider();

            client.MessageReceived += OnDiscordMessage;
            client.Disconnected += OnDiscordDisconnect;
        }

        public async Task SpawnBot()
        {
            await client.LoginAsync(TokenType.Bot, config.DiscordBotToken);
            await client.StartAsync();
        }

        /* When we disconnect from discord (we got booted off), we'll remake */
        public async Task OnDiscordDisconnect(Exception ex)
        {
            /* Create a new thread to kill the session. We cannot block
             * this Disconnect call */
            new System.Threading.Thread(() => { session.Kill(); }).Start();

            await Log(new LogMessage(LogSeverity.Critical, "OnDiscordDisconnect", ex.Message));
        }

        public async Task OnDiscordMessage(SocketMessage messageParam)
        {
            string url = "";
            if (!(messageParam is SocketUserMessage message)) return;

            if (message.Author.Id == client.CurrentUser.Id) return; // block self

            if (!messageParam.Channel.Name.Contains(config.DiscordChannelName)) return; // only relay trough specified channels

            if (config.DiscordUserIDBlacklist != null) //bcompat support
            {
                /**
                 * We'll loop blacklisted user ids. If the user ID is found,
                 * then we return out and prevent the call
                 */
                foreach (string id in config.DiscordUserIDBlacklist)
                {
                    if (message.Author.Id == ulong.Parse(id))
                    {
                        return;
                    }
                }
            }

            string formatted = messageParam.Content;
            string text = "```";
            if (formatted.Contains(text))
            {
                int start = formatted.IndexOf(text, StringComparison.CurrentCulture);
                int end = formatted.IndexOf(text, start + text.Length, StringComparison.CurrentCulture);

                string code = formatted.Substring(start + text.Length, (end - start) - text.Length);

                url = UploadMarkDown(code);

                formatted = formatted.Remove(start, (end - start) + text.Length);
            }

            /* Santize discord-specific notation to human readable things */
            formatted = MentionToUsername(formatted, message);
            formatted = EmojiToName(formatted, message);
            formatted = ChannelMentionToName(formatted, message);
            formatted = Unescape(formatted);

            if (config.SpamFilter != null) //bcompat for older configurations
            {
                foreach (string badstr in config.SpamFilter)
                {
                    if (formatted.ToLower().Contains(badstr.ToLower()))
                    {
                        await messageParam.Channel.SendMessageAsync(messageParam.Author.Mention + ": Message with blacklisted input will not be relayed!");
                        await messageParam.DeleteAsync();
                        return;
                    }
                }
            }

            // Send IRC Message
            if (formatted.Length > 1000)
            {
                await messageParam.Channel.SendMessageAsync(messageParam.Author.Mention + ": messages > 1000 characters cannot be successfully transmitted to IRC!");
                await messageParam.DeleteAsync();
                return;
            }

            string[] parts = formatted.Split('\n');

            if (parts.Length > 3) // don't spam IRC, please.
            {
                await messageParam.Channel.SendMessageAsync(messageParam.Author.Mention + ": Too many lines! If you're meaning to post" +
                    " code blocks, please use \\`\\`\\` to open & close the codeblock." +
                    "\nYour message has been deleted and was not relayed to IRC. Please try again.");
                await messageParam.DeleteAsync();

                await messageParam.Author.SendMessageAsync("To prevent you from having to re-type your message,"
                    + " here's what you tried to send: \n ```"
                    + messageParam.Content
                    + "```");

                return;
            }

            if (config.IRCLogMessages)
                LogManager.WriteLog(MsgSendType.DiscordToIRC, messageParam.Author.Username, formatted, "log.txt");

            foreach (var attachment in message.Attachments)
            {
                session.SendMessage(Session.MessageDestination.IRC, attachment.Url, messageParam.Author.Username);
            }

            foreach (String part in parts) // we're going to send each line indpependently instead of letting irc clients handle it.
            {
                if (part.Replace(" ", "").Replace("\n", "").Replace("\t", "").Length != 0) // if the string is not empty or just spaces
                {
                    session.SendMessage(Session.MessageDestination.IRC, part, messageParam.Author.Username);
                }
            }

            if (!url.Equals("")) // hastebin upload is succesfuly if url contains any data
            {
                if (config.IRCLogMessages)
                    LogManager.WriteLog(MsgSendType.DiscordToIRC, messageParam.Author.Username, url, "log.txt");

                session.SendMessage(Session.MessageDestination.IRC, url, messageParam.Author.Username);
            }
        }

        public Task Log(LogMessage msg)
        {
            return Task.Run(() => Console.WriteLine(msg.ToString()));
        }

        public void Dispose()
        {
            client.Dispose();
        }

        /**     Helper methods      **/

        public static string UploadMarkDown(string input)
        {
            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.ContentType] = "text/plain";

                var response = client.UploadString("https://hastebin.com/documents", input);
                JObject obj = JObject.Parse(response);

                if (!obj.HasValues)
                {
                    return "";
                }

                string key = (string)obj["key"];
                string hasteUrl = "https://hastebin.com/" + key + ".cs";

                return hasteUrl;
            }
        }
        public static string MentionToUsername(string input, SocketUserMessage message)
        {
            Regex regex = new Regex("<@!?([0-9]+)>"); // create patern

            var m = regex.Matches(input); // find all matches
            var itRegex = m.GetEnumerator(); // lets iterate matches
            var itUsers = message.MentionedUsers.GetEnumerator(); // iterate mentions, too
            int difference = 0; // will explain later
            while (itUsers.MoveNext() && itRegex.MoveNext()) // we'll loop iterators together
            {
                var match = (Match)itRegex.Current; // C# makes us cast here.. gross
                var user = itUsers.Current;
                int len = match.Length;
                int start = match.Index;
                string removal = input.Substring(start - difference, len); // seperate what we're trying to replace

                /**
                * Since we're replacing `input` after every iteration, we have to
                * store the difference in length after our edits. This is because that
                * the Match object is going to use lengths from before the replacments
                * occured. Thus, we add the length and then subtract after the replace
                */
                difference += input.Length;
                input = ReplaceFirst(input, removal, user.Username);
                difference -= input.Length;
            }

            return input;
        }

        public static string Unescape(string input)
        {
            /* Main StringBuilder for messages that aren't in '`' */
            StringBuilder sb = new StringBuilder();

            /*
            * locations - List of indices where the first '`' lies
            * peices - List of strings which live inbetween the '`'s
            */
            List<int> locations = new List<int>();
            List<StringBuilder> peices = new List<StringBuilder>();
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '`') // we hit a '`'
                {
                    int j;

                    StringBuilder slice = new StringBuilder(); // used for capturing the str inbetween '`'
                    slice.Append('`'); // append the '`' for insertion later

                    /* we'll loop from here until we encounter the next '`',
                    * appending as we go.
                    */
                    for (j = i + 1; j < input.Length && input[j] != '`'; j++)
                    {
                        slice.Append(input[j]);
                    }

                    if (j < input.Length)
                        slice.Append('`'); // append the '`' for insertion later

                    locations.Add(i); // push the index of the first '`'
                    peices.Add(slice); // push the captured string

                    i = j; // advance the outer loop to where our inner one stopped
                }
                else // we didn't hit a '`', so just append :)
                {
                    sb.Append(input[i]);
                }
            }

            // From here we prep the return string by doing our regex on the input that's not in '`'
            string retstr = Regex.Replace(sb.ToString(), @"\\([^A-Za-z0-9])", "$1");

            // Now we'll just loop the peices, inserting @ the locations we saved earlier
            for (int i = 0; i < peices.Count; i++)
            {
                retstr = retstr.Insert(locations[i], peices[i].ToString());
            }

            return retstr; // thank fuck we're done
        }

        public static string ChannelMentionToName(string input, SocketUserMessage message)
        {
            Regex regex = new Regex("<#([0-9]+)>"); // create patern

            var m = regex.Matches(input); // find all matches
            var itRegex = m.GetEnumerator(); // lets iterate matches
            var itChan = message.MentionedChannels.GetEnumerator(); // iterate mentions, too
            int difference = 0; // will explain later
            while (itChan.MoveNext() && itRegex.MoveNext()) // we'll loop iterators together
            {
                var match = (Match)itRegex.Current; // C# makes us cast here.. gross
                var channel = itChan.Current;
                int len = match.Length;
                int start = match.Index;
                string removal = input.Substring(start - difference, len); // seperate what we're trying to replace

                /**
                * Since we're replacing `input` after every iteration, we have to
                * store the difference in length after our edits. This is because that
                * the Match object is going to use lengths from before the replacments
                * occured. Thus, we add the length and then subtract after the replace
                */
                difference += input.Length;
                input = ReplaceFirst(input, removal, "#" + channel.Name);
                difference -= input.Length;
            }

            return input;
        }

        public static string ReplaceFirst(string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }

        // Converts <:emoji:23598052306> to :emoji:
        public static string EmojiToName(string input, SocketUserMessage message)
        {
            string returnString = input;

            Regex regex = new Regex("<[A-Za-z0-9-_]?:[A-Za-z0-9-_]+:[0-9]+>");
            Match match = regex.Match(input);
            if (match.Success) // contains a emoji
            {
                string substring = input.Substring(match.Index, match.Length);
                string[] sections = substring.Split(':');

                returnString = input.Replace(substring, ":" + sections[1] + ":");
            }

            return returnString;
        }

        public void SendMessageAllToTarget(string targetGuild, string message, string targetChannel)
        {
            foreach (SocketGuild guild in Client.Guilds) // loop through each discord guild
            {
                if (guild.Name.ToLower().Contains(targetGuild.ToLower())) // find target 
                {
                    SocketTextChannel channel = FindChannel(guild, targetChannel); // find desired channel

                    if (channel != null) // target exists
                    {
                        channel.SendMessageAsync(message);
                    }
                }
            }
        }

        public static SocketTextChannel FindChannel(SocketGuild guild, string text)
        {
            foreach (SocketTextChannel channel in guild.TextChannels)
            {
                if (channel.Name.Contains(text))
                {
                    return channel;
                }
            }

            return null;
        }
    }
}

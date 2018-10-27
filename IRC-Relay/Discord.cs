/*  Discord IRC Relay - A Discord & IRC bot that relays messages 
 *
 *  Copyright (C) 2018 Michael Flaherty // michaelwflaherty.com // michaelwflaherty@me.com
 * 
 * This program is free software: you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by the Free
 * Software Foundation, either version 3 of the License, or (at your option) 
 * any later version.
 *
 * This program is distributed in the hope that it will be useful, but WITHOUT 
 * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS 
 * FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License along with 
 * this program. If not, see http://www.gnu.org/licenses/.
 */

using System;
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
using System.Net.Http;

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
                LogLevel = LogSeverity.Critical
            };

            client = new DiscordSocketClient(socketConfig);
            commands = new CommandService();

            client.Log += Log;

            services = new ServiceCollection().BuildServiceProvider();

            client.MessageReceived += OnDiscordMessage;
            client.Connected += OnDiscordConnected;
            client.Disconnected += OnDiscordDisconnect;
        }

        public async Task SpawnBot()
        {
            await client.LoginAsync(TokenType.Bot, config.DiscordBotToken);
            await client.StartAsync();
        }

        public async Task OnDiscordConnected()
        {
            await Discord.Log(new LogMessage(LogSeverity.Critical, "DiscSpawn", "Discord bot initialized."));
        }

        /* When we disconnect from discord (we got booted off), we'll remake */
        public async Task OnDiscordDisconnect(Exception ex)
        {
            /* Create a new thread to kill the session. We cannot block
             * this Disconnect call */
            new System.Threading.Thread(async() => { await session.Kill(Session.TargetBot.Discord); }).Start();

            await Log(new LogMessage(LogSeverity.Critical, "OnDiscordDisconnect", ex.Message));
        }

        public void Kill()
        {
            try
            {
                this.Dispose();
            }
            catch { }
        }

        public async Task OnDiscordMessage(SocketMessage messageParam)
        {
            if (!(messageParam is SocketUserMessage message)) return;

            if (message.Author.Id == client.CurrentUser.Id) return; // block self

            if (!messageParam.Channel.Name.Contains(config.DiscordChannelName)) return; // only relay trough specified channels
            if (messageParam.Content.Contains("__NEVER_BE_SENT_PLEASE")) return; // don't break me

            if (Program.HasMember(config, "DiscordUserIDBlacklist")) //bcompat for older configurations
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

            /* Santize discord-specific notation to human readable things */
            string formatted = RemoveCodeblock(messageParam.Content, message, out string code);
            formatted = MentionToNickname(formatted, message);
            formatted = EmojiToName(formatted, message);
            formatted = ChannelMentionToName(formatted, message);
            formatted = Unescape(formatted);

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

            if (code.Length > 0)
            {
                if (Program.HasMember(config, "StikkedCreateUrlAndKey") && config.StikkedCreateUrlAndKey.Length > 0)
                    await DoStikkedUpload(code, message);
                else
                    DoHastebinUpload(code, message);
            }

            if (Program.HasMember(config, "SpamFilter")) //bcompat for older configurations
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

            string username = (messageParam.Author as SocketGuildUser)?.Nickname ?? message.Author.Username;
            if (config.IRCLogMessages)
                LogManager.WriteLog(MsgSendType.DiscordToIRC, username, formatted, "log.txt");

            foreach (var attachment in message.Attachments)
            {
                session.SendMessage(Session.TargetBot.IRC, attachment.Url, username);
            }

            foreach (String part in parts) // we're going to send each line indpependently instead of letting irc clients handle it.
            {
                if (part.Trim().Length != 0) // if the string is not empty or just spaces
                {
                    session.SendMessage(Session.TargetBot.IRC, part, username);
                }
            }
        }

        public static Task Log(LogMessage msg)
        {
            return Task.Run(() => {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(msg.ToString());
            });
        }

        public void Dispose()
        {
            client.Dispose();
        }

        /**     Helper methods      **/

        public string RemoveCodeblock(string input, SocketUserMessage msg, out string codeout)
        {
            codeout = "";
            string text = "```";
            if (input.Contains("```"))
            {
                int start = input.IndexOf(text, StringComparison.CurrentCulture);
                int end = input.IndexOf(text, start + text.Length, StringComparison.CurrentCulture);

                string code = input.Substring(start + text.Length, (end - start) - text.Length);
                codeout = code; //await DoStikkedUpload(code, msg);
                input = input.Remove(start, (end - start) + text.Length);
            }
            return input;
        }

        private async Task DoStikkedUpload(string input, SocketUserMessage msg)
        {
            string[] langs = { "cpp", "csharp", "c", "java", "php"}; // we'll only do a small subset
            string language = "";
            for (int i = 0; i < langs.Length && language.Length == 0; i++)
            {
                if (input.StartsWith(langs[i]))
                {
                    language = langs[i];
                    input = input.Remove(0, langs[i].Length);
                }
            }

            using (var client = new HttpClient())
            {
                string username = (msg.Author as SocketGuildUser)?.Nickname ?? msg.Author.Username;
                var values = new Dictionary<string, string>
                {
                    { "name", username },
                    { "text", input.Trim() },
                    { "title", "Automated discord upload" },
                    { "lang", language }
                };
                var content = new FormUrlEncodedContent(values);
                var response = await client.PostAsync(config.StikkedCreateUrlAndKey, content); // config.StikkedCreateUrlAndKey
                var url = await response.Content.ReadAsStringAsync();

                if (config.IRCLogMessages)
                    LogManager.WriteLog(MsgSendType.DiscordToIRC, msg.Author.Username, url, "log.txt");
                session.SendMessage(Session.TargetBot.IRC, url, msg.Author.Username);

            }
        }

        private void DoHastebinUpload(string input, SocketUserMessage msg)
        {
            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.ContentType] = "text/plain";
                client.UploadDataCompleted += Hastebin_UploadCompleted;
                client.UploadDataAsync(new Uri("https://hastebin.com/documents"), null, Encoding.ASCII.GetBytes(input), msg);
            }
        }

        private void Hastebin_UploadCompleted(object sender, UploadDataCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                Log(new LogMessage(LogSeverity.Critical, "HastebinUpload", e.Error.Message));
                return;
            }
            JObject obj = JObject.Parse(Encoding.UTF8.GetString(e.Result));

            if (obj.HasValues)
            {
                string key = (string)obj["key"];
                string result = "https://hastebin.com/" + key + ".cs";

                var msg = (SocketUserMessage)e.UserState;
                if (config.IRCLogMessages)
                    LogManager.WriteLog(MsgSendType.DiscordToIRC, msg.Author.Username, result, "log.txt");

                session.SendMessage(Session.TargetBot.IRC, result, msg.Author.Username);
            }
        }

        public static string MentionToNickname(string input, SocketUserMessage message)
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
                string username = ((user as SocketGuildUser)?.Nickname ?? user.Username);
                input = ReplaceFirst(input, removal, username);
                difference -= input.Length;
            }

            return input;
        }

        public static string Unescape(string input)
        {
            Regex reg = new Regex("\\`[^`]*\\`");

            int count = 0;
            List<string> peices = new List<string>();
            reg.Replace(input, (m) => {
                peices.Add(m.Value);
                input = input.Replace(m.Value, string.Format("__NEVER_BE_SENT_PLEASE_{0}_!@#%", count));
                count++;
                return ""; // doesn't matter what we replace with
            });

            string retstr = Regex.Replace(input, @"\\([^A-Za-z0-9])", "$1");

            // From here we prep the return string by doing our regex on the input that's not in '`'
            reg = new Regex("__NEVER_BE_SENT_PLEASE_([0-9]+)_!@#%");
            input = reg.Replace(retstr, (m) => {
                return peices[int.Parse(m.Result("$1"))].ToString();
            });

            return input; // thank fuck we're done
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

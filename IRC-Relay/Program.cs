using System;
using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;
using Discord.Commands;

using Microsoft.Extensions.DependencyInjection;

using IRCRelay.Logs;
using Discord.Net.Providers.WS4Net;
using JsonConfig;
using System.IO;

namespace IRCRelay
{
    class Program
    {
        public static Program Instance; //Entry to access DiscordSocketClient for Helpers.cs
        public DiscordSocketClient client;

        /* Instance Vars */
        private IRC irc;
        private CommandService commands;
        private IServiceProvider services;
        private static dynamic config;

        public static void Main(string[] args)
        {
            var stream = new StreamReader("settings.json");
            config = Config.ApplyJson(stream.ReadToEnd(), new ConfigObject());

            Instance = new Program();
                
            Instance.MainAsync().GetAwaiter().GetResult();
        }

        private async Task MainAsync()
        {
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

            await client.LoginAsync(TokenType.Bot, config.DiscordBotToken);
            await client.StartAsync();

            irc = new IRC(config.IRCServer,
                          config.IRCPort,
                          config.IRCNick,
                          config.IRCChannel,
                          config.IRCLoginName,
                          config.IRCAuthString,
                          config.IRCAuthUser,
                          config.DiscordGuildName,
                          config.DiscordChannelName,
                          config.IRCLogMessages);

            irc.SpawnBot();

            await Task.Delay(-1);
        }

        public async Task OnDiscordMessage(SocketMessage messageParam)
        {
            string url = "";
            var message = messageParam as SocketUserMessage;
            if (message == null) return;

            if (message.Author.Id == client.CurrentUser.Id) return; // block self

            if (!messageParam.Channel.Name.Contains(config.DiscordChannelName)) return; // only relay trough specified channels

            /* Santize discord-specific notation to human readable things */
            string formatted = Helpers.MentionToUsername(messageParam.Content, message);
            formatted = Helpers.EmojiToName(formatted, message);
            formatted = Helpers.ChannelMentionToName(formatted, message);
            formatted = Helpers.Unescape(formatted);

            string text = "```";
            if (formatted.Contains(text))
            {
                int start = formatted.IndexOf(text, StringComparison.CurrentCulture);
                int end = formatted.IndexOf(text, start + text.Length, StringComparison.CurrentCulture);

                string code = formatted.Substring(start + text.Length, (end - start) - text.Length);

                url = Helpers.UploadMarkDown(code);

                formatted = formatted.Remove(start, (end - start) + text.Length);
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
                    " code blocks, please use \\`\\`\\` to open & close the codeblock."  +
                    "\nYour message has been deleted and was not relayed to IRC. Please try again.");
                await messageParam.DeleteAsync();
                return;
            }

            if (config.IRCLogMessages)
                LogManager.WriteLog(MsgSendType.DiscordToIRC, messageParam.Author.Username, formatted, "log.txt");

            foreach (var attachment in message.Attachments)
            {
                irc.SendMessage(messageParam.Author.Username, attachment.Url);
            }

            foreach (String part in parts) // we're going to send each line indpependently instead of letting irc clients handle it.
            {
                if (part.Replace(" ", "").Replace("\n", "").Replace("\t", "").Length != 0) // if the string is not empty or just spaces
                {
                    irc.SendMessage(messageParam.Author.Username, part);
                }
            }

            if (!url.Equals("")) // hastebin upload is succesfuly if url contains any data
            {
                if (config.IRCLogMessages)
                    LogManager.WriteLog(MsgSendType.DiscordToIRC, messageParam.Author.Username, url, "log.txt");

                irc.SendMessage(messageParam.Author.Username, url);
            }
        }

        public Task Log(LogMessage msg)
        {
            return Task.Run(() => Console.WriteLine(msg.ToString()));
        }
    }
}

using System;
using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;
using Discord.Commands;

using Microsoft.Extensions.DependencyInjection;

using IRCRelay.Logs;
using IRCRelay.Settings;

namespace IRCRelay
{
    class Program
    {
        public static Program Instance; //Entry to access DiscordSocketClient for Helpers.cs
        public DiscordSocketClient client;

        /* Instance Vars */
        private IRC irc;
        private Settings.Config config;
        private CommandService commands;
        private IServiceProvider services;

        public static void Main(string[] args)
        {
            Instance = new Program();
                
            Instance.MainAsync().GetAwaiter().GetResult();
        }

        private async Task MainAsync()
        {
            try
            {
                config = Settings.Config.Load();
            }
            catch
            {
                Console.WriteLine("Unable to load config. Ensure Settings.xml is formatted correctly.");
                config = Settings.Config.CreateDefaultConfig();
                Settings.Config.Save(config);
                return;
            }

            client = new DiscordSocketClient();
            commands = new CommandService();

            client.Log += Log;

            services = new ServiceCollection().BuildServiceProvider();

            client.MessageReceived += OnDiscordMessage;

            await client.LoginAsync(TokenType.Bot, config.DiscordBotToken);
            await client.StartAsync();

            int.TryParse(config.IRCPort, out int port);
            irc = new IRC(config.IRCServer,
                          port,
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
                await messageParam.Channel.SendMessageAsync("Error: messages > 1000 characters cannot be sent!");
                return;
            }

            if (formatted.Replace(" ", "").Replace("\n", "").Replace("\t", "").Length != 0) // if the string is not empty or just spaces
            {
                if (config.IRCLogMessages)
                    LogManager.WriteLog(MsgSendType.DiscordToIRC, messageParam.Author.Username, formatted, "log.txt");

                string[] parts = formatted.Split('\n');

                foreach (String part in parts)
                {
                    irc.SendMessage("<" + messageParam.Author.Username + "> " + part);
                }
            }

            if (!url.Equals(""))
            {
                if (config.IRCLogMessages)
                    LogManager.WriteLog(MsgSendType.DiscordToIRC, messageParam.Author.Username, url, "log.txt");

                irc.SendMessage("<" + messageParam.Author.Username + "> " + url);
            }
        }

        public Task Log(LogMessage msg)
        {
            return Task.Run(() => Console.WriteLine(msg.ToString()));
        }
    }
}

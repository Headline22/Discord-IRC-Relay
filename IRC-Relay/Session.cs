using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace IRCRelay
{
    public class Session
    {
        public enum MessageDestination {
            Discord,
            IRC
        };

        private Discord discord;
        private IRC irc;
        private dynamic config;
        private bool alive;

        public bool IsAlive { get => alive; }

        public Session(dynamic config)
        {
            this.config = config;
            alive = true;
        }

        public void Kill()
        {
            discord.Client.Dispose();
            irc.Client.RfcQuit();

            this.alive = false;
        }

        public async Task StartSession()
        {
            this.discord = new Discord(config, this);
            this.irc = new IRC(config, this);

            await discord.SpawnBot();
            await irc.SpawnBot();
        }

        public void SendMessage(MessageDestination dest, string message, string username = "")
        {
            switch (dest)
            {
                case MessageDestination.Discord:
                    Helpers.SendMessageAllToTarget(config.DiscordGuildName, message, config.DiscordChannelName, discord);
                    break;
                case MessageDestination.IRC:
                    irc.SendMessage(username, message);
                    break;
            }
        }
    }
}

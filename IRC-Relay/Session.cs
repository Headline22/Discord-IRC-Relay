using System.Threading.Tasks;

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
        public IRC Irc { get => irc; }
        internal Discord Discord { get => discord; }

        public Session(dynamic config)
        {
            this.config = config;
            alive = true;
        }

        public void Kill()
        {
            discord.Dispose();
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
                    discord.SendMessageAllToTarget(config.DiscordGuildName, message, config.DiscordChannelName);
                    break;
                case MessageDestination.IRC:
                    irc.SendMessage(username, message);
                    break;
            }
        }
    }
}

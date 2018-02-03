using System.IO;
using System.Xml.Serialization;

namespace IRCRelay.Settings
{
    public class Config
    {
        // ---------------------------------------------------------------------------------------
        // Insert configurable values below:
        public string IRCServer;
        public string IRCPort;
        public string IRCChannel;
        public string IRCNick;

        public bool IRCLogMessages;

        public string IRCAuthUser;
        public string IRCLoginName;
        public string IRCAuthString;

        public string DiscordBotToken;
        public string DiscordGuildName;
        public string DiscordChannelName;
        // ---------------------------------------------------------------------------------------

        // Name of configuration file.
        [XmlIgnore]
        public const string FileName = "Settings.xml";

        // Empty constructor for XmlSerializer.
        public Config()
        {
        }

        // Used to load the default configuration if Load() fails.
        public static Config CreateDefaultConfig()
        {
            Config config = new Config()
            {
                IRCServer = "server",
                IRCPort = "port",
                IRCChannel = "#channel",
                IRCNick = "nick",

                IRCLoginName = "auth login name",
                IRCAuthString = "some command here",
                IRCAuthUser = "some user",

                DiscordBotToken = "token",
                DiscordGuildName = "server name",
                DiscordChannelName = "text channel"
            };

            return config;
        }

        // Loads the configuration from file.
        public static Config Load()
        {
            var serializer = new XmlSerializer(typeof(Config));

            using (var fStream = new FileStream(Config.FileName, FileMode.Open))
                return (Config)serializer.Deserialize(fStream);
        }

        // Saves the configuration to file.
        public static void Save(Config config)
        {
            var serializer = new XmlSerializer(typeof(Config));

            using (var fStream = new FileStream(Config.FileName, FileMode.Create))
                serializer.Serialize(fStream, config);
        }
    }
}
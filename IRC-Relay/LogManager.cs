using System;
using System.IO;
using System.Globalization;

namespace IRCRelay.Logs
{
    public enum MsgSendType
    {
        DiscordToIRC,
        IRCToDiscord
    };

    public class LogManager
    {
        public static void WriteLog(MsgSendType type, string name, string message, string filename)
        {
            string prefix;
            if (type == MsgSendType.DiscordToIRC)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                prefix = "Discord -> IRC";
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                prefix = "IRC -> Discord";
            }
            try
            {
                string date = "[" + DateTime.Now.ToString(new CultureInfo("en-US")) + "]";

                string logMessage = string.Format("{0} {1} <{2}> {3}", date, prefix, name, message);

                using (StreamWriter stream = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + filename, true))
                {
                    stream.WriteLine(logMessage);
                }

                Console.WriteLine(logMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error Writing To File: {0}", ex.Message);
            }
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}

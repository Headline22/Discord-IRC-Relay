using System;
using System.IO;

using System.Threading.Tasks;
using JsonConfig;

namespace IRCRelay
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var config = Config.ApplyJson(new StreamReader("settings.json").ReadToEnd(), new ConfigObject());

            StartSessions(config).GetAwaiter().GetResult();
        }

        private static async Task StartSessions(dynamic config)
        {
            Session session = new Session(config);
            do
            {
                await session.StartSession();
                Console.WriteLine("Session failure... New session starting.");
            } while (!session.IsAlive);   
        }
    }
}

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
using System.IO;

using System.Threading.Tasks;
using JsonConfig;

namespace IRCRelay
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.Title = "Discord IRC Relay (c) Michael Flaherty 2018";
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

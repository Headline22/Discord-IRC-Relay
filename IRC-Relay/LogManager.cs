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
            if (message.Trim().Length == 0)
                return;

            string prefix;
            if (type == MsgSendType.DiscordToIRC)
            {
                prefix = "[Discord]";
            }
            else
            {
                prefix = "[IRC]";
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(prefix);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(" <{0}>", name);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(" {0}", message);

            try
            {
                string date = "[" + DateTime.Now.ToString(new CultureInfo("en-US")) + "]";
                string logMessage = string.Format("{0} {1} <{2}> {3}", date, prefix, name, message);

                using (StreamWriter stream = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + filename, true))
                {
                    stream.WriteLine(logMessage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error Writing To File: {0}", ex.Message);
            }
        }
    }
}

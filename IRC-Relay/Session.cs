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

using System.Threading;
using System.Threading.Tasks;
using Discord;

namespace IRCRelay
{
    public class Session
    {
        public enum TargetBot {
            Discord,
            IRC,
            Both
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

        public async Task Kill(TargetBot bot)
        {
            switch (bot)
            {
                case TargetBot.Discord:
                    discord.Kill();
                    await Discord.Log(new LogMessage(LogSeverity.Critical, "KillSesh", "Discord connection closed."));
                    new Thread(async() =>
                    {
                        Thread.Sleep(System.TimeSpan.FromSeconds(1).Milliseconds);
                        this.discord = new Discord(config, this);
                        await discord.SpawnBot();
                    }).Start();
                    break;
                case TargetBot.IRC:
                    irc.Client.RfcQuit();
                    await irc.SpawnBot();
                    await Discord.Log(new LogMessage(LogSeverity.Critical, "KillSesh", "IRC connection closed."));
                    break;
                case TargetBot.Both: // if we kill both, let main loop recover
                    discord.Kill();
                    irc.Client.RfcQuit();
                    this.alive = false;
                    await Discord.Log(new LogMessage(LogSeverity.Critical, "KillSesh", "Discord connection closed."));
                    break;
            }

            this.alive = false;
        }

        public async Task StartSession()
        {
            this.discord = new Discord(config, this);
            this.irc = new IRC(config, this);

            await Discord.Log(new LogMessage(LogSeverity.Critical, "StartSesh", "Session started."));
            await discord.SpawnBot();
            await irc.SpawnBot();
        }

        public void SendMessage(TargetBot dest, string message, string username = "")
        {
            switch (dest)
            {
                case TargetBot.Discord:
                    discord.SendMessageAllToTarget(config.DiscordGuildName, message, config.DiscordChannelName);
                    break;
                case TargetBot.IRC:
                    irc.SendMessage(username, message);
                    break;
            }
        }
    }
}

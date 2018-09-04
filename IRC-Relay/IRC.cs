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
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using Meebey.SmartIrc4net;

using IRCRelay.Logs;
using Discord;

namespace IRCRelay
{
    public class IRC
    {
        private Session session;
        private dynamic config;
        private IrcClient ircClient;
        public bool shouldrun;

        public IrcClient Client { get => ircClient; set => ircClient = value; }

        public IRC(dynamic config, Session session)
        {
            this.config = config;
            this.session = session;

            ircClient = new IrcClient
            {
                Encoding = System.Text.Encoding.UTF8,
                SendDelay = 50,

                ActiveChannelSyncing = true,

                AutoRetry = true,
                AutoRejoin = true,
                AutoRelogin = true,
                AutoRejoinOnKick = true
            };

            ircClient.OnConnected += OnConnected;
            ircClient.OnError += this.OnError;
            ircClient.OnChannelMessage += this.OnChannelMessage;
        }

        public void SendMessage(string username, string message)
        {
            ircClient.SendMessage(SendType.Message, config.IRCChannel, "<" + username + "> " + message);
        }

        public async Task SpawnBot()
        {
            shouldrun = true;
            await Task.Run(() =>
            {
                ircClient.Connect(config.IRCServer, config.IRCPort);

                ircClient.Login(config.IRCNick, config.IRCLoginName);

                if (config.IRCAuthString.Length != 0)
                {
                    ircClient.SendMessage(SendType.Message, config.IRCAuthUser, config.IRCAuthString);

                    Thread.Sleep(1000); // login delay
                }

                ircClient.RfcJoin(config.IRCChannel);
                ircClient.Listen();
            });
        }

        private void OnConnected(object sender, EventArgs e)
        {
            session.Discord.Log(new LogMessage(LogSeverity.Critical, "IRCSpawn", "IRC bot initalized."));
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            /* Create a new thread to kill the session. We cannot block
             * this Disconnect call */
            new System.Threading.Thread(() => { session.Kill(); }).Start();

            session.Discord.Log(new LogMessage(LogSeverity.Critical, "IRCOnError", e.ErrorMessage));
        }

        private void OnChannelMessage(object sender, IrcEventArgs e)
        {
            if (e.Data.Nick.Equals(this.config.IRCNick))
                return;

            if (config.IRCNameBlacklist != null) // bcompat support
            {
                /**
                 * We'll loop all blacklisted names, if the sender
                 * has a blacklisted name, we won't relay and ret out
                 */
                foreach (string name in config.IRCNameBlacklist)
                {
                    if (e.Data.Nick.Equals(name))
                    {
                        return;
                    }
                }
            }

            if (config.IRCLogMessages)
                LogManager.WriteLog(MsgSendType.IRCToDiscord, e.Data.Nick, e.Data.Message, "log.txt");

            string msg = e.Data.Message;
            if (msg.Contains("@everyone"))
            {
                msg = msg.Replace("@everyone", "\\@everyone");
            }

            string prefix = "";

            var usr = e.Data.Irc.GetChannelUser(config.IRCChannel, e.Data.Nick);
            if (usr.IsOp)
            {
                prefix = "@";
            }
            else if (usr.IsVoice)
            {
                prefix = "+";
            }

            if (config.SpamFilter != null) //bcompat for older configurations
            {
                foreach (string badstr in config.SpamFilter)
                {
                    if (msg.ToLower().Contains(badstr.ToLower()))
                    {
                        ircClient.SendMessage(SendType.Message, config.IRCChannel, "Message with blacklisted input will not be relayed!");
                        return;
                    }
                }
            }

            session.SendMessage(Session.MessageDestination.Discord, "**<" + prefix + Regex.Escape(e.Data.Nick) + ">** " + msg);
        }
    }
}

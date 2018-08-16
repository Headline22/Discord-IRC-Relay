using System;

using Meebey.SmartIrc4net;
using System.Threading;
using System.Timers;
using IRCRelay.Logs;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace IRCRelay
{
    public class IRC
    {
        private IrcClient ircClient;
        private System.Timers.Timer timer = null;

        private string server;
        private int port;
        private string nick;
        private string channel;
        private string loginName;
        private string authstring;
        private string authuser;
        private string targetGuild;
        private string targetChannel;

        private bool logMessages;
        private Object[] blacklistNames;

        public IRC(string server, int port, string nick, string channel, string loginName, 
                   string authstring, string authuser, string targetGuild, string targetChannel, bool logMessages, Object[] blacklistNames)
        {
            ircClient = new IrcClient();

            ircClient.Encoding = System.Text.Encoding.UTF8;
            ircClient.SendDelay = 200;

            ircClient.ActiveChannelSyncing = true;
            
            ircClient.AutoRetry = true;
            ircClient.AutoRejoin = true;
            ircClient.AutoRelogin = true;
            ircClient.AutoRejoinOnKick = true;

            ircClient.OnError += this.OnError;
            ircClient.OnChannelMessage += this.OnChannelMessage;
            ircClient.OnDisconnected += this.OnDisconnected;

            timer = new System.Timers.Timer();

            timer.Elapsed += Timer_Callback;

            timer.Enabled = true;
            timer.AutoReset = true;
            timer.Interval = TimeSpan.FromSeconds(30.0).TotalMilliseconds;

            /* Connection Info */
            this.server = server;
            this.port = port;
            this.nick = nick;
            this.channel = channel;
            this.loginName = loginName;
            this.authstring = authstring;
            this.authuser = authuser;
            this.targetGuild = targetGuild;
            this.targetChannel = targetChannel;
            this.logMessages = logMessages;
            this.blacklistNames = blacklistNames;
        }

        public void SendMessage(string username, string message)
        {
            ircClient.SendMessage(SendType.Message, channel, "<" + username + "> " + message);
        }

        public void SpawnBot()
        {
            new Thread(() =>
            {
                try
                {
                    ircClient.Connect(server, port);

                    ircClient.Login(nick, loginName);

                    if (authstring.Length != 0)
                    {
                        ircClient.SendMessage(SendType.Message, authuser, authstring);

                        Thread.Sleep(1000); // login delay
                    }

                    ircClient.RfcJoin(channel);

                    ircClient.Listen();

                    timer.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return;
                }

            }).Start();
        }

        private void Timer_Callback(Object source, ElapsedEventArgs e)
        {
            if (ircClient.IsConnected)
            {
                return;
            }

            Console.WriteLine("Bot disconnected! Retrying...");
            this.SpawnBot();
        }

        private void OnDisconnected(object sender, EventArgs e)
        {
            Console.WriteLine("Disconnecting");
        }

        private void OnError(object sender, Meebey.SmartIrc4net.ErrorEventArgs e)
        {
            Console.WriteLine("Error: " + e.ErrorMessage);
            Environment.Exit(0);
        }

        private void OnChannelMessage(object sender, IrcEventArgs e)
        {
            if (e.Data.Nick.Equals(this.nick))
                return;

            if (blacklistNames != null) // bcompat support
            {
                /**
                 * We'll loop all blacklisted names, if the sender
                 * has a blacklisted name, we won't relay and ret out
                 */
                foreach (string name in blacklistNames)
                {
                    if (e.Data.Nick.Equals(name))
                    {
                        return;
                    }
                }
            }

            if (logMessages)
                LogManager.WriteLog(MsgSendType.IRCToDiscord, e.Data.Nick, e.Data.Message, "log.txt");

            string msg = e.Data.Message;
            if (msg.Contains("@everyone"))
            {
                msg = msg.Replace("@everyone", "\\@everyone");
            }

            string prefix = "";

            var usr = e.Data.Irc.GetChannelUser(channel, e.Data.Nick);
            if (usr.IsOp)
            {
                prefix = "@";
            }
            else if (usr.IsVoice)
            {
                prefix = "+";
            }

            if (Program.config.SpamFilter != null) //bcompat for older configurations
            {
                foreach (string badstr in Program.config.SpamFilter)
                {
                    if (msg.ToLower().Contains(badstr.ToLower()))
                    {
                        ircClient.SendMessage(SendType.Message, channel, "Message with blacklisted input will not be relayed!");
                        return;
                    }
                }
            }

            Helpers.SendMessageAllToTarget(targetGuild, "**<" + prefix + Regex.Escape(e.Data.Nick) + ">** " + msg, targetChannel);
        }
    }
}

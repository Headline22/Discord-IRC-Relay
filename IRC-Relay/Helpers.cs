using System.Net;
using System.Linq;
using Newtonsoft.Json.Linq;

using System.Text.RegularExpressions;
using System.Collections.Generic;

using Discord.WebSocket;
using System.Text;

namespace IRCRelay
{
    class Helpers
    {
        public static string UploadMarkDown(string input)
        {
            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.ContentType] = "text/plain";

                var response = client.UploadString("https://hastebin.com/documents", input);
                JObject obj = JObject.Parse(response);

                if (!obj.HasValues)
                {
                    return "";
                }

                string key = (string)obj["key"];
                string hasteUrl = "https://hastebin.com/" + key + ".cs";

                return hasteUrl;
            }
        }

        public static string MentionToUsername(string input, SocketUserMessage message)
        {
            Regex regex = new Regex("<@!?([0-9]+)>"); // create patern

            var m = regex.Matches(input); // find all matches
            var itRegex = m.GetEnumerator(); // lets iterate matches
            var itUsers = message.MentionedUsers.GetEnumerator(); // iterate mentions, too
            int difference = 0; // will explain later
            while (itUsers.MoveNext() && itRegex.MoveNext()) // we'll loop iterators together
            {
                var match = (Match)itRegex.Current; // C# makes us cast here.. gross
                var user = itUsers.Current;
                int len = match.Length;
                int start = match.Index;
                string removal = input.Substring(start - difference, len); // seperate what we're trying to replace

                /**
                * Since we're replacing `input` after every iteration, we have to
                * store the difference in length after our edits. This is because that
                * the Match object is going to use lengths from before the replacments
                * occured. Thus, we add the length and then subtract after the replace
                */
                difference += input.Length;
                input = ReplaceFirst(input, removal, user.Username);
                difference -= input.Length;
            }

            return input;
        }

        public static string Unescape(string input)
        {
            /* Main StringBuilder for messages that aren't in '`' */
            StringBuilder sb = new StringBuilder();

            /*
            * locations - List of indices where the first '`' lies
            * peices - List of strings which live inbetween the '`'s
            */
            List<int> locations = new List<int>();
            List<StringBuilder> peices = new List<StringBuilder>();
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '`') // we hit a '`'
                {
                    int j;

                    StringBuilder slice = new StringBuilder(); // used for capturing the str inbetween '`'
                    slice.Append('`'); // append the '`' for insertion later

                    /* we'll loop from here until we encounter the next '`',
                    * appending as we go.
                    */
                    for (j = i+1; j < input.Length && input[j] != '`'; j++) {
                        slice.Append(input[j]);
                    }

                    if (j < input.Length)
                        slice.Append('`'); // append the '`' for insertion later

                    locations.Add(i); // push the index of the first '`'
                    peices.Add(slice); // push the captured string

                    i = j; // advance the outer loop to where our inner one stopped
                }
                else // we didn't hit a '`', so just append :)
                {
                    sb.Append(input[i]);
                }
            }

            // From here we prep the return string by doing our regex on the input that's not in '`'
            string retstr = Regex.Replace(sb.ToString(), @"\\([^A-Za-z0-9])", "$1");

            // Now we'll just loop the peices, inserting @ the locations we saved earlier
            for (int i = 0; i < peices.Count; i++)
            {
                retstr = retstr.Insert(locations[i], peices[i].ToString());
            }

            return retstr; // thank fuck we're done
        }

        public static string ChannelMentionToName(string input, SocketUserMessage message)
        {
            Regex regex = new Regex("<#([0-9]+)>"); // create patern

            var m = regex.Matches(input); // find all matches
            var itRegex = m.GetEnumerator(); // lets iterate matches
            var itChan = message.MentionedChannels.GetEnumerator(); // iterate mentions, too
            int difference = 0; // will explain later
            while (itChan.MoveNext() && itRegex.MoveNext()) // we'll loop iterators together
            {
                var match = (Match)itRegex.Current; // C# makes us cast here.. gross
                var channel = itChan.Current;
                int len = match.Length;
                int start = match.Index;
                string removal = input.Substring(start - difference, len); // seperate what we're trying to replace

                /**
                * Since we're replacing `input` after every iteration, we have to
                * store the difference in length after our edits. This is because that
                * the Match object is going to use lengths from before the replacments
                * occured. Thus, we add the length and then subtract after the replace
                */
                difference += input.Length;
                input = ReplaceFirst(input, removal, "#" + channel.Name);
                difference -= input.Length;
            }

            return input;
        }

        public static string ReplaceFirst(string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }

        // Converts <:emoji:23598052306> to :emoji:
        public static string EmojiToName(string input, SocketUserMessage message)
        {
            string returnString = input;

            Regex regex = new Regex("<[A-Za-z0-9-_]?:[A-Za-z0-9-_]+:[0-9]+>");
            Match match = regex.Match(input);
            if (match.Success) // contains a emoji
            {
                string substring = input.Substring(match.Index, match.Length);
                string[] sections = substring.Split(':');

                returnString = input.Replace(substring, ":" + sections[1] + ":");
            }

            return returnString;
        }

        public static void SendMessageAllToTarget(string targetGuild, string message, string targetChannel, Discord discord)
        {
            foreach (SocketGuild guild in discord.Client.Guilds) // loop through each discord guild
            {
                
                if (guild.Name.ToLower().Contains(targetGuild.ToLower())) // find target 
                {
                    SocketTextChannel channel = FindChannel(guild, targetChannel); // find desired channel

                    if (channel != null) // target exists
                    {
                        channel.SendMessageAsync(message);
                    }
                }
            }
        }

        public static SocketTextChannel FindChannel(SocketGuild guild, string text)
        {
            foreach (SocketTextChannel channel in guild.TextChannels)
            {
                if (channel.Name.Contains(text))
                {
                    return channel;
                }
            }

            return null;
        }
    }
}

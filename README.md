# Discord-IRC-Relay
![thing](https://i.gyazo.com/2add2e5f5c56abe66b63564e71c4152c.gif)

This project is a simple Discord/IRC bot that relays irc messages to and from a Discord channel. This bot's goal is to provide a bridge between IRC channels and Discord text channels as seemlessly as possible.

## Features
* Discord emoji name conversion (`<:thinking:213123123>` -> `:thinking:`)
* Discord user mention conversion (`<@293102930912>` -> `Headline`)
* Discord channel mention conversion (`<#9102930912509>` -> `#general`)
* Discord intended escaping -> escaped output (`\#general` -> `#general`)
* Auto code block `hastebin.com` uploads. (code blocks created like \``` \<code> \``` will be uploaded)
* IRC +v and +o flags are expressed in Discord as '+' & '@', respectively. 
* Discord attachment uploads expressed as urls to IRC

## Installation
- Build the project or [download from our releases page](https://github.com/Headline22/Discord-IRC-Relay/releases)
- Configure Settings.json
- Ensure Settings.json is alongside the executable
- Run the executable!

## Credits 
Much of the code was adapted from [VoiDeD's bot](https://github.com/VoiDeD/steam-irc-bot/), so many thanks for the guidance. Also I'd like to thank the guys on #opensteamworks in gamesurge irc.

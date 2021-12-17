# Discord Twilio Voice Bot

The goal of this project is to create a Discord bot that can join a voice channel and make phone calls.
All participants of that voice channel can hear the phone call and the recipient of the phone call can hear the participants in the voice channel.
This should be possible by using Discord's APIs and Twilio's Voice APIs.

The following steps are needed to build this proof of concept to work:   
- [x] Build a Discord bot
- [x] Build a webhook to generate TwiML
- [x] Build a websocket handler that can pass the websocket to the Discord bot (to transmit audio between Discord and TwiML Voice Stream)
- [ ] WIP: Develop code to transform the PCM audio data (RAW) from Discord to Mulaw/8000 as required by Twilio
- [ ] Develop code to transform Mulaw/8000 audio from Twilio to PCM for Discord
- [ ] ⚠️ NEED HELP ⚠️ Develop code to receive and send data over the Websocket with Twilio
- [ ] Add code to easily update the Twilio phone number webhook to use ngrok and the specific Discord Guild/Channel ID parameters

This proof of concept would be even more powerful with the following features:
1. Buy a phone number through the bot
2. Configure the webhook with the correct Discord Guild/Channel ID from the bot
3. Initiate phone call from the Discord server and by calling the phone number

## Relevant resources
- [DSharpPlus](https://github.com/DSharpPlus/DSharpPlus): library used to develop the Discord bot
- [Sockets Docs for ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-6.0)
- [Voice TwiML Stream](https://www.twilio.com/docs/voice/twiml/stream): The Voice TwiML docs for streaming audio using websockets
- [Websocket Manager](https://github.com/radu-matei/websocket-manager): This project isn't being used in this project, but some of the source code is used and modified to fit the needs of the project

## Technology choice
This project will be using .NET 6 and should run on all .NET supported platforms.
There may be better technology to built this, but the goal is to do this in .NET.
If you want to use other technology to build the same bot, I'd love to see it, let me know!

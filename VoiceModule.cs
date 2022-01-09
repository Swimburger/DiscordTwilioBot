using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using DSharpPlus.VoiceNext.EventArgs;
using Microsoft.Toolkit.HighPerformance;
using NAudio.Codecs;
using NAudio.Wave;

namespace DiscordTwilioBot;

public class VoiceModule : BaseCommandModule
{
    private static readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private string socketId = null;
    private readonly ILogger<DiscordBotWorker> logger;
    private readonly TwilioSocketConnectionManager twilioSocketConnectionManager;

    public VoiceModule(ILogger<DiscordBotWorker> logger, TwilioSocketConnectionManager webSocketConnectionManager)
    {
        this.logger = logger;
        this.twilioSocketConnectionManager = webSocketConnectionManager;
    }

    [Command("join")]
    public async Task JoinCommand(CommandContext context, DiscordChannel channel = null)
    {
        channel ??= context.Member.VoiceState?.Channel;
        var connection = await channel.ConnectAsync();
        socketId = $"{context.Guild.Id}/{connection.TargetChannel.Id}";
        await context.RespondAsync($"Socket ID: {socketId}");
        connection.VoiceReceived += VoiceReceiveHandler;
    }

    [Command("leave")]
    public async Task LeaveCommand(CommandContext context)
    {
        var vnext = context.Client.GetVoiceNext();
        var connection = vnext.GetConnection(context.Guild);
        connection.VoiceReceived -= VoiceReceiveHandler;
        connection.Disconnect();
    }

    private async Task VoiceReceiveHandler(VoiceNextConnection connection, VoiceReceiveEventArgs args)
    {
        var fileName = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var ffmpeg = Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $@"-hide_banner -ac 2 -f s16le -ar 48000 -i pipe:0 -c:a pcm_mulaw -f mulaw -ar 8000 -ac 1 pipe:1",
            RedirectStandardInput = true,
            RedirectStandardOutput = true
        });

        //byte[] trimmedData = new byte[args.PcmData.Length - 44];
        //Buffer.BlockCopy(args.PcmData.ToArray(), 44, trimmedData, 0, trimmedData.Length);

        await ffmpeg.StandardInput.BaseStream.WriteAsync(args.PcmData);
        ffmpeg.StandardInput.Close();
        byte[] data;
        using(var memoryStream = new MemoryStream())
        {
            ffmpeg.StandardOutput.BaseStream.CopyTo(memoryStream);
            data = memoryStream.ToArray();
        }
        ffmpeg.Dispose();

        //byte[] trimmedData = new byte[data.Length - 44];
        //Buffer.BlockCopy(data, 44, trimmedData, 0, trimmedData.Length);

        //return;

        if (twilioSocketConnectionManager.TryGetSocketById(socketId, out var twilioSocket) && twilioSocket.Socket.State == WebSocketState.Open)
        {
            var json = JsonSerializer.Serialize<MediaMessage>
            (
                new MediaMessage("media", twilioSocket.StreamSid, new MediaPayload(Convert.ToBase64String(data))), 
                jsonSerializerOptions
            );
            logger.LogInformation(json);
            var bytes = Encoding.Default.GetBytes(json);
            var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);
            await twilioSocket.Socket.SendAsync(arraySegment, WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, CancellationToken.None);
        }
    }
}

public readonly record struct MediaMessage(string Event, string StreamSid, MediaPayload Media);
public readonly record struct MediaPayload(string Payload);
public readonly record struct MarkMessage(string Event, string StreamSid, object obj);
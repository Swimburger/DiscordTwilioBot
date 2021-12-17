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

    [Command("play")]
    public async Task PlayCommand(CommandContext context, string path)
    {
        var vnext = context.Client.GetVoiceNext();
        var connection = vnext.GetConnection(context.Guild);

        var transmit = connection.GetTransmitSink();

        var pcm = ConvertAudioToPcm(path);
        await pcm.CopyToAsync(transmit);
        await pcm.DisposeAsync();
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
        if (twilioSocketConnectionManager.TryGetSocketById(socketId, out var twilioSocket))
        {
            var media = ConvertPcmToMulawBase64Encoded(args.PcmData);
            var json = JsonSerializer.Serialize<MediaMessage>(new MediaMessage("media", twilioSocket.StreamSid, new MediaPayload(media)), jsonSerializerOptions);
            logger.LogInformation(json);
            var bytes = Encoding.Default.GetBytes(json);
            var arraySegment = new ArraySegment<byte>(bytes);
            await twilioSocket.Socket.SendAsync(arraySegment, WebSocketMessageType.Text, WebSocketMessageFlags.None, CancellationToken.None);

            var mark = new MarkMessage("mark", twilioSocket.StreamSid, null);
            json = JsonSerializer.Serialize<MarkMessage>(mark, jsonSerializerOptions);
            bytes = Encoding.Default.GetBytes(json);
            arraySegment = new ArraySegment<byte>(bytes);
            await twilioSocket.Socket.SendAsync(arraySegment, WebSocketMessageType.Text, WebSocketMessageFlags.None, CancellationToken.None);
        }
    }

    private static string ConvertPcmToMulawBase64Encoded(ReadOnlyMemory<byte> pcmData)
    {
        int channels = 1;
        int sampleRate = 8000;
        var waveFormat = new WaveFormat(sampleRate, 16, channels);
        var mulawFormat = WaveFormat.CreateMuLawFormat(sampleRate, channels);
        var rs = new RawSourceWaveStream(pcmData.AsStream(), waveFormat);

        var bytes = new Byte[(int)rs.Length];
        rs.Seek(0, SeekOrigin.Begin);
        rs.Read(bytes, 0, (int)rs.Length);
        return Convert.ToBase64String(bytes);
    }

    private Stream ConvertAudioToPcm(string filePath)
    {
        var ffmpeg = Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $@"-i ""{filePath}"" -ac 2 -f s16le -ar 48000 pipe:1",
            RedirectStandardOutput = true,
            UseShellExecute = false
        });

        return ffmpeg.StandardOutput.BaseStream;
    }

    private async Task ConvertPcmToAudio(ReadOnlyMemory<byte> pcmData)
    {
        var fileName = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var ffmpeg = Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $@"-ac 2 -f s16le -ar 48000 -i pipe:0 -ac 2 -ar 44100 Output/{fileName}.wav",
            RedirectStandardInput = true
        });

        await ffmpeg.StandardInput.BaseStream.WriteAsync(pcmData);
        ffmpeg.Dispose();
    }
}

public readonly record struct MediaMessage(string Event, string StreamSid, MediaPayload MediaPayload);
public readonly record struct MediaPayload(string Media);
public readonly record struct MarkMessage(string Event, string StreamSid, object obj);
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
        
        if (twilioSocketConnectionManager.TryGetSocketById(socketId, out var twilioSocket) && twilioSocket.Socket.State == WebSocketState.Open)
        {
            var media = ConvertPcmToMulawBase64Encoded(args.AudioFormat, args.PcmData.ToArray());
            var json = JsonSerializer.Serialize<MediaMessage>
            (
                new MediaMessage("media", twilioSocket.StreamSid, new MediaPayload(media)), 
                jsonSerializerOptions
            );
            logger.LogInformation(json);
            var bytes = Encoding.Default.GetBytes(json);
            var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);
            await twilioSocket.Socket.SendAsync(arraySegment, WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, CancellationToken.None);
        }
    }

    private static string ConvertPcmToMulawBase64Encoded(AudioFormat audioFormat, byte[] pcmData)
    {
         
        var sourceFormat = new WaveFormat(audioFormat.SampleRate, 16, audioFormat.ChannelCount);
        return Convert.ToBase64String(EncodeMuLaw(pcmData, 0, pcmData.Length));
    }

    public static byte[] EncodeMuLaw(byte[] data, int offset, int length)
    {
        var encoded = new byte[length / 2];
        int outIndex = 0;
        for(int n = 0; n < length; n+=2)
        {
            encoded[outIndex++] = MuLawEncoder.LinearToMuLawSample(BitConverter.ToInt16(data, offset + n));
        }
        return encoded;
    }

    public static byte[] DecodeMuLaw(byte[] data, int offset, int length)
    {
        var decoded = new byte[length * 2];
        int outIndex = 0;
        for (int n = 0; n < length; n++)
        {
            short decodedSample = MuLawDecoder.MuLawToLinearSample(data[n + offset]);
            decoded[outIndex++] = (byte)(decodedSample & 0xFF);
            decoded[outIndex++] = (byte)(decodedSample >> 8);
        }
        return decoded;
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

public readonly record struct MediaMessage(string Event, string StreamSid, MediaPayload Media);
public readonly record struct MediaPayload(string Payload);
public readonly record struct MarkMessage(string Event, string StreamSid, object obj);
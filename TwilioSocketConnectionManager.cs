using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace DiscordTwilioBot;

public class TwilioSocketConnectionManager
{
    private static readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private readonly ILogger<TwilioSocketConnectionManager> logger;
    private ConcurrentDictionary<string, TwilioSocket> sockets = new ConcurrentDictionary<string, TwilioSocket>();

    public TwilioSocketConnectionManager(ILogger<TwilioSocketConnectionManager> logger)
    {
        this.logger = logger;
    }

    public bool TryGetSocketById(string discordChannelId, out TwilioSocket socket) => sockets.TryGetValue(discordChannelId, out socket);

    public ConcurrentDictionary<string, TwilioSocket> GetAll() => sockets;

    public string GetId(TwilioSocket socket) => sockets.FirstOrDefault(p => p.Value == socket).Key;

    public bool AddSocket(TwilioSocket socket) => sockets.TryAdd(socket.DiscordChannelId, socket);

    public async Task RemoveSocket(string discordChannelId)
    {
        if (discordChannelId == null) return;

        sockets.TryRemove(discordChannelId, out var twilioSocket);

        if (twilioSocket.Socket.State == WebSocketState.Open)
        {
            await twilioSocket.Socket.CloseAsync(closeStatus: WebSocketCloseStatus.NormalClosure,
                                    statusDescription: "Closed by the WebSocketManager",
                                    cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }

        twilioSocket.TaskCompletionSource.SetResult(null);
    }

    public async Task Listen(TwilioSocket twilioSocket)
    {
        var socket = twilioSocket.Socket;
        while (socket.State == WebSocketState.Open)
        {
            ArraySegment<Byte> buffer = new ArraySegment<byte>(new Byte[1024 * 4]);
            string serializedMessage = null;
            WebSocketReceiveResult result = null;
            using (var ms = new MemoryStream())
            {
                do
                {
                    result = await socket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                }
                while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);

                using (var reader = new StreamReader(ms, Encoding.UTF8))
                {
                    serializedMessage = await reader.ReadToEndAsync().ConfigureAwait(false);
                }
            }

            if (result.MessageType == WebSocketMessageType.Text)
            {
                //logger.LogInformation("Message: {0}", serializedMessage);
                var json = JsonSerializer.Deserialize<JsonDocument>(serializedMessage, jsonSerializerOptions);
                string messageEvent = json.RootElement.GetProperty("event").GetString();
                switch(messageEvent)
                {
                    case "start":
                        var streamSid = json.RootElement.GetProperty("streamSid").GetString();
                        logger.LogInformation("Start StreamSid: {0}", streamSid);
                        twilioSocket.StreamSid = streamSid;
                        break;
                    case "media":
                        var media = json.RootElement.GetProperty("media").GetProperty("payload").GetString();
                        break;
                    default:
                        break;
                }
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false);
                break;
            }
        }
    }
}
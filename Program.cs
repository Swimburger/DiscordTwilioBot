using System.Net;
using System.Net.WebSockets;
using System.Text;
using DiscordTwilioBot;
using Twilio.TwiML;
using Twilio.TwiML.Voice;
using Task = System.Threading.Tasks.Task;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<TwilioSocketConnectionManager>();
builder.Services.AddHostedService<DiscordBotWorker>();
var app = builder.Build();
app.UseWebSockets();
app.MapMethods("/connect/{guildId}/{channelId}", new[] { "get", "post" }, (
    HttpResponse response,
    string guildId,
    string channelId
) =>
{
    response.ContentType = "text/xml";
    var twiml = new VoiceResponse();
    var connect = new Connect();
    connect.Stream(url: $"wss://7632a62e9ef4.ngrok.io/ws/{guildId}/{channelId}");
    twiml.Append(connect);
    response.WriteAsync(twiml.ToString());
});

app.MapGet("/ws/{guildId}/{channelId}", async (
    HttpContext context,
    TwilioSocketConnectionManager webSocketConnectionManager,
    string guildId,
    string channelId
) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using (WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync())
        {
            var twilioSocket = new TwilioSocket
            {
                DiscordChannelId = $"{guildId}/{channelId}",
                Socket = webSocket,
                TaskCompletionSource = new TaskCompletionSource<object>()
            };
            webSocketConnectionManager.AddSocket(twilioSocket);
            await webSocketConnectionManager.Listen(twilioSocket);
            //await twilioSocket.TaskCompletionSource.Task;
        }
    }
    else
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
});

app.Run();
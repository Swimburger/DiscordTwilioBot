using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace DiscordTwilioBot;

public class TwilioSocket
{
    public string DiscordChannelId { get; set; }
    public string StreamSid { get; set; }
    public WebSocket Socket { get; set; }
    public TaskCompletionSource<object> TaskCompletionSource { get; set; }
}
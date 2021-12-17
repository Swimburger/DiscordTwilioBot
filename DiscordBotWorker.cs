using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.EventArgs;
using DSharpPlus.VoiceNext;

namespace DiscordTwilioBot;

public class DiscordBotWorker : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private ILogger<DiscordBotWorker> logger;
    private IConfiguration configuration;
    private DiscordClient? discordClient;

    public DiscordBotWorker(IServiceProvider serviceProvider, ILogger<DiscordBotWorker> logger, IConfiguration configuration)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        this.configuration = configuration;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting discord bot");

        string discordBotToken = configuration["DiscordBotToken"];
        discordClient = new DiscordClient(new DiscordConfiguration()
        {
            Token = discordBotToken,
            TokenType = TokenType.Bot,
            Intents = DiscordIntents.AllUnprivileged
        });

        discordClient.UseVoiceNext(new VoiceNextConfiguration()
        {
            EnableIncoming = true
        });
        var commands = discordClient.UseCommandsNext(new CommandsNextConfiguration()
        {
            StringPrefixes = new[] { "!" },
            Services = serviceProvider
        });

        commands.RegisterCommands<VoiceModule>();

        await discordClient.ConnectAsync();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await discordClient!.DisconnectAsync();
        discordClient.Dispose();
        logger.LogInformation("Discord bot stopped");
    }
}
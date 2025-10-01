using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using RS3ClanHelper;
using System;
using System.Net.Http;
using System.Threading.Tasks;

// --- Build services ---
var services = new ServiceCollection()
    .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers,
        AlwaysDownloadUsers = true,
        LogLevel = LogSeverity.Info
    }))
    // ✅ Give InteractionService the client it needs
    .AddSingleton(sp => new InteractionService(sp.GetRequiredService<DiscordSocketClient>()))
    .AddSingleton<AppState>()
    .AddSingleton<HttpClient>()
    .BuildServiceProvider();


var client = services.GetRequiredService<DiscordSocketClient>();
var interactions = services.GetRequiredService<InteractionService>();

client.Log += m => { Console.WriteLine(m.ToString()); return Task.CompletedTask; };
interactions.Log += m => { Console.WriteLine(m.ToString()); return Task.CompletedTask; };

// Register modules & slash commands
client.Ready += async () =>
{
    await interactions.AddModulesAsync(typeof(ClanModule).Assembly, services);

    // Register commands to each guild for faster iteration (dev mode).
    foreach (var g in client.Guilds)
        await interactions.RegisterCommandsToGuildAsync(g.Id);

    Console.WriteLine("Slash commands registered.");
};

client.InteractionCreated += async raw =>
{
    try
    {
        var ctx = new SocketInteractionContext(client, raw);
        await interactions.ExecuteCommandAsync(ctx, services);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Interaction error: {ex}");
    }
};

// Token & start
var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    Console.WriteLine("DISCORD_TOKEN not set. Set it as an environment variable.");
    return;
}

await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();

// If you ever see ISO-8859-1 decode issues, uncomment:
// System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

await Task.Delay(-1);

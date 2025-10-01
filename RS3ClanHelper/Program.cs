using Microsoft.Extensions.DependencyInjection;
using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using RS3ClanHelper.State;
using RS3ClanHelper.Services;
using RS3ClanHelper.Modules;
using System.Net.Http;

// Register code pages if needed (ISO-8859-1)
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

// --- Build services ---
var services = new ServiceCollection()
    .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers,
        AlwaysDownloadUsers = true,
        LogLevel = LogSeverity.Info
    }))
    .AddSingleton(sp => new InteractionService(sp.GetRequiredService<DiscordSocketClient>(), new InteractionServiceConfig
    {
        UseCompiledLambda = true
    }))
    .AddSingleton<AppState>()
    .AddSingleton<HttpClient>()
    .AddSingleton<INameNormalizer, NameNormalizer>()
    .AddSingleton<IClanApiClient, ClanApiClient>()
    .AddSingleton<IRoleSyncService, RoleSyncService>()
    .AddSingleton<IScheduledSyncService, ScheduledSyncService>()
    .BuildServiceProvider();

var client = services.GetRequiredService<DiscordSocketClient>();
var interactions = services.GetRequiredService<InteractionService>();

client.Log += m => { Console.WriteLine(m.ToString()); return Task.CompletedTask; };
interactions.Log += m => { Console.WriteLine(m.ToString()); return Task.CompletedTask; };

client.Ready += async () =>
{
    await interactions.AddModulesAsync(typeof(ClanModule).Assembly, services);
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

var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    Console.WriteLine("DISCORD_TOKEN not set. Set env var and rerun.");
    return;
}

await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();
await Task.Delay(-1);

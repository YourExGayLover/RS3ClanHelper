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

    // Existing HttpClient used by ClanApiClient
    .AddSingleton<HttpClient>()

    // New: storage for json config/snapshots
    .AddSingleton<StorageService>()

    // New: hiscores client used by SnapshotService
    .AddSingleton<HiscoresClient>()

    // Existing: Clan API client that depends on HttpClient
    .AddSingleton<INameNormalizer, NameNormalizer>()
    .AddSingleton<IClanApiClient, ClanApiClient>()
    .AddSingleton<IRoleSyncService, RoleSyncService>()
    .AddSingleton<IScheduledSyncService, ScheduledSyncService>()

    // New: snapshots/leaderboards engine
    .AddSingleton<SnapshotService>()

    .BuildServiceProvider();

var client = services.GetRequiredService<DiscordSocketClient>();
var interactions = services.GetRequiredService<InteractionService>();

client.Log += m => { Console.WriteLine(m.ToString()); return Task.CompletedTask; };
interactions.Log += m => { Console.WriteLine(m.ToString()); return Task.CompletedTask; };

//client.Ready += async () =>
//{
//    // Use the SAME provider 'services' so DI can resolve SnapshotService, etc.
//    await interactions.AddModulesAsync(typeof(ClanModule).Assembly, services);
//    foreach (var g in client.Guilds)
//        await interactions.RegisterCommandsToGuildAsync(g.Id);
//    Console.WriteLine("Slash commands registered.");
//};
client.Ready += async () =>
{
    // Add modules using the SAME provider
    await interactions.AddModulesAsync(typeof(ClanModule).Assembly, services);

    try
    {
        Console.WriteLine("=== Slash Command Audit ===");

        // Map: top-level command name -> list of "commandName [moduleNamePath]"
        var topNameToCommands = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var cmd in interactions.SlashCommands)
        {
            // Walk up to the root module for this command
            var top = cmd.Module;
            while (top.Parent != null) top = top.Parent;

            // The top-level app command name:
            // - If root is a slash group, that's the /<name>
            // - Otherwise the command itself is top-level
            var topLevelName = top.IsSlashGroup ? top.SlashGroupName : cmd.Name;

            // Build a readable module path like "ClanModule/LookupModule"
            var chain = new List<string>();
            var cursor = cmd.Module;
            while (cursor != null)
            {
                // Prefer SlashGroupName when present, else Module Name
                var chunk = !string.IsNullOrWhiteSpace(cursor.SlashGroupName)
                    ? cursor.SlashGroupName
                    : cursor.Name;
                chain.Add(chunk);
                cursor = cursor.Parent;
            }
            chain.Reverse();
            var modulePath = string.Join("/", chain);

            if (!topNameToCommands.TryGetValue(topLevelName, out var list))
            {
                list = new List<string>();
                topNameToCommands[topLevelName] = list;
            }

            list.Add($"{cmd.Name}  [module: {modulePath}]");
        }

        // Print the command inventory grouped by top level
        foreach (var kv in topNameToCommands.OrderBy(k => k.Key))
        {
            Console.WriteLine($"Top-level '{kv.Key}' => {kv.Value.Count} slash commands");
            foreach (var line in kv.Value.OrderBy(v => v))
                Console.WriteLine($"  - {line}");
        }

        // Look for duplicate top-level *group* names (most common cause)
        var duplicateTopGroups =
            interactions.Modules
                .Where(m => m.Parent == null && m.IsSlashGroup && !string.IsNullOrWhiteSpace(m.SlashGroupName))
                .GroupBy(m => m.SlashGroupName, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);

        foreach (var dup in duplicateTopGroups)
        {
            Console.WriteLine($"❗ Duplicate top-level group name '{dup.Key}' defined by modules:");
            foreach (var mod in dup)
                Console.WriteLine($"   • {(!string.IsNullOrWhiteSpace(mod.SlashGroupName) ? mod.SlashGroupName : mod.Name)} (root module)");
        }

        Console.WriteLine("=== End Slash Command Audit ===");
    }
    catch (Exception auditEx)
    {
        Console.WriteLine($"[Command Audit Error] {auditEx}");
    }

    // Will still fail if duplicates exist, but now you'll see exactly where
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

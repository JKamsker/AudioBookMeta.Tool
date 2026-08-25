using System.Net;
using AudiobookMeta.Tool.Commands;
using AudiobookMeta.Tool.Commands.Config;
using AudiobookMeta.Tool.Commands.Author;
using AudiobookMeta.Tool.Commands.Providers;
using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;
using AudiobookMeta.Tool.Providers;
using AudiobookMeta.Tool.Render;
using AudiobookMeta.Tool.Search;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

var noColor = args.Contains("--no-color", StringComparer.Ordinal) || Environment.GetEnvironmentVariable("NO_COLOR") is not null;
var ansiConsole = AnsiConsole.Create(new AnsiConsoleSettings
{
    Ansi = noColor ? AnsiSupport.No : AnsiSupport.Detect,
    ColorSystem = noColor ? ColorSystemSupport.NoColors : ColorSystemSupport.Detect,
    Out = new AnsiConsoleOutput(Console.Out)
});

var services = new ServiceCollection();
services.AddSingleton(new AppConsole(ansiConsole));
services.AddSingleton<ConfigPathResolver>();
services.AddSingleton<ConfigLoader>();
services.AddSingleton<ConfigDocumentStore>();
services.AddSingleton<ProviderSelector>();
services.AddSingleton<ProviderFactory>();
services.AddSingleton<ProviderTransport>();
services.AddSingleton<KiotaClientFactory>();
services.AddSingleton<SearchCache>();
services.AddSingleton<ResultRanker>();
services.AddSingleton<ResultClusterer>();
services.AddSingleton<SearchEngine>();
services.AddSingleton<SearchRenderer>();
services.AddSingleton<AudiobookRenderer>();
services.AddSingleton<DiagnosticLogger>();
services.AddHttpClient("provider", client => client.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.All,
        ConnectTimeout = Timeout.InfiniteTimeSpan,
        MaxConnectionsPerServer = 16
    });

var app = new CommandApp(new TypeRegistrar(services));
app.Configure(config =>
{
    config.SetApplicationName(ApplicationIdentity.Command);
    config.SetApplicationVersion(ApplicationIdentity.Version);
    config.AddCommand<SearchCommand>("search")
        .WithDescription("Search and locally rank metadata across configured providers.")
        .WithExample("search", "har pot philos")
        .WithExample("search", "dune", "-p", "libex", "--json");
    config.AddCommand<GetCommand>("get")
        .WithDescription("Retrieve a native provider record by PROVIDER:ID.")
        .WithExample("get", "libex:B00B7NPRY8");
    config.AddBranch("author", author =>
    {
        author.SetDescription("Find audiobook metadata through provider-native author lookup.");
        author.AddCommand<AuthorBooksCommand>("books")
            .WithDescription("List all audiobooks returned for an author by Libex.")
            .WithExample("author", "books", "Andy Weir", "--provider", "libex");
    });
    config.AddBranch("providers", providers =>
    {
        providers.SetDescription("Inspect providers, capabilities, and connectivity without exposing secrets.");
        providers.AddCommand<ProvidersListCommand>("list").WithDescription("List configured provider instances.");
        providers.AddCommand<ProvidersShowCommand>("show").WithDescription("Show one provider with secrets redacted.");
        providers.AddCommand<ProvidersTestCommand>("test").WithDescription("Run non-destructive connectivity and contract checks.");
        providers.AddCommand<ProvidersCapabilitiesCommand>("capabilities").WithDescription("Show tri-state documented and configured capabilities.");
    });
    config.AddBranch("config", branch =>
    {
        branch.SetDescription("Locate, inspect, edit, and validate the TOML configuration.");
        branch.AddCommand<ConfigPathCommand>("path").WithDescription("Print the resolved configuration path.");
        branch.AddCommand<ConfigGetCommand>("get")
            .WithDescription("Read one configuration value with secrets redacted.")
            .WithExample("config", "get", "search.limit");
        branch.AddCommand<ConfigSetCommand>("set")
            .WithDescription("Set one configuration value, creating the file when needed.")
            .WithExample("config", "set", "search.limit", "20")
            .WithExample("config", "set", "providers.catalog.base_url", "https://metadata.example");
        branch.AddCommand<ConfigUnsetCommand>("unset")
            .WithDescription("Remove one configuration value or provider.")
            .WithExample("config", "unset", "providers.catalog", "--dry-run");
        branch.AddCommand<ConfigValidateCommand>("validate").WithDescription("Validate structure, targets, groups, and secret references.");
    });
    config.AddCommand<CompletionCommand>("completion").WithDescription("Generate shell completion setup for bash, zsh, fish, or PowerShell.");
    config.SetExceptionHandler((exception, resolver) =>
    {
        var console = (AppConsole?)resolver?.Resolve(typeof(AppConsole)) ?? new AppConsole(ansiConsole);
        var known = exception as AudiobookMetaException;
        var provider = exception as ProviderException;
        var usage = exception as CommandRuntimeException;
        var message = known?.Message ?? provider?.Message ?? usage?.Message ?? (exception is OperationCanceledException ? "Operation cancelled." : "The command failed unexpectedly.");
        console.Error($"error: {message}");
        if (known?.Recovery is not null)
            console.Error($"next: {known.Recovery}");
        else if (provider is not null)
            console.Error($"next: Check provider '{provider.Provider}' connectivity and configuration, then retry.");
        else if (usage is not null)
            console.Error("next: Run the command with --help to review required arguments and valid options.");
        var quiet = args.Contains("--quiet", StringComparer.Ordinal);
        try
        {
            if (usage is null && known is null)
            {
                var logger = (DiagnosticLogger?)resolver?.Resolve(typeof(DiagnosticLogger));
                var path = logger?.Write(exception, args);
                if (!quiet && path is not null)
                    console.Error($"Diagnostic log saved to {path}");
            }
        }
        catch (Exception) when (exception is not OutOfMemoryException) { }
        return known?.ExitCode ?? (usage is not null ? ExitCodes.Usage : provider is not null ? ExitCodes.ProvidersFailed : exception is OperationCanceledException ? ExitCodes.Cancelled : ExitCodes.General);
    });
});

var exitCode = await app.RunAsync(args);
return exitCode < 0 ? ExitCodes.Usage : exitCode;

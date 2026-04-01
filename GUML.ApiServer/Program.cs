using System.Reflection;
using Microsoft.Build.Locator;
using Serilog;

namespace GUML.LSP;

/// <summary>
/// Entry point for the GUML API analysis server.
/// Provides Roslyn-based project analysis via JSON-RPC over stdin/stdout.
/// Supports <c>--stop</c>, <c>--restart</c>, <c>--status</c>, and <c>--version</c> commands.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Handle command-line arguments before any heavy initialization
        if (args.Contains("--version"))
        {
            PrintVersion();
            return 0;
        }

        if (args.Contains("--status"))
        {
            ProcessControl.PrintStatus();
            return 0;
        }

        if (args.Contains("--stop"))
        {
            int stopped = ProcessControl.StopAll();
            Console.Error.WriteLine(stopped > 0
                ? $"Stopped {stopped} instance(s)."
                : "No running instances found.");
            return 0;
        }

        bool isRestart = args.Contains("--restart");
        if (isRestart)
        {
            int stopped = ProcessControl.StopAll();
            Console.Error.WriteLine(stopped > 0
                ? $"Stopped {stopped} instance(s), restarting..."
                : "No running instances found, starting fresh...");
        }

        // Normal server mode
        return await RunServerAsync();
    }

    private static async Task<int> RunServerAsync()
    {
        // Initialize logging to stderr (stdout is reserved for JSON-RPC)
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
            .MinimumLevel.Debug()
            .CreateLogger();

        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";
        Log.Logger.Information("guml-api-server v{Version} starting", version);

        // Register MSBuild so that MSBuildWorkspace can locate SDK targets
        if (!MSBuildLocator.IsRegistered)
            MSBuildLocator.RegisterDefaults();

        // Set up process control (PID file + Named Pipe)
        using var processControl = new ProcessControl();
        processControl.Register();

        // Set up JSON-RPC transport over stdin/stdout
        using var transport = new JsonRpcTransport(
            Console.OpenStandardInput(),
            Console.OpenStandardOutput());

        // Create and run the API server
        using var server = new ApiServer(transport, processControl);

        try
        {
            await server.RunAsync();
        }
        catch (OperationCanceledException)
        {
            Log.Logger.Information("Server shutdown (cancelled)");
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Server crashed");
            return 1;
        }
        finally
        {
            Log.Logger.Information("guml-api-server exiting");
            await Log.CloseAndFlushAsync();
        }

        return 0;
    }

    private static void PrintVersion()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";
        Console.WriteLine($"guml-api-server {version}");
    }
}

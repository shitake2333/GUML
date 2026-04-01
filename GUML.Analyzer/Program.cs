using System.CommandLine;
using System.Reflection;
using Microsoft.Build.Locator;
using Serilog;

namespace GUML.Analyzer;

/// <summary>
/// Entry point for the GUML Analyzer.
/// Provides Roslyn-based project analysis and LSP features via JSON-RPC over stdin/stdout.
/// CLI subcommands: <c>check</c>, <c>format</c>, <c>generate-api</c>, <c>stop</c>, <c>status</c>.
/// When invoked without a subcommand, starts the LSP server.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        var restartOption = new Option<bool>("--restart")
        {
            Description = "Stop existing instances before starting"
        };

        var rootCommand = new RootCommand("GUML language analyzer for Godot .NET — LSP server, static analysis, and formatting tool");
        rootCommand.Options.Add(restartOption);

        rootCommand.Subcommands.Add(BuildCheckCommand());
        rootCommand.Subcommands.Add(BuildFormatCommand());
        rootCommand.Subcommands.Add(BuildGenerateApiCommand());
        rootCommand.Subcommands.Add(BuildStopCommand());
        rootCommand.Subcommands.Add(BuildStatusCommand());

        // Default (no subcommand): start LSP server.
        rootCommand.SetAction(parseResult =>
        {
            bool restart = parseResult.GetValue(restartOption);
            if (restart)
            {
                int stopped = ProcessControl.StopAll();
                Console.Error.WriteLine(stopped > 0
                    ? $"Stopped {stopped} instance(s), restarting..."
                    : "No running instances found, starting fresh...");
            }

            return RunServerAsync().GetAwaiter().GetResult();
        });

        return rootCommand.Parse(args).Invoke();
    }

    // ── Subcommand builders ──

    private static Command BuildCheckCommand()
    {
        var pathOption = new Option<DirectoryInfo?>("--path")
        {
            Description = "Project root directory (default: current directory)"
        };
        var formatOption = new Option<string>("--format")
        {
            Description = "Output format: text or json",
            DefaultValueFactory = _ => "text"
        };
        var severityOption = new Option<string>("--severity")
        {
            Description = "Minimum severity level: error, warning, info, or hint",
            DefaultValueFactory = _ => "info"
        };

        var cmd = new Command("check", "Run static analysis on all .guml files in a Godot project")
        {
            pathOption, formatOption, severityOption
        };

        cmd.SetAction(parseResult =>
        {
            var path = parseResult.GetValue(pathOption);
            string format = parseResult.GetValue(formatOption)!;
            string severity = parseResult.GetValue(severityOption)!;
            string rootPath = path?.FullName ?? Directory.GetCurrentDirectory();
            return StaticCheckRunner.RunAsync(rootPath, format, severity).GetAwaiter().GetResult();
        });

        return cmd;
    }

    private static Command BuildFormatCommand()
    {
        var pathOption = new Option<DirectoryInfo?>("--path")
        {
            Description = "Project root directory (default: current directory)"
        };
        var dryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Report files that need formatting without modifying them"
        };
        var tabSizeOption = new Option<int>("--tab-size")
        {
            Description = "Number of spaces per indentation level",
            DefaultValueFactory = _ => 4
        };
        var useTabsOption = new Option<bool>("--use-tabs")
        {
            Description = "Use tab characters instead of spaces for indentation"
        };

        var cmd = new Command("format", "Format all .guml files in a project")
        {
            pathOption, dryRunOption, tabSizeOption, useTabsOption
        };

        cmd.SetAction(parseResult =>
        {
            var path = parseResult.GetValue(pathOption);
            bool dryRun = parseResult.GetValue(dryRunOption);
            int tabSize = parseResult.GetValue(tabSizeOption);
            bool useTabs = parseResult.GetValue(useTabsOption);
            string rootPath = path?.FullName ?? Directory.GetCurrentDirectory();
            return FormatRunner.RunAsync(rootPath, dryRun, tabSize, useTabs).GetAwaiter().GetResult();
        });

        return cmd;
    }

    private static Command BuildGenerateApiCommand()
    {
        var godotVersionOption = new Option<string?>("--godot-version")
        {
            Description = "Godot version to generate API for (e.g. 4.6.0)"
        };
        var godotSourceOption = new Option<DirectoryInfo?>("--godot-source")
        {
            Description = "Path to local Godot source tree"
        };
        var outputOption = new Option<string?>("--output")
        {
            Description = "Output file path (default: ~/.guml/api/godot_api_<version>.json)"
        };

        var cmd = new Command("generate-api", "Generate Godot API metadata from XML documentation")
        {
            godotVersionOption, godotSourceOption, outputOption
        };

        cmd.SetAction(parseResult =>
        {
            string? godotVersion = parseResult.GetValue(godotVersionOption);
            string? godotSource = parseResult.GetValue(godotSourceOption)?.FullName;
            string? output = parseResult.GetValue(outputOption);
            return RunGenerateApiAsync(godotVersion, godotSource, output).GetAwaiter().GetResult();
        });

        return cmd;
    }

    private static Command BuildStopCommand()
    {
        var cmd = new Command("stop", "Stop all running guml-analyzer instances");
        cmd.SetAction(_ =>
        {
            int stopped = ProcessControl.StopAll();
            Console.Error.WriteLine(stopped > 0
                ? $"Stopped {stopped} instance(s)."
                : "No running instances found.");
        });
        return cmd;
    }

    private static Command BuildStatusCommand()
    {
        var cmd = new Command("status", "Show running guml-analyzer instances");
        cmd.SetAction(_ =>
        {
            ProcessControl.PrintStatus();
        });
        return cmd;
    }

    // ── Server mode ──

    private static async Task<int> RunServerAsync()
    {
        // Initialize logging to stderr (stdout is reserved for JSON-RPC)
        var logConfig = new LoggerConfiguration()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose);
#if DEBUG
        logConfig = logConfig.MinimumLevel.Debug();
#else
        logConfig = logConfig.MinimumLevel.Information();
#endif
        Log.Logger = logConfig.CreateLogger();

        string version = Assembly.GetExecutingAssembly()
                             .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                         ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                         ?? "unknown";
        Log.Logger.Information("guml-analyzer v{Version} starting", version);

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

        // Create and run the analyzer server
        using var server = new AnalyzerServer(transport, processControl);

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
            Log.Logger.Information("guml-analyzer exiting");
            await Log.CloseAndFlushAsync();
        }

        return 0;
    }

    /// <summary>
    /// Standalone API generation mode: parses Godot XML docs and writes a JSON API file.
    /// </summary>
    private static async Task<int> RunGenerateApiAsync(
        string? godotVersion, string? godotSource, string? output)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .MinimumLevel.Information()
            .CreateLogger();

        if (godotVersion == null && godotSource == null)
        {
            await Console.Error.WriteLineAsync(
                "Error: --godot-version or --godot-source is required.");
            return 1;
        }

        try
        {
            string? resultPath;

            if (godotSource != null)
            {
                // Local source mode
                godotVersion ??= "unknown";
                resultPath = GodotApiCatalog.GenerateFromLocal(
                    godotSource, godotVersion, output,
                    (stage, msg) => Console.Error.WriteLine($"[{stage}] {msg}"));
            }
            else
            {
                // GitHub clone mode
                resultPath = await GodotApiCatalog.GenerateFromGitHubAsync(
                    godotVersion!, output,
                    (stage, msg) => Console.Error.WriteLine($"[{stage}] {msg}"));
            }

            if (resultPath == null)
            {
                await Console.Error.WriteLineAsync("API generation failed.");
                return 1;
            }

            Console.WriteLine(resultPath);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "API generation failed");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}

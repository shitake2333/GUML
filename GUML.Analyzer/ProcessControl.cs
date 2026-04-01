using System.Diagnostics;
using System.IO.Pipes;
using Serilog;

namespace GUML.Analyzer;

/// <summary>
/// Manages PID file and Named Pipe for cross-process control of the API server.
/// Allows external processes to send stop commands via <c>--stop</c> / <c>--restart</c>.
/// </summary>
public sealed class ProcessControl : IDisposable
{
    /// <summary>
    /// The directory where PID files are stored.
    /// </summary>
    public static readonly string PidDirectory =
        Path.Combine(Path.GetTempPath(), "guml-analyzer");

    private const string StopCommand = "STOP";

    private readonly string _pipeName;
    private readonly string _pidFilePath;
    private CancellationTokenSource? _pipeCts;
    private bool _disposed;

    /// <summary>
    /// Raised when a STOP command is received via the Named Pipe.
    /// </summary>
    public event Action? StopRequested;

    /// <summary>
    /// Creates a new ProcessControl instance for the current process.
    /// The pipe name and PID file are derived from the current process ID.
    /// </summary>
    public ProcessControl()
    {
        int pid = Environment.ProcessId;
        _pipeName = $"guml-analyzer-{pid}";
        _pidFilePath = Path.Combine(PidDirectory, $"{pid}.pid");
    }

    /// <summary>
    /// Registers this server instance by writing a PID file and starting the Named Pipe listener.
    /// </summary>
    public void Register()
    {
        Directory.CreateDirectory(PidDirectory);

        // Write PID file with process ID and start time
        File.WriteAllText(_pidFilePath,
            $"{Environment.ProcessId}\n{DateTime.UtcNow:O}");

        _pipeCts = new CancellationTokenSource();
        _ = ListenForCommandsAsync(_pipeCts.Token);
        Log.Logger.Debug("ProcessControl registered: PID={Pid}, Pipe={Pipe}", Environment.ProcessId, _pipeName);
    }

    /// <summary>
    /// Unregisters this server instance by deleting the PID file and stopping the pipe listener.
    /// </summary>
    public void Unregister()
    {
        _pipeCts?.Cancel();
        try
        {
            File.Delete(_pidFilePath);
        }
        catch
        {
            /* ignore */
        }

        Log.Logger.Debug("ProcessControl unregistered");
    }

    /// <summary>
    /// Stops all running guml-api-server instances by sending STOP via Named Pipe
    /// or killing the process if the pipe is unreachable.
    /// </summary>
    /// <returns>The number of instances that were stopped.</returns>
    public static int StopAll()
    {
        if (!Directory.Exists(PidDirectory))
            return 0;

        int stopped = 0;
        foreach (string pidFile in Directory.GetFiles(PidDirectory, "*.pid"))
        {
            try
            {
                string content = File.ReadAllText(pidFile);
                string pidStr = content.Split('\n')[0].Trim();
                if (!int.TryParse(pidStr, out int pid)) continue;

                if (TrySendStopCommand(pid))
                {
                    Console.Error.WriteLine($"Sent STOP to process {pid}");
                    stopped++;
                    // Wait briefly for the process to clean up
                    WaitForProcessExit(pid, timeoutMs: 3000);
                }
                else if (TryKillProcess(pid))
                {
                    Console.Error.WriteLine($"Force-killed process {pid}");
                    stopped++;
                }

                // Clean up orphaned PID file
                try
                {
                    File.Delete(pidFile);
                }
                catch
                {
                    /* ignore */
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing PID file {pidFile}: {ex.Message}");
            }
        }

        return stopped;
    }

    /// <summary>
    /// Prints status information about all running instances to stderr.
    /// </summary>
    public static void PrintStatus()
    {
        if (!Directory.Exists(PidDirectory))
        {
            Console.Error.WriteLine("No running instances found.");
            return;
        }

        string[] pidFiles = Directory.GetFiles(PidDirectory, "*.pid");
        if (pidFiles.Length == 0)
        {
            Console.Error.WriteLine("No running instances found.");
            return;
        }

        foreach (string pidFile in pidFiles)
        {
            try
            {
                string[] lines = File.ReadAllLines(pidFile);
                string pidStr = lines[0].Trim();
                string startTime = lines.Length > 1 ? lines[1].Trim() : "unknown";

                if (!int.TryParse(pidStr, out int pid)) continue;

                bool alive = IsProcessAlive(pid);
                string status = alive ? "RUNNING" : "DEAD (orphaned PID file)";

                string uptime = "";
                if (alive && DateTime.TryParse(startTime, out var started))
                {
                    var elapsed = DateTime.UtcNow - started;
                    uptime = $", uptime={elapsed.TotalMinutes:F0}min";
                }

                Console.Error.WriteLine($"  PID={pid}, status={status}{uptime}");
            }
            catch
            {
                Console.Error.WriteLine($"  (unreadable: {Path.GetFileName(pidFile)})");
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Unregister();
        _pipeCts?.Dispose();
    }

    // ── Private helpers ──

    private async Task ListenForCommandsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct);

                using var reader = new StreamReader(server);
                string? command = await reader.ReadLineAsync(ct);

                if (command?.Trim().Equals(StopCommand, StringComparison.OrdinalIgnoreCase) == true)
                {
                    Log.Logger.Information("Received STOP command via Named Pipe");
                    StopRequested?.Invoke();
                    return; // stop listening after receiving STOP
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    Log.Logger.Warning(ex, "Error in Named Pipe listener");
            }
        }
    }

    private static bool TrySendStopCommand(int pid)
    {
        try
        {
            string pipeName = $"guml-analyzer-{pid}";
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            client.Connect(timeout: 2000);
            using var writer = new StreamWriter(client);
            writer.AutoFlush = true;
            writer.WriteLine(StopCommand);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryKillProcess(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            process.Kill(entireProcessTree: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static void WaitForProcessExit(int pid, int timeoutMs)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            process.WaitForExit(timeoutMs);
        }
        catch
        {
            // Process already gone
        }
    }
}


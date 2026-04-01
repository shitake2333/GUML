using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace GUML.Analyzer;

/// <summary>
/// A JSON-RPC 2.0 request message (Plugin → Server).
/// </summary>
public sealed class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")] public int? Id { get; set; }

    [JsonPropertyName("method")] public string Method { get; set; } = "";

    [JsonPropertyName("params")] public JsonElement? Params { get; set; }
}

/// <summary>
/// A JSON-RPC 2.0 response message (Server → Plugin).
/// </summary>
public sealed class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")] public int? Id { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; set; }
}

/// <summary>
/// A JSON-RPC 2.0 error object.
/// </summary>
public sealed class JsonRpcError
{
    [JsonPropertyName("code")] public int Code { get; set; }

    [JsonPropertyName("message")] public string Message { get; set; } = "";
}

/// <summary>
/// A JSON-RPC 2.0 notification message (Server → Plugin, no id).
/// </summary>
public sealed class JsonRpcNotification
{
    [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("method")] public string Method { get; set; } = "";

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; set; }
}

/// <summary>
/// Handles reading and writing JSON-RPC messages over stdin/stdout
/// using the LSP-compatible Content-Length header framing.
/// </summary>
public sealed class JsonRpcTransport : IDisposable
{
    private static readonly JsonSerializerOptions s_serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    /// A JsonElement representing JSON <c>null</c>. Used as a non-null sentinel
    /// so that <c>WhenWritingNull</c> does not omit the "result" field in responses.
    /// </summary>
    private static readonly JsonElement s_jsonNull = JsonDocument.Parse("null").RootElement.Clone();

    private readonly Stream _input;
    private readonly Stream _output;
    private readonly Lock _writeLock = new();
    private readonly byte[] _readBuf = new byte[1];

    /// <summary>
    /// Creates a new transport using the given input/output streams.
    /// </summary>
    /// <param name="input">The input stream to read requests from (typically stdin).</param>
    /// <param name="output">The output stream to write responses to (typically stdout).</param>
    public JsonRpcTransport(Stream input, Stream output)
    {
        _input = input;
        _output = output;
    }

    /// <summary>
    /// Reads the next JSON-RPC request from the input stream.
    /// Returns null when the stream is closed.
    /// </summary>
    public async Task<JsonRpcRequest?> ReadRequestAsync(CancellationToken ct = default)
    {
        try
        {
            int contentLength = await ReadHeaderAsync(ct);
            if (contentLength <= 0) return null;

            byte[] body = new byte[contentLength];
            int totalRead = 0;
            while (totalRead < contentLength)
            {
                int read = await _input.ReadAsync(body.AsMemory(totalRead, contentLength - totalRead), ct);
                if (read == 0) return null; // stream closed
                totalRead += read;
            }

            return JsonSerializer.Deserialize<JsonRpcRequest>(body, s_serializerOptions);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to read JSON-RPC request");
            return null;
        }
    }

    /// <summary>
    /// Sends a JSON-RPC response to the output stream.
    /// When result is null, a JSON null is still written so the response
    /// remains valid per the JSON-RPC 2.0 spec.
    /// </summary>
    public void SendResponse(int id, object? result)
    {
        // Wrap null in a JsonElement so WhenWritingNull does not omit "result".
        var response = new JsonRpcResponse { Id = id, Result = result ?? s_jsonNull };
        WriteMessage(response);
    }

    /// <summary>
    /// Sends a JSON-RPC error response to the output stream.
    /// </summary>
    public void SendError(int id, int code, string message)
    {
        var response = new JsonRpcResponse { Id = id, Error = new JsonRpcError { Code = code, Message = message } };
        WriteMessage(response);
    }

    /// <summary>
    /// Sends a JSON-RPC notification (no id) to the output stream.
    /// </summary>
    public void SendNotification(string method, object? @params = null)
    {
        var notification = new JsonRpcNotification { Method = method, Params = @params };
        WriteMessage(notification);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Do not dispose stdin/stdout — they are owned by the process
    }

    private void WriteMessage(object message)
    {
        try
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(message, s_serializerOptions);
            byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {json.Length}\r\n\r\n");

            lock (_writeLock)
            {
                _output.Write(header);
                _output.Write(json);
                _output.Flush();
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to write JSON-RPC message");
        }
    }

    private async Task<int> ReadHeaderAsync(CancellationToken ct)
    {
        // Read lines until we find Content-Length header, then blank line
        int contentLength = -1;
        StringBuilder lineBuffer = new();

        while (true)
        {
            int b = await ReadByteAsync(ct);
            if (b == -1) return -1; // stream closed

            char ch = (char)b;
            if (ch == '\n')
            {
                string line = lineBuffer.ToString().TrimEnd('\r');
                lineBuffer.Clear();

                if (line.Length == 0)
                {
                    // Blank line = end of headers
                    return contentLength;
                }

                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    string value = line["Content-Length:".Length..].Trim();
                    if (int.TryParse(value, out int len))
                        contentLength = len;
                }
            }
            else
            {
                lineBuffer.Append(ch);
            }
        }
    }

    private async Task<int> ReadByteAsync(CancellationToken ct)
    {
        int read = await _input.ReadAsync(_readBuf.AsMemory(0, 1), ct);
        return read == 0 ? -1 : _readBuf[0];
    }
}


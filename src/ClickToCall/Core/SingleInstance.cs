using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace ClickToCall.Core;

/// <summary>
/// Ensures only one instance runs per Windows session. Further starts (e.g. a clicked tel: link)
/// forward their command line to the running instance through a named pipe and exit.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private static readonly string Id = $"ClickToCall.{Environment.UserName}.{Process.GetCurrentProcess().SessionId}";

    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cts = new();

    public bool IsFirstInstance { get; }

    /// <summary>Raised on a background thread with the arguments of a later start.</summary>
    public event Action<string[]>? ArgumentsReceived;

    public SingleInstance()
    {
        _mutex = new Mutex(initiallyOwned: true, @"Local\" + Id, out var createdNew);
        IsFirstInstance = createdNew;
    }

    public void StartListening() => _ = Task.Run(ListenLoopAsync);

    public static bool SendToFirstInstance(string[] args)
    {
        // Allow the running instance to bring its dial window to the foreground
        NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);

        try
        {
            using var client = new NamedPipeClientStream(".", Id, PipeDirection.Out);
            client.Connect(3000);
            var payload = Encoding.UTF8.GetBytes(string.Join('\n', args));
            client.Write(payload);
            client.Flush();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Could not reach running instance", ex);
            return false;
        }
    }

    private async Task ListenLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    Id, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(_cts.Token);

                using var reader = new StreamReader(server, Encoding.UTF8);
                var text = await reader.ReadToEndAsync(_cts.Token);
                var args = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                ArgumentsReceived?.Invoke(args);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Error("Pipe server error", ex);
                await Task.Delay(500);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        if (IsFirstInstance)
            _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}

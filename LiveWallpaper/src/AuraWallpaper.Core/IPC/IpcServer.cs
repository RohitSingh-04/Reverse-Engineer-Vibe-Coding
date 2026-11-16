using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AuraWallpaper.Core.IPC
{
    public class IpcServer : IDisposable
    {
        public const string DefaultPipeName = "AuraWallpaperPipe";
        public string PipeName { get; }
        private CancellationTokenSource? _cts;
        private readonly Func<IpcRequest, IpcResponse> _handler;
        private bool _isRunning;

        public bool IsRunning => _isRunning;

        public IpcServer(Func<IpcRequest, IpcResponse> handler, string pipeName = DefaultPipeName)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            PipeName = pipeName;
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();
            Task.Run(() => ServerLoop(_cts.Token));
        }

        private async Task ServerLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous
                    );

                    await pipe.WaitForConnectionAsync(token);

                    using var reader = new StreamReader(pipe);
                    using var writer = new StreamWriter(pipe) { AutoFlush = true };

                    string? line = await reader.ReadLineAsync(token);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        IpcResponse response;
                        try
                        {
                            var req = JsonSerializer.Deserialize<IpcRequest>(line);
                            response = req != null ? _handler(req) : IpcResponse.Fail("Invalid JSON request.");
                        }
                        catch (Exception ex)
                        {
                            response = IpcResponse.Fail($"Error processing request: {ex.Message}");
                        }

                        string respJson;
                        try
                        {
                            respJson = JsonSerializer.Serialize(response);
                        }
                        catch (Exception ex)
                        {
                            respJson = JsonSerializer.Serialize(IpcResponse.Fail($"Serialization error: {ex.Message}"));
                        }
                        await writer.WriteLineAsync(respJson);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Delay slightly on unexpected error before restarting pipe listener
                    await Task.Delay(100, token);
                }
            }
            _isRunning = false;
        }

        public void Stop()
        {
            _cts?.Cancel();
            _isRunning = false;
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}

using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Tasks;

namespace AuraWallpaper.Core.IPC
{
    public static class IpcClient
    {
        public static async Task<IpcResponse> SendCommandAsync(IpcRequest request, int timeoutMs = 4000, string pipeName = IpcServer.DefaultPipeName)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(timeoutMs);

                using var reader = new StreamReader(pipe);
                using var writer = new StreamWriter(pipe) { AutoFlush = true };

                string json = JsonSerializer.Serialize(request);
                await writer.WriteLineAsync(json);

                string? respJson = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(respJson))
                {
                    return IpcResponse.Fail("Received empty response from Aura Wallpaper service.");
                }

                var response = JsonSerializer.Deserialize<IpcResponse>(respJson);
                return response ?? IpcResponse.Fail("Failed to deserialize response.");
            }
            catch (TimeoutException)
            {
                return IpcResponse.Fail("Connection timed out. Aura Wallpaper application is not running or not responding.");
            }
            catch (Exception ex)
            {
                return IpcResponse.Fail($"IPC communication error: {ex.Message}");
            }
        }
    }
}

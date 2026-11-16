using System;
using AuraWallpaper.Core.Engine;
using AuraWallpaper.Core.IPC;
using AuraWallpaper.Core.Models;
using AuraWallpaper.Core.Storage;
using Xunit;

namespace AuraWallpaper.Tests
{
    public class IpcTests
    {
        [Fact]
        public void HandleIpcCommand_PlayAndPause_ReturnsSuccess()
        {
            var manager = WallpaperManager.Instance;

            var pauseReq = new IpcRequest { Command = "pause" };
            var pauseResp = manager.HandleIpcCommand(pauseReq);
            Assert.True(pauseResp.Success);

            var playReq = new IpcRequest { Command = "play" };
            var playResp = manager.HandleIpcCommand(playReq);
            Assert.True(playResp.Success);
        }

        [Fact]
        public void HandleIpcCommand_Volume_SetsCorrectLevel()
        {
            var manager = WallpaperManager.Instance;
            var req = new IpcRequest { Command = "volume", Volume = 75 };
            var resp = manager.HandleIpcCommand(req);

            Assert.True(resp.Success);
            Assert.Contains("75", resp.Message);
        }

        [Fact]
        public void HandleIpcCommand_Status_ReturnsSystemMetrics()
        {
            var manager = WallpaperManager.Instance;
            var req = new IpcRequest { Command = "status" };
            var resp = manager.HandleIpcCommand(req);

            Assert.True(resp.Success);
            Assert.NotNull(resp.Data);
        }

        [Fact]
        public void HandleIpcCommand_UnknownCommand_ReturnsFailure()
        {
            var manager = WallpaperManager.Instance;
            var req = new IpcRequest { Command = "invalid_nonexistent_command" };
            var resp = manager.HandleIpcCommand(req);

            Assert.False(resp.Success);
            Assert.Contains("Unknown command", resp.Message);
        }

        [Fact]
        public async System.Threading.Tasks.Task IpcServer_And_IpcClient_Communication_Succeeds()
        {
            string testPipe = "AuraWallpaper_TestPipe_" + Guid.NewGuid().ToString("N");
            using var server = new IpcServer(req => IpcResponse.Ok("Echo: " + req.Command), testPipe);
            server.Start();
            var resp = await IpcClient.SendCommandAsync(new IpcRequest { Command = "ping" }, 4000, testPipe);
            Assert.True(resp.Success);
            Assert.Equal("Echo: ping", resp.Message);
        }
    }
}

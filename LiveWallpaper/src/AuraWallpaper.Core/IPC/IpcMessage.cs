using System;
using System.Collections.Generic;

namespace AuraWallpaper.Core.IPC
{
    public class IpcRequest
    {
        public string Command { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public int MonitorIndex { get; set; } = -1; // -1 = default / all
        public int Volume { get; set; } = -1;
        public bool Random { get; set; }
        public bool All { get; set; }
        public string? Mode { get; set; }
        public string? Scaling { get; set; }
        public string? PropertyName { get; set; }
        public string? PropertyValue { get; set; }
        public Dictionary<string, string> ExtraArgs { get; set; } = new Dictionary<string, string>();
    }

    public class IpcResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }

        public static IpcResponse Ok(string message = "OK", object? data = null) =>
            new IpcResponse { Success = true, Message = message, Data = data };

        public static IpcResponse Fail(string message, object? data = null) =>
            new IpcResponse { Success = false, Message = message, Data = data };
    }
}

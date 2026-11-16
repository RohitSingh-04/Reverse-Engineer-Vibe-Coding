using System;
using System.Text.Json;
using AuraWallpaper.Core.Audio;

namespace AuraWallpaper.Core.Utils
{
    public static class WallpaperSdkBridge
    {
        public const string InjectedJavaScript = @"
(function() {
    window.aura = {
        isAuraWallpaper: true,
        version: '1.0.0',
        onPropertyChange: null,
        onAudioData: null,
        onMediaChange: null,
        onPause: null,
        onResume: null,
        postMessage: function(msg) {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage(msg);
            }
        }
    };

    // Forward Lively Wallpaper event handlers for complete backwards compatibility
    window.livelyAudioListener = window.livelyAudioListener || function(audioArray) {};
    window.livelyPropertyListener = window.livelyPropertyListener || function(name, val) {};
    window.livelyCurrentTrack = window.livelyCurrentTrack || function(trackJson) {};

    // Listen for messages from Aura Host
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.addEventListener('message', function(event) {
            var data = event.data;
            if (!data || !data.type) return;

            if (data.type === 'audio') {
                if (window.auraAudioListener) window.auraAudioListener(data.spectrum);
                if (window.livelyAudioListener) window.livelyAudioListener(data.spectrum);
                if (window.aura.onAudioData) window.aura.onAudioData(data);
            } else if (data.type === 'property') {
                if (window.auraPropertyListener) window.auraPropertyListener(data.name, data.value);
                if (window.livelyPropertyListener) window.livelyPropertyListener(data.name, data.value);
                if (window.aura.onPropertyChange) window.aura.onPropertyChange(data.name, data.value);
            } else if (data.type === 'media') {
                if (window.auraCurrentTrack) window.auraCurrentTrack(data.track);
                if (window.livelyCurrentTrack) window.livelyCurrentTrack(JSON.stringify(data.track));
                if (window.aura.onMediaChange) window.aura.onMediaChange(data.track);
            } else if (data.type === 'lifecycle') {
                if (data.action === 'pause' && window.aura.onPause) window.aura.onPause();
                if (data.action === 'resume' && window.aura.onResume) window.aura.onResume();
            }
        });
    }
})();
";

        public static string CreateAudioMessageJson(AudioFrameData frame)
        {
            var msg = new
            {
                type = "audio",
                spectrum = frame.Spectrum64,
                spectrum32 = frame.Spectrum32,
                spectrum16 = frame.Spectrum16,
                bass = frame.Bass,
                mid = frame.Mid,
                treble = frame.Treble,
                peak = frame.Peak
            };
            return JsonSerializer.Serialize(msg);
        }

        public static string CreatePropertyMessageJson(string name, object? value)
        {
            var msg = new
            {
                type = "property",
                name = name,
                value = value
            };
            return JsonSerializer.Serialize(msg);
        }

        public static string CreateMediaMessageJson(MediaTrackInfo track)
        {
            var msg = new
            {
                type = "media",
                track = track
            };
            return JsonSerializer.Serialize(msg);
        }
    }
}

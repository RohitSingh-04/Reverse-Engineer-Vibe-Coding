using System;
using NAudio.Wave;

namespace AuraWallpaper.Core.Audio
{
    public class AudioFrameData
    {
        public float[] Spectrum16 { get; set; } = new float[16];
        public float[] Spectrum32 { get; set; } = new float[32];
        public float[] Spectrum64 { get; set; } = new float[64];
        public float Bass { get; set; }
        public float Mid { get; set; }
        public float Treble { get; set; }
        public float Peak { get; set; }
    }

    public class WasapiAudioCapture : IDisposable
    {
        private static WasapiAudioCapture? _instance;
        public static WasapiAudioCapture Instance => _instance ??= new WasapiAudioCapture();

        private WasapiLoopbackCapture? _capture;
        private readonly float[] _fftBuffer = new float[1024];
        private readonly float[] _smoothedSpectrum = new float[64];
        private bool _isCapturing;
        private readonly object _lock = new object();

        public event EventHandler<AudioFrameData>? AudioDataAvailable;
        public bool IsCapturing => _isCapturing;
        public double Sensitivity { get; set; } = 1.0;

        private WasapiAudioCapture()
        {
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_isCapturing) return;

                try
                {
                    _capture = new WasapiLoopbackCapture();
                    _capture.DataAvailable += OnDataAvailable;
                    _capture.RecordingStopped += (s, a) => _isCapturing = false;
                    _capture.StartRecording();
                    _isCapturing = true;
                }
                catch
                {
                    // If no audio playback device is active or permission denied, fail gracefully
                    _isCapturing = false;
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!_isCapturing || _capture == null) return;
                try
                {
                    _capture.StopRecording();
                    _capture.Dispose();
                }
                catch
                {
                }
                finally
                {
                    _capture = null;
                    _isCapturing = false;
                }
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0) return;

            int bytesPerSample = 4; // 32-bit float standard in WASAPI
            int sampleCount = e.BytesRecorded / bytesPerSample;
            int step = Math.Max(1, sampleCount / _fftBuffer.Length);

            float peak = 0f;
            for (int i = 0; i < _fftBuffer.Length; i++)
            {
                int sampleIndex = i * step * bytesPerSample;
                if (sampleIndex + 3 < e.BytesRecorded)
                {
                    float sample = BitConverter.ToSingle(e.Buffer, sampleIndex);
                    // Apply Hann window
                    float hann = 0.5f * (1f - (float)Math.Cos(2 * Math.PI * i / (_fftBuffer.Length - 1)));
                    _fftBuffer[i] = sample * hann;
                    float abs = Math.Abs(sample);
                    if (abs > peak) peak = abs;
                }
                else
                {
                    _fftBuffer[i] = 0f;
                }
            }

            // Real FFT computation (magnitude spectrum)
            float[] magnitudes = ComputeFftMagnitudes(_fftBuffer);

            // Group into 64 frequency bands
            var frame = new AudioFrameData { Peak = peak * (float)Sensitivity };
            int binSize = Math.Max(1, magnitudes.Length / 64);

            float bassSum = 0;
            float midSum = 0;
            float trebleSum = 0;

            for (int band = 0; band < 64; band++)
            {
                float sum = 0;
                int start = band * binSize;
                int end = Math.Min(start + binSize, magnitudes.Length);
                for (int b = start; b < end; b++)
                {
                    sum += magnitudes[b];
                }
                float val = (sum / (end - start)) * (float)Sensitivity;
                if (val > 1.0f) val = 1.0f;

                // Smooth: 30% new, 70% previous
                _smoothedSpectrum[band] = (_smoothedSpectrum[band] * 0.7f) + (val * 0.3f);
                frame.Spectrum64[band] = _smoothedSpectrum[band];

                // Bass (first 8 bands ~ 20Hz-250Hz)
                if (band < 8) bassSum += _smoothedSpectrum[band];
                // Mid (bands 8 to 36 ~ 250Hz-4000Hz)
                else if (band < 36) midSum += _smoothedSpectrum[band];
                // Treble (bands 36 to 64 ~ 4000Hz-20000Hz)
                else trebleSum += _smoothedSpectrum[band];
            }

            frame.Bass = Math.Min(1.0f, bassSum / 8f);
            frame.Mid = Math.Min(1.0f, midSum / 28f);
            frame.Treble = Math.Min(1.0f, trebleSum / 28f);

            // Downsample for 32 and 16 bins
            for (int i = 0; i < 32; i++)
            {
                frame.Spectrum32[i] = (frame.Spectrum64[i * 2] + frame.Spectrum64[i * 2 + 1]) / 2f;
            }
            for (int i = 0; i < 16; i++)
            {
                frame.Spectrum16[i] = (frame.Spectrum32[i * 2] + frame.Spectrum32[i * 2 + 1]) / 2f;
            }

            AudioDataAvailable?.Invoke(this, frame);
        }

        private static float[] ComputeFftMagnitudes(float[] input)
        {
            int n = input.Length;
            int half = n / 2;
            float[] mags = new float[half];

            // In-place Radix-2 Cooley-Tukey FFT
            float[] real = new float[n];
            float[] imag = new float[n];
            Array.Copy(input, real, n);

            // Bit-reversal permutation
            int j = 0;
            for (int i = 0; i < n - 1; i++)
            {
                if (i < j)
                {
                    float tempR = real[i];
                    real[i] = real[j];
                    real[j] = tempR;
                }
                int k = n / 2;
                while (k <= j)
                {
                    j -= k;
                    k /= 2;
                }
                j += k;
            }

            // Cooley-Tukey calculation
            for (int len = 2; len <= n; len <<= 1)
            {
                double angle = -2 * Math.PI / len;
                float wlenR = (float)Math.Cos(angle);
                float wlenI = (float)Math.Sin(angle);
                for (int i = 0; i < n; i += len)
                {
                    float wR = 1f;
                    float wI = 0f;
                    for (int k = 0; k < len / 2; k++)
                    {
                        float uR = real[i + k];
                        float uI = imag[i + k];
                        float vR = (real[i + k + len / 2] * wR) - (imag[i + k + len / 2] * wI);
                        float vI = (real[i + k + len / 2] * wI) + (imag[i + k + len / 2] * wR);

                        real[i + k] = uR + vR;
                        imag[i + k] = uI + vI;
                        real[i + k + len / 2] = uR - vR;
                        imag[i + k + len / 2] = uI - vI;

                        float nextWR = (wR * wlenR) - (wI * wlenI);
                        float nextWI = (wR * wlenI) + (wI * wlenR);
                        wR = nextWR;
                        wI = nextWI;
                    }
                }
            }

            // Calculate magnitude
            for (int i = 0; i < half; i++)
            {
                mags[i] = (float)Math.Sqrt((real[i] * real[i]) + (imag[i] * imag[i])) / half;
            }

            return mags;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

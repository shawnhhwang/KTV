using System;
using System.Runtime.InteropServices;
using System.Windows;
using LibVLCSharp.Shared;
using Serilog;

namespace KTV
{
    public partial class PlayerWindow : Window
    {
        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;
        private AudioOutputChannel _currentChannel = AudioOutputChannel.Stereo;

        // SoundTouch & NAudio
        private SoundTouchProcessor? _soundTouchProcessor;
        private NAudio.Wave.IWavePlayer? _audioOutput;
        private NAudio.Wave.BufferedWaveProvider? _bufferedWaveProvider;

        // Keep delegate references to prevent GC garbage collection crashes
        private MediaPlayer.LibVLCAudioPlayCb? _audioPlayCb;
        private MediaPlayer.LibVLCAudioPauseCb? _audioPauseCb;
        private MediaPlayer.LibVLCAudioResumeCb? _audioResumeCb;
        private MediaPlayer.LibVLCAudioFlushCb? _audioFlushCb;
        private MediaPlayer.LibVLCAudioDrainCb? _audioDrainCb;

        public PlayerWindow()
        {
            InitializeComponent();
            Loaded += PlayerWindow_Loaded;
            Unloaded += PlayerWindow_Unloaded;
        }

        private void PlayerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Core.Initialize();
                _libVLC = new LibVLC();
                _mediaPlayer = new MediaPlayer(_libVLC);

                // 1. Initialize SoundTouch and NAudio
                _soundTouchProcessor = new SoundTouchProcessor(44100, 2);
                InitializeAudioOutput();

                // 2. Setup VLC audio callbacks redirection
                _audioPlayCb = AudioPlayCallback;
                _audioPauseCb = AudioPauseCallback;
                _audioResumeCb = AudioResumeCallback;
                _audioFlushCb = AudioFlushCallback;
                _audioDrainCb = AudioDrainCallback;

                _mediaPlayer.SetAudioFormat("FL32", 44100, 2);
                _mediaPlayer.SetAudioCallbacks(_audioPlayCb, _audioPauseCb, _audioResumeCb, _audioFlushCb, _audioDrainCb);

                VlcVideoView.MediaPlayer = _mediaPlayer;
                Log.Information("LibVLC and NAudio+SoundTouch pipeline initialized in PlayerWindow.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize LibVLC/Audio pipeline.");
                MessageBox.Show($"VLC/Audio Init Error: {ex.Message}", "Init Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeAudioOutput()
        {
            var waveFormat = NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
            _bufferedWaveProvider = new NAudio.Wave.BufferedWaveProvider(waveFormat)
            {
                DiscardOnBufferOverflow = true
            };

            int latency = 20;
            if (App.Configuration != null)
            {
                int.TryParse(App.Configuration["Audio:BufferLatencyMs"] ?? "20", out latency);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    _audioOutput = new NAudio.Wave.WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, latency);
                    _audioOutput.Init(_bufferedWaveProvider);
                    _audioOutput.Play();
                    Log.Information("NAudio WASAPI output initialized successfully. Latency={Latency}ms.", latency);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to initialize NAudio WASAPI output. Falling back to WaveOutEvent.");
                    try
                    {
                        _audioOutput = new NAudio.Wave.WaveOutEvent { DesiredLatency = latency };
                        _audioOutput.Init(_bufferedWaveProvider);
                        _audioOutput.Play();
                        Log.Information("Fallback WaveOutEvent initialized.");
                    }
                    catch (Exception fallbackEx)
                    {
                        Log.Error(fallbackEx, "Failed to initialize fallback WaveOutEvent.");
                    }
                }
            }
            else
            {
                Log.Warning("Not running on Windows. NAudio device output simulated.");
            }
        }

        private void AudioPlayCallback(IntPtr data, IntPtr samples, uint count, long pts)
        {
            int numFloatElements = (int)count * 2;
            float[] pcmSamples = new float[numFloatElements];
            Marshal.Copy(samples, pcmSamples, 0, numFloatElements);

            if (_soundTouchProcessor != null && _soundTouchProcessor.IsNativeLoaded)
            {
                _soundTouchProcessor.PutSamples(pcmSamples, (int)count);
                
                float[] shiftedSamples = new float[numFloatElements * 2];
                int receivedSamplesCount = _soundTouchProcessor.ReceiveSamples(shiftedSamples, (int)count * 2);
                
                if (receivedSamplesCount > 0)
                {
                    int byteCount = receivedSamplesCount * 2 * sizeof(float);
                    byte[] byteArray = new byte[byteCount];
                    Buffer.BlockCopy(shiftedSamples, 0, byteArray, 0, byteCount);
                    _bufferedWaveProvider?.AddSamples(byteArray, 0, byteArray.Length);
                }
            }
            else
            {
                // Bypass SoundTouch (macOS or missing native DLL)
                int byteCount = numFloatElements * sizeof(float);
                byte[] byteArray = new byte[byteCount];
                Buffer.BlockCopy(pcmSamples, 0, byteArray, 0, byteCount);
                _bufferedWaveProvider?.AddSamples(byteArray, 0, byteArray.Length);
            }
        }

        private void AudioPauseCallback(IntPtr data, long pts)
        {
            _audioOutput?.Pause();
        }

        private void AudioResumeCallback(IntPtr data, long pts)
        {
            _audioOutput?.Play();
        }

        private void AudioFlushCallback(IntPtr data, long pts)
        {
            _bufferedWaveProvider?.ClearBuffer();
            _soundTouchProcessor?.Clear();
        }

        private void AudioDrainCallback(IntPtr data)
        {
            // Simple drain behavior - wait until played or continue
        }

        public void PlayMedia(string path)
        {
            if (_libVLC == null || _mediaPlayer == null) return;

            try
            {
                _mediaPlayer.Stop();

                Media media;
                if (Uri.TryCreate(path, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                {
                    media = new Media(_libVLC, uriResult);
                }
                else
                {
                    media = new Media(_libVLC, path, FromType.FromPath);
                }

                using (media)
                {
                    _mediaPlayer.Play(media);
                }
                Log.Information("Playing media: {Path}", path);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to play media: {Path}", path);
                MessageBox.Show($"Playback Error: {ex.Message}", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void StopMedia()
        {
            _mediaPlayer?.Stop();
            _audioOutput?.Stop();
        }

        public void SetPitch(int semitones)
        {
            if (_soundTouchProcessor != null)
            {
                _soundTouchProcessor.SetPitchSemiTones(semitones);
            }
        }

        public string ToggleVocalChannel()
        {
            if (_mediaPlayer == null) return "無播放器";

            if (_currentChannel == AudioOutputChannel.Stereo)
            {
                _currentChannel = AudioOutputChannel.Left; // Copy Left to both (typically original)
            }
            else if (_currentChannel == AudioOutputChannel.Left)
            {
                _currentChannel = AudioOutputChannel.Right; // Copy Right to both (typically accompaniment)
            }
            else
            {
                _currentChannel = AudioOutputChannel.Stereo; // Standard Stereo
            }

            bool success = _mediaPlayer.SetChannel(_currentChannel);
            Log.Information("Vocal channel toggled to: {Channel}. Success: {Success}", _currentChannel, success);
            return _currentChannel switch
            {
                AudioOutputChannel.Left => "原唱 (左聲道)",
                AudioOutputChannel.Right => "伴唱 (右聲道)",
                _ => "立體聲"
            };
        }

        public MediaPlayer? GetMediaPlayer() => _mediaPlayer;

        private void PlayerWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
            _soundTouchProcessor?.Dispose();
            _audioOutput?.Dispose();
        }
    }
}

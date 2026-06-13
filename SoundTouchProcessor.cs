using System;
using Serilog;

namespace KTV
{
    public class SoundTouchProcessor : IDisposable
    {
        private IntPtr _handle;
        private bool _isDisposed = false;

        public SoundTouchProcessor(int sampleRate, int channels)
        {
            try
            {
                _handle = SoundTouchInterop.soundtouch_createInstance();
                if (_handle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create SoundTouch instance.");
                }

                SoundTouchInterop.soundtouch_setSampleRate(_handle, (uint)sampleRate);
                SoundTouchInterop.soundtouch_setChannels(_handle, (uint)channels);
                Log.Information("SoundTouch instance created: SampleRate={SampleRate}, Channels={Channels}", sampleRate, channels);
            }
            catch (DllNotFoundException ex)
            {
                Log.Warning("SoundTouch native DLL not found. SoundTouch processor running in mock mode. Error: {Message}", ex.Message);
                _handle = IntPtr.Zero;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize SoundTouch processor.");
                _handle = IntPtr.Zero;
            }
        }

        public bool IsNativeLoaded => _handle != IntPtr.Zero;

        public void SetPitchSemiTones(float pitchSemiTones)
        {
            if (_handle == IntPtr.Zero) return;
            SoundTouchInterop.soundtouch_setPitchSemiTones(_handle, pitchSemiTones);
            Log.Information("SoundTouch pitch set to {Pitch} semitones.", pitchSemiTones);
        }

        public void PutSamples(float[] samples, int numSamples)
        {
            if (_handle == IntPtr.Zero) return;
            SoundTouchInterop.soundtouch_putSamples(_handle, samples, (uint)numSamples);
        }

        public int ReceiveSamples(float[] outBuffer, int maxSamples)
        {
            if (_handle == IntPtr.Zero) return 0;
            return (int)SoundTouchInterop.soundtouch_receiveSamples(_handle, outBuffer, (uint)maxSamples);
        }

        public void Clear()
        {
            if (_handle == IntPtr.Zero) return;
            SoundTouchInterop.soundtouch_clear(_handle);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                if (_handle != IntPtr.Zero)
                {
                    try
                    {
                        SoundTouchInterop.soundtouch_destroyInstance(_handle);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error destroying SoundTouch instance.");
                    }
                    _handle = IntPtr.Zero;
                }
                _isDisposed = true;
            }
            GC.SuppressFinalize(this);
        }

        ~SoundTouchProcessor()
        {
            Dispose();
        }
    }
}

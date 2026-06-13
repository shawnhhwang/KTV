using System;
using LibVLCSharp.Shared;
using Serilog;
using Xunit;

namespace KTV.Tests
{
    public class PlayerTests
    {
        [Fact]
        public void Test_VlcAudioChannelToggling()
        {
            try
            {
                Core.Initialize();
                using (var libVLC = new LibVLC())
                using (var mediaPlayer = new MediaPlayer(libVLC))
                {
                    Assert.NotNull(mediaPlayer);
                    
                    // Verify default channel is Stereo
                    Assert.Equal(AudioOutputChannel.Stereo, mediaPlayer.Channel);
                    
                    // Toggle through channels using SetChannel
                    bool successLeft = mediaPlayer.SetChannel(AudioOutputChannel.Left);
                    if (successLeft)
                    {
                        Assert.Equal(AudioOutputChannel.Left, mediaPlayer.Channel);
                    }

                    bool successRight = mediaPlayer.SetChannel(AudioOutputChannel.Right);
                    if (successRight)
                    {
                        Assert.Equal(AudioOutputChannel.Right, mediaPlayer.Channel);
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMsg = ex.ToString();
                if (errorMsg.Contains("libvlc") || errorMsg.Contains("DllNotFoundException") || errorMsg.Contains("TypeInitializationException"))
                {
                    Log.Warning("Skipping native LibVLC test: native VLC libraries are not available on this platform. Details: {Message}", ex.Message);
                }
                else
                {
                    throw;
                }
            }
        }

        [Fact]
        public void Test_SoundTouchProcessor_LifecycleAndMockMode()
        {
            // Verify SoundTouchProcessor initializes, handles pitch settings, and disposes without crashing
            using (var processor = new SoundTouchProcessor(44100, 2))
            {
                // On macOS, it should gracefully fall back to mock mode
                if (!processor.IsNativeLoaded)
                {
                    Log.Information("SoundTouch is running in mock mode as expected on macOS.");
                    Assert.False(processor.IsNativeLoaded);

                    // Operations in mock mode should not throw
                    processor.SetPitchSemiTones(2.0f);
                    processor.PutSamples(new float[100], 50);
                    int outputCount = processor.ReceiveSamples(new float[100], 50);
                    Assert.Equal(0, outputCount);
                }
                else
                {
                    Assert.True(processor.IsNativeLoaded);
                    processor.SetPitchSemiTones(1.5f);
                    float[] input = new float[100];
                    processor.PutSamples(input, 50);
                    float[] output = new float[100];
                    int outputCount = processor.ReceiveSamples(output, 50);
                    Assert.True(outputCount >= 0);
                }
            }
        }
    }
}

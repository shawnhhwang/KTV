using System;
using System.Runtime.InteropServices;

namespace KTV
{
    public static class SoundTouchInterop
    {
        private const string DllName = "SoundTouch.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr soundtouch_createInstance();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void soundtouch_destroyInstance(IntPtr h);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void soundtouch_setPitchSemiTones(IntPtr h, float newPitch);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void soundtouch_setSampleRate(IntPtr h, uint srate);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void soundtouch_setChannels(IntPtr h, uint channels);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void soundtouch_putSamples(IntPtr h, [In] float[] samples, uint numSamples);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint soundtouch_receiveSamples(IntPtr h, [Out] float[] outBuffer, uint maxSamples);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void soundtouch_clear(IntPtr h);
    }
}

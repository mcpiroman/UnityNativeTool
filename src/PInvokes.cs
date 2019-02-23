using System;
using System.Runtime.InteropServices;

namespace DllManipulator.Internal
{
    internal static class PInvokes
    {
#if UNITY_STANDALONE_WIN
        [DisableMocking]
        [DllImport("kernel32", EntryPoint = "LoadLibrary")]
        public static extern IntPtr Windows_LoadLibrary(string lpFileName);

        [DisableMocking]
        [DllImport("kernel32", EntryPoint = "FreeLibrary")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Windows_FreeLibrary(IntPtr hModule);

        [DisableMocking]
        [DllImport("kernel32", EntryPoint = "GetProcAddress")]
        public static extern IntPtr Windows_GetProcAddress(IntPtr hModule, string procedureName);
#elif UNITY_STANDALONE_LINUX
        [DisableMocking]
        [DllImport("libdl.so", EntryPoint = "dlopen")]
        public static extern IntPtr Linux_dlopen(string filename, int flags);

        [DisableMocking]
        [DllImport("libdl.so", EntryPoint = "dlsym")]
        public static extern IntPtr Linux_dlsym(IntPtr handle, string symbol);

        [DisableMocking]
        [DllImport("libdl.so", EntryPoint = "dlclose")]
        public static extern int Linux_dlclose(IntPtr handle);
#elif UNITY_STANDALONE_OSX
        [DisableMocking]
        [DllImport("libdl.dylib", EntryPoint = "dlopen")]
        public static extern IntPtr Osx_dlopen(string filename, int flags);

        [DisableMocking]
        [DllImport("libdl.dylib", EntryPoint = "dlsym")]
        public static extern IntPtr Osx_dlsym(IntPtr handle, string symbol);

        [DisableMocking]
        [DllImport("libdl.dylib", EntryPoint = "dlclose")]
        public static extern int Osx_dlclose(IntPtr handle);
#endif
    }
}

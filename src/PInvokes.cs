using System;
using System.Runtime.InteropServices;

namespace DllManipulator.Internal
{
    [DisableMocking]
    internal static class PInvokes
    {
        [DllImport("kernel32", EntryPoint = "LoadLibrary")]
        public static extern IntPtr Windows_LoadLibrary(string lpFileName);

        [DllImport("kernel32", EntryPoint = "FreeLibrary")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Windows_FreeLibrary(IntPtr hModule);

        [DllImport("kernel32", EntryPoint = "GetProcAddress")]
        public static extern IntPtr Windows_GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("kernel32", EntryPoint = "VirtualProtect")]
        internal static extern bool Windows_VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, Protection flNewProtect, out Protection lpflOldProtect);


        [DllImport("libdl.so", EntryPoint = "dlopen")]
        public static extern IntPtr Linux_dlopen(string filename, int flags);

        [DllImport("libdl.so", EntryPoint = "dlsym")]
        public static extern IntPtr Linux_dlsym(IntPtr handle, string symbol);

        [DllImport("libdl.so", EntryPoint = "dlclose")]
        public static extern int Linux_dlclose(IntPtr handle);


        [DllImport("libdl.dylib", EntryPoint = "dlopen")]
        public static extern IntPtr Osx_dlopen(string filename, int flags);

        [DllImport("libdl.dylib", EntryPoint = "dlsym")]
        public static extern IntPtr Osx_dlsym(IntPtr handle, string symbol);

        [DllImport("libdl.dylib", EntryPoint = "dlclose")]
        public static extern int Osx_dlclose(IntPtr handle);
    }
}

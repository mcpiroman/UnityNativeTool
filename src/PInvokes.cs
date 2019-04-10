using System;
using System.Runtime.InteropServices;

namespace UnityNativeTool
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
        internal static extern bool Windows_VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, Windows_Protection flNewProtect, out Windows_Protection lpflOldProtect);


        [DllImport("libdl.so", EntryPoint = "dlopen")]
        public static extern IntPtr Linux_dlopen(string filename, int flags);

        [DllImport("libdl.so", EntryPoint = "dlsym")]
        public static extern IntPtr Linux_dlsym(IntPtr handle, string symbol);

        [DllImport("libdl.so", EntryPoint = "dlclose")]
        public static extern int Linux_dlclose(IntPtr handle);

        [DllImport("libc.so", EntryPoint = "mprotect")]
        public static extern int Linux_mprotect(IntPtr addr, UIntPtr len, Linux_Prot prot);

        [DllImport("libc.so", EntryPoint = "sysconf")]
        public static extern IntPtr Linux_sysconf(int name);

        public const int Linux_SC_PAGE_SIZE = 30;


        [DllImport("libdl.dylib", EntryPoint = "dlopen")]
        public static extern IntPtr Osx_dlopen(string filename, int flags);

        [DllImport("libdl.dylib", EntryPoint = "dlsym")]
        public static extern IntPtr Osx_dlsym(IntPtr handle, string symbol);

        [DllImport("libdl.dylib", EntryPoint = "dlclose")]
        public static extern int Osx_dlclose(IntPtr handle);

    }

    /// <summary>A bit-field of flags for protections</summary>
    [Flags]
    internal enum Windows_Protection
    {
        /// <summary>No access</summary>
        PAGE_NOACCESS = 0x01,
        /// <summary>Read only</summary>
        PAGE_READONLY = 0x02,
        /// <summary>Read write</summary>
        PAGE_READWRITE = 0x04,
        /// <summary>Write copy</summary>
        PAGE_WRITECOPY = 0x08,
        /// <summary>No access</summary>
        PAGE_EXECUTE = 0x10,
        /// <summary>Execute read</summary>
        PAGE_EXECUTE_READ = 0x20,
        /// <summary>Execute read write</summary>
        PAGE_EXECUTE_READWRITE = 0x40,
        /// <summary>Execute write copy</summary>
        PAGE_EXECUTE_WRITECOPY = 0x80,
        /// <summary>guard</summary>
        PAGE_GUARD = 0x100,
        /// <summary>No cache</summary>
        PAGE_NOCACHE = 0x200,
        /// <summary>Write combine</summary>
        PAGE_WRITECOMBINE = 0x400
    }

    [Flags]
    internal enum Linux_Prot
    {
        /// <summary>page can be read</summary>
        PROT_READ = 0x1,
        /// <summary>page can be written</summary>
        PROT_WRITE = 0x2,
        /// <summary>page can be executed</summary>
        PROT_EXEC = 0x4,
        /// <summary>page may be used for atomic ops</summary>
        PROT_SEM = 0x8,
        /// <summary>page can not be accessed</summary>
        PROT_NONE = 0x0,
        /// <summary>extend change to start of growsdown vma</summary>
        PROT_GROWSDOWN = 0x01000000,
        /// <summary>extend change to end of growsup vma</summary>
        PROT_GROWSUP = 0x02000000,
    }

    public enum Unix_DlopenFlags : int
    {
        Lazy = 0x00001,
        Now = 0x00002,
        Lazy_Global = 0x00100 | Lazy,
        Now_Global = 0x00100 | Now
    }
}

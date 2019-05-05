using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace UnityNativeTool.Internal
{
    [DisableMocking]
    internal static class PInvokes_Windows
    {
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        /// <summary>A bit-field of flags for protections</summary>
        [Flags]
        public enum Protection
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
        public enum LoadLibraryFlags : uint
        {
            None = 0,
            DONT_RESOLVE_DLL_REFERENCES = 0x00000001,
            LOAD_IGNORE_CODE_AUTHZ_LEVEL = 0x00000010,
            LOAD_LIBRARY_AS_DATAFILE = 0x00000002,
            LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE = 0x00000040,
            LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020,
            LOAD_LIBRARY_SEARCH_APPLICATION_DIR = 0x00000200,
            LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000,
            LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100,
            LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800,
            LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400,
            LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008
        }

        [Flags]
        public enum FinalPathFlags : uint
        {
            VOLUME_NAME_DOS = 0x0,
            FILE_NAME_NORMALIZED = 0x0,
            VOLUME_NAME_GUID = 0x1,
            VOLUME_NAME_NT = 0x2,
            VOLUME_NAME_NONE = 0x4,
            FILE_NAME_OPENED = 0x8
        }

        /// <summary>
        /// Contains values that specify security impersonation levels. Security impersonation levels govern the degree to which a server process can act on behalf of a client process.
        /// </summary>
        public enum SECURITY_IMPERSONATION_LEVEL
        {
            /// <summary>
            /// The server process cannot obtain identification information about the client, and it cannot impersonate the client. It is defined with no value given, and thus, by ANSI C rules, defaults to a value of zero.
            /// </summary>
            SecurityAnonymous,

            /// <summary>
            /// The server process can obtain information about the client, such as security identifiers and privileges, but it cannot impersonate the client. This is useful for servers that export their own objects, for example, database products that export tables and views. Using the retrieved client-security information, the server can make access-validation decisions without being able to use other services that are using the client's security context.
            /// </summary>
            SecurityIdentification,

            /// <summary>
            /// The server process can impersonate the client's security context on its local system. The server cannot impersonate the client on remote systems.
            /// </summary>
            SecurityImpersonation,

            /// <summary>
            /// The server process can impersonate the client's security context on remote systems.
            /// </summary>
            SecurityDelegation,
        }

        [Flags]
        public enum CreateFileFlags : uint
        {
            /// <summary>
            ///     A file or directory that is an archive file or directory. Applications typically use this attribute to mark
            ///     files for backup or removal.
            /// </summary>
            FILE_ATTRIBUTE_ARCHIVE = 0x20,

            /// <summary>
            ///     A file or directory that is encrypted. For a file, all data streams in the file are encrypted. For a
            ///     directory, encryption is the default for newly created files and subdirectories.
            /// </summary>
            FILE_ATTRIBUTE_ENCRYPTED = 0x4000,

            /// <summary>The file or directory is hidden. It is not included in an ordinary directory listing.</summary>
            FILE_ATTRIBUTE_HIDDEN = 0x2,

            /// <summary>A file that does not have other attributes set. This attribute is valid only when used alone.</summary>
            FILE_ATTRIBUTE_NORMAL = 0x80,

            /// <summary>
            ///     The data of a file is not available immediately. This attribute indicates that the file data is physically
            ///     moved to offline storage. This attribute is used by Remote Storage, which is the hierarchical storage management
            ///     software. Applications should not arbitrarily change this attribute.
            /// </summary>
            FILE_ATTRIBUTE_OFFLINE = 0x1000,

            /// <summary>
            ///     A file that is read-only. Applications can read the file, but cannot write to it or delete it. This attribute
            ///     is not honored on directories.
            /// </summary>
            FILE_ATTRIBUTE_READONLY = 0x1,

            /// <summary>A file or directory that the operating system uses a part of, or uses exclusively.</summary>
            FILE_ATTRIBUTE_SYSTEM = 0x4,

            /// <summary>
            ///     A file that is being used for temporary storage. File systems avoid writing data back to mass storage if
            ///     sufficient cache memory is available, because typically, an application deletes a temporary file after the handle
            ///     is closed. In that scenario, the system can entirely avoid writing the data. Otherwise, the data is written after
            ///     the handle is closed.
            /// </summary>
            FILE_ATTRIBUTE_TEMPORARY = 0x100,

            /// <summary>
            ///     The file is being opened or created for a backup or restore operation. The system ensures that the calling
            ///     process overrides file security checks when the process has SE_BACKUP_NAME and SE_RESTORE_NAME privileges. For more
            ///     information, see Changing Privileges in a Token. You must set this flag to obtain a handle to a directory. A
            ///     directory handle can be passed to some functions instead of a file handle. For more information, see the Remarks
            ///     section.
            /// </summary>
            FILE_FLAG_BACKUP_SEMANTICS = 0x02000000,

            /// <summary>
            ///     The file is to be deleted immediately after all of its handles are closed, which includes the specified handle
            ///     and any other open or duplicated handles. If there are existing open handles to a file, the call fails unless they
            ///     were all opened with the <see cref="FileShare.FILE_SHARE_DELETE" /> share mode. Subsequent open requests for the
            ///     file fail, unless the <see cref="FileShare.FILE_SHARE_DELETE" /> share mode is specified.
            /// </summary>
            FILE_FLAG_DELETE_ON_CLOSE = 0x04000000,

            /// <summary>
            ///     The file or device is being opened with no system caching for data reads and writes. This flag does not affect
            ///     hard disk caching or memory mapped files. There are strict requirements for successfully working with files opened
            ///     with CreateFile using the <see cref="FILE_FLAG_NO_BUFFERING" /> flag.
            /// </summary>
            FILE_FLAG_NO_BUFFERING = 0x20000000,

            /// <summary>
            ///     The file data is requested, but it should continue to be located in remote storage. It should not be
            ///     transported back to local storage. This flag is for use by remote storage systems.
            /// </summary>
            FILE_FLAG_OPEN_NO_RECALL = 0x00100000,

            /// <summary>
            ///     Normal reparse point processing will not occur; CreateFile will attempt to open the reparse point. When a file
            ///     is opened, a file handle is returned, whether or not the filter that controls the reparse point is operational.
            ///     This flag cannot be used with the <see cref="CreationDisposition.CREATE_ALWAYS" /> flag. If the file is not a
            ///     reparse point, then this flag is ignored. For more information, see the Remarks section.
            /// </summary>
            FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000,

            /// <summary>
            ///     The file or device is being opened or created for asynchronous I/O. When subsequent I/O operations are
            ///     completed on this handle, the event specified in the OVERLAPPED structure will be set to the signaled state. If
            ///     this flag is specified, the file can be used for simultaneous read and write operations. If this flag is not
            ///     specified, then I/O operations are serialized, even if the calls to the read and write functions specify an
            ///     OVERLAPPED structure.For information about considerations when using a file handle created with this flag, see the
            ///     Synchronous and Asynchronous I/O Handles section of this topic.
            /// </summary>
            FILE_FLAG_OVERLAPPED = 0x40000000,

            /// <summary>
            ///     Access will occur according to POSIX rules. This includes allowing multiple files with names, differing only
            ///     in case, for file systems that support that naming. Use care when using this option, because files created with
            ///     this flag may not be accessible by applications that are written for MS-DOS or 16-bit Windows.
            /// </summary>
            FILE_FLAG_POSIX_SEMANTICS = 0x0100000,

            /// <summary>
            ///     Access is intended to be random. The system can use this as a hint to optimize file caching. This flag has no
            ///     effect if the file system does not support cached I/O and <see cref="FILE_FLAG_NO_BUFFERING" />. For more
            ///     information, see the Caching Behavior section of this topic.
            /// </summary>
            FILE_FLAG_RANDOM_ACCESS = 0x10000000,

            /// <summary>
            ///     The file or device is being opened with session awareness. If this flag is not specified, then per-session
            ///     devices (such as a redirected USB device) cannot be opened by processes running in session 0. This flag has no
            ///     effect for callers not in session 0. This flag is supported only on server editions of Windows.
            /// </summary>
            FILE_FLAG_SESSION_AWARE = 0x00800000,

            /// <summary>
            ///     Access is intended to be sequential from beginning to end. The system can use this as a hint to optimize file
            ///     caching. This flag should not be used if read-behind (that is, reverse scans) will be used. This flag has no effect
            ///     if the file system does not support cached I/O and <see cref="FILE_FLAG_NO_BUFFERING" />. For more information, see
            ///     the Caching Behavior section of this topic.
            /// </summary>
            FILE_FLAG_SEQUENTIAL_SCAN = 0x08000000,

            /// <summary>
            ///     Write operations will not go through any intermediate cache, they will go directly to disk. For additional
            ///     information, see the Caching Behavior section of this topic.
            /// </summary>
            FILE_FLAG_WRITE_THROUGH = 0x80000000,

            /// <summary>When this flag is present, one or more other flags ending with Security may also be specified.</summary>
            SECURITY_SQOS_PRESENT = 0x00100000,

            /// <summary>Impersonates a client at the Anonymous impersonation level.</summary>
            SECURITY_ANONYMOUS = SECURITY_IMPERSONATION_LEVEL.SecurityAnonymous << 16,

            /// <summary>The security tracking mode is dynamic. If this flag is not specified, the security tracking mode is static.</summary>
            SECURITY_CONTEXT_TRACKING = 0x00040000,

            /// <summary>Impersonates a client at the Delegation impersonation level.</summary>
            SECURITY_DELEGATION = SECURITY_IMPERSONATION_LEVEL.SecurityDelegation << 16,

            /// <summary>
            ///     Only the enabled aspects of the client's security context are available to the server. If you do not specify
            ///     this flag, all aspects of the client's security context are available. This allows the client to limit the groups
            ///     and privileges that a server can use while impersonating the client.
            /// </summary>
            SECURITY_EFFECTIVE_ONLY = 0x00080000,

            /// <summary>Impersonates a client at the Identification impersonation level.</summary>
            SECURITY_IDENTIFICATION = SECURITY_IMPERSONATION_LEVEL.SecurityIdentification << 16,

            /// <summary>
            ///     Impersonate a client at the impersonation level. This is the default behavior if no other flags are specified
            ///     along with the <see cref="SECURITY_SQOS_PRESENT" /> flag.
            /// </summary>
            SECURITY_IMPERSONATION = SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation << 16
        }

        [Flags]
        public enum FormatMessageFlags : uint
        {
            FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100,
            FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200,
            FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000,
            FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x00002000,
            FORMAT_MESSAGE_FROM_HMODULE = 0x00000800,
            FORMAT_MESSAGE_FROM_STRING = 0x00000400
        }


        [DllImport("kernel32")]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        public static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, LoadLibraryFlags dwFlags);

        [DllImport("kernel32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("kernel32")]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32")]
        internal static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, Protection flNewProtect, out Protection lpflOldProtect);

        [DllImport("kernel32")]
        internal static extern bool FlushInstructionCache(IntPtr hProcess, IntPtr lpBaseAddress, UIntPtr dwSize);

        [DllImport("kernel32")]
        public static extern IntPtr CreateFile(string lpFileName, FileAccess dwDesiredAccess, FileShare dwShareMode,
            IntPtr lpSecurityAttributes, FileMode dwCreationDisposition, CreateFileFlags dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32")]
        public static extern uint GetFinalPathNameByHandle(IntPtr hFile, StringBuilder lpszFilePath, uint cchFilePath, FinalPathFlags dwFlags);

        [DllImport("psapi")]
        public static extern uint GetMappedFileName(IntPtr m_hProcess, IntPtr lpv, [Out] StringBuilder lpFilename, uint nSize);

        [DllImport("kernel32", SetLastError = true)]
        public static extern IntPtr LocalFree(IntPtr hMem);

        [DllImport("kernel32", SetLastError = true)]
        public static extern int FormatMessage(FormatMessageFlags dwFlags, IntPtr lpSource, uint dwMessageId, uint dwLanguageId, ref IntPtr lpBuffer, uint nSize, IntPtr Arguments);

    }

    [DisableMocking]
    internal static class PInvokes_Linux
    {
        private const string LIB_DL = "libdl.so";
        private const string LIB_C = "libc.so";

        public const int _SC_PAGE_SIZE = 30;

        [Flags]
        internal enum Prot
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


        [DllImport(LIB_DL)]
        public static extern IntPtr dlopen(string filename, int flags);

        [DllImport(LIB_DL)]
        public static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport(LIB_DL)]
        public static extern int dlclose(IntPtr handle);

        [DllImport(LIB_C)]
        public static extern int mprotect(IntPtr addr, UIntPtr len, Prot prot);

        [DllImport(LIB_C)]
        public static extern IntPtr sysconf(int name);
    }

    [DisableMocking]
    internal static class PInvokes_Osx
    {
        [DllImport("libdl.dylib")]
        public static extern IntPtr dlopen(string filename, int flags);

        [DllImport("libdl.dylib")]
        public static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport("libdl.dylib")]
        public static extern int dlclose(IntPtr handle);
    }

    public enum Unix_DlopenFlags : int
    {
        Lazy = 0x00001,
        Now = 0x00002,
        Lazy_Global = 0x00100 | Lazy,
        Now_Global = 0x00100 | Now
    }
}

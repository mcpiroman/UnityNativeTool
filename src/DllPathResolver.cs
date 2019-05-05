using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace UnityNativeTool.Internal
{
    internal class DllPathResolver
    {
        private readonly Dictionary<string, string> _dllPathsCache = new Dictionary<string, string>();

        public string ResolveDllPath(string dllName)
        {
#if UNITY_STANDALONE_WIN
            return ResolveDllPath_Windows(dllName);
#elif UNITY_STANDALONE_LINUX
#elif UNITY_STANDALONE_OSX
#endif
        }

        private string ResolveDllPath_Windows(string dllName)
        {
            if (_dllPathsCache.TryGetValue(dllName, out var path))
            {
                return path;
            }

            IntPtr libHandle = PInvokes_Windows.LoadLibraryEx(dllName, IntPtr.Zero, PInvokes_Windows.LoadLibraryFlags.LOAD_LIBRARY_AS_DATAFILE);
            if (libHandle == IntPtr.Zero) //FIXME: LoadLibraryEx returns 0 with error 0x2: "File not found" first time each library is loaded
            {
                var errorCode = Marshal.GetLastWin32Error();
                Debug.LogWarning($"LoadLibraryEx returned 0. Error code: {errorCode}");
                Debug.LogWarning($"Error message: {GetSystemMessage(errorCode)}");
                Debug.LogWarning($"dllName: {dllName}");
                return null;
            }
            
            try
            {
                IntPtr procHandle = PInvokes_Windows.GetCurrentProcess();

                var devicePathBufferSize = 260;
                var devicePath = new StringBuilder(devicePathBufferSize);
                var pathLength = PInvokes_Windows.GetMappedFileName(procHandle, libHandle, devicePath, (uint)devicePathBufferSize);
                if (pathLength == 0)
                {
                    throw new Win32Exception();
                }

                var dosPath = devicePath == null ? null : WindowsDeviceToDosPath(devicePath.ToString());
                _dllPathsCache.Add(dllName, dosPath);
                return dosPath;
            }
            finally
            {
                PInvokes_Windows.FreeLibrary(libHandle);
            }
        }

        private string WindowsDeviceToDosPath(string devicePath)
        {
            IntPtr hFile = PInvokes_Windows.CreateFile(devicePath.Replace("\\Device\\", "\\\\?\\"), FileAccess.Read, FileShare.Read | FileShare.Write | FileShare.Delete, 
                IntPtr.Zero, FileMode.Open, PInvokes_Windows.CreateFileFlags.FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
            if(hFile == PInvokes_Windows.INVALID_HANDLE_VALUE)
            {
                return null;
            }

            int dosPathBufferLength = 261;
            var dosPathBuffer = new StringBuilder(dosPathBufferLength);
            var dosPathLength = PInvokes_Windows.GetFinalPathNameByHandle(hFile, dosPathBuffer, (uint)dosPathBufferLength, PInvokes_Windows.FinalPathFlags.VOLUME_NAME_DOS);
            if(dosPathLength == 0)
            {
                throw new Win32Exception();
            }

            var dosPath = dosPathBuffer.ToString();
            const string CUT_PREFIX = "\\\\?\\";
            if(dosPath.StartsWith(CUT_PREFIX))
            {
                dosPath = dosPath.Substring(CUT_PREFIX.Length);
            }

            return dosPath;
        }


        public void ClearCache()
        {
            _dllPathsCache.Clear();
        }


        /// <summary>
        /// Gets a user friendly string message for a system error code
        /// </summary>
        /// <param name="errorCode">System error code</param>
        /// <returns>Error string</returns>
        public static string GetSystemMessage(int errorCode)
        {
            try
            {
                IntPtr lpMsgBuf = IntPtr.Zero;

                int dwChars = PInvokes_Windows.FormatMessage(
                    PInvokes_Windows.FormatMessageFlags.FORMAT_MESSAGE_ALLOCATE_BUFFER | 
                    PInvokes_Windows.FormatMessageFlags.FORMAT_MESSAGE_FROM_SYSTEM | 
                    PInvokes_Windows.FormatMessageFlags.FORMAT_MESSAGE_IGNORE_INSERTS,
                    IntPtr.Zero,
                    (uint)errorCode,
                    0, // Default language
                    ref lpMsgBuf,
                    0,
                    IntPtr.Zero);
                if (dwChars == 0)
                {
                    // Handle the error.
                    int le = Marshal.GetLastWin32Error();
                    return "Unable to get error code string from System - Error " + le.ToString();
                }

                string sRet = Marshal.PtrToStringAnsi(lpMsgBuf);

                // Free the buffer.
                lpMsgBuf = PInvokes_Windows.LocalFree(lpMsgBuf);
                return sRet;
            }
            catch (Exception e)
            {
                return "Unable to get error code string from System -> " + e.ToString();
            }
        }
    }
}

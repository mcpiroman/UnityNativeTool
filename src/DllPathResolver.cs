using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace UnityNativeTool.Internal
{
    internal class DllPathResolver
    {
        private readonly Dictionary<string, string> _dllPathsCache = new Dictionary<string, string>();

        public string Resolve(string dllName)
        {
#if UNITY_STANDALONE_WIN
            return Resolve_Windows(dllName);
#elif UNITY_STANDALONE_LINUX
#elif UNITY_STANDALONE_OSX
#endif
        }

        private string Resolve_Windows(string dllName)
        {
            if (_dllPathsCache.TryGetValue(dllName, out var val))
            {
                return val;
            }

            IntPtr hLib = PInvokes_Windows.LoadLibraryEx(dllName, IntPtr.Zero, PInvokes_Windows.LoadLibraryFlags.LOAD_LIBRARY_AS_DATAFILE);
            if (hLib == IntPtr.Zero)
            {
                var errorCode = Marshal.GetLastWin32Error();
                if (errorCode == PInvokes_Windows.ERROR_FILE_NOT_FOUND)
                {
                    var path = ResolveFromAssets(dllName);
                    _dllPathsCache.Add(dllName, path);
                    return path;
                }
                else
                {
                    Debug.LogWarning($"LoadLibraryEx returned with error code {errorCode}");
                    _dllPathsCache.Add(dllName, null);
                    return null;
                }
            }
            else
            {
                try
                {
                    IntPtr hProc = PInvokes_Windows.GetCurrentProcess();

                    var devicePathBufferSize = 260;
                    var devicePath = new StringBuilder(devicePathBufferSize);
                    var pathLength = PInvokes_Windows.GetMappedFileName(hProc, hLib, devicePath, (uint)devicePathBufferSize);
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
                    PInvokes_Windows.FreeLibrary(hLib);
                }
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
            const string CUT_DOS_PREFIX = "\\\\?\\";
            if(dosPath.StartsWith(CUT_DOS_PREFIX))
            {
                dosPath = dosPath.Substring(CUT_DOS_PREFIX.Length);
            }

            return dosPath;
        }

        private string ResolveFromAssets(string dllName)
        {
#if UNITY_STANDALONE_WIN
            var fileNameRegex = new Regex("^" + dllName.TrimEnd(".dll") + ".dll$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
#elif UNITY_STANDALONE_LINUX
            var fileNameRegex = new Regex("^(lib)?" + dllName.TrimEnd(".so") + ".so$", RegexOptions.CultureInvariant | RegexOptions.Singleline);
#elif UNITY_STANDALONE_OSX
            var fileNameRegex = new Regex("^(lib)?" + dllName.TrimEnd(".dynlib") + ".dynlib$",RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
#endif

            return PluginImporter.GetAllImporters()
                .Where(plugin => Directory.GetParent(plugin.assetPath).FullName.ToLower() == Path.Combine(Application.dataPath, "plugins").ToLower() ||
                    (IntPtr.Size == 8 ? new[] { "x64", "x86_64" } : new[] { "x86" }).Contains(Directory.GetParent(plugin.assetPath).Name))
                .Where(plugin => plugin.isNativePlugin && plugin.GetCompatibleWithEditor())
                .FirstOrDefault(plugin => fileNameRegex.IsMatch(Path.GetFileName(plugin.assetPath)))?.assetPath;
        }

        public void ClearCache()
        {
            _dllPathsCache.Clear();
        }
    }
}

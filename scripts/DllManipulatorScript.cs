using System;
using System.Reflection;
using System.Threading;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityNativeTool.Internal;

namespace UnityNativeTool
{
    [ExecuteInEditMode]
    public class DllManipulatorScript : MonoBehaviour
    {
        private static DllManipulatorScript _singletonInstance = null;
        public TimeSpan? InitializationTime { get; private set; } = null;
        public DllManipulatorOptions Options = new DllManipulatorOptions()
        {
            dllPathPattern =
#if UNITY_STANDALONE_WIN
            "{assets}/Plugins/__{name}.dll",
#elif UNITY_STANDALONE_LINUX
            "{assets}/Plugins/__{name}.so",
#elif UNITY_STANDALONE_OSX
            "{assets}/Plugins/__{name}.dylib",
#endif
            assemblyPaths = new string[0],
            loadingMode = DllLoadingMode.Lazy,
            posixDlopenFlags = PosixDlopenFlags.Lazy,
            threadSafe = false,
            enableCrashLogs = false,
            crashLogsDir = "{assets}/",
            crashLogsStackTrace = false,
            mockAllNativeFunctions = true,
            onlyInEditor = true,
        };

        private void OnEnable()
        {
#if !UNITY_EDITOR
            if (Options.onlyInEditor)
                return;
#endif

            if (_singletonInstance != null)
            {
                if (EditorApplication.isPlaying)
                    Destroy(gameObject);
                else
                    enabled = false;
                return;
            }
            _singletonInstance = this;
            if(EditorApplication.isPlaying)
                DontDestroyOnLoad(gameObject);

            var initTimer = System.Diagnostics.Stopwatch.StartNew();

            DllManipulator.Options = Options;
            DllManipulator.Initialize(Thread.CurrentThread.ManagedThreadId, Application.dataPath);

            initTimer.Stop();
            InitializationTime = initTimer.Elapsed;
        }

        private void OnDisable()
        {
            if (_singletonInstance == this)
            {
                //Note on threading: Because we don't wait for other threads to finish, we might be stealing function delegates from under their nose if Unity doesn't happen to close them yet.
                //On Preloaded mode this leads to NullReferenceException, but on Lazy mode the DLL and function would be just reloaded so we would up with loaded DLL after game exit.
                //Thankfully thread safety with Lazy mode is not implemented yet.

                DllManipulator.UnloadAll();
                DllManipulator.ForgetAllDlls();
                DllManipulator.ClearCrashLogs();         
                _singletonInstance = null;
            }
        }
    }
}
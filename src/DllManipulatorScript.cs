using System;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace UnityNativeTool
{
    public class DllManipulatorScript : MonoBehaviour
    {
        private static DllManipulatorScript _singletonInstance = null;
        public TimeSpan? InitializationTime { get; private set; } = null;
        public DllManipulatorOptions Options = new DllManipulatorOptions()
        {
#if UNITY_STANDALONE_WIN
            dllPathPattern = "{assets}/Plugins/__{name}.dll",
#elif UNITY_STANDALONE_LINUX
            dllPathPattern = "{assets}/Plugins/__{name}.so",
#elif UNITY_STANDALONE_OSX
            dllPathPattern = "{assets}/Plugins/__{name}.dylib",
#endif
            loadingMode = DllLoadingMode.Lazy,
            unixDlopenFlags = Unix_DlopenFlags.Lazy,
            threadSafe = false,
            crashLogs = false,
            crashLogsDir = "{assets}/",
            crashLogsStackTrace = false,
            mockAllNativeFunctions = true,
        };

        private void OnEnable()
        {
            if (_singletonInstance != null)
            {
                if (_singletonInstance != this)
                {
                    Destroy(gameObject);
                }

                return;
            }
            _singletonInstance = this;
            DontDestroyOnLoad(gameObject);

            DllManipulator.SetUnityContext(Thread.CurrentThread.ManagedThreadId, Application.dataPath);
            DllManipulator.Options = Options;

            var timer = System.Diagnostics.Stopwatch.StartNew();
            foreach (var function in DllManipulator.FindNativeFunctionsToMock(Assembly.GetExecutingAssembly()))
            {
                DllManipulator.MockNativeFunction(function);
            }

            if (DllManipulator.Options.loadingMode == DllLoadingMode.Preload)
            {
                DllManipulator.LoadAll();
            }

            timer.Stop();
            InitializationTime = timer.Elapsed;
        }

        private void OnApplicationQuit()
        {
            //FIXME: Because we don't wait for other threads to finish, we might be stealing function delegates from under their nose if Unity doesn't happen to close them yet.
            //On Preloaded mode this leads to NullReferenceException, but on Lazy mode the DLL and function would be just reloaded so we would up with loaded DLL after game exit.
            //Thankfully thread safety with Lazy mode is not implemented yet.

            DllManipulator.UnloadAll();
            DllManipulator.ForgetAllDlls();
            DllManipulator.ClearCrashLogs();
        }

    }
}
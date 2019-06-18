using System;
using System.Reflection;
using System.Threading;
using System.Linq;
using UnityEngine;
using UnityNativeTool.Internal;

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
            assemblyPaths = new string[0],
            loadingMode = DllLoadingMode.Lazy,
            unixDlopenFlags = Unix_DlopenFlags.Lazy,
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
                if (_singletonInstance != this)
                    Destroy(gameObject);

                return;
            }
            _singletonInstance = this;
            DontDestroyOnLoad(gameObject);

            var timer = System.Diagnostics.Stopwatch.StartNew();
            DllManipulator.SetUnityContext(Thread.CurrentThread.ManagedThreadId, Application.dataPath);
            DllManipulator.Options = Options;

            Assembly[] assemblies;
            if (Options.assemblyPaths.Length == 0)
            {
                assemblies = new[] { Assembly.GetExecutingAssembly() };
            }
            else
            {
                var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                assemblies = allAssemblies.Where(a => !a.IsDynamic && Options.assemblyPaths.Any(p => p == PathUtils.NormallizeSystemAssemblyPath(a.Location))).ToArray();
                var missingAssemblies = Options.assemblyPaths.Except(assemblies.Select(a => PathUtils.NormallizeSystemAssemblyPath(a.Location)));
                foreach(var assemblyPath in missingAssemblies)
                {
                    Debug.LogError($"Could not find assembly at path {assemblyPath}");
                }
            }

            foreach (var assembly in assemblies)
            {
                foreach (var function in DllManipulator.FindNativeFunctionsToMock(assembly))
                {
                    DllManipulator.MockNativeFunction(function);
                }
            }

            if (DllManipulator.Options.loadingMode == DllLoadingMode.Preload)
                DllManipulator.LoadAll();

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
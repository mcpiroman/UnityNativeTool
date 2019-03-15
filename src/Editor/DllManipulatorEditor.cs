using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace DllManipulator
{
    [CustomEditor(typeof(DllManipulator))]
    public class DllManipulatorEditor : Editor
    {
        private readonly GUIContent DLL_PATH_PATTERN_GUI_CONTENT = new GUIContent("DLL path pattern", 
            "Available macros:\n\n" +
            $"{DllManipulator.DLL_PATH_PATTERN_DLL_NAME_MACRO} - name of DLL as specified in [DllImport] attribute.\n\n" +
            $"{DllManipulator.DLL_PATH_PATTERN_ASSETS_MACRO} - assets folder of current project.\n\n" +
            $"{DllManipulator.DLL_PATH_PATTERN_PROJECT_MACRO} - project folder i.e. one above Assets.");
        private readonly GUIContent DLL_LOADING_MODE_GUI_CONTENT = new GUIContent("DLL loading mode", 
            "Specifies how DLLs and functions will be loaded.\n\n" +
            "Lazy - All DLLs and functions are loaded as they're first called. This allows them to be easily unloaded and loaded within game execution.\n\n" +
            "Preloaded - Slight performance benefit over Lazy mode. All declared DLLs and functions are loaded at startup (OnEnable()). Mid-execution it's not safe to unload them unless game is paused.");
        private readonly GUIContent UNIX_DLOPEN_FLAGS_GUI_CONTENT = new GUIContent("dlopen flags",
            "Flags used in dlopen() P/Invoke on Linux and OSX systems. Has minor meaning unless library is large.");
        private readonly GUIContent THREAD_SAFE_GUI_CONTENT = new GUIContent("Thread safe",
            "Ensures synchronization required for native calls from any other than Unity main thread. Overhead might be few times higher, with uncontended locks.\n\n" +
            "Only in Preloaded mode.");
        private readonly GUIContent CRASH_LOGS_GUI_CONTENT = new GUIContent("Crash logs",
            "Logs each native call to file. In case of crash or hang caused by native function, you can than see what function was that, along with arguments and, optionally, stack trace.\n\n" +
            "In multi-threaded scenario there will be one file for each thread and you'll have to guess the right one (call index will be a hint).\n\n" +
            "Note that existence of log files doesn't mean the crash was caused by any tracked native function.\n\n" +
            "Overhead is HIGH (on poor PC there might be just few native calls per update to disturb 60 fps.)");
        private readonly GUIContent CRASH_LOGS_DIR_GUI_CONTENT = new GUIContent("Logs directory",
            "Path to directory in which crash logs will be stored. You can use macros as in DLL path. Note that this file(s) will usually exist during majority of game execution.");
        private readonly GUIContent CRASH_LOGS_STACK_TRACE_GUI_CONTENT = new GUIContent("Stack trace",
            "Whether to include stack trace in crash log.\n\n" +
            "Overhead is about 4 times higher.");
        private readonly GUIContent MOCK_ALL_NATIVE_FUNCTIONS_GUI_CONTENT = new GUIContent("Mock all native functions", 
            "If true, all native functions in current assembly will be mocked.\n\n" +
            $"If false, you have to use [{nameof(MockNativeDeclarationsAttribute)}] or [{nameof(MockNativeDeclarationAttribute)}] in order to select native functions to be mocked.");
        private readonly GUIContent UNLOAD_ALL_DLLS_IN_PLAY_PRELOADED_GUI_CONTENT = new GUIContent("Unload all DLLs [dangerous]",
            "Use only if you are sure no mocked native calls will be made while DLL is unloaded.");

        private bool _showLoadedLibraries = true;
        

        public DllManipulatorEditor()
        {
            EditorApplication.pauseStateChanged += _ => Repaint();
            EditorApplication.playModeStateChanged += _ => Repaint();
        }

        public override void OnInspectorGUI()
        {
            var t = (DllManipulator)this.target;

            DrawOptions(t.Options);
            EditorGUILayout.Space();

            var usedDlls = DllManipulator.GetUsedDllsInfos();
            if (usedDlls.Count != 0)
            {
                if(t.Options.loadingMode == DllLoadingMode.Preload && usedDlls.Any(d => !d.isLoaded))
                {
                    if (EditorApplication.isPaused)
                    {
                        if (GUILayout.Button("Load all DLLs & Unpause"))
                        {
                            DllManipulator.LoadAll();
                            EditorApplication.isPaused = false;
                        }
                    }

                    if (GUILayout.Button("Load all DLLs"))
                    {
                        DllManipulator.LoadAll();
                    }
                }
                else if(EditorApplication.isPaused)
                {
                    if (GUILayout.Button("Unpause"))
                    {
                        EditorApplication.isPaused = false;
                    }
                }

                if (usedDlls.Any(d => d.isLoaded))
                {
                    if (EditorApplication.isPlaying && !EditorApplication.isPaused)
                    {
                        if (GUILayout.Button("Unload all DLLs & Pause"))
                        {
                            EditorApplication.isPaused = true;
                            DllManipulator.UnloadAll();
                        }
                    }

                    if(EditorApplication.isPlaying && !EditorApplication.isPaused && t.Options.loadingMode == DllLoadingMode.Preload)
                    {
                        if (GUILayout.Button(UNLOAD_ALL_DLLS_IN_PLAY_PRELOADED_GUI_CONTENT))
                        {
                            DllManipulator.UnloadAll();
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Unload all DLLs"))
                        {
                            DllManipulator.UnloadAll();
                        }
                    }
                }

                _showLoadedLibraries = EditorGUILayout.Foldout(_showLoadedLibraries, "Mocked DLLs");
                if (_showLoadedLibraries)
                {
                    var prevIndent = EditorGUI.indentLevel;
                    EditorGUI.indentLevel += 1;
                    bool isFirstDll = true;
                    foreach (var dll in usedDlls)
                    {
                        if (!isFirstDll)
                        {
                            EditorGUILayout.Space();
                        }

                        var stateAttributes = new List<string>
                        {
                            dll.isLoaded ? "LOADED" : "NOT LOADED"
                        };
                        if (dll.loadingError)
                        {
                            stateAttributes.Add("LOAD ERROR");
                        }
                        if(dll.symbolError)
                        {
                            stateAttributes.Add("SYMBOL ERRROR");
                        }
                        var state = string.Join(" | ", stateAttributes);

                        EditorGUILayout.LabelField($"[{state}] {dll.name}");
                        EditorGUILayout.LabelField(dll.path);
                        isFirstDll = false;
                    }
                    EditorGUI.indentLevel = prevIndent;
                }
            }
            else if(EditorApplication.isPlaying)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("No DLLs to mock");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            if(EditorApplication.isPlaying && DllManipulator.InitializationTime != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.Space();
                var time = DllManipulator.InitializationTime.Value;
                EditorGUILayout.LabelField($"Initialized in: {(int)time.TotalSeconds}.{time.Milliseconds.ToString("D3")}s");
            }
        }

        private void DrawOptions(DllManipulatorOptions options)
        {
            var guiEnabledStack = new Stack<bool>();

            options.dllPathPattern = EditorGUILayout.TextField(DLL_PATH_PATTERN_GUI_CONTENT, options.dllPathPattern);

            guiEnabledStack.Push(GUI.enabled);
            if (EditorApplication.isPlaying)
            {
                GUI.enabled = false;
            }
            options.loadingMode = (DllLoadingMode)EditorGUILayout.EnumPopup(DLL_LOADING_MODE_GUI_CONTENT, options.loadingMode);

#if UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
            options.unixDlopenFlags = (UnixDlopenFlags)EditorGUILayout.EnumPopup(UNIX_DLOPEN_FLAGS_GUI_CONTENT, options.unixDlopenFlags);
#endif

            guiEnabledStack.Push(GUI.enabled);
            if (options.loadingMode != DllLoadingMode.Preload)
            {
                options.threadSafe = false;
                GUI.enabled = false;
            }
            options.threadSafe = EditorGUILayout.Toggle(THREAD_SAFE_GUI_CONTENT, options.threadSafe);
            GUI.enabled = guiEnabledStack.Pop();

            options.crashLogs = EditorGUILayout.Toggle(CRASH_LOGS_GUI_CONTENT, options.crashLogs);

            if (options.crashLogs)
            {
                var prevIndent = EditorGUI.indentLevel;

                EditorGUI.indentLevel += 1;
                options.crashLogsDir = EditorGUILayout.TextField(CRASH_LOGS_DIR_GUI_CONTENT, options.crashLogsDir);

                options.crashLogsStackTrace = EditorGUILayout.Toggle(CRASH_LOGS_STACK_TRACE_GUI_CONTENT, options.crashLogsStackTrace);

                EditorGUI.indentLevel = prevIndent;
            }

            options.mockAllNativeFunctions = EditorGUILayout.Toggle(MOCK_ALL_NATIVE_FUNCTIONS_GUI_CONTENT, options.mockAllNativeFunctions);

            GUI.enabled = guiEnabledStack.Pop();
        }
    }
}
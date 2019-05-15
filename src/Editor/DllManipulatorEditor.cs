using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System.IO;

namespace UnityNativeTool.Internal
{
    [CustomEditor(typeof(DllManipulatorScript))]
    public class DllManipulatorEditor : Editor
    {
        private readonly GUIContent TARGET_ALL_NATIVE_FUNCTIONS_GUI_CONTENT = new GUIContent("All native functions",
            "If true, all found native functions will be mocked.\n\n" +
            $"If false, you have to select them by using [{nameof(MockNativeDeclarationsAttribute)}] or [{nameof(MockNativeDeclarationAttribute)}].");
        private readonly GUIContent TARGET_ONLY_EXECUTING_ASSEMBLY_GUI_CONTENT = new GUIContent("Only executing assembly",
            "If true, native functions will be mocked only in assembly that contains DllManipulator (usually Assembly-CSharp)");
        private readonly GUIContent TARGET_ASSEMBLIES_GUI_CONTENT = new GUIContent("Target assemblies",
            "Choose from which assemblies to mock native functions");
        private readonly GUIContent IGNORED_DLL_NAMES_GUI_CONTENT = new GUIContent("Ignored DLLs",
            "List of DLL names that will be handled by Unity in usual way. Uses Regex.");
        private readonly GUIContent IGNORED_DLL_PATHS_GUI_CONTENT = new GUIContent("Ignored DLL paths",
            "If DLL is found at matching path, it will be handled by Unity in usual way.");
        private readonly GUIContent DLL_PATH_PATTERN_GUI_CONTENT = new GUIContent("DLL path pattern", 
            "Available macros:\n\n" +
            $"{DllManipulator.DLL_PATH_PATTERN_DLL_NAME_MACRO} - name of DLL as specified in [DllImport] attribute.\n\n" +
            $"{DllManipulator.DLL_PATH_PATTERN_ASSETS_MACRO} - assets folder of current project.\n\n" +
            $"{DllManipulator.DLL_PATH_PATTERN_PROJECT_MACRO} - project folder i.e. one above Assets.");
        private readonly GUIContent DLL_LOADING_MODE_GUI_CONTENT = new GUIContent("DLL loading mode", 
            "Specifies how DLLs and functions will be loaded.\n\n" +
            "Lazy - All DLLs and functions are loaded each time they are called, if not loaded yet. This allows them to be easily unloaded and loaded within game execution.\n\n" +
            "Preloaded - Slight performance benefit over Lazy mode. All declared DLLs and functions are loaded at startup (OnEnable()) and not reloaded later. Mid-execution it's not safe to unload them unless game is paused.");
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
        private readonly GUIContent UNLOAD_ALL_DLLS_IN_PLAY_PRELOADED_GUI_CONTENT = new GUIContent("Unload all DLLs [dangerous]",
            "Use only if you are sure no mocked native calls will be made while DLL is unloaded.");
        private readonly GUIContent UNLOAD_ALL_DLLS_WITH_THREAD_SAFETY_GUI_CONTENT = new GUIContent("Unload all DLLs [dangerous]",
            "Use only if you are sure no other thread will be call mocked natives.");
        private readonly GUIContent UNLOAD_ALL_DLLS_AND_PAUSE_WITH_THREAD_SAFETY_GUI_CONTENT = new GUIContent("Unload all DLLs & Pause [dangerous]",
            "Use only if you are sure no other thread will be call mocked natives.");

        private bool _showIgnoredDllNames = false;
        private bool _showIgnoredDllPaths = false;
        private bool _showTargetAssemblies = true;
        private bool _showLoadedLibraries = true;

        public DllManipulatorEditor()
        {
            EditorApplication.pauseStateChanged += _ => Repaint();
            EditorApplication.playModeStateChanged += _ => Repaint();
        }

        public override void OnInspectorGUI()
        {
            var t = (DllManipulatorScript)this.target;

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

                if (usedDlls.Any(d => d.isLoaded))
                {
                    if (EditorApplication.isPlaying && !EditorApplication.isPaused)
                    {
                        bool pauseAndUnloadAll;
                        if(t.Options.threadSafe)
                        {
                            pauseAndUnloadAll = GUILayout.Button(UNLOAD_ALL_DLLS_AND_PAUSE_WITH_THREAD_SAFETY_GUI_CONTENT);
                        }
                        else
                        {
                            pauseAndUnloadAll = GUILayout.Button("Unload all DLLs & Pause");
                        }

                        if(pauseAndUnloadAll)
                        {
                            EditorApplication.isPaused = true;
                            DllManipulator.UnloadAll();
                        }
                    }


                    bool unloadAll;
                    if(EditorApplication.isPlaying && t.Options.threadSafe)
                    {
                        unloadAll = GUILayout.Button(UNLOAD_ALL_DLLS_WITH_THREAD_SAFETY_GUI_CONTENT);
                    }
                    else if (EditorApplication.isPlaying && !EditorApplication.isPaused && t.Options.loadingMode == DllLoadingMode.Preload)
                    {
                        unloadAll = GUILayout.Button(UNLOAD_ALL_DLLS_IN_PLAY_PRELOADED_GUI_CONTENT);
                    }
                    else
                    {
                        unloadAll = GUILayout.Button("Unload all DLLs");
                    }

                    if(unloadAll)
                    {
                        DllManipulator.UnloadAll();
                    }
                }

                DrawUsedDlls(usedDlls);
            }
            else if(EditorApplication.isPlaying)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("No DLLs to mock");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            if(EditorApplication.isPlaying && t.InitializationTime != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.Space();
                var time = t.InitializationTime.Value;
                EditorGUILayout.LabelField($"Initialized in: {(int)time.TotalSeconds}.{time.Milliseconds.ToString("D3")}s");
            }
        }

        private void DrawUsedDlls(IList<NativeDllInfo> usedDlls)
        {
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
                    if (dll.symbolError)
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

        private void DrawOptions(DllManipulatorOptions options)
        {
            var guiEnabledStack = new Stack<bool>();

            guiEnabledStack.Push(GUI.enabled);
            if (EditorApplication.isPlaying)
            {
                GUI.enabled = false;
            }
            options.mockAllNativeFunctions = EditorGUILayout.Toggle(TARGET_ALL_NATIVE_FUNCTIONS_GUI_CONTENT, options.mockAllNativeFunctions);

            if (EditorGUILayout.Toggle(TARGET_ONLY_EXECUTING_ASSEMBLY_GUI_CONTENT, options.assemblyPaths.Length == 0))
            {
                options.assemblyPaths = new string[0];
            }
            else
            {
                var prevIndent1 = EditorGUI.indentLevel;
                EditorGUI.indentLevel++;

                var allAssemblies = CompilationPipeline.GetAssemblies();
                if (options.assemblyPaths.Length == 0)
                {
                    options.assemblyPaths = new[] { GetFirstAssemblyPath(allAssemblies) };
                }

                _showTargetAssemblies = EditorGUILayout.Foldout(_showTargetAssemblies, TARGET_ASSEMBLIES_GUI_CONTENT);
                if (_showTargetAssemblies)
                {
                    var prevIndent2 = EditorGUI.indentLevel;
                    EditorGUI.indentLevel++;

                    var selectedAssemblyPaths = options.assemblyPaths.Where(p => allAssemblies.Any(a => PathUtils.DllPathsEquals(a.outputPath, p)));
                    var selectedAssemblies = selectedAssemblyPaths.Select(p => allAssemblies.First(a => PathUtils.DllPathsEquals(a.outputPath, p))).ToList();
                    var notSelectedAssemblies = allAssemblies.Except(selectedAssemblies).ToArray();
                    DrawList(selectedAssemblies, i =>
                    {
                        var values = new List<string> { selectedAssemblies[i].name };
                        values.AddRange(notSelectedAssemblies.Select(a => a.name));
                        var selectedIndex = EditorGUILayout.Popup(0, values.ToArray());
                        return selectedIndex == 0 ? selectedAssemblies[i] : notSelectedAssemblies[selectedIndex - 1];
                    }, notSelectedAssemblies.Length > 0, () => notSelectedAssemblies[0]);
                    options.assemblyPaths = selectedAssemblies.Select(a => PathUtils.NormallizeUnityAssemblyPath(a.outputPath)).ToArray();

                    EditorGUI.indentLevel = prevIndent2;
                }

                EditorGUI.indentLevel = prevIndent1;
            }

            _showIgnoredDllNames = EditorGUILayout.Foldout(_showIgnoredDllNames, IGNORED_DLL_NAMES_GUI_CONTENT);
            if (_showIgnoredDllNames)
            {
                var prevIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel++;
                var ignoredDllNames = options.ignoredDllNames.ToList();
                DrawList(ignoredDllNames, i => EditorGUILayout.TextField(ignoredDllNames[i]), true, () => "");
                options.ignoredDllNames = ignoredDllNames.ToArray();
                EditorGUI.indentLevel = prevIndent;
            }

            _showIgnoredDllPaths = EditorGUILayout.Foldout(_showIgnoredDllPaths, IGNORED_DLL_PATHS_GUI_CONTENT);
            if (_showIgnoredDllPaths)
            {
                var prevIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel++;
                var ignoredDllPaths = options.ignoredDllPaths.ToList();
                DrawList(ignoredDllPaths, i => EditorGUILayout.TextField(ignoredDllPaths[i]), true, () => "");
                options.ignoredDllPaths = ignoredDllPaths.ToArray();
                EditorGUI.indentLevel = prevIndent;
            }

            options.dllPathPattern = EditorGUILayout.TextField(DLL_PATH_PATTERN_GUI_CONTENT, options.dllPathPattern);
            
            options.loadingMode = (DllLoadingMode)EditorGUILayout.EnumPopup(DLL_LOADING_MODE_GUI_CONTENT, options.loadingMode);

#if UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
            options.unixDlopenFlags = (Unix_DlopenFlags)EditorGUILayout.EnumPopup(UNIX_DLOPEN_FLAGS_GUI_CONTENT, options.unixDlopenFlags);
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

            GUI.enabled = guiEnabledStack.Pop();
        }

        private void DrawList<T>(IList<T> elements, System.Func<int, T> drawElement, bool canAddNewElement, System.Func<T> getNewElement)
        {
            int indexToRemove = -1;
            for (int i = 0; i < elements.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                elements[i] = drawElement(i);
                if(GUILayout.Button("X", GUILayout.Width(20)))
                {
                    indexToRemove = i;
                }
                EditorGUILayout.EndHorizontal();
            }

            if(indexToRemove != -1)
            {
                elements.RemoveAt(indexToRemove);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 15);
            var prevGuiEnabled = GUI.enabled;
            GUI.enabled = canAddNewElement;
            if (GUILayout.Button("Add", GUILayout.Width(40)))
            {
                elements.Add(getNewElement());
            }
            GUI.enabled = prevGuiEnabled;
            GUILayout.EndHorizontal();
        }

        string GetFirstAssemblyPath(Assembly[] allAssemblies)
        {
            var path = allAssemblies.FirstOrDefault(a => PathUtils.DllPathsEquals(a.outputPath, typeof(DllManipulator).Assembly.Location))?.outputPath 
                ?? allAssemblies.FirstOrDefault().outputPath;
            return PathUtils.NormallizeUnityAssemblyPath(path);
        }
    }
}
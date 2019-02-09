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
            $"{DllManipulator.DLL_PATH_PATTERN_NAME_MACRO} - name of DLL as specified in [DllImport] attribute.\n\n" +
            $"{DllManipulator.DLL_PATH_PATTERN_ASSETS_MACRO} - assets folder of current project.\n\n" +
            $"{DllManipulator.DLL_PATH_PATTERN_PROJECT_MACRO} - project folder i.e. one above Assets.");
        private readonly GUIContent DLL_LOADING_MODE_GUI_CONTENT = new GUIContent("DLL loading mode", 
            "Specifies how DLLs and functions will be loaded.\n\n" +
            "Lazy - Easiest to use and most flexible way. All DLLs and functions are loaded as they're first called. This allows them to be easily unloaded and loaded within game execution.\n\n" +
            "Preloaded - Slight performance benefit over Lazy mode. All DLLs and functions are loaded at startup (OnEnable()). Calls to unloaded DLLs lead to crash, so in mid-execution it's safest to manipulate DLLs if game is paused.");
        private readonly GUIContent LINUX_DLOPEN_FLAGS_GUI_CONTENT = new GUIContent("dlopen flags",
            $"Flags used in dlopen() P/Invoke on Linux systems. Has minor meaning unless library is large.");
        private readonly GUIContent MOCK_ALL_NATIVE_FUNCTIONS_GUI_CONTENT = new GUIContent("Mock all native functions", 
            $"If true, all native functions in current assembly will be mocked.\n\n" +
            $"If false, you have to use [{nameof(MockNativeDeclarationsAttribute)}] or [{nameof(MockNativeDeclarationAttribute)}] in order to select native functions to be mocked.");
        private readonly GUIContent MOCK_CALLS_IN_ALL_TYPES_GUI_CONTENT = new GUIContent("Mock native calls in all types", 
            $"If true, calls of native functions in all methods in current assembly will be mocked. This however can cause significant performance issues at startup in big code base. You may use [{nameof(DisableMockingAttribute)}].\n\n" +
            $"If false, you have to use [{nameof(MockNativeCallsAttribute)}] in order to mock native function calls.");
        private readonly GUIContent UNLOAD_ALL_DLLS_IN_PLAY_PRELOADED_GUI_CONTENT = new GUIContent("Unload all DLLs [DANGEROUS!]",
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

            t.Options.dllPathPattern = EditorGUILayout.TextField(DLL_PATH_PATTERN_GUI_CONTENT, t.Options.dllPathPattern);
            if (EditorApplication.isPlaying)
            {
                GUI.enabled = false;
            }
            t.Options.loadingMode = (DllLoadingMode)EditorGUILayout.EnumPopup(DLL_LOADING_MODE_GUI_CONTENT, t.Options.loadingMode);
#if UNITY_STANDALONE_LINUX
            t.Options.linuxDlopenFlags = (LinuxDlopenFlags)EditorGUILayout.EnumPopup(LINUX_DLOPEN_FLAGS_GUI_CONTENT, t.Options.linuxDlopenFlags);
#endif
            t.Options.mockAllNativeFunctions = EditorGUILayout.Toggle(MOCK_ALL_NATIVE_FUNCTIONS_GUI_CONTENT, t.Options.mockAllNativeFunctions);
            t.Options.mockCallsInAllTypes = EditorGUILayout.Toggle(MOCK_CALLS_IN_ALL_TYPES_GUI_CONTENT, t.Options.mockCallsInAllTypes);
            GUI.enabled = true;

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
                var time = DllManipulator.InitializationTime.Value;
                EditorGUILayout.LabelField($"Initialized in: {(int)time.TotalSeconds}.{time.Milliseconds.ToString("D3")}s");
            }
        }
    }
}
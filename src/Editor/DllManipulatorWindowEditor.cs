using System.Linq;
using UnityEngine;
using UnityEditor;

namespace DllManipulator
{
    public class DllManipulatorWindowEditor : EditorWindow
    {
        [MenuItem("UnityNativeTool/DLL Manipulator")]
        static void Init()
        {
            var window = GetWindow<DllManipulatorWindowEditor>();
            window.Show();
        }

        void OnGUI()
        {
            var dllManipulator = FindObjectOfType<DllManipulator>();
            if (dllManipulator == null)
            {
                dllManipulator = Resources.FindObjectsOfTypeAll<DllManipulator>()
                    .FirstOrDefault(d => !EditorUtility.IsPersistent(d) && d.gameObject.scene.IsValid());
            }

            if (dllManipulator != null)
            {
                var editor = Editor.CreateEditor(dllManipulator);
                editor.OnInspectorGUI();
            }
            else
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"There is no {nameof(DllManipulator)} script in the scene.");
            }
        }
    }
}
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Fokiga.GameplayTags;

namespace Fokiga.GameplayTags.Editor
{
    [CustomEditor(typeof(GameplayTagDatabase))]
    public sealed class GameplayTagDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var database = (GameplayTagDatabase)target;
            var report = database.Validate();
            EditorGUILayout.LabelField("GameplayTag Database", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Nodes", database.Nodes.Count.ToString());

            if (report.IsValid)
            {
                EditorGUILayout.HelpBox("Database is valid.", MessageType.Info);
            }
            else
            {
                foreach (var error in report.Errors)
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
            }

            if (GUILayout.Button("Open Gameplay Tags Window"))
            {
                GameplayTagDatabaseWindow.ShowWindow();
            }
        }
    }
}
#endif

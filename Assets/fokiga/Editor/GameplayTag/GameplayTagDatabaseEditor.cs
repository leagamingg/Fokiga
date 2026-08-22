#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Fokiga.Runtime.Gameplay;

namespace Fokiga.Editor
{
    [CustomEditor(typeof(GameplayTagDatabase))]
    public sealed class GameplayTagDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var database = (GameplayTagDatabase)target;
            var report = database.Validate();
            EditorGUILayout.LabelField("GameplayTag 数据库", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"标签 {database.Nodes.Count}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(report.IsValid ? "状态：正常" : "状态：错误", EditorStyles.miniLabel);
            }
            EditorGUILayout.HelpBox(
            "GUID 是 GameObject 和 Prefab 保存的稳定身份；运行时 ID 只在当前注册表生命周期内有效。",
            MessageType.Info);

            if (report.IsValid)
            {
                EditorGUILayout.HelpBox("数据库校验通过。", MessageType.Info);
            }
            else
            {
                foreach (var error in report.Errors)
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
            }

            if (GUILayout.Button("打开 GameplayTag 标签窗口"))
            {
                GameplayTagDatabaseWindow.ShowWindow();
            }
        }
    }
}
#endif

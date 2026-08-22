#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Fokiga.Runtime.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Fokiga.Editor
{
    [CustomEditor(typeof(GameplayTagComponent))]
    public sealed class GameplayTagComponentEditor : UnityEditor.Editor
    {
        private SerializedProperty mTagGuids;

        private void OnEnable()
        {
            mTagGuids = serializedObject.FindProperty("mTagGuids");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            RemoveEmptyAndDuplicateGuids();
            var database = GameplayTagEditorUtility.FindDatabase();
            if (database == null)
            {
                EditorGUILayout.HelpBox("请先创建 Assets/fokiga/Resources/GameplayTags.asset，再编辑 GameplayTag。", MessageType.Warning);
                EditorGUILayout.PropertyField(mTagGuids, true);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            var nodes = database.Nodes
            .Where(node => node != null)
            .OrderBy(node => GameplayTagEditorUtility.GetPath(database, node), StringComparer.Ordinal)
            .ToList();
            var labels = nodes.Select(node => GameplayTagEditorUtility.GetPath(database, node)).ToArray();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"GameplayTag 标签（{mTagGuids.arraySize}）", EditorStyles.boldLabel);
            }
            for (var index = 0; index < mTagGuids.arraySize; index++)
            {
                var element = mTagGuids.GetArrayElementAtIndex(index);
                var selectedIndex = nodes.FindIndex(node => node.Guid == element.stringValue);

                using (new EditorGUILayout.HorizontalScope())
                {
                    var label = selectedIndex >= 0
                    ? nodes[selectedIndex].Name
                    : string.IsNullOrEmpty(element.stringValue)
                    ? "<未选择>"
                    : $"<失效：{GameplayTagEditorUtility.GetShortGuid(element.stringValue)}>";

                    var tooltip = selectedIndex >= 0 ? labels[selectedIndex] : label;

                    if (GUILayout.Button(new GUIContent(label, tooltip), EditorStyles.popup))
                    {
                        OpenPicker(database, index, element.stringValue);
                    }

                    if (GUILayout.Button(new GUIContent("x", "移除标签"), GUILayout.Width(24f)))
                    {
                        mTagGuids.DeleteArrayElementAtIndex(index);
                        break;
                    }
                }

                if (selectedIndex < 0)
                {
                    EditorGUILayout.HelpBox(
                    string.IsNullOrEmpty(element.stringValue)
                    ? "请选择一个 GameplayTag。"
                    : $"未知标签 GUID：{GameplayTagEditorUtility.GetShortGuid(element.stringValue)}... 请重新选择。",
                    MessageType.Error);
                }
            }

            using (new EditorGUI.DisabledScope(nodes.Count == 0))
            {
                if (GUILayout.Button("添加标签"))
                {
                    mTagGuids.arraySize++;
                    var newIndex = mTagGuids.arraySize - 1;
                    mTagGuids.GetArrayElementAtIndex(newIndex).stringValue = string.Empty;
                    serializedObject.ApplyModifiedProperties();
                    OpenPicker(database, newIndex, string.Empty);
                    return;
                }
            }

            if (GUILayout.Button("打开 GameplayTag 标签窗口"))
            {
                GameplayTagDatabaseWindow.ShowWindow();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OpenPicker(GameplayTagDatabase database, int index, string selectedGuid)
        {
            GameplayTagPickerWindow.Show(database, selectedGuid, guid =>
            {
                if (target == null)
                {
                    return;
                }

                serializedObject.Update();
                if (index < 0 || index >= mTagGuids.arraySize)
                {
                    return;
                }

                mTagGuids.GetArrayElementAtIndex(index).stringValue = guid;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            });
        }

        private void RemoveEmptyAndDuplicateGuids()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = mTagGuids.arraySize - 1; index >= 0; index--)
            {
                var value = mTagGuids.GetArrayElementAtIndex(index).stringValue;
                if (string.IsNullOrEmpty(value) || !seen.Add(value))
                {
                    mTagGuids.DeleteArrayElementAtIndex(index);
                }
            }
        }
    }
}
#endif

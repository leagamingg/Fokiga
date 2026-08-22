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
        private SerializedProperty _tagGuids;

        private void OnEnable()
        {
            _tagGuids = serializedObject.FindProperty("_tagGuids");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            RemoveEmptyAndDuplicateGuids();
            var database = GameplayTagEditorUtility.FindDatabase();
            if (database == null)
            {
                EditorGUILayout.HelpBox("请先创建 Assets/fokiga/Resources/GameplayTags.asset，再编辑 GameplayTag。", MessageType.Warning);
                EditorGUILayout.PropertyField(_tagGuids, true);
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
                EditorGUILayout.LabelField($"GameplayTag 标签（{_tagGuids.arraySize}）", EditorStyles.boldLabel);
            }
            for (var index = 0; index < _tagGuids.arraySize; index++)
            {
                var element = _tagGuids.GetArrayElementAtIndex(index);
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
                        _tagGuids.DeleteArrayElementAtIndex(index);
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
                    _tagGuids.arraySize++;
                    var newIndex = _tagGuids.arraySize - 1;
                    _tagGuids.GetArrayElementAtIndex(newIndex).stringValue = string.Empty;
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
                if (index < 0 || index >= _tagGuids.arraySize)
                {
                    return;
                }

                _tagGuids.GetArrayElementAtIndex(index).stringValue = guid;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            });
        }

        private void RemoveEmptyAndDuplicateGuids()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = _tagGuids.arraySize - 1; index >= 0; index--)
            {
                var value = _tagGuids.GetArrayElementAtIndex(index).stringValue;
                if (string.IsNullOrEmpty(value) || !seen.Add(value))
                {
                    _tagGuids.DeleteArrayElementAtIndex(index);
                }
            }
        }
    }
}
#endif

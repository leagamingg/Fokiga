#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Fokiga.GameplayTags;
using UnityEditor;
using UnityEngine;

namespace Fokiga.GameplayTags.Editor
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
            var database = GameplayTagEditorUtility.FindDatabase();
            if (database == null)
            {
                EditorGUILayout.HelpBox("Create Assets/fokiga/Resources/GameplayTags.asset to edit GameplayTags.", MessageType.Warning);
                EditorGUILayout.PropertyField(_tagGuids, true);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            var nodes = database.Nodes
                .Where(node => node != null)
                .OrderBy(node => GameplayTagEditorUtility.GetPath(database, node), StringComparer.Ordinal)
                .ToList();
            var labels = nodes.Select(node => GameplayTagEditorUtility.GetPath(database, node)).ToArray();

            EditorGUILayout.LabelField("Gameplay Tags", EditorStyles.boldLabel);
            for (var index = 0; index < _tagGuids.arraySize; index++)
            {
                var element = _tagGuids.GetArrayElementAtIndex(index);
                var selectedIndex = nodes.FindIndex(node => node.Guid == element.stringValue);
                var popupIndex = selectedIndex;

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (selectedIndex < 0)
                    {
                        var missingLabels = new string[labels.Length + 1];
                        missingLabels[0] = string.IsNullOrEmpty(element.stringValue)
                            ? "<Unassigned>"
                            : $"<Missing: {element.stringValue}>";
                        Array.Copy(labels, 0, missingLabels, 1, labels.Length);
                        var missingSelection = EditorGUILayout.Popup(0, missingLabels);
                        popupIndex = missingSelection - 1;
                    }
                    else
                    {
                        popupIndex = EditorGUILayout.Popup(selectedIndex, labels);
                    }

                    if (GUILayout.Button("-", GUILayout.Width(24f)))
                    {
                        _tagGuids.DeleteArrayElementAtIndex(index);
                        break;
                    }
                }

                if (popupIndex >= 0 && popupIndex < nodes.Count && popupIndex != selectedIndex)
                {
                    element.stringValue = nodes[popupIndex].Guid;
                }

                if (selectedIndex < 0)
                {
                    EditorGUILayout.HelpBox($"Unknown tag GUID: {element.stringValue}", MessageType.Error);
                }
            }

            if (GUILayout.Button("Add Tag"))
            {
                _tagGuids.arraySize++;
                _tagGuids.GetArrayElementAtIndex(_tagGuids.arraySize - 1).stringValue = nodes.Count > 0 ? nodes[0].Guid : string.Empty;
            }

            if (GUILayout.Button("Open Gameplay Tags Window"))
            {
                GameplayTagDatabaseWindow.ShowWindow();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Fokiga.Runtime.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Fokiga.Editor
{
    internal sealed class GameplayTagPickerWindow : EditorWindow
    {
        private GameplayTagDatabase mDatabase;
        private string mSelectedGuid;
        private string mSearch = string.Empty;
        private Vector2 mScroll;
        private Action<string> mOnSelected;
        private readonly HashSet<string> mExpanded = new HashSet<string>(StringComparer.Ordinal);

        public static void Show(
        GameplayTagDatabase database,
        string selectedGuid,
        Action<string> onSelected)
        {
            if (database == null || onSelected == null)
            {
                return;
            }

            var window = CreateInstance<GameplayTagPickerWindow>();
            window.mDatabase = database;
            window.mSelectedGuid = selectedGuid ?? string.Empty;
            window.mOnSelected = onSelected;
            window.titleContent = new GUIContent("选择 GameplayTag");
            window.minSize = new Vector2(420f, 360f);
            foreach (var root in database.Nodes.Where(node => node != null && string.IsNullOrEmpty(node.ParentGuid)))
            {
                window.mExpanded.Add(root.Guid);
            }

            window.ShowUtility();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (mDatabase == null)
            {
                EditorGUILayout.HelpBox("GameplayTag 数据库不可用。", MessageType.Error);
                return;
            }

            var report = mDatabase.Validate();
            if (!report.IsValid)
            {
                EditorGUILayout.HelpBox(
                $"数据库存在 {report.Errors.Count} 个校验错误，请修复后再分配标签。",
                MessageType.Error);
            }

            EditorGUILayout.LabelField("选择一个标签", EditorStyles.boldLabel);
            mScroll = EditorGUILayout.BeginScrollView(mScroll);
            var children = GameplayTagEditorUtility.BuildChildrenMap(mDatabase);
            if (string.IsNullOrWhiteSpace(mSearch))
            {
                if (children.TryGetValue(string.Empty, out var roots))
                {
                    foreach (var root in roots)
                    {
                        DrawNode(root, 0, children);
                    }
                }
            }
            else
            {
                foreach (var node in mDatabase.Nodes
                .Where(node => node != null)
                .OrderBy(node => GameplayTagEditorUtility.GetPath(mDatabase, node), StringComparer.Ordinal))
                {
                    var path = GameplayTagEditorUtility.GetPath(mDatabase, node);
                    if (path.IndexOf(mSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        DrawNodeRow(node, path, 0, false);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var style = GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField;
                mSearch = GUILayout.TextField(mSearch, style);
                EditorGUILayout.LabelField("搜索", EditorStyles.miniLabel, GUILayout.Width(32f));
                if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                {
                    mSearch = string.Empty;
                    GUI.FocusControl(null);
                }
            }
        }

        private void DrawNode(
        GameplayTagNodeData node,
        int depth,
        IReadOnlyDictionary<string, List<GameplayTagNodeData>> children)
        {
            var hasChildren = children.TryGetValue(node.Guid, out var childNodes) && childNodes.Count > 0;
            DrawNodeRow(node, GameplayTagEditorUtility.GetPath(mDatabase, node), depth, hasChildren);

            if (!hasChildren || !mExpanded.Contains(node.Guid))
            {
                return;
            }

            foreach (var child in childNodes)
            {
                DrawNode(child, depth + 1, children);
            }
        }

        private void DrawNodeRow(GameplayTagNodeData node, string path, int depth, bool hasChildren)
        {
            var rowHeight = EditorGUIUtility.singleLineHeight + 3f;
            var rowRect = GUILayoutUtility.GetRect(0f, rowHeight, GUILayout.ExpandWidth(true));
            var selected = node.Guid == mSelectedGuid;
            var expanded = mExpanded.Contains(node.Guid);
            var indentWidth = depth * 16f;
            var arrowRect = new Rect(rowRect.x + indentWidth, rowRect.y, 18f, rowRect.height);
            var iconRect = new Rect(arrowRect.xMax + 2f, rowRect.y + 2f, 16f, 16f);
            var labelRect = new Rect(iconRect.xMax + 4f, rowRect.y, rowRect.xMax - iconRect.xMax - 4f, rowRect.height);

            if (selected)
            {
                var selectionColor = EditorGUIUtility.isProSkin
                ? new Color(0.24f, 0.48f, 0.78f, 0.55f)
                : new Color(0.30f, 0.52f, 0.86f, 0.22f);
                EditorGUI.DrawRect(rowRect, selectionColor);
            }

            for (var level = 0; level < depth; level++)
            {
                var lineX = rowRect.x + level * 16f + 8f;
                EditorGUI.DrawRect(
                new Rect(lineX, rowRect.y, 1f, rowRect.height),
                EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.12f)
                : new Color(0f, 0f, 0f, 0.12f));
            }

            if (hasChildren)
            {
                var nextExpanded = EditorGUI.Foldout(arrowRect, expanded, GUIContent.none, false);
                if (nextExpanded != expanded)
                {
                    if (nextExpanded)
                    {
                        mExpanded.Add(node.Guid);
                    }
                    else
                    {
                        mExpanded.Remove(node.Guid);
                    }

                    Repaint();
                }
            }

            var icon = EditorGUIUtility.IconContent(hasChildren ? "d_Folder Icon" : "d_FilterByLabel");
            if (icon != null && icon.image != null)
            {
                GUI.Label(iconRect, icon);
            }

            var content = new GUIContent(
            node.Name,
            $"{path}\nGUID：{GameplayTagEditorUtility.GetShortGuid(node.Guid)}");
            var previousColor = GUI.color;
            if (selected && EditorGUIUtility.isProSkin)
            {
                GUI.color = Color.white;
            }

            GUI.Label(labelRect, content, EditorStyles.label);
            GUI.color = previousColor;

            var currentEvent = UnityEngine.Event.current;
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && rowRect.Contains(currentEvent.mousePosition))
            {
                if (!hasChildren || !arrowRect.Contains(currentEvent.mousePosition))
                {
                    mSelectedGuid = node.Guid;
                    mOnSelected(node.Guid);
                    currentEvent.Use();
                    Close();
                    GUIUtility.ExitGUI();
                }
            }
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Fokiga.Runtime.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Fokiga.Editor
{
    public sealed class GameplayTagDatabaseWindow : EditorWindow
    {
        private GameplayTagDatabase mDatabase;
        private string mSearch = string.Empty;
        private string mSelectedGuid = string.Empty;
        private string mNameBuffer = string.Empty;
        private readonly HashSet<string> mExpanded = new HashSet<string>(StringComparer.Ordinal);
        private Vector2 mTreeScroll;
        private Vector2 mDetailsScroll;
        private List<string> mReferencePaths;
        private bool mShowIdentityDetails;
        [SerializeField]
        private float mTreeWidth = -1f;
        private bool mDraggingSplitter;

        public static void ShowWindow()
        {
            var window = GetWindow<GameplayTagDatabaseWindow>("GameplayTag 标签");
            window.minSize = new Vector2(760f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            mDatabase = GameplayTagEditorUtility.FindDatabase();
            Undo.undoRedoPerformed += HandleUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (mDatabase == null)
            {
                EditorGUILayout.HelpBox("请创建或选择一个 GameplayTag 数据库资产。", MessageType.Info);
                if (GUILayout.Button("创建默认数据库", GUILayout.Width(180f)))
                {
                    mDatabase = GameplayTagEditorUtility.CreateDefaultDatabase();
                }

                return;
            }

            DrawValidationSummary();
            DrawContent();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("GameplayTag 标签", EditorStyles.boldLabel, GUILayout.Width(105f));
                var selected = (GameplayTagDatabase)EditorGUILayout.ObjectField(
                mDatabase,
                typeof(GameplayTagDatabase),
                false,
                GUILayout.Width(230f));
                if (selected != mDatabase)
                {
                    mDatabase = selected;
                    mSelectedGuid = string.Empty;
                }

                if (GUILayout.Button("创建默认库", EditorStyles.toolbarButton, GUILayout.Width(78f)))
                {
                    mDatabase = GameplayTagEditorUtility.CreateDefaultDatabase();
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("搜索", EditorStyles.miniLabel, GUILayout.Width(32f));
                mSearch = GUILayout.TextField(mSearch, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.Width(180f));
                if (GUILayout.Button("校验", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                {
                    ValidateDatabase();
                }
            }
        }

        private void DrawValidationSummary()
        {
            var report = mDatabase.Validate();
            if (!report.IsValid)
            {
                EditorGUILayout.HelpBox(
                $"数据库存在 {report.Errors.Count} 个校验错误，请修复后再进入运行模式。",
                MessageType.Error);
            }
        }

        private void DrawContent()
        {
            const float splitterWidth = 5f;
            const float minimumTreeWidth = 260f;
            const float minimumDetailsWidth = 280f;
            var maximumTreeWidth = Mathf.Max(
            minimumTreeWidth,
            position.width - minimumDetailsWidth - splitterWidth);
            if (mTreeWidth <= 0f)
            {
                mTreeWidth = position.width * 0.56f;
            }

            mTreeWidth = Mathf.Clamp(mTreeWidth, minimumTreeWidth, maximumTreeWidth);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(
                EditorStyles.helpBox,
                GUILayout.Width(mTreeWidth)))
                {
                    EditorGUILayout.LabelField("标签层级", EditorStyles.boldLabel);
                    DrawTreeToolbar();
                    mTreeScroll = EditorGUILayout.BeginScrollView(mTreeScroll);
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
                        foreach (var node in mDatabase.Nodes.OrderBy(node => GameplayTagEditorUtility.GetPath(mDatabase, node), StringComparer.Ordinal))
                        {
                            if (node != null && GameplayTagEditorUtility.GetPath(mDatabase, node).IndexOf(mSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                DrawNodeRow(node, 0, false);
                            }
                        }
                    }

                    EditorGUILayout.EndScrollView();
                }

                var splitterRect = GUILayoutUtility.GetRect(
                splitterWidth,
                1f,
                GUILayout.Width(splitterWidth),
                GUILayout.ExpandHeight(true));
                DrawSplitter(splitterRect, minimumTreeWidth, maximumTreeWidth);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    mDetailsScroll = EditorGUILayout.BeginScrollView(mDetailsScroll);
                    EditorGUILayout.LabelField("标签详情", EditorStyles.boldLabel);
                    DrawDetails();
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawSplitter(Rect splitterRect, float minimumTreeWidth, float maximumTreeWidth)
        {
            var backgroundColor = EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.05f)
            : new Color(0f, 0f, 0f, 0.06f);
            var gripColor = EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.28f)
            : new Color(0f, 0f, 0f, 0.22f);
            EditorGUI.DrawRect(splitterRect, backgroundColor);
            EditorGUI.DrawRect(
            new Rect(splitterRect.center.x - 1f, splitterRect.center.y - 16f, 2f, 32f),
            gripColor);
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

            var splitterId = GUIUtility.GetControlID(
            "GameplayTagDatabaseSplitter".GetHashCode(),
            FocusType.Passive,
            splitterRect);
            var currentEvent = UnityEngine.Event.current;
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && splitterRect.Contains(currentEvent.mousePosition))
            {
                GUIUtility.hotControl = splitterId;
                mDraggingSplitter = true;
                currentEvent.Use();
            }
            else if (mDraggingSplitter && GUIUtility.hotControl == splitterId && currentEvent.type == EventType.MouseDrag)
            {
                mTreeWidth = Mathf.Clamp(currentEvent.mousePosition.x, minimumTreeWidth, maximumTreeWidth);
                Repaint();
                currentEvent.Use();
            }
            else if (mDraggingSplitter && GUIUtility.hotControl == splitterId && currentEvent.type == EventType.MouseUp)
            {
                mDraggingSplitter = false;
                GUIUtility.hotControl = 0;
                currentEvent.Use();
            }
        }

        private void DrawTreeToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("添加根标签", EditorStyles.toolbarButton, GUILayout.Width(78f)))
                {
                    AddRoot();
                }

                if (GUILayout.Button("全部展开", EditorStyles.toolbarButton, GUILayout.Width(68f)))
                {
                    foreach (var node in mDatabase.Nodes.Where(node => node != null))
                    {
                        mExpanded.Add(node.Guid);
                    }
                }

                if (GUILayout.Button("全部折叠", EditorStyles.toolbarButton, GUILayout.Width(68f)))
                {
                    mExpanded.Clear();
                }

                GUILayout.FlexibleSpace();
                var status = mDatabase.Validate().IsValid ? "状态：正常" : "状态：错误";
                EditorGUILayout.LabelField(status, EditorStyles.miniLabel, GUILayout.Width(72f));
                EditorGUILayout.LabelField($"{mDatabase.Nodes.Count} 个标签", EditorStyles.miniLabel, GUILayout.Width(72f));
            }
        }

        private void DrawNode(GameplayTagNodeData node, int depth, IReadOnlyDictionary<string, List<GameplayTagNodeData>> children)
        {
            var hasChildren = children.TryGetValue(node.Guid, out var childNodes) && childNodes.Count > 0;
            var isExpanded = mExpanded.Contains(node.Guid);
            DrawNodeRow(node, depth, hasChildren);

            if (hasChildren && isExpanded)
            {
                foreach (var child in childNodes)
                {
                    DrawNode(child, depth + 1, children);
                }
            }
        }

        private void DrawNodeRow(GameplayTagNodeData node, int depth, bool hasChildren)
        {
            var rowHeight = EditorGUIUtility.singleLineHeight + 3f;
            var rowRect = GUILayoutUtility.GetRect(0f, rowHeight, GUILayout.ExpandWidth(true));
            var selected = mSelectedGuid == node.Guid;
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

            var path = GameplayTagEditorUtility.GetPath(mDatabase, node);
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
                    if (mSelectedGuid != node.Guid)
                    {
                        mSelectedGuid = node.Guid;
                        mNameBuffer = node.Name;
                        mReferencePaths = null;
                    }

                    Repaint();
                    currentEvent.Use();
                }
            }
        }

        private void DrawDetails()
        {
            if (!mDatabase.TryGetNode(mSelectedGuid, out var node))
            {
                EditorGUILayout.LabelField("请选择一个标签", EditorStyles.boldLabel);
                var report = mDatabase.Validate();
                if (!report.IsValid)
                {
                    EditorGUILayout.LabelField("校验错误", EditorStyles.boldLabel);
                    foreach (var error in report.Errors)
                    {
                        EditorGUILayout.HelpBox(error, MessageType.Error);
                    }
                }

                return;
            }

            EditorGUILayout.LabelField(node.Name, EditorStyles.boldLabel);
            var tagPath = GameplayTagEditorUtility.GetPath(mDatabase, node);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("路径", tagPath);
                if (GUILayout.Button("复制", GUILayout.Width(42f)))
                {
                    EditorGUIUtility.systemCopyBuffer = tagPath;
                    ShowNotification(new GUIContent("路径已复制"));
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("GUID", GameplayTagEditorUtility.GetShortGuid(node.Guid));
                if (GUILayout.Button("复制", GUILayout.Width(42f)))
                {
                    EditorGUIUtility.systemCopyBuffer = node.Guid;
                    ShowNotification(new GUIContent("GUID 已复制"));
                }
            }

            mShowIdentityDetails = EditorGUILayout.Foldout(mShowIdentityDetails, "身份信息", true);
            if (mShowIdentityDetails && GameplayTagEditorUtility.TryGetRuntimeTag(mDatabase, node.Guid, out var runtimeTag))
            {
                EditorGUILayout.LabelField("完整 GUID", node.Guid, EditorStyles.miniLabel);
                EditorGUILayout.LabelField("运行时 ID", runtimeTag.Id.Value.ToString());
                EditorGUILayout.LabelField("有效范围", "当前注册表生命周期", EditorStyles.miniLabel);
            }
            else if (mShowIdentityDetails)
            {
                EditorGUILayout.LabelField("完整 GUID", node.Guid, EditorStyles.miniLabel);
                EditorGUILayout.LabelField("运行时 ID", "数据库有效后可用", EditorStyles.miniLabel);
            }

            var children = GameplayTagEditorUtility.BuildChildrenMap(mDatabase);
            var childCount = children.TryGetValue(node.Guid, out var childNodes) ? childNodes.Count : 0;
            var descendantCount = GameplayTagEditorUtility.GetDescendantGuids(mDatabase, node.Guid).Count;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"子标签 {childCount}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"全部后代 {descendantCount}", EditorStyles.miniLabel);
            }
            EditorGUILayout.Space(6f);

            mNameBuffer = EditorGUILayout.TextField("名称", mNameBuffer);
            if (GUILayout.Button("重命名"))
            {
                ApplyDatabaseChange("重命名 GameplayTag", () => mDatabase.Rename(node.Guid, mNameBuffer));
            }

            EditorGUILayout.Space(6f);
            var parentCandidates = GameplayTagEditorUtility.GetParentCandidates(mDatabase, node.Guid);
            var parentLabels = new List<string> { "<根节点>" };
            parentLabels.AddRange(parentCandidates.Select(candidate => GameplayTagEditorUtility.GetPath(mDatabase, candidate)));
            var currentParentIndex = 0;
            if (!string.IsNullOrEmpty(node.ParentGuid))
            {
                var parentIndex = parentCandidates.FindIndex(candidate => candidate.Guid == node.ParentGuid);
                currentParentIndex = parentIndex >= 0 ? parentIndex + 1 : 0;
            }

            var selectedParentIndex = EditorGUILayout.Popup("父节点", currentParentIndex, parentLabels.ToArray());
            if (selectedParentIndex != currentParentIndex)
            {
                var parentGuid = selectedParentIndex == 0 ? string.Empty : parentCandidates[selectedParentIndex - 1].Guid;
                ApplyDatabaseChange("移动 GameplayTag", () => mDatabase.Reparent(node.Guid, parentGuid));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("添加子标签"))
                {
                    GameplayTagNodeData child = null;
                    if (ApplyDatabaseChange("添加 GameplayTag 子标签", () =>
                    {
                        child = mDatabase.AddChild("新建标签", node.Guid);
                        return true;
                    }))
                    {
                        mSelectedGuid = child.Guid;
                        mNameBuffer = child.Name;
                    }
                }

                if (GUILayout.Button("删除分支"))
                {
                    var references = FindSubtreeReferences(node.Guid);
                    var message = references.Count == 0
                    ? "确定删除此标签及其全部后代吗？"
                    : $"有 {references.Count} 个资源引用此标签或其后代，仍要删除整个分支吗？";
                    if (EditorUtility.DisplayDialog("删除 GameplayTag", message, "删除", "取消"))
                    {
                        if (ApplyDatabaseChange("删除 GameplayTag 分支", () => mDatabase.RemoveSubtree(node.Guid)))
                        {
                            mSelectedGuid = string.Empty;
                            mReferencePaths = null;
                        }
                    }
                }
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("查找资源引用"))
            {
                mReferencePaths = GameplayTagEditorUtility.FindSerializedReferences(mDatabase, node.Guid);
            }

            if (mReferencePaths != null)
            {
                EditorGUILayout.LabelField($"引用资源：{mReferencePaths.Count}", EditorStyles.boldLabel);
                foreach (var referencePath in mReferencePaths)
                {
                    EditorGUILayout.LabelField(referencePath, EditorStyles.miniLabel);
                }
            }
        }

        private void AddRoot()
        {
            GameplayTagNodeData node = null;
            if (ApplyDatabaseChange("添加 GameplayTag 根标签", () =>
            {
                node = mDatabase.AddRoot("新建标签");
                return true;
            }))
            {
                mSelectedGuid = node.Guid;
                mNameBuffer = node.Name;
            }
        }

        private void ValidateDatabase()
        {
            var report = mDatabase.Validate();
            if (report.IsValid)
            {
                ShowNotification(new GUIContent("GameplayTag 数据库校验通过"));
                return;
            }

            ShowNotification(new GUIContent($"发现 {report.Errors.Count} 个校验错误"));
        }

        private bool ApplyDatabaseChange(string action, Func<bool> change)
        {
            Undo.RegisterCompleteObjectUndo(mDatabase, action);
            if (!change())
            {
                Undo.PerformUndo();
                return false;
            }

            var report = mDatabase.Validate();
            if (!report.IsValid)
            {
                Undo.PerformUndo();
                ShowNotification(new GUIContent(report.Errors[0]));
                return false;
            }

            CommitChange();
            return true;
        }

        private void CommitChange()
        {
            GameplayTagEditorUtility.MarkChanged(mDatabase);
            Repaint();
        }

        private List<string> FindSubtreeReferences(string guid)
        {
            var references = new HashSet<string>(StringComparer.Ordinal);
            var guids = GameplayTagEditorUtility.GetDescendantGuids(mDatabase, guid);
            guids.Add(guid);
            foreach (var descendantGuid in guids)
            {
                foreach (var path in GameplayTagEditorUtility.FindSerializedReferences(mDatabase, descendantGuid))
                {
                    references.Add(path);
                }
            }

            return references.OrderBy(path => path, StringComparer.Ordinal).ToList();
        }

        private void HandleUndoRedo()
        {
            if (mDatabase == null)
            {
                return;
            }

            GameplayTagEditorUtility.MarkChanged(mDatabase);
            mReferencePaths = null;
            Repaint();
        }
    }
}
#endif

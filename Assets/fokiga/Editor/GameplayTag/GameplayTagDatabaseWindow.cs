#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Fokiga.GameplayTags;
using UnityEditor;
using UnityEngine;

namespace Fokiga.GameplayTags.Editor
{
    public sealed class GameplayTagDatabaseWindow : EditorWindow
    {
        private GameplayTagDatabase _database;
        private string _search = string.Empty;
        private string _selectedGuid = string.Empty;
        private string _nameBuffer = string.Empty;
        private readonly HashSet<string> _expanded = new HashSet<string>(StringComparer.Ordinal);
        private Vector2 _treeScroll;
        private Vector2 _detailsScroll;
        private List<string> _referencePaths;

        public static void ShowWindow()
        {
            var window = GetWindow<GameplayTagDatabaseWindow>("Gameplay Tags");
            window.minSize = new Vector2(760f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            _database = GameplayTagEditorUtility.FindDatabase();
            Undo.undoRedoPerformed += HandleUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_database == null)
            {
                EditorGUILayout.HelpBox("Create or select a GameplayTagDatabase asset to begin.", MessageType.Info);
                if (GUILayout.Button("Create Default Database", GUILayout.Width(180f)))
                {
                    _database = GameplayTagEditorUtility.CreateDefaultDatabase();
                }

                return;
            }

            DrawContent();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var selected = (GameplayTagDatabase)EditorGUILayout.ObjectField(
                    _database,
                    typeof(GameplayTagDatabase),
                    false,
                    GUILayout.Width(260f));
                if (selected != _database)
                {
                    _database = selected;
                    _selectedGuid = string.Empty;
                }

                if (GUILayout.Button("Create Default", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                {
                    _database = GameplayTagEditorUtility.CreateDefaultDatabase();
                }

                GUILayout.FlexibleSpace();
                _search = GUILayout.TextField(_search, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.Width(180f));
                if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    ValidateDatabase();
                }
            }
        }

        private void DrawContent()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.56f)))
                {
                    DrawTreeToolbar();
                    _treeScroll = EditorGUILayout.BeginScrollView(_treeScroll);
                    var children = GameplayTagEditorUtility.BuildChildrenMap(_database);
                    if (string.IsNullOrWhiteSpace(_search))
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
                        foreach (var node in _database.Nodes.OrderBy(node => GameplayTagEditorUtility.GetPath(_database, node), StringComparer.Ordinal))
                        {
                            if (node != null && GameplayTagEditorUtility.GetPath(_database, node).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                DrawNodeRow(node, 0, false);
                            }
                        }
                    }

                    EditorGUILayout.EndScrollView();
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);
                    DrawDetails();
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawTreeToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Root", GUILayout.Width(80f)))
                {
                    AddRoot();
                }

                if (GUILayout.Button("Expand All", GUILayout.Width(80f)))
                {
                    foreach (var node in _database.Nodes.Where(node => node != null))
                    {
                        _expanded.Add(node.Guid);
                    }
                }

                if (GUILayout.Button("Collapse All", GUILayout.Width(90f)))
                {
                    _expanded.Clear();
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"{_database.Nodes.Count} nodes", EditorStyles.miniLabel, GUILayout.Width(70f));
            }
        }

        private void DrawNode(GameplayTagNodeData node, int depth, IReadOnlyDictionary<string, List<GameplayTagNodeData>> children)
        {
            var hasChildren = children.TryGetValue(node.Guid, out var childNodes) && childNodes.Count > 0;
            var isExpanded = _expanded.Contains(node.Guid);
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
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(depth * 16f);
                if (hasChildren)
                {
                    var expanded = GUILayout.Button(_expanded.Contains(node.Guid) ? "v" : ">", GUI.skin.label, GUILayout.Width(18f));
                    if (expanded)
                    {
                        if (!_expanded.Add(node.Guid))
                        {
                            _expanded.Remove(node.Guid);
                        }
                    }
                }
                else
                {
                    GUILayout.Space(18f);
                }

                var selected = _selectedGuid == node.Guid;
                if (GUILayout.Toggle(selected, GameplayTagEditorUtility.GetPath(_database, node), "Button"))
                {
                    if (_selectedGuid != node.Guid)
                    {
                        _selectedGuid = node.Guid;
                        _nameBuffer = node.Name;
                        _referencePaths = null;
                    }
                }
            }
        }

        private void DrawDetails()
        {
            if (!_database.TryGetNode(_selectedGuid, out var node))
            {
                EditorGUILayout.LabelField("Select a tag", EditorStyles.boldLabel);
                var report = _database.Validate();
                if (!report.IsValid)
                {
                    EditorGUILayout.LabelField("Validation errors", EditorStyles.boldLabel);
                    foreach (var error in report.Errors)
                    {
                        EditorGUILayout.HelpBox(error, MessageType.Error);
                    }
                }

                return;
            }

            EditorGUILayout.LabelField("Selected Tag", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Path", GameplayTagEditorUtility.GetPath(_database, node));
            EditorGUILayout.LabelField("GUID", node.Guid);
            EditorGUILayout.Space(6f);

            _nameBuffer = EditorGUILayout.TextField("Name", _nameBuffer);
            if (GUILayout.Button("Rename"))
            {
                ApplyDatabaseChange("Rename Gameplay Tag", () => _database.Rename(node.Guid, _nameBuffer));
            }

            EditorGUILayout.Space(6f);
            var parentCandidates = GameplayTagEditorUtility.GetParentCandidates(_database, node.Guid);
            var parentLabels = new List<string> { "<Root>" };
            parentLabels.AddRange(parentCandidates.Select(candidate => GameplayTagEditorUtility.GetPath(_database, candidate)));
            var currentParentIndex = 0;
            if (!string.IsNullOrEmpty(node.ParentGuid))
            {
                var parentIndex = parentCandidates.FindIndex(candidate => candidate.Guid == node.ParentGuid);
                currentParentIndex = parentIndex >= 0 ? parentIndex + 1 : 0;
            }

            var selectedParentIndex = EditorGUILayout.Popup("Parent", currentParentIndex, parentLabels.ToArray());
            if (selectedParentIndex != currentParentIndex)
            {
                var parentGuid = selectedParentIndex == 0 ? string.Empty : parentCandidates[selectedParentIndex - 1].Guid;
                ApplyDatabaseChange("Reparent Gameplay Tag", () => _database.Reparent(node.Guid, parentGuid));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Child"))
                {
                    GameplayTagNodeData child = null;
                    if (ApplyDatabaseChange("Add Gameplay Tag Child", () =>
                    {
                        child = _database.AddChild("NewChild", node.Guid);
                        return true;
                    }))
                    {
                        _selectedGuid = child.Guid;
                        _nameBuffer = child.Name;
                    }
                }

                if (GUILayout.Button("Delete Subtree"))
                {
                    if (EditorUtility.DisplayDialog("Delete Gameplay Tag", "Delete this tag and all descendants?", "Delete", "Cancel"))
                    {
                        if (ApplyDatabaseChange("Delete Gameplay Tag Subtree", () => _database.RemoveSubtree(node.Guid)))
                        {
                            _selectedGuid = string.Empty;
                            _referencePaths = null;
                        }
                    }
                }
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Find Serialized References"))
            {
                _referencePaths = GameplayTagEditorUtility.FindSerializedReferences(node.Guid);
            }

            if (_referencePaths != null)
            {
                EditorGUILayout.LabelField($"References: {_referencePaths.Count}", EditorStyles.boldLabel);
                foreach (var path in _referencePaths)
                {
                    EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
                }
            }
        }

        private void AddRoot()
        {
            GameplayTagNodeData node = null;
            if (ApplyDatabaseChange("Add Gameplay Tag Root", () =>
            {
                node = _database.AddRoot("NewTag");
                return true;
            }))
            {
                _selectedGuid = node.Guid;
                _nameBuffer = node.Name;
            }
        }

        private void ValidateDatabase()
        {
            var report = _database.Validate();
            if (report.IsValid)
            {
                ShowNotification(new GUIContent("GameplayTag database is valid."));
                return;
            }

            ShowNotification(new GUIContent($"Found {report.Errors.Count} validation error(s)."));
        }

        private bool ApplyDatabaseChange(string action, Func<bool> change)
        {
            Undo.RegisterCompleteObjectUndo(_database, action);
            if (!change())
            {
                Undo.PerformUndo();
                return false;
            }

            var report = _database.Validate();
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
            GameplayTagEditorUtility.MarkChanged(_database);
            Repaint();
        }

        private void HandleUndoRedo()
        {
            if (_database == null)
            {
                return;
            }

            GameplayTagEditorUtility.MarkChanged(_database);
            _referencePaths = null;
            Repaint();
        }
    }
}
#endif

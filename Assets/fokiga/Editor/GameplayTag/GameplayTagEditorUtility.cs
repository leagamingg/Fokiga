#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fokiga.Runtime.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Fokiga.Editor
{
    internal static class GameplayTagEditorUtility
    {
        public static GameplayTagDatabase FindDatabase()
        {
            var guids = AssetDatabase.FindAssets("t:GameplayTagDatabase");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var database = AssetDatabase.LoadAssetAtPath<GameplayTagDatabase>(path);
                if (database != null)
                {
                    return database;
                }
            }

            return null;
        }

        public static GameplayTagDatabase CreateDefaultDatabase()
        {
            const string assetPath = "Assets/fokiga/Resources/GameplayTags.asset";
            var existing = AssetDatabase.LoadAssetAtPath<GameplayTagDatabase>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            var database = ScriptableObject.CreateInstance<GameplayTagDatabase>();
            AssetDatabase.CreateAsset(database, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = database;
            return database;
        }

        public static void MarkChanged(GameplayTagDatabase database)
        {
            if (database == null)
            {
                return;
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            GameplayTagRegistry.Initialize(database);
        }

        public static bool TryGetRuntimeTag(GameplayTagDatabase database, string guid, out GameplayTag tag)
        {
            tag = GameplayTag.Invalid;
            if (database == null || string.IsNullOrEmpty(guid))
            {
                return false;
            }

            if (!GameplayTagRegistry.IsInitialized || !ReferenceEquals(GameplayTagRegistry.Database, database))
            {
                if (Application.isPlaying || !GameplayTagRegistry.Initialize(database))
                {
                    return false;
                }
            }

            return GameplayTagRegistry.TryGetTagByGuid(guid, out tag);
        }

        public static string GetShortGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return "<empty>";
            }

            return guid.Length <= 8 ? guid : guid.Substring(0, 8);
        }

        public static string GetPath(GameplayTagDatabase database, GameplayTagNodeData node)
        {
            return database != null && node != null && database.TryGetPath(node.Guid, out var path)
            ? path
            : node?.Name ?? string.Empty;
        }

        public static Dictionary<string, List<GameplayTagNodeData>> BuildChildrenMap(GameplayTagDatabase database)
        {
            var result = new Dictionary<string, List<GameplayTagNodeData>>(StringComparer.Ordinal);
            if (database == null)
            {
                return result;
            }

            foreach (var node in database.Nodes)
            {
                if (node == null)
                {
                    continue;
                }

                if (!result.TryGetValue(node.ParentGuid ?? string.Empty, out var children))
                {
                    children = new List<GameplayTagNodeData>();
                    result[node.ParentGuid ?? string.Empty] = children;
                }

                children.Add(node);
            }

            foreach (var children in result.Values)
            {
                children.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            }

            return result;
        }

        public static HashSet<string> GetDescendantGuids(GameplayTagDatabase database, string guid)
        {
            var descendants = new HashSet<string>(StringComparer.Ordinal);
            var children = BuildChildrenMap(database);
            CollectDescendants(guid, children, descendants);
            return descendants;
        }

        public static List<GameplayTagNodeData> GetParentCandidates(GameplayTagDatabase database, string selectedGuid)
        {
            var descendants = GetDescendantGuids(database, selectedGuid);
            descendants.Add(selectedGuid);

            return database.Nodes
            .Where(node => node != null && !descendants.Contains(node.Guid))
            .OrderBy(node => GetPath(database, node), StringComparer.Ordinal)
            .ToList();
        }

        public static List<string> FindSerializedReferences(string guid)
        {
            return FindSerializedReferences(null, guid);
        }

        public static List<string> FindSerializedReferences(GameplayTagDatabase database, string guid)
        {
            var references = new List<string>();
            if (string.IsNullOrEmpty(guid))
            {
                return references;
            }

            var databasePath = database != null ? AssetDatabase.GetAssetPath(database) : string.Empty;

            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (!string.IsNullOrEmpty(databasePath) && string.Equals(path, databasePath, StringComparison.Ordinal))
                {
                    continue;
                }

                var extension = Path.GetExtension(path);
                if (!string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    if (File.ReadAllText(path).IndexOf(guid, StringComparison.Ordinal) >= 0)
                    {
                        references.Add(path);
                    }
                }
                catch (Exception)
                {
                    // Some imported assets cannot be read as text. They are not valid tag component targets.
                }
            }

            return references;
        }

        private static void CollectDescendants(
        string parentGuid,
        IReadOnlyDictionary<string, List<GameplayTagNodeData>> children,
        ISet<string> result)
        {
            if (!children.TryGetValue(parentGuid ?? string.Empty, out var childNodes))
            {
                return;
            }

            foreach (var child in childNodes)
            {
                if (result.Add(child.Guid))
                {
                    CollectDescendants(child.Guid, children, result);
                }
            }
        }

        [MenuItem("工具/GameplayTag 标签", priority = 20)]
        private static void OpenWindow()
        {
            GameplayTagDatabaseWindow.ShowWindow();
        }

        [MenuItem("Assets/Create/Fokiga/GameplayTag/数据库", priority = 20)]
        private static void CreateDatabaseAsset()
        {
            var database = CreateDefaultDatabase();
            Selection.activeObject = database;
        }
    }
}
#endif

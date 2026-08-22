using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fokiga.GameplayTags
{
    public static class GameplayTagRegistry
    {
        private sealed class RuntimeNode
        {
            public string Guid;
            public string Path;
            public int ParentId = -1;
            public int[] Children = Array.Empty<int>();
            public int[] Ancestors = Array.Empty<int>();
            public int[] Descendants = Array.Empty<int>();
            public ulong[] AncestorBits = Array.Empty<ulong>();
            public GameplayTag[] ChildTags = Array.Empty<GameplayTag>();
            public GameplayTag[] AncestorTags = Array.Empty<GameplayTag>();
            public GameplayTag[] DescendantTags = Array.Empty<GameplayTag>();
        }

        private static readonly object SyncRoot = new object();
        private static Dictionary<string, GameplayTagId> _pathToId;
        private static Dictionary<string, GameplayTagId> _guidToId;
        private static RuntimeNode[] _nodes = Array.Empty<RuntimeNode>();
        private static GameplayTag[] _tags = Array.Empty<GameplayTag>();
        private static int _version;
        private static int _wordCount;
        private static GameplayTagDatabase _database;
        private static bool _initialized;
        private static bool _initializationSucceeded;

        public static bool IsInitialized => _initialized;

        public static int Version => _version;

        public static int Count => _nodes.Length;

        public static int WordCount => _wordCount;

        public static GameplayTagDatabase Database => _database;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            lock (SyncRoot)
            {
                _pathToId = null;
                _guidToId = null;
                _nodes = Array.Empty<RuntimeNode>();
                _tags = Array.Empty<GameplayTag>();
                _version = 0;
                _wordCount = 0;
                _database = null;
                _initialized = false;
                _initializationSucceeded = false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (_initialized)
            {
                return;
            }

            var database = Resources.Load<GameplayTagDatabase>("GameplayTags");
            if (database == null)
            {
                Debug.LogError("GameplayTag database was not found at Resources/GameplayTags.asset.");
                Initialize(null);
                return;
            }

            Initialize(database);
        }

        public static bool Initialize(GameplayTagDatabase database)
        {
            lock (SyncRoot)
            {
                if (_initialized && ReferenceEquals(_database, database) && Application.isPlaying)
                {
                    return _initializationSucceeded;
                }

                if (_initialized && Application.isPlaying)
                {
                    Debug.LogError("GameplayTagRegistry cannot be rebuilt after runtime initialization.");
                    return false;
                }

                _database = database;
                _version = Math.Max(1, _version + 1);
                _pathToId = new Dictionary<string, GameplayTagId>(StringComparer.Ordinal);
                _guidToId = new Dictionary<string, GameplayTagId>(StringComparer.Ordinal);
                _nodes = Array.Empty<RuntimeNode>();
                _tags = Array.Empty<GameplayTag>();
                _wordCount = 0;
                _initialized = true;
                _initializationSucceeded = false;

                if (database == null)
                {
                    return false;
                }

                var report = database.Validate();
                if (!report.IsValid)
                {
                    foreach (var error in report.Errors)
                    {
                        Debug.LogError($"GameplayTag database error: {error}", database);
                    }

                    return false;
                }

                Build(database);
                _initializationSucceeded = true;
                return true;
            }
        }

        public static bool TryGetTag(string path, out GameplayTag tag)
        {
            tag = GameplayTag.Invalid;
            EnsureInitialized();
            if (string.IsNullOrEmpty(path) || _pathToId == null || !_pathToId.TryGetValue(path, out var id))
            {
                return false;
            }

            tag = _tags[id.Value];
            return true;
        }

        public static bool TryGetTagByGuid(string guid, out GameplayTag tag)
        {
            tag = GameplayTag.Invalid;
            EnsureInitialized();
            if (string.IsNullOrEmpty(guid) || _guidToId == null || !_guidToId.TryGetValue(guid, out var id))
            {
                return false;
            }

            tag = _tags[id.Value];
            return true;
        }

        public static bool IsValid(GameplayTag tag)
        {
            return tag.IsValid && tag.RegistryVersion == _version && tag.Id.Value < _nodes.Length;
        }

        public static bool IsChildOf(GameplayTag tag, GameplayTag ancestor, bool includeSelf = true)
        {
            if (!IsValid(tag) || !IsValid(ancestor))
            {
                return false;
            }

            if (tag.Id == ancestor.Id)
            {
                return includeSelf;
            }

            return IsBitSet(_nodes[tag.Id.Value].AncestorBits, ancestor.Id.Value);
        }

        public static GameplayTag GetParent(GameplayTag tag)
        {
            if (!IsValid(tag))
            {
                return GameplayTag.Invalid;
            }

            var parentId = _nodes[tag.Id.Value].ParentId;
            return parentId >= 0 ? _tags[parentId] : GameplayTag.Invalid;
        }

        public static IReadOnlyList<GameplayTag> GetChildren(GameplayTag tag)
        {
            if (!IsValid(tag))
            {
                return Array.Empty<GameplayTag>();
            }

            return _nodes[tag.Id.Value].ChildTags;
        }

        public static IReadOnlyList<GameplayTag> GetAncestors(GameplayTag tag)
        {
            if (!IsValid(tag))
            {
                return Array.Empty<GameplayTag>();
            }

            return _nodes[tag.Id.Value].AncestorTags;
        }

        public static IReadOnlyList<GameplayTag> GetDescendants(GameplayTag tag)
        {
            if (!IsValid(tag))
            {
                return Array.Empty<GameplayTag>();
            }

            return _nodes[tag.Id.Value].DescendantTags;
        }

        public static string GetPath(GameplayTag tag)
        {
            return IsValid(tag) ? tag.Path : string.Empty;
        }

        internal static ulong[] GetAncestorBits(GameplayTag tag)
        {
            return IsValid(tag) ? _nodes[tag.Id.Value].AncestorBits : Array.Empty<ulong>();
        }

        internal static int GetRuntimeIdCount(GameplayTag tag)
        {
            return IsValid(tag) ? _nodes.Length : 0;
        }

        internal static int GetVersion(GameplayTag tag)
        {
            return tag.RegistryVersion;
        }

        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                AutoInitialize();
            }
        }

        private static void Build(GameplayTagDatabase database)
        {
            var pathByGuid = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var node in database.Nodes)
            {
                database.TryGetPath(node.Guid, out var path);
                pathByGuid[node.Guid] = path;
            }

            var orderedNodes = database.Nodes
                .OrderBy(node => pathByGuid[node.Guid], StringComparer.Ordinal)
                .ThenBy(node => node.Guid, StringComparer.Ordinal)
                .ToList();

            _wordCount = Math.Max(1, (orderedNodes.Count + 63) / 64);
            _nodes = new RuntimeNode[orderedNodes.Count];
            _tags = new GameplayTag[orderedNodes.Count];
            var parentGuids = new string[orderedNodes.Count];

            for (var index = 0; index < orderedNodes.Count; index++)
            {
                var source = orderedNodes[index];
                var node = new RuntimeNode
                {
                    Guid = source.Guid,
                    Path = pathByGuid[source.Guid],
                    AncestorBits = new ulong[_wordCount]
                };
                _nodes[index] = node;
                _tags[index] = new GameplayTag(new GameplayTagId(index), _version, node.Path, node.Guid);
                parentGuids[index] = source.ParentGuid;
                _pathToId[node.Path] = new GameplayTagId(index);
                _guidToId[node.Guid] = new GameplayTagId(index);
            }

            var children = Enumerable.Range(0, orderedNodes.Count)
                .Select(_ => new List<int>())
                .ToArray();

            for (var index = 0; index < orderedNodes.Count; index++)
            {
                if (!string.IsNullOrEmpty(parentGuids[index]) && _guidToId.TryGetValue(parentGuids[index], out var parentId))
                {
                    _nodes[index].ParentId = parentId.Value;
                    children[parentId.Value].Add(index);
                }
            }

            for (var index = 0; index < _nodes.Length; index++)
            {
                _nodes[index].Children = children[index].ToArray();
                var ancestors = new List<int>();
                var current = index;
                while (current >= 0)
                {
                    ancestors.Add(current);
                    SetBit(_nodes[index].AncestorBits, current);
                    current = _nodes[current].ParentId;
                }

                _nodes[index].Ancestors = ancestors.ToArray();
            }

            for (var index = 0; index < _nodes.Length; index++)
            {
                var descendants = new List<int>();
                CollectDescendants(index, children, descendants);
                _nodes[index].Descendants = descendants.ToArray();
                _nodes[index].ChildTags = _nodes[index].Children.Select(id => _tags[id]).ToArray();
                _nodes[index].AncestorTags = _nodes[index].Ancestors
                    .Where(id => id != index)
                    .Select(id => _tags[id])
                    .ToArray();
                _nodes[index].DescendantTags = _nodes[index].Descendants.Select(id => _tags[id]).ToArray();
            }
        }

        private static void CollectDescendants(int parentId, IReadOnlyList<List<int>> children, List<int> result)
        {
            foreach (var childId in children[parentId])
            {
                result.Add(childId);
                CollectDescendants(childId, children, result);
            }
        }

        private static void SetBit(ulong[] words, int index)
        {
            words[index / 64] |= 1UL << (index % 64);
        }

        private static bool IsBitSet(ulong[] words, int index)
        {
            return index >= 0 && index / 64 < words.Length && (words[index / 64] & (1UL << (index % 64))) != 0;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fokiga.Runtime.Gameplay
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
        private static Dictionary<string, GameplayTagId> mPathToId;
        private static Dictionary<string, GameplayTagId> mGuidToId;
        private static RuntimeNode[] mNodes = Array.Empty<RuntimeNode>();
        private static GameplayTag[] mTags = Array.Empty<GameplayTag>();
        private static int mVersion;
        private static int mWordCount;
        private static GameplayTagDatabase mDatabase;
        private static bool mInitialized;
        private static bool mInitializationSucceeded;

        public static bool IsInitialized => mInitialized;

        public static int Version => mVersion;

        public static int Count => mNodes.Length;

        public static int WordCount => mWordCount;

        public static GameplayTagDatabase Database => mDatabase;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            lock (SyncRoot)
            {
                mPathToId = null;
                mGuidToId = null;
                mNodes = Array.Empty<RuntimeNode>();
                mTags = Array.Empty<GameplayTag>();
                mVersion = 0;
                mWordCount = 0;
                mDatabase = null;
                mInitialized = false;
                mInitializationSucceeded = false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (mInitialized)
            {
                return;
            }

            var database = Resources.Load<GameplayTagDatabase>("GameplayTags");
            if (database == null)
            {
                Debug.LogError("未找到 GameplayTag 数据库：Resources/GameplayTags.asset。");
                Initialize(null);
                return;
            }

            Initialize(database);
        }

        public static bool Initialize(GameplayTagDatabase database)
        {
            lock (SyncRoot)
            {
                if (mInitialized && ReferenceEquals(mDatabase, database) && Application.isPlaying)
                {
                    return mInitializationSucceeded;
                }

                if (mInitialized && Application.isPlaying)
                {
                    Debug.LogError("GameplayTag 注册表在运行时初始化后不能重建。");
                    return false;
                }

                mDatabase = database;
                mVersion = Math.Max(1, mVersion + 1);
                mPathToId = new Dictionary<string, GameplayTagId>(StringComparer.Ordinal);
                mGuidToId = new Dictionary<string, GameplayTagId>(StringComparer.Ordinal);
                mNodes = Array.Empty<RuntimeNode>();
                mTags = Array.Empty<GameplayTag>();
                mWordCount = 0;
                mInitialized = true;
                mInitializationSucceeded = false;

                if (database == null)
                {
                    return false;
                }

                var report = database.Validate();
                if (!report.IsValid)
                {
                    foreach (var error in report.Errors)
                    {
                        Debug.LogError($"GameplayTag 数据库错误：{error}", database);
                    }

                    return false;
                }

                Build(database);
                mInitializationSucceeded = true;
                return true;
            }
        }

        public static bool TryGetTag(string path, out GameplayTag tag)
        {
            tag = GameplayTag.Invalid;
            EnsureInitialized();
            if (string.IsNullOrEmpty(path) || mPathToId == null || !mPathToId.TryGetValue(path, out var id))
            {
                return false;
            }

            tag = mTags[id.Value];
            return true;
        }

        public static bool TryGetTagByGuid(string guid, out GameplayTag tag)
        {
            tag = GameplayTag.Invalid;
            EnsureInitialized();
            if (string.IsNullOrEmpty(guid) || mGuidToId == null || !mGuidToId.TryGetValue(guid, out var id))
            {
                return false;
            }

            tag = mTags[id.Value];
            return true;
        }

        public static bool IsValid(GameplayTag tag)
        {
            return tag.IsValid && tag.RegistryVersion == mVersion && tag.Id.Value < mNodes.Length;
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

            return IsBitSet(mNodes[tag.Id.Value].AncestorBits, ancestor.Id.Value);
        }

        public static GameplayTag GetParent(GameplayTag tag)
        {
            if (!IsValid(tag))
            {
                return GameplayTag.Invalid;
            }

            var parentId = mNodes[tag.Id.Value].ParentId;
            return parentId >= 0 ? mTags[parentId] : GameplayTag.Invalid;
        }

        public static IReadOnlyList<GameplayTag> GetChildren(GameplayTag tag)
        {
            if (!IsValid(tag))
            {
                return Array.Empty<GameplayTag>();
            }

            return mNodes[tag.Id.Value].ChildTags;
        }

        public static IReadOnlyList<GameplayTag> GetAncestors(GameplayTag tag)
        {
            if (!IsValid(tag))
            {
                return Array.Empty<GameplayTag>();
            }

            return mNodes[tag.Id.Value].AncestorTags;
        }

        public static IReadOnlyList<GameplayTag> GetDescendants(GameplayTag tag)
        {
            if (!IsValid(tag))
            {
                return Array.Empty<GameplayTag>();
            }

            return mNodes[tag.Id.Value].DescendantTags;
        }

        public static string GetPath(GameplayTag tag)
        {
            return IsValid(tag) ? tag.Path : string.Empty;
        }

        internal static ulong[] GetAncestorBits(GameplayTag tag)
        {
            return IsValid(tag) ? mNodes[tag.Id.Value].AncestorBits : Array.Empty<ulong>();
        }

        internal static int GetRuntimeIdCount(GameplayTag tag)
        {
            return IsValid(tag) ? mNodes.Length : 0;
        }

        internal static int GetVersion(GameplayTag tag)
        {
            return tag.RegistryVersion;
        }

        private static void EnsureInitialized()
        {
            if (!mInitialized)
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

            mWordCount = Math.Max(1, (orderedNodes.Count + 63) / 64);
            mNodes = new RuntimeNode[orderedNodes.Count];
            mTags = new GameplayTag[orderedNodes.Count];
            var parentGuids = new string[orderedNodes.Count];

            for (var index = 0; index < orderedNodes.Count; index++)
            {
                var source = orderedNodes[index];
                var node = new RuntimeNode
                {
                    Guid = source.Guid,
                    Path = pathByGuid[source.Guid],
                    AncestorBits = new ulong[mWordCount]
                };
                mNodes[index] = node;
                mTags[index] = new GameplayTag(new GameplayTagId(index), mVersion, node.Path, node.Guid);
                parentGuids[index] = source.ParentGuid;
                mPathToId[node.Path] = new GameplayTagId(index);
                mGuidToId[node.Guid] = new GameplayTagId(index);
            }

            var children = Enumerable.Range(0, orderedNodes.Count)
            .Select(_ => new List<int>())
            .ToArray();

            for (var index = 0; index < orderedNodes.Count; index++)
            {
                if (!string.IsNullOrEmpty(parentGuids[index]) && mGuidToId.TryGetValue(parentGuids[index], out var parentId))
                {
                    mNodes[index].ParentId = parentId.Value;
                    children[parentId.Value].Add(index);
                }
            }

            for (var index = 0; index < mNodes.Length; index++)
            {
                mNodes[index].Children = children[index].ToArray();
                var ancestors = new List<int>();
                var current = index;
                while (current >= 0)
                {
                    ancestors.Add(current);
                    SetBit(mNodes[index].AncestorBits, current);
                    current = mNodes[current].ParentId;
                }

                mNodes[index].Ancestors = ancestors.ToArray();
            }

            for (var index = 0; index < mNodes.Length; index++)
            {
                var descendants = new List<int>();
                CollectDescendants(index, children, descendants);
                mNodes[index].Descendants = descendants.ToArray();
                mNodes[index].ChildTags = mNodes[index].Children.Select(id => mTags[id]).ToArray();
                mNodes[index].AncestorTags = mNodes[index].Ancestors
                .Where(id => id != index)
                .Select(id => mTags[id])
                .ToArray();
                mNodes[index].DescendantTags = mNodes[index].Descendants.Select(id => mTags[id]).ToArray();
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

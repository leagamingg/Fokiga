using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fokiga.Runtime.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Fokiga/GameplayTag/GameplayTag 组件")]
    public sealed class GameplayTagComponent : MonoBehaviour
    {
        [SerializeField]
        private List<string> mTagGuids = new List<string>();

        private GameplayTagContainer mContainer;
        private readonly HashSet<string> mReportedInvalidGuids = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyList<string> SerializedTagGuids => mTagGuids;

        public GameplayTagContainer Container
        {
            get
            {
                EnsureContainer();
                return mContainer;
            }
        }

        private void Awake()
        {
            EnsureContainer();
        }

        private void OnEnable()
        {
            EnsureContainer();
        }

        public bool AddTag(GameplayTag tag)
        {
            EnsureContainer();
            if (!GameplayTagRegistry.IsValid(tag) || !mContainer.Add(tag))
            {
                return false;
            }

            if (!mTagGuids.Contains(tag.Guid))
            {
                mTagGuids.Add(tag.Guid);
            }

            return true;
        }

        public bool AddTag(string path)
        {
            return GameplayTagRegistry.TryGetTag(path, out var tag) && AddTag(tag);
        }

        public bool RemoveTag(GameplayTag tag)
        {
            EnsureContainer();
            if (!GameplayTagRegistry.IsValid(tag) || !mContainer.Remove(tag))
            {
                return false;
            }

            mTagGuids.Remove(tag.Guid);
            return true;
        }

        public bool RemoveTag(string path)
        {
            return GameplayTagRegistry.TryGetTag(path, out var tag) && RemoveTag(tag);
        }

        public bool HasTagExact(GameplayTag tag) => Container.HasTagExact(tag);

        public bool HasTag(GameplayTag tag) => Container.HasTag(tag);

        public bool HasAny(IReadOnlyList<GameplayTag> tags) => Container.HasAny(tags);

        public bool HasAll(IReadOnlyList<GameplayTag> tags) => Container.HasAll(tags);

        public bool HasTagExact(string path)
        {
            return GameplayTagRegistry.TryGetTag(path, out var tag) && HasTagExact(tag);
        }

        public bool HasTag(string path)
        {
            return GameplayTagRegistry.TryGetTag(path, out var tag) && HasTag(tag);
        }

        public void ClearTags()
        {
            EnsureContainer();
            mContainer.Clear();
            mTagGuids.Clear();
        }

        internal void RebuildContainer()
        {
            mContainer = new GameplayTagContainer();
            if (!GameplayTagRegistry.IsInitialized)
            {
                return;
            }

            foreach (var guid in mTagGuids)
            {
                if (GameplayTagRegistry.TryGetTagByGuid(guid, out var tag))
                {
                    mContainer.Add(tag);
                }
                else if (!string.IsNullOrEmpty(guid) && mReportedInvalidGuids.Add(guid))
                {
                    Debug.LogError($"GameplayTagComponent“{name}”引用了未知标签 GUID“{guid}”。", this);
                }
            }
        }

        private void EnsureContainer()
        {
            if (mContainer == null || (GameplayTagRegistry.IsInitialized && !mContainer.IsCurrent))
            {
                RebuildContainer();
            }
        }

    }
}

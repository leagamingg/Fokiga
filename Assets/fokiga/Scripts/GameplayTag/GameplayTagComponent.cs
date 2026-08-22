using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fokiga.Runtime.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Fokiga/GameplayTag/GameplayTag 组件")]
    public sealed class GameplayTagComponent : MonoBehaviour
    {
        [SerializeField] private List<string> _tagGuids = new List<string>();

        private GameplayTagContainer _container;
        private readonly HashSet<string> _reportedInvalidGuids = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyList<string> SerializedTagGuids => _tagGuids;

        public GameplayTagContainer Container
        {
            get
            {
                EnsureContainer();
                return _container;
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
            if (!GameplayTagRegistry.IsValid(tag) || !_container.Add(tag))
            {
                return false;
            }

            if (!_tagGuids.Contains(tag.Guid))
            {
                _tagGuids.Add(tag.Guid);
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
            if (!GameplayTagRegistry.IsValid(tag) || !_container.Remove(tag))
            {
                return false;
            }

            _tagGuids.Remove(tag.Guid);
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
            _container.Clear();
            _tagGuids.Clear();
        }

        internal void RebuildContainer()
        {
            _container = new GameplayTagContainer();
            if (!GameplayTagRegistry.IsInitialized)
            {
                return;
            }

            foreach (var guid in _tagGuids)
            {
                if (GameplayTagRegistry.TryGetTagByGuid(guid, out var tag))
                {
                    _container.Add(tag);
                }
                else if (!string.IsNullOrEmpty(guid) && _reportedInvalidGuids.Add(guid))
                {
                    Debug.LogError($"GameplayTagComponent“{name}”引用了未知标签 GUID“{guid}”。", this);
                }
            }
        }

        private void EnsureContainer()
        {
            if (_container == null || (GameplayTagRegistry.IsInitialized && !_container.IsCurrent))
            {
                RebuildContainer();
            }
        }

    }
}

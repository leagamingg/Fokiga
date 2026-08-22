using System;
using System.Collections.Generic;

namespace Fokiga.Runtime.Gameplay
{
    public sealed class GameplayTagContainer
    {
        private ulong[] _explicitBits = Array.Empty<ulong>();
        private int[] _ancestorCounts = Array.Empty<int>();
        private int _registryVersion;

        public int Count { get; private set; }

        internal bool IsCurrent => _registryVersion == GameplayTagRegistry.Version && _ancestorCounts.Length == GameplayTagRegistry.Count;

        public bool Add(GameplayTag tag)
        {
            if (!GameplayTagRegistry.IsValid(tag))
            {
                return false;
            }

            EnsureRegistryState();
            var id = tag.Id.Value;
            if (IsBitSet(_explicitBits, id))
            {
                return false;
            }

            SetBit(_explicitBits, id);
            Count++;
            IncrementAncestors(GameplayTagRegistry.GetAncestorBits(tag));
            return true;
        }

        public bool Add(string path)
        {
            return GameplayTagRegistry.TryGetTag(path, out var tag) && Add(tag);
        }

        public bool Remove(GameplayTag tag)
        {
            if (!GameplayTagRegistry.IsValid(tag))
            {
                return false;
            }

            EnsureRegistryState();
            var id = tag.Id.Value;
            if (!IsBitSet(_explicitBits, id))
            {
                return false;
            }

            ClearBit(_explicitBits, id);
            Count--;
            DecrementAncestors(GameplayTagRegistry.GetAncestorBits(tag));
            return true;
        }

        public bool Remove(string path)
        {
            return GameplayTagRegistry.TryGetTag(path, out var tag) && Remove(tag);
        }

        public bool HasTagExact(GameplayTag tag)
        {
            return GameplayTagRegistry.IsValid(tag) && _registryVersion == GameplayTagRegistry.Version && IsBitSet(_explicitBits, tag.Id.Value);
        }

        public bool HasTag(GameplayTag tag)
        {
            return GameplayTagRegistry.IsValid(tag) && _registryVersion == GameplayTagRegistry.Version && tag.Id.Value < _ancestorCounts.Length && _ancestorCounts[tag.Id.Value] > 0;
        }

        public bool HasAny(IReadOnlyList<GameplayTag> tags)
        {
            if (tags == null)
            {
                return false;
            }

            for (var index = 0; index < tags.Count; index++)
            {
                if (HasTag(tags[index]))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasAll(IReadOnlyList<GameplayTag> tags)
        {
            if (tags == null)
            {
                return false;
            }

            for (var index = 0; index < tags.Count; index++)
            {
                if (!HasTag(tags[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public void Clear()
        {
            EnsureRegistryState();
            Array.Clear(_explicitBits, 0, _explicitBits.Length);
            Array.Clear(_ancestorCounts, 0, _ancestorCounts.Length);
            Count = 0;
        }

        private void EnsureRegistryState()
        {
            if (_registryVersion == GameplayTagRegistry.Version && _ancestorCounts.Length == GameplayTagRegistry.Count)
            {
                return;
            }

            _registryVersion = GameplayTagRegistry.Version;
            _explicitBits = new ulong[Math.Max(1, GameplayTagRegistry.WordCount)];
            _ancestorCounts = new int[GameplayTagRegistry.Count];
            Count = 0;
        }

        private void IncrementAncestors(ulong[] ancestorBits)
        {
            for (var wordIndex = 0; wordIndex < ancestorBits.Length; wordIndex++)
            {
                var word = ancestorBits[wordIndex];
                while (word != 0)
                {
                    var bit = TrailingZeroCount(word);
                    _ancestorCounts[wordIndex * 64 + bit]++;
                    word &= word - 1;
                }
            }
        }

        private void DecrementAncestors(ulong[] ancestorBits)
        {
            for (var wordIndex = 0; wordIndex < ancestorBits.Length; wordIndex++)
            {
                var word = ancestorBits[wordIndex];
                while (word != 0)
                {
                    var bit = TrailingZeroCount(word);
                    _ancestorCounts[wordIndex * 64 + bit]--;
                    word &= word - 1;
                }
            }
        }

        private static int TrailingZeroCount(ulong value)
        {
            var count = 0;
            while ((value & 1UL) == 0)
            {
                value >>= 1;
                count++;
            }

            return count;
        }

        private static bool IsBitSet(ulong[] words, int index)
        {
            return index >= 0 && index / 64 < words.Length && (words[index / 64] & (1UL << (index % 64))) != 0;
        }

        private static void SetBit(ulong[] words, int index)
        {
            words[index / 64] |= 1UL << (index % 64);
        }

        private static void ClearBit(ulong[] words, int index)
        {
            words[index / 64] &= ~(1UL << (index % 64));
        }
    }
}

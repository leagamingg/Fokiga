using System;

namespace Fokiga.Runtime.Gameplay
{
    [Serializable]
    public readonly struct GameplayTagId : IEquatable<GameplayTagId>
    {
        public static readonly GameplayTagId Invalid = new GameplayTagId(-1);

        public int Value { get; }

        internal GameplayTagId(int value)
        {
            Value = value;
        }

        public bool IsValid => Value >= 0;

        public bool Equals(GameplayTagId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is GameplayTagId other && Equals(other);

        public override int GetHashCode() => Value;

        public static bool operator ==(GameplayTagId left, GameplayTagId right) => left.Equals(right);

        public static bool operator !=(GameplayTagId left, GameplayTagId right) => !left.Equals(right);

        public override string ToString() => IsValid ? Value.ToString() : "Invalid";
    }

    [Serializable]
    public readonly struct GameplayTag : IEquatable<GameplayTag>
    {
        public static readonly GameplayTag Invalid = new GameplayTag(GameplayTagId.Invalid, 0, string.Empty, string.Empty);

        private readonly GameplayTagId mId;
        private readonly int mRegistryVersion;
        private readonly string mPath;
        private readonly string mGuid;

        internal GameplayTag(GameplayTagId id, int registryVersion, string path, string guid)
        {
            mId = id;
            mRegistryVersion = registryVersion;
            mPath = path ?? string.Empty;
            mGuid = guid ?? string.Empty;
        }

        public GameplayTagId Id => mId;

        public string Path => mPath;

        public string Guid => mGuid;

        public bool IsValid => mId.IsValid && mRegistryVersion > 0;

        internal int RegistryVersion => mRegistryVersion;

        public bool Equals(GameplayTag other)
        {
            return mId == other.mId && mRegistryVersion == other.mRegistryVersion;
        }

        public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(mId, mRegistryVersion);

        public static bool operator ==(GameplayTag left, GameplayTag right) => left.Equals(right);

        public static bool operator !=(GameplayTag left, GameplayTag right) => !left.Equals(right);

        public override string ToString() => IsValid ? mPath : "<无效 GameplayTag>";
    }
}

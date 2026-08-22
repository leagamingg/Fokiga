using System;

namespace Fokiga.GameplayTags
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

        private readonly GameplayTagId _id;
        private readonly int _registryVersion;
        private readonly string _path;
        private readonly string _guid;

        internal GameplayTag(GameplayTagId id, int registryVersion, string path, string guid)
        {
            _id = id;
            _registryVersion = registryVersion;
            _path = path ?? string.Empty;
            _guid = guid ?? string.Empty;
        }

        public GameplayTagId Id => _id;

        public string Path => _path;

        public string Guid => _guid;

        public bool IsValid => _id.IsValid && _registryVersion > 0;

        internal int RegistryVersion => _registryVersion;

        public bool Equals(GameplayTag other)
        {
            return _id == other._id && _registryVersion == other._registryVersion;
        }

        public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(_id, _registryVersion);

        public static bool operator ==(GameplayTag left, GameplayTag right) => left.Equals(right);

        public static bool operator !=(GameplayTag left, GameplayTag right) => !left.Equals(right);

        public override string ToString() => IsValid ? _path : "<Invalid GameplayTag>";
    }
}

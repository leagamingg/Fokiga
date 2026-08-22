using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fokiga.GameplayTags
{
    [Serializable]
    public sealed class GameplayTagNodeData
    {
        [SerializeField] private string _guid;
        [SerializeField] private string _name;
        [SerializeField] private string _parentGuid;

        public string Guid => _guid;

        public string Name => _name;

        public string ParentGuid => _parentGuid;

        public GameplayTagNodeData(string guid, string name, string parentGuid)
        {
            _guid = guid;
            _name = name;
            _parentGuid = parentGuid ?? string.Empty;
        }

        public GameplayTagNodeData()
        {
            _guid = string.Empty;
            _name = string.Empty;
            _parentGuid = string.Empty;
        }

        public void SetName(string name)
        {
            _name = name ?? string.Empty;
        }

        public void SetParentGuid(string parentGuid)
        {
            _parentGuid = parentGuid ?? string.Empty;
        }

        public void SetGuid(string guid)
        {
            _guid = guid ?? string.Empty;
        }
    }

    [Serializable]
    public sealed class GameplayTagValidationReport
    {
        public readonly List<string> Errors = new List<string>();

        public bool IsValid => Errors.Count == 0;

        public void Add(string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                Errors.Add(error);
            }
        }
    }

    [CreateAssetMenu(fileName = "GameplayTags", menuName = "Fokiga/Gameplay Tags/Database")]
    public sealed class GameplayTagDatabase : ScriptableObject
    {
        [SerializeField] private List<GameplayTagNodeData> _nodes = new List<GameplayTagNodeData>();

        public IReadOnlyList<GameplayTagNodeData> Nodes => _nodes;

        public GameplayTagNodeData AddRoot(string name)
        {
            return AddNode(name, string.Empty);
        }

        public GameplayTagNodeData AddChild(string name, string parentGuid)
        {
            return AddNode(name, parentGuid);
        }

        public bool TryGetNode(string guid, out GameplayTagNodeData node)
        {
            node = _nodes.FirstOrDefault(item => item != null && item.Guid == guid);
            return node != null;
        }

        public bool Rename(string guid, string name)
        {
            if (!TryGetNode(guid, out var node))
            {
                return false;
            }

            node.SetName(name);
            return true;
        }

        public bool Reparent(string guid, string parentGuid)
        {
            if (!TryGetNode(guid, out var node))
            {
                return false;
            }

            node.SetParentGuid(parentGuid);
            return true;
        }

        public bool RemoveSubtree(string guid)
        {
            if (!TryGetNode(guid, out _))
            {
                return false;
            }

            var toRemove = new HashSet<string> { guid };
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var node in _nodes)
                {
                    if (node != null && !toRemove.Contains(node.Guid) && toRemove.Contains(node.ParentGuid))
                    {
                        toRemove.Add(node.Guid);
                        changed = true;
                    }
                }
            }

            _nodes.RemoveAll(node => node == null || toRemove.Contains(node.Guid));
            return true;
        }

        public GameplayTagValidationReport Validate()
        {
            var report = new GameplayTagValidationReport();
            var byGuid = new Dictionary<string, GameplayTagNodeData>(StringComparer.Ordinal);

            foreach (var node in _nodes)
            {
                if (node == null)
                {
                    report.Add("Database contains a null node.");
                    continue;
                }

                if (string.IsNullOrEmpty(node.Guid))
                {
                    report.Add("A tag node has an empty GUID.");
                }
                else if (!byGuid.TryAdd(node.Guid, node))
                {
                    report.Add($"Duplicate tag GUID: {node.Guid}");
                }

                if (!IsValidSegmentName(node.Name))
                {
                    report.Add($"Invalid tag node name: '{node.Name}'.");
                }

                if (node.Guid == node.ParentGuid && !string.IsNullOrEmpty(node.Guid))
                {
                    report.Add($"Tag '{node.Guid}' cannot be its own parent.");
                }
            }

            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in _nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Guid))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(node.ParentGuid) && !byGuid.ContainsKey(node.ParentGuid))
                {
                    report.Add($"Tag '{node.Guid}' references missing parent '{node.ParentGuid}'.");
                }

                if (TryBuildPath(node, byGuid, out var path, out var error))
                {
                    if (!paths.Add(path))
                    {
                        report.Add($"Duplicate tag path: {path}");
                    }
                }
                else
                {
                    report.Add(error);
                }
            }

            return report;
        }

        public bool TryGetPath(string guid, out string path)
        {
            path = string.Empty;
            if (!TryGetNode(guid, out var node))
            {
                return false;
            }

            var byGuid = _nodes
                .Where(item => item != null && !string.IsNullOrEmpty(item.Guid))
                .GroupBy(item => item.Guid, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            return TryBuildPath(node, byGuid, out path, out _);
        }

        public static bool IsValidSegmentName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            foreach (var character in name)
            {
                if (character == '.' || character == '/' || character == '\\' || char.IsWhiteSpace(character) || char.IsControl(character))
                {
                    return false;
                }
            }

            return true;
        }

        private GameplayTagNodeData AddNode(string name, string parentGuid)
        {
            var node = new GameplayTagNodeData(System.Guid.NewGuid().ToString("N"), name, parentGuid);
            _nodes.Add(node);
            return node;
        }

        private static bool TryBuildPath(
            GameplayTagNodeData start,
            IReadOnlyDictionary<string, GameplayTagNodeData> byGuid,
            out string path,
            out string error)
        {
            var names = new List<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var current = start;

            while (current != null)
            {
                if (!visited.Add(current.Guid))
                {
                    path = string.Empty;
                    error = $"Cycle detected while resolving tag '{start.Guid}'.";
                    return false;
                }

                names.Add(current.Name);
                if (string.IsNullOrEmpty(current.ParentGuid))
                {
                    names.Reverse();
                    path = string.Join(".", names);
                    error = string.Empty;
                    return true;
                }

                var parentGuid = current.ParentGuid;
                if (!byGuid.TryGetValue(parentGuid, out current))
                {
                    path = string.Empty;
                    error = $"Tag '{start.Guid}' has missing parent '{parentGuid}'.";
                    return false;
                }
            }

            path = string.Empty;
            error = $"Unable to resolve tag '{start.Guid}'.";
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fokiga.Runtime.Gameplay
{
    [Serializable]
    public sealed class GameplayTagNodeData
    {
        [SerializeField]
        private string mGuid;
        [SerializeField]
        private string mName;
        [SerializeField]
        private string mParentGuid;

        public string Guid => mGuid;

        public string Name => mName;

        public string ParentGuid => mParentGuid;

        public GameplayTagNodeData(string guid, string name, string parentGuid)
        {
            mGuid = guid;
            mName = name;
            mParentGuid = parentGuid ?? string.Empty;
        }

        public GameplayTagNodeData()
        {
            mGuid = string.Empty;
            mName = string.Empty;
            mParentGuid = string.Empty;
        }

        public void SetName(string name)
        {
            mName = name ?? string.Empty;
        }

        public void SetParentGuid(string parentGuid)
        {
            mParentGuid = parentGuid ?? string.Empty;
        }

        public void SetGuid(string guid)
        {
            mGuid = guid ?? string.Empty;
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
        [SerializeField]
        private List<GameplayTagNodeData> mNodes = new List<GameplayTagNodeData>();

        public IReadOnlyList<GameplayTagNodeData> Nodes => mNodes;

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
            node = mNodes.FirstOrDefault(item => item != null && item.Guid == guid);
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
                foreach (var node in mNodes)
                {
                    if (node != null && !toRemove.Contains(node.Guid) && toRemove.Contains(node.ParentGuid))
                    {
                        toRemove.Add(node.Guid);
                        changed = true;
                    }
                }
            }

            mNodes.RemoveAll(node => node == null || toRemove.Contains(node.Guid));
            return true;
        }

        public GameplayTagValidationReport Validate()
        {
            var report = new GameplayTagValidationReport();
            var byGuid = new Dictionary<string, GameplayTagNodeData>(StringComparer.Ordinal);

            foreach (var node in mNodes)
            {
                if (node == null)
                {
                    report.Add("数据库包含空标签节点。");
                    continue;
                }

                if (string.IsNullOrEmpty(node.Guid))
                {
                    report.Add("标签节点存在空 GUID。");
                }
                else
                {
                    if (!Guid.TryParseExact(node.Guid, "N", out _))
                    {
                        report.Add($"标签 GUID“{node.Guid}”不是有效的 32 位十六进制 GUID。");
                    }

                    if (!byGuid.TryAdd(node.Guid, node))
                    {
                        report.Add($"重复的标签 GUID：{node.Guid}");
                    }
                }

                if (!IsValidSegmentName(node.Name))
                {
                    report.Add($"标签节点名称无效：“{node.Name}”。");
                }

                if (node.Guid == node.ParentGuid && !string.IsNullOrEmpty(node.Guid))
                {
                    report.Add($"标签“{node.Guid}”不能将自身作为父节点。");
                }
            }

            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in mNodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Guid))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(node.ParentGuid) && !byGuid.ContainsKey(node.ParentGuid))
                {
                    report.Add($"标签“{node.Guid}”引用了不存在的父节点“{node.ParentGuid}”。");
                }

                if (TryBuildPath(node, byGuid, out var path, out var error))
                {
                    if (!paths.Add(path))
                    {
                        report.Add($"重复的标签路径：{path}");
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

            var byGuid = mNodes
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
            mNodes.Add(node);
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
                    error = $"解析标签“{start.Guid}”时发现循环关系。";
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
                    error = $"标签“{start.Guid}”缺少父节点“{parentGuid}”。";
                    return false;
                }
            }

            path = string.Empty;
            error = $"无法解析标签“{start.Guid}”。";
            return false;
        }
    }
}

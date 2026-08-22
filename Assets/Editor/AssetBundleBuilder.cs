using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Fokiga.Editor
{
    public class AssetBundleBuilder : EditorWindow
    {
        // 配置选项
        public int maxAssetBundleSize = 10 * 1024 * 1024; // 默认10MB
        public string assetBundleOutputPath = "AssetBundles";
        public bool autoMarkAssetBundles = true;
        public string resourcesFolderPath = "Assets/fokiga/Resources";
        public bool splitLargeBundles = true;

        // 目录到AssetBundle名称的映射
        private Dictionary<string, string> directoryToBundleMap = new Dictionary<string, string>();

        // 资源大小缓存
        private Dictionary<string, long> assetSizeCache = new Dictionary<string, long>();

        [MenuItem("Tools/AssetBundle Builder")]
        public static void ShowWindow()
        {
            GetWindow<AssetBundleBuilder>("AssetBundle Builder");
        }

        private void OnGUI()
        {
            GUILayout.Label("AssetBundle 构建配置", EditorStyles.boldLabel);

            // 最大AB包大小设置
            maxAssetBundleSize = EditorGUILayout.IntField("最大AB包大小 (字节)", maxAssetBundleSize);
            EditorGUILayout.LabelField("最大AB包大小 (MB)", (maxAssetBundleSize / (1024f * 1024f)).ToString("0.00"));

            // 输出路径设置
            assetBundleOutputPath = EditorGUILayout.TextField("AssetBundle 输出路径", assetBundleOutputPath);

            // 资源文件夹路径
            resourcesFolderPath = EditorGUILayout.TextField("资源文件夹路径", resourcesFolderPath);

            // 自动标记选项
            autoMarkAssetBundles = EditorGUILayout.Toggle("自动标记AssetBundles", autoMarkAssetBundles);

            // 分割大型AB包选项
            splitLargeBundles = EditorGUILayout.Toggle("分割大型AB包", splitLargeBundles);

            GUILayout.Space(20);

            // 操作按钮
            if (GUILayout.Button("自动标记AssetBundles"))
            {
                AutoMarkAssetBundles();
            }

            if (GUILayout.Button("构建AssetBundles"))
            {
                BuildAssetBundles();
            }

            if (GUILayout.Button("清除AssetBundle标记"))
            {
                ClearAssetBundleMarks();
            }
        }

        /// <summary>
        /// 自动标记AssetBundles
        /// </summary>
        private void AutoMarkAssetBundles()
        {
            if (!Directory.Exists(resourcesFolderPath))
            {
                Debug.LogError($"资源文件夹不存在: {resourcesFolderPath}");
                return;
            }

            directoryToBundleMap.Clear();
            assetSizeCache.Clear();

            // 遍历资源文件夹
            TraverseDirectory(resourcesFolderPath, "");

            // 处理大型AB包
            if (splitLargeBundles)
            {
                SplitLargeBundles();
            }

            Debug.Log("AssetBundle 自动标记完成");
        }

        /// <summary>
        /// 遍历目录并标记AssetBundles
        /// </summary>
        private void TraverseDirectory(string directoryPath, string relativePath)
        {
            // 获取目录下的所有文件
            string[] files = Directory.GetFiles(directoryPath);

            // 计算当前目录的资源大小
            long directorySize = 0;
            foreach (string file in files)
            {
                if (IsAssetFile(file))
                {
                    long fileSize = GetAssetSize(file);
                    directorySize += fileSize;
                    assetSizeCache[file] = fileSize;
                }
            }

            // 生成AssetBundle名称
            string bundleName = relativePath.Replace("/", "_").ToLower();
            if (string.IsNullOrEmpty(bundleName))
            {
                bundleName = "main";
            }

            // 标记当前目录下的资源
            foreach (string file in files)
            {
                if (IsAssetFile(file))
                {
                    MarkAssetBundle(file, bundleName);
                }
            }

            // 递归处理子目录
            string[] subdirectories = Directory.GetDirectories(directoryPath);
            foreach (string subdirectory in subdirectories)
            {
                string subdirectoryName = Path.GetFileName(subdirectory);
                string newRelativePath = string.IsNullOrEmpty(relativePath) ? subdirectoryName : $"{relativePath}/{subdirectoryName}";
                TraverseDirectory(subdirectory, newRelativePath);
            }
        }

        /// <summary>
        /// 检查文件是否为Unity资产文件
        /// </summary>
        private bool IsAssetFile(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return extension == ".prefab" || extension == ".mat" || extension == ".fbx" || extension == ".png" || extension == ".jpg" || extension == ".mp3" || extension == ".wav";
        }

        /// <summary>
        /// 获取资产文件的大小
        /// </summary>
        private long GetAssetSize(string filePath)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                return fileInfo.Length;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 标记资产的AssetBundle
        /// </summary>
        private void MarkAssetBundle(string filePath, string bundleName)
        {
            AssetImporter importer = AssetImporter.GetAtPath(filePath);
            if (importer != null)
            {
                importer.assetBundleName = bundleName;
                directoryToBundleMap[Path.GetDirectoryName(filePath)] = bundleName;
            }
        }

        /// <summary>
        /// 分割大型AB包
        /// </summary>
        private void SplitLargeBundles()
        {
            // 计算每个AB包的大小
            Dictionary<string, long> bundleSizes = new Dictionary<string, long>();

            foreach (var kvp in assetSizeCache)
            {
                string assetPath = kvp.Key;
                long assetSize = kvp.Value;

                string directoryPath = Path.GetDirectoryName(assetPath);
                if (directoryToBundleMap.TryGetValue(directoryPath, out string bundleName))
                {
                    if (!bundleSizes.ContainsKey(bundleName))
                    {
                        bundleSizes[bundleName] = 0;
                    }
                    bundleSizes[bundleName] += assetSize;
                }
            }

            // 分割大型AB包
            foreach (var kvp in bundleSizes)
            {
                string bundleName = kvp.Key;
                long bundleSize = kvp.Value;

                if (bundleSize > maxAssetBundleSize)
                {
                    // 找到属于该AB包的所有资源
                    List<string> bundleAssets = new List<string>();
                    foreach (var assetKvp in assetSizeCache)
                    {
                        string assetPath = assetKvp.Key;
                        string directoryPath = Path.GetDirectoryName(assetPath);

                        if (directoryToBundleMap.TryGetValue(directoryPath, out string assetBundleName) && assetBundleName == bundleName)
                        {
                            bundleAssets.Add(assetPath);
                        }
                    }

                    // 分割资源到多个AB包
                    SplitBundle(bundleName, bundleAssets);
                }
            }
        }

        /// <summary>
        /// 分割单个AB包
        /// </summary>
        private void SplitBundle(string originalBundleName, List<string> assets)
        {
            int bundleIndex = 1;
            long currentSize = 0;
            List<string> currentBundleAssets = new List<string>();

            foreach (string assetPath in assets)
            {
                long assetSize = assetSizeCache[assetPath];

                // 如果添加当前资源会超过大小限制，则创建新的AB包
                if (currentSize + assetSize > maxAssetBundleSize && currentBundleAssets.Count > 0)
                {
                    // 标记当前AB包
                    string newBundleName = $"{originalBundleName}_{bundleIndex}";
                    foreach (string bundleAsset in currentBundleAssets)
                    {
                        MarkAssetBundle(bundleAsset, newBundleName);
                    }

                    // 重置计数器
                    bundleIndex++;
                    currentSize = 0;
                    currentBundleAssets.Clear();
                }

                // 添加资源到当前AB包
                currentBundleAssets.Add(assetPath);
                currentSize += assetSize;
            }

            // 处理最后一个AB包
            if (currentBundleAssets.Count > 0)
            {
                string newBundleName = bundleIndex > 1 ? $"{originalBundleName}_{bundleIndex}" : originalBundleName;
                foreach (string bundleAsset in currentBundleAssets)
                {
                    MarkAssetBundle(bundleAsset, newBundleName);
                }
            }
        }

        /// <summary>
        /// 构建AssetBundles
        /// </summary>
        private void BuildAssetBundles()
        {
            string outputPath = Path.Combine(Application.streamingAssetsPath, assetBundleOutputPath);

            // 确保输出目录存在
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // 构建AssetBundles
            BuildPipeline.BuildAssetBundles(outputPath, BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget);

            Debug.Log($"AssetBundles 构建完成，输出路径: {outputPath}");
        }

        /// <summary>
        /// 清除所有AssetBundle标记
        /// </summary>
        private void ClearAssetBundleMarks()
        {
            string[] assetPaths = AssetDatabase.GetAllAssetPaths();

            foreach (string assetPath in assetPaths)
            {
                AssetImporter importer = AssetImporter.GetAtPath(assetPath);
                if (importer != null && !string.IsNullOrEmpty(importer.assetBundleName))
                {
                    importer.assetBundleName = "";
                }
            }

            AssetDatabase.Refresh();
            Debug.Log("AssetBundle 标记已清除");
        }
    }
}

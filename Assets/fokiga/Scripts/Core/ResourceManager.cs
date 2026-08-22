using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using UnityEngine;
using System.IO;

namespace Fokiga.Runtime.Core
{
    // 为Unity的异步操作添加await支持
    public static class AsyncOperationExtensions
    {
        public static TaskAwaiter<object> GetAwaiter(this UnityEngine.AsyncOperation operation)
        {
            var tcs = new TaskCompletionSource<object>();
            operation.completed += _ => tcs.SetResult(null);
            return tcs.Task.GetAwaiter();
        }
    }

    /// <summary>
    /// 资源管理接口，定义资源管理的核心功能
    /// </summary>
    public interface IResourceManager
    {
        // 同步加载资源
        T Load<T>(string path) where T : UnityEngine.Object;

        // 异步加载资源
        Task<T> LoadAsync<T>(string path) where T : UnityEngine.Object;

        // 归还资源
        void Return<T>(T resource) where T : UnityEngine.Object;

        // 直接卸载资源
        void Unload<T>(T resource) where T : UnityEngine.Object;

        // 清空所有资源
        void ClearAll();

        // 获取资源使用情况
        int GetResourceCount();
    }

    /// <summary>
    /// 模板池接口，用于管理可复用的对象模板
    /// </summary>
    public interface ITemplatePool
    {
        // 获取模板实例
        T Get<T>(string templateName) where T : UnityEngine.Object;

        // 归还模板实例
        void ReturnTemplate<T>(T instance) where T : UnityEngine.Object;

        // 预热模板
        Task Preload<T>(string templateName, int count = 1) where T : UnityEngine.Object;

        // 清空模板池
        void Clear();
    }

    /// <summary>
    /// 资源加载策略
    /// </summary>
    public enum ResourceLoadStrategy
    {
        Auto,           // 自动选择策略
        Resources,      // 使用 Resources 文件夹
        AssetBundle     // 使用 AssetBundle
    }

    /// <summary>
    /// 资源管理器，负责资源的加载、归还和管理
    /// </summary>
    public class ResourceManager : MonoBehaviour, IResourceManager, ITemplatePool
    {
        // 单例实例
        public static ResourceManager Instance { get; private set; }

        // 线程安全锁
        private readonly object _resourceLock = new object();
        private readonly object _poolLock = new object();

        // 资源记录
        private readonly Dictionary<string, ResourceInfo> _loadedResources = new Dictionary<string, ResourceInfo>();

        // 模板池
        private readonly Dictionary<string, Queue<UnityEngine.Object>> _templatePools = new Dictionary<string, Queue<UnityEngine.Object>>();
        private readonly Dictionary<UnityEngine.Object, string> _instanceToTemplate = new Dictionary<UnityEngine.Object, string>();

        // AssetBundle 缓存
        private readonly Dictionary<string, AssetBundle> _loadedAssetBundles = new Dictionary<string, AssetBundle>();

        // 默认加载策略
        public ResourceLoadStrategy DefaultLoadStrategy = ResourceLoadStrategy.Auto;

        /// <summary>
        /// 资源信息类，记录资源的使用情况
        /// </summary>
        private class ResourceInfo
        {
            public UnityEngine.Object Resource { get; set; }
            public int ReferenceCount { get; set; }
            public bool IsAsync { get; set; }
            public ResourceLoadStrategy Strategy { get; set; }
            public string AssetBundleName { get; set; }
        }

        /// <summary>
        /// 确保单例唯一
        /// </summary>
        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            OnAwake();
        }

        /// <summary>
        /// 自定义初始化方法
        /// </summary>
        protected virtual void OnAwake()
        {
            // 子类可以重写此方法进行初始化
        }

        /// <summary>
        /// 同步加载资源
        /// </summary>
        public virtual T Load<T>(string path) where T : UnityEngine.Object
        {
            return Load<T>(path, DefaultLoadStrategy);
        }

        /// <summary>
        /// 同步加载资源
        /// </summary>
        public virtual T Load<T>(string path, ResourceLoadStrategy strategy) where T : UnityEngine.Object
        {
            lock (_resourceLock)
            {
                string key = $"{typeof(T).Name}:{path}";

                // 检查资源是否已加载
                if (_loadedResources.TryGetValue(key, out var info))
                {
                    info.ReferenceCount++;
                    return info.Resource as T;
                }

                // 根据策略加载资源
                T resource = null;
                ResourceLoadStrategy actualStrategy = strategy;

                if (strategy == ResourceLoadStrategy.Auto)
                {
                    // 自动选择策略：先尝试 AssetBundle，再尝试 Resources
                    resource = LoadWithAssetBundle<T>(path);
                    if (resource != null)
                    {
                        actualStrategy = ResourceLoadStrategy.AssetBundle;
                    }
                    else
                    {
                        resource = LoadWithResources<T>(path);
                        actualStrategy = ResourceLoadStrategy.Resources;
                    }
                }
                else if (strategy == ResourceLoadStrategy.Resources)
                {
                    resource = LoadWithResources<T>(path);
                }
                else if (strategy == ResourceLoadStrategy.AssetBundle)
                {
                    resource = LoadWithAssetBundle<T>(path);
                }

                if (resource == null)
                {
                    Debug.LogError($"资源加载失败: {path}, 策略: {actualStrategy}");
                    return null;
                }

                // 记录资源信息
                _loadedResources[key] = new ResourceInfo
                {
                    Resource = resource,
                    ReferenceCount = 1,
                    IsAsync = false,
                    Strategy = actualStrategy
                };

                Debug.Log($"同步加载资源成功: {path}, 策略: {actualStrategy}");
                return resource;
            }
        }

        /// <summary>
        /// 使用 Resources 加载资源
        /// </summary>
        private T LoadWithResources<T>(string path) where T : UnityEngine.Object
        {
            return Resources.Load<T>(path);
        }

        /// <summary>
        /// 使用 AssetBundle 加载资源
        /// </summary>
        private T LoadWithAssetBundle<T>(string path) where T : UnityEngine.Object
        {
            try
            {
                // 简单实现：假设资源路径格式为 "bundleName/assetName"
                string[] parts = path.Split('/');
                if (parts.Length < 2)
                {
                    return null;
                }

                string bundleName = parts[0];
                string assetName = string.Join("/", parts, 1, parts.Length - 1);

                // 加载 AssetBundle
                AssetBundle bundle = LoadAssetBundle(bundleName);
                if (bundle == null)
                {
                    return null;
                }

                // 加载资源
                T resource = bundle.LoadAsset<T>(assetName);
                return resource;
            }
            catch (Exception ex)
            {
                Debug.LogError($"AssetBundle 加载失败: {path}, {ex.Message}");
                return null;
            }
        }



        /// <summary>
        /// 加载 AssetBundle
        /// </summary>
        private AssetBundle LoadAssetBundle(string bundleName)
        {
            if (_loadedAssetBundles.TryGetValue(bundleName, out var bundle))
            {
                return bundle;
            }

            try
            {
                // 简单实现：从 StreamingAssets 加载
                string bundlePath = Path.Combine(Application.streamingAssetsPath, bundleName);
                if (File.Exists(bundlePath))
                {
                    bundle = AssetBundle.LoadFromFile(bundlePath);
                    if (bundle != null)
                    {
                        _loadedAssetBundles[bundleName] = bundle;
                        Debug.Log($"AssetBundle 加载成功: {bundleName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"AssetBundle 加载失败: {bundleName}, {ex.Message}");
            }

            return bundle;
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        public virtual Task<T> LoadAsync<T>(string path) where T : UnityEngine.Object
        {
            return LoadAsync<T>(path, DefaultLoadStrategy);
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        public virtual async Task<T> LoadAsync<T>(string path, ResourceLoadStrategy strategy) where T : UnityEngine.Object
        {
            lock (_resourceLock)
            {
                string key = $"{typeof(T).Name}:{path}";

                // 检查资源是否已加载
                if (_loadedResources.TryGetValue(key, out var info))
                {
                    info.ReferenceCount++;
                    return info.Resource as T;
                }
            }

            try
            {
                T resource = null;
                ResourceLoadStrategy actualStrategy = strategy;

                if (strategy == ResourceLoadStrategy.Auto)
                {
                    // 自动选择策略：先尝试 AssetBundle，再尝试 Resources
                    resource = await LoadWithAssetBundleAsync<T>(path);
                    if (resource != null)
                    {
                        actualStrategy = ResourceLoadStrategy.AssetBundle;
                    }
                    else
                    {
                        resource = await LoadWithResourcesAsync<T>(path);
                        actualStrategy = ResourceLoadStrategy.Resources;
                    }
                }
                else if (strategy == ResourceLoadStrategy.Resources)
                {
                    resource = await LoadWithResourcesAsync<T>(path);
                }
                else if (strategy == ResourceLoadStrategy.AssetBundle)
                {
                    resource = await LoadWithAssetBundleAsync<T>(path);
                }

                if (resource == null)
                {
                    Debug.LogError($"异步资源加载失败: {path}, 策略: {actualStrategy}");
                    return null;
                }

                lock (_resourceLock)
                {
                    string key = $"{typeof(T).Name}:{path}";
                    // 记录资源信息
                    _loadedResources[key] = new ResourceInfo
                    {
                        Resource = resource,
                        ReferenceCount = 1,
                        IsAsync = true,
                        Strategy = actualStrategy
                    };
                }

                Debug.Log($"异步加载资源成功: {path}, 策略: {actualStrategy}");
                return resource;
            }
            catch (Exception ex)
            {
                Debug.LogError($"异步加载资源异常: {path}, {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 使用 Resources 异步加载资源
        /// </summary>
        private async Task<T> LoadWithResourcesAsync<T>(string path) where T : UnityEngine.Object
        {
            var operation = Resources.LoadAsync<T>(path);
            await operation;
            return operation.asset as T;
        }

        /// <summary>
        /// 使用 AssetBundle 异步加载资源
        /// </summary>
        private async Task<T> LoadWithAssetBundleAsync<T>(string path) where T : UnityEngine.Object
        {
            try
            {
                // 简单实现：假设资源路径格式为 "bundleName/assetName"
                string[] parts = path.Split('/');
                if (parts.Length < 2)
                {
                    return null;
                }

                string bundleName = parts[0];
                string assetName = string.Join("/", parts, 1, parts.Length - 1);

                // 加载 AssetBundle
                AssetBundle bundle = await LoadAssetBundleAsync(bundleName);
                if (bundle == null)
                {
                    return null;
                }

                // 加载资源
                var operation = bundle.LoadAssetAsync<T>(assetName);
                await operation;

                T resource = operation.asset as T;
                return resource;
            }
            catch (Exception ex)
            {
                Debug.LogError($"AssetBundle 异步加载失败: {path}, {ex.Message}");
                return null;
            }
        }



        /// <summary>
        /// 异步加载 AssetBundle
        /// </summary>
        private async Task<AssetBundle> LoadAssetBundleAsync(string bundleName)
        {
            if (_loadedAssetBundles.TryGetValue(bundleName, out var bundle))
            {
                return bundle;
            }

            try
            {
                // 简单实现：从 StreamingAssets 加载
                string bundlePath = Path.Combine(Application.streamingAssetsPath, bundleName);
                if (File.Exists(bundlePath))
                {
                    var operation = AssetBundle.LoadFromFileAsync(bundlePath);
                    await operation;

                    bundle = operation.assetBundle;
                    if (bundle != null)
                    {
                        _loadedAssetBundles[bundleName] = bundle;
                        Debug.Log($"AssetBundle 异步加载成功: {bundleName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"AssetBundle 异步加载失败: {bundleName}, {ex.Message}");
            }

            return bundle;
        }

        /// <summary>
        /// 归还资源
        /// </summary>
        public virtual void Return<T>(T resource) where T : UnityEngine.Object
        {
            if (resource == null)
            {
                Debug.LogWarning("归还资源失败: 资源为null");
                return;
            }

            lock (_resourceLock)
            {
                // 查找资源
                foreach (var kvp in _loadedResources)
                {
                    if (kvp.Value.Resource == resource)
                    {
                        kvp.Value.ReferenceCount--;

                        // 引用计数为0时卸载资源
                        if (kvp.Value.ReferenceCount <= 0)
                        {
                            UnloadResource(kvp.Key, kvp.Value);
                        }

                        Debug.Log($"归还资源: {kvp.Key}, 剩余引用: {kvp.Value.ReferenceCount}");
                        return;
                    }
                }

                Debug.LogWarning($"归还资源失败: 未找到资源记录");
            }
        }

        /// <summary>
        /// 直接卸载资源
        /// </summary>
        public virtual void Unload<T>(T resource) where T : UnityEngine.Object
        {
            if (resource == null)
            {
                Debug.LogWarning("卸载资源失败: 资源为null");
                return;
            }

            lock (_resourceLock)
            {
                // 查找资源
                string targetKey = null;
                ResourceInfo targetInfo = null;

                foreach (var kvp in _loadedResources)
                {
                    if (kvp.Value.Resource == resource)
                    {
                        targetKey = kvp.Key;
                        targetInfo = kvp.Value;
                        break;
                    }
                }

                if (targetKey != null && targetInfo != null)
                {
                    UnloadResource(targetKey, targetInfo);
                    Debug.Log($"直接卸载资源: {targetKey}");
                }
                else
                {
                    Debug.LogWarning($"卸载资源失败: 未找到资源记录");
                }
            }
        }

        /// <summary>
        /// 卸载资源的内部方法
        /// </summary>
        private void UnloadResource(string key, ResourceInfo info)
        {
            switch (info.Strategy)
            {
                case ResourceLoadStrategy.AssetBundle:
                    if (!string.IsNullOrEmpty(info.AssetBundleName))
                    {
                        // 这里不卸载 AssetBundle，因为可能还有其他资源在使用
                        // 实际项目中可能需要更复杂的 AssetBundle 管理策略
                        Debug.Log($"AssetBundle 资源已卸载: {key}, AssetBundle: {info.AssetBundleName}");
                    }
                    else
                    {
                        Resources.UnloadAsset(info.Resource);
                        Debug.Log($"AssetBundle 资源已卸载: {key}");
                    }
                    break;

                case ResourceLoadStrategy.Resources:
                default:
                    Resources.UnloadAsset(info.Resource);
                    Debug.Log($"Resources 资源已卸载: {key}");
                    break;
            }

            _loadedResources.Remove(key);
            Debug.Log($"资源已卸载: {key}");
        }

        /// <summary>
        /// 清空所有资源
        /// </summary>
        public virtual void ClearAll()
        {
            lock (_resourceLock)
            {
                foreach (var kvp in _loadedResources)
                {
                    UnloadResource(kvp.Key, kvp.Value);
                }

                _loadedResources.Clear();

                // 卸载所有 AssetBundle
                foreach (var bundle in _loadedAssetBundles.Values)
                {
                    bundle.Unload(true);
                }
                _loadedAssetBundles.Clear();
            }

            lock (_poolLock)
            {
                Clear();
            }

            Debug.Log("所有资源已清空");
        }

        /// <summary>
        /// 获取资源使用情况
        /// </summary>
        public virtual int GetResourceCount()
        {
            lock (_resourceLock)
            {
                return _loadedResources.Count;
            }
        }

        #region ITemplatePool 实现

        /// <summary>
        /// 获取模板实例
        /// </summary>
        public virtual T Get<T>(string templateName) where T : UnityEngine.Object
        {
            lock (_poolLock)
            {
                string key = $"{typeof(T).Name}:{templateName}";

                // 检查模板池是否存在
                if (_templatePools.TryGetValue(key, out var pool) && pool.Count > 0)
                {
                    var instance = pool.Dequeue() as T;
                    if (instance != null)
                    {
                        _instanceToTemplate[instance] = key;
                        Debug.Log($"从模板池获取实例: {key}");
                        return instance;
                    }
                }

                // 模板池为空，加载新模板并实例化
                var template = Load<T>(templateName);
                if (template == null)
                {
                    Debug.LogError($"模板加载失败: {templateName}");
                    return null;
                }

                T newInstance = null;
                if (template is GameObject gameObjectTemplate)
                {
                    newInstance = GameObject.Instantiate(gameObjectTemplate) as T;
                }
                else
                {
                    // 对于非GameObject类型，直接返回模板
                    newInstance = template;
                }

                if (newInstance != null)
                {
                    _instanceToTemplate[newInstance] = key;
                    Debug.Log($"创建新模板实例: {key}");
                }

                return newInstance;
            }
        }

        /// <summary>
        /// 归还模板实例
        /// </summary>
        public virtual void ReturnTemplate<T>(T instance) where T : UnityEngine.Object
        {
            if (instance == null)
            {
                Debug.LogWarning("归还模板实例失败: 实例为null");
                return;
            }

            lock (_poolLock)
            {
                if (_instanceToTemplate.TryGetValue(instance, out var templateKey))
                {
                    // 确保模板池存在
                    if (!_templatePools.TryGetValue(templateKey, out var pool))
                    {
                        pool = new Queue<UnityEngine.Object>();
                        _templatePools[templateKey] = pool;
                    }

                    // 处理GameObject实例
                    if (instance is GameObject gameObjectInstance)
                    {
                        // 重置GameObject状态
                        gameObjectInstance.SetActive(false);
                        gameObjectInstance.transform.position = Vector3.zero;
                        gameObjectInstance.transform.rotation = Quaternion.identity;
                        gameObjectInstance.transform.localScale = Vector3.one;
                    }

                    // 将实例放回池
                    pool.Enqueue(instance);
                    _instanceToTemplate.Remove(instance);

                    Debug.Log($"归还模板实例到池: {templateKey}");
                }
                else
                {
                    Debug.LogWarning("归还模板实例失败: 未找到实例记录");
                }
            }
        }

        /// <summary>
        /// 预热模板
        /// </summary>
        public virtual async Task Preload<T>(string templateName, int count = 1) where T : UnityEngine.Object
        {
            // 加载模板
            var template = await LoadAsync<T>(templateName);
            if (template == null)
            {
                Debug.LogError($"模板预热失败: {templateName}");
                return;
            }

            lock (_poolLock)
            {
                string key = $"{typeof(T).Name}:{templateName}";

                // 确保模板池存在
                if (!_templatePools.TryGetValue(key, out var pool))
                {
                    pool = new Queue<UnityEngine.Object>();
                    _templatePools[key] = pool;
                }

                // 创建实例并加入池
                for (int i = 0; i < count; i++)
                {
                    T instance = null;
                    if (template is GameObject gameObjectTemplate)
                    {
                        instance = GameObject.Instantiate(gameObjectTemplate) as T;
                        if (instance is GameObject gameObjectInstance)
                        {
                            gameObjectInstance.SetActive(false);
                        }
                    }
                    else
                    {
                        // 对于非GameObject类型，直接使用模板
                        instance = template;
                    }

                    if (instance != null)
                    {
                        pool.Enqueue(instance);
                    }
                }

                Debug.Log($"模板预热完成: {key}, 数量: {count}");
            }
        }

        /// <summary>
        /// 清空模板池
        /// </summary>
        public virtual void Clear()
        {
            lock (_poolLock)
            {
                foreach (var pool in _templatePools.Values)
                {
                    foreach (var instance in pool)
                    {
                        if (instance is GameObject gameObjectInstance)
                        {
                            GameObject.Destroy(gameObjectInstance);
                        }
                    }
                    pool.Clear();
                }

                _templatePools.Clear();
                _instanceToTemplate.Clear();

                Debug.Log("模板池已清空");
            }
        }

        #endregion

        /// <summary>
        /// 销毁时清理资源
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                ClearAll();
                Instance = null;
            }
        }
    }
}

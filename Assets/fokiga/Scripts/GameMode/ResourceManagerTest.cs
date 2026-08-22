using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using Fokiga.Runtime.Core;

namespace Fokiga.Runtime.Gameplay
{
    public class ResourceManagerTest : MonoBehaviour
    {
        public string testPrefabPath = "Character/unitychan";
        public int preloadCount = 3;

        private List<GameObject> mTestInstances = new List<GameObject>();

        private async void Start()
        {
            Debug.Log("=== 资源管理器测试开始 ===");

            // 测试1: 同步加载资源
            await TestSyncLoad();

            // 测试2: 异步加载资源
            await TestAsyncLoad();

            // 测试3: 模板池功能
            await TestTemplatePool();

            // 测试4: 资源归还
            TestResourceReturn();

            // 测试5: 资源清空
            TestClearAll();

            Debug.Log("=== 资源管理器测试完成 ===");
        }

        private async Task TestSyncLoad()
        {
            Debug.Log("\n=== 测试同步加载资源 ===");

            // 同步加载预制体
            var prefab = ResourceManager.Instance.Load<GameObject>(testPrefabPath);
            if (prefab != null)
            {
                Debug.Log("同步加载预制体成功");

                // 实例化预制体
                var instance = Instantiate(prefab);
                instance.name = "SyncTestInstance";
                instance.transform.position = new Vector3(-5, 0, 0);
                mTestInstances.Add(instance);
            }
            else
            {
                Debug.LogError("同步加载预制体失败");
            }

            // 检查资源数量
            Debug.Log($"当前加载的资源数量: {ResourceManager.Instance.GetResourceCount()}");

            await Task.Yield();
        }

        private async Task TestAsyncLoad()
        {
            Debug.Log("\n=== 测试异步加载资源 ===");

            // 异步加载预制体
            var prefab = await ResourceManager.Instance.LoadAsync<GameObject>(testPrefabPath);
            if (prefab != null)
            {
                Debug.Log("异步加载预制体成功");

                // 实例化预制体
                var instance = Instantiate(prefab);
                instance.name = "AsyncTestInstance";
                instance.transform.position = new Vector3(0, 0, 0);
                mTestInstances.Add(instance);
            }
            else
            {
                Debug.LogError("异步加载预制体失败");
            }

            // 检查资源数量
            Debug.Log($"当前加载的资源数量: {ResourceManager.Instance.GetResourceCount()}");

            await Task.Yield();
        }

        private async Task TestTemplatePool()
        {
            Debug.Log("\n=== 测试模板池功能 ===");

            // 预热模板
            Debug.Log($"预热模板，数量: {preloadCount}");
            await ResourceManager.Instance.Preload<GameObject>(testPrefabPath, preloadCount);

            // 从模板池获取实例
            for (int i = 0; i < preloadCount + 2; i++)
            {
                var instance = ResourceManager.Instance.Get<GameObject>(testPrefabPath);
                if (instance != null)
                {
                    instance.name = $"PoolTestInstance_{i}";
                    instance.transform.position = new Vector3(5 + i, 0, 0);
                    instance.SetActive(true);
                    mTestInstances.Add(instance);
                    Debug.Log($"从模板池获取实例: {instance.name}");
                }
            }

            await Task.Yield();
        }

        private void TestResourceReturn()
        {
            Debug.Log("\n=== 测试资源归还 ===");

            // 归还模板实例
            foreach (var instance in mTestInstances)
            {
                if (instance != null)
                {
                    ResourceManager.Instance.Return(instance);
                    Debug.Log($"归还实例: {instance.name}");
                }
            }

            mTestInstances.Clear();

            // 检查资源数量
            Debug.Log($"当前加载的资源数量: {ResourceManager.Instance.GetResourceCount()}");
        }

        private void TestClearAll()
        {
            Debug.Log("\n=== 测试资源清空 ===");

            // 清空所有资源
            ResourceManager.Instance.ClearAll();

            // 检查资源数量
            Debug.Log($"当前加载的资源数量: {ResourceManager.Instance.GetResourceCount()}");
        }

        private void OnDestroy()
        {
            // 清理测试实例
            foreach (var instance in mTestInstances)
            {
                if (instance != null)
                {
                    Destroy(instance);
                }
            }
            mTestInstances.Clear();
        }
    }
}

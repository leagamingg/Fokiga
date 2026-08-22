using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

public class ResourceManagerAdvancedTest : MonoBehaviour
{
    public string testPrefabPath = "Character/unitychan";
    public string testAssetBundlePath = "characters/unitychan";
    public string testAddressablesPath = "Assets/Resources/Character/unitychan.prefab";
    public int preloadCount = 3;
    
    private List<GameObject> _testInstances = new List<GameObject>();
    
    private async void Start()
    {
        Debug.Log("=== 高级资源管理器测试开始 ===");
        
        // 测试1: 使用不同策略加载资源
        await TestDifferentStrategies();
        
        // 测试2: 模板池功能
        await TestTemplatePool();
        
        // 测试3: 资源归还
        TestResourceReturn();
        
        // 测试4: 资源清空
        TestClearAll();
        
        Debug.Log("=== 高级资源管理器测试完成 ===");
    }
    
    private async Task TestDifferentStrategies()
    {
        Debug.Log("\n=== 测试不同加载策略 ===");
        
        // 测试 Resources 策略
        Debug.Log("\n测试 Resources 策略:");
        var prefab1 = ResourceManager.Instance.Load<GameObject>(testPrefabPath, ResourceLoadStrategy.Resources);
        if (prefab1 != null)
        {
            var instance1 = Instantiate(prefab1);
            instance1.name = "ResourcesTestInstance";
            instance1.transform.position = new Vector3(-6, 0, 0);
            _testInstances.Add(instance1);
            Debug.Log("Resources 策略加载成功");
        }
        
        // 测试 AssetBundle 策略
        Debug.Log("\n测试 AssetBundle 策略:");
        var prefab2 = ResourceManager.Instance.Load<GameObject>(testAssetBundlePath, ResourceLoadStrategy.AssetBundle);
        if (prefab2 != null)
        {
            var instance2 = Instantiate(prefab2);
            instance2.name = "AssetBundleTestInstance";
            instance2.transform.position = new Vector3(-3, 0, 0);
            _testInstances.Add(instance2);
            Debug.Log("AssetBundle 策略加载成功");
        }
        else
        {
            Debug.Log("AssetBundle 策略加载失败，可能需要先构建 AssetBundle");
        }
        
        // 测试 AssetBundle 策略（使用不同路径）
        Debug.Log("\n测试 AssetBundle 策略（不同路径）:");
        var prefab3 = ResourceManager.Instance.Load<GameObject>(testAssetBundlePath, ResourceLoadStrategy.AssetBundle);
        if (prefab3 != null)
        {
            var instance3 = Instantiate(prefab3);
            instance3.name = "AssetBundleTestInstance2";
            instance3.transform.position = new Vector3(0, 0, 0);
            _testInstances.Add(instance3);
            Debug.Log("AssetBundle 策略加载成功");
        }
        else
        {
            Debug.Log("AssetBundle 策略加载失败，可能需要先构建 AssetBundle");
        }
        
        // 测试 Auto 策略
        Debug.Log("\n测试 Auto 策略:");
        var prefab4 = ResourceManager.Instance.Load<GameObject>(testPrefabPath, ResourceLoadStrategy.Auto);
        if (prefab4 != null)
        {
            var instance4 = Instantiate(prefab4);
            instance4.name = "AutoTestInstance";
            instance4.transform.position = new Vector3(3, 0, 0);
            _testInstances.Add(instance4);
            Debug.Log("Auto 策略加载成功");
        }
        
        // 检查资源数量
        Debug.Log($"\n当前加载的资源数量: {ResourceManager.Instance.GetResourceCount()}");
        
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
                instance.transform.position = new Vector3(6 + i, 0, 0);
                instance.SetActive(true);
                _testInstances.Add(instance);
                Debug.Log($"从模板池获取实例: {instance.name}");
            }
        }
        
        await Task.Yield();
    }
    
    private void TestResourceReturn()
    {
        Debug.Log("\n=== 测试资源归还 ===");
        
        // 归还模板实例
        foreach (var instance in _testInstances)
        {
            if (instance != null)
            {
                ResourceManager.Instance.Return(instance);
                Debug.Log($"归还实例: {instance.name}");
            }
        }
        
        _testInstances.Clear();
        
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
        foreach (var instance in _testInstances)
        {
            if (instance != null)
            {
                Destroy(instance);
            }
        }
        _testInstances.Clear();
    }
}

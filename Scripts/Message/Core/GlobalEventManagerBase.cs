using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 事件作用域枚举（共享定义）
/// </summary>
public enum EventScope
{
    Global,   // 全局事件，所有对象都能监听
    Instance  // 实例事件，仅特定对象实例响应
}

/// <summary>
/// 事件定义基类（共享定义）
/// </summary>
public abstract class EventDefinition
{
    /// <summary>
    /// 事件名称（子类必须定义）
    /// </summary>
    public abstract string EventName { get; }

    /// <summary>
    /// 事件作用域（子类必须定义）
    /// </summary>
    public abstract EventScope Scope { get; }
}

/// <summary>
/// 事件定义缓存（共享工具类）
/// </summary>
internal static class EventDefinitionCache
{
    private static readonly Dictionary<string, Type> _allEventTypes = new Dictionary<string, Type>();
    private static bool _isInitialized;

    internal static void Initialize()
    {
        if (_isInitialized) return;

        DiscoverEventDefinitions();
        Debug.Log($"事件定义缓存初始化完成，共发现 {_allEventTypes.Count} 个事件定义");
        _isInitialized = true;
    }

    private static void DiscoverEventDefinitions()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsSubclassOf(typeof(EventDefinition)) && !type.IsAbstract)
                    {
                        var instance = Activator.CreateInstance(type) as EventDefinition;
                        if (instance != null && !_allEventTypes.ContainsKey(instance.EventName))
                        {
                            _allEventTypes.Add(instance.EventName, type);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"反射获取事件定义时出错: {ex.Message}");
            }
        }
    }

    internal static bool TryGetEventType(string eventName, out Type type)
    {
        return _allEventTypes.TryGetValue(eventName, out type);
    }

    internal static bool ContainsEvent(string eventName)
    {
        return _allEventTypes.ContainsKey(eventName);
    }

    internal static IEnumerable<string> GetAllEventNames()
    {
        return _allEventTypes.Keys;
    }
}

/// <summary>
/// 全局事件管理器（单例）
/// </summary>
public class GlobalEventManager
{
    // 单例实例
    private static GlobalEventManager _instance;
    private static readonly object _lock = new object();

    // 全局事件字典：事件名称 -> 处理方法列表
    private readonly Dictionary<string, List<Action<EventDefinition>>> _globalEvents =
        new Dictionary<string, List<Action<EventDefinition>>>();

    public static GlobalEventManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new GlobalEventManager();
                        _instance.Initialize();
                    }
                }
            }
            return _instance;
        }
    }

    private GlobalEventManager() { }

    private void Initialize()
    {
        EventDefinitionCache.Initialize();
        Debug.Log("全局事件管理器初始化完成");
    }

    /// <summary>
    /// 添加全局事件监听
    /// </summary>
    public void AddListener<TEvt>(Action<TEvt> listener) where TEvt : EventDefinition, new()
    {
        var eventInstance = new TEvt();
        var eventName = eventInstance.EventName;

        if (!EventDefinitionCache.TryGetEventType(eventName, out var eventType) || eventType != typeof(TEvt))
        {
            Debug.LogError($"添加全局事件失败：事件 '{eventName}' 未定义或类型不匹配");
            return;
        }

        if (eventInstance.Scope != EventScope.Global)
        {
            Debug.LogError($"添加全局事件失败：事件 '{eventName}' 不是全局事件");
            return;
        }

        Action<EventDefinition> baseListener = args => listener((TEvt)args);

        if (!_globalEvents.ContainsKey(eventName))
        {
            _globalEvents[eventName] = new List<Action<EventDefinition>>();
        }

        if (!_globalEvents[eventName].Contains(baseListener))
        {
            _globalEvents[eventName].Add(baseListener);
        }
    }

    /// <summary>
    /// 移除全局事件监听
    /// </summary>
    public void RemoveListener<TEvt>(Action<TEvt> listener) where TEvt : EventDefinition, new()
    {
        var eventInstance = new TEvt();
        var eventName = eventInstance.EventName;

        Action<EventDefinition> baseListener = args => listener((TEvt)args);

        if (_globalEvents.ContainsKey(eventName) && _globalEvents[eventName].Contains(baseListener))
        {
            _globalEvents[eventName].Remove(baseListener);

            if (_globalEvents[eventName].Count == 0)
            {
                _globalEvents.Remove(eventName);
            }
        }
    }

    /// <summary>
    /// 广播全局事件
    /// </summary>
    public void Broadcast<TEvt>(TEvt eventData) where TEvt : EventDefinition
    {
        if (eventData == null)
        {
            Debug.LogError("广播全局事件失败：事件数据不能为null");
            return;
        }

        var eventName = eventData.EventName;

        if (!EventDefinitionCache.TryGetEventType(eventName, out var eventType) || eventType != typeof(TEvt))
        {
            Debug.LogError($"广播全局事件失败：事件 '{eventName}' 未定义或类型不匹配");
            return;
        }

        if (eventData.Scope != EventScope.Global)
        {
            Debug.LogError($"广播全局事件失败：事件 '{eventName}' 不是全局事件");
            return;
        }

        if (_globalEvents.TryGetValue(eventName, out var listeners))
        {
            var listenersCopy = new List<Action<EventDefinition>>(listeners);
            foreach (var listener in listenersCopy)
            {
                try
                {
                    listener?.Invoke(eventData);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"处理全局事件 '{eventName}' 时出错: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }

    /// <summary>
    /// 检查全局事件是否已定义
    /// </summary>
    public bool IsEventDefined(string eventName)
    {
        return EventDefinitionCache.ContainsEvent(eventName);
    }

    /// <summary>
    /// 获取所有已注册的事件名称
    /// </summary>
    public IEnumerable<string> GetAllEventNames()
    {
        return EventDefinitionCache.GetAllEventNames();
    }
}

/// <summary>
/// 全局事件扩展方法（增强版）
/// </summary>
public static class GlobalEventExtensions
{
    // 原有的扩展方法保持不变
    public static void AddGlobalListener<TEvt>(this Action<TEvt> listener) where TEvt : EventDefinition, new()
    {
        GlobalEventManager.Instance.AddListener(listener);
    }

    public static void RemoveGlobalListener<TEvt>(this Action<TEvt> listener) where TEvt : EventDefinition, new()
    {
        GlobalEventManager.Instance.RemoveListener(listener);
    }

    public static void BroadcastGlobalEvent<TEvt>(this TEvt eventData) where TEvt : EventDefinition
    {
        GlobalEventManager.Instance.Broadcast(eventData);
    }

    // 新增扩展方法：直接接受方法作为参数，无需显式声明Action变量
    public static void AddGlobalListener<TEvt>(this MonoBehaviour sender, Action<TEvt> listener)
        where TEvt : EventDefinition, new()
    {
        GlobalEventManager.Instance.AddListener(listener);
    }

    // 新增扩展方法：用于移除监听
    public static void RemoveGlobalListener<TEvt>(this MonoBehaviour sender, Action<TEvt> listener)
        where TEvt : EventDefinition, new()
    {
        GlobalEventManager.Instance.RemoveListener(listener);
    }
}


/*
// 定义一个全局事件：玩家得分变化事件
public class PlayerScoreChangedEvent : EventDefinition
{
    // 事件携带的数据
    public int NewScore { get; set; }
    public int OldScore { get; set; }

    // 事件名称（必须唯一）
    public override string EventName => "PlayerScoreChanged";

    // 事件作用域（全局事件）
    public override EventScope Scope => EventScope.Global;
}

// 再定义一个示例事件：玩家死亡事件
public class PlayerDeathEvent : EventDefinition
{
    public string PlayerName { get; set; }
    public int DeathReason { get; set; } // 0=敌人击杀, 1=掉落, 2=超时

    public override string EventName => "PlayerDeath";
    public override EventScope Scope => EventScope.Global;
}

using UnityEngine;

public class UIScoreDisplay : MonoBehaviour
{
    private void OnEnable()
    {
        // 方法1：直接通过管理器注册
        GlobalEventManager.Instance.AddListener<PlayerScoreChangedEvent>(OnScoreChanged);

        // 方法2：使用扩展方法（更简洁）
        this.AddGlobalListener<PlayerDeathEvent>(OnPlayerDeath);
    }

    private void OnDisable()
    {
        // 移除监听器（必须与注册对应，避免内存泄漏）
        GlobalEventManager.Instance.RemoveListener<PlayerScoreChangedEvent>(OnScoreChanged);
        this.RemoveGlobalListener<PlayerDeathEvent>(OnPlayerDeath);
    }

    // 处理得分变化事件
    private void OnScoreChanged(PlayerScoreChangedEvent evt)
    {
        Debug.Log($"分数变化: 从 {evt.OldScore} 到 {evt.NewScore}");
        // 更新UI显示...
    }

    // 处理玩家死亡事件
    private void OnPlayerDeath(PlayerDeathEvent evt)
    {
        Debug.Log($"{evt.PlayerName} 死亡，原因: {evt.DeathReason}");
        // 显示死亡面板...
    }
}

public class PlayerController : MonoBehaviour
{
    private int _currentScore;

    public void AddScore(int amount)
    {
        int oldScore = _currentScore;
        _currentScore += amount;

        // 创建事件实例并设置数据
        var scoreEvent = new PlayerScoreChangedEvent
        {
            OldScore = oldScore,
            NewScore = _currentScore
        };

        // 广播事件（两种方式）
        // 方式1：通过管理器广播
        GlobalEventManager.Instance.Broadcast(scoreEvent);

        // 方式2：使用扩展方法
        // scoreEvent.BroadcastGlobalEvent();
    }

    public void Die(int reason)
    {
        var deathEvent = new PlayerDeathEvent
        {
            PlayerName = "MainPlayer",
            DeathReason = reason
        };

        deathEvent.BroadcastGlobalEvent(); // 使用扩展方法广播
    }
}

// 检查事件是否已定义
bool isDefined = GlobalEventManager.Instance.IsEventDefined("PlayerScoreChanged");

// 获取所有已定义的事件名称
IEnumerable<string> allEvents = GlobalEventManager.Instance.GetAllEventNames();
foreach (var eventName in allEvents)
{
    Debug.Log("已定义事件: " + eventName);
}
 */
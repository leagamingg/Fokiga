using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 局部事件管理器实现
/// </summary>
public class LocalEventManager
{
    // 局部事件存储结构：实例 -> (事件名称 -> 监听器列表)
    private readonly Dictionary<object, Dictionary<string, List<Action<EventDefinition>>>> _localEvents =
        new Dictionary<object, Dictionary<string, List<Action<EventDefinition>>>>();

    // 移除单例相关代码，改为公共构造函数
    public LocalEventManager()
    {
        Initialize();
    }

    private void Initialize()
    {
        // 确保事件定义已初始化
        EventDefinitionCache.Initialize();
        Debug.Log("局部事件管理器初始化完成");
    }

    /// <summary>
    /// 为指定实例添加事件监听器
    /// </summary>
    public void AddListener<TEvt>(object instance, Action<TEvt> listener) where TEvt : EventDefinition, new()
    {
        if (instance == null)
        {
            Debug.LogError("添加局部事件失败：实例不能为null");
            return;
        }

        var eventInstance = new TEvt();
        var eventName = eventInstance.EventName;

        if (!EventDefinitionCache.TryGetEventType(eventName, out var eventType) || eventType != typeof(TEvt))
        {
            Debug.LogError($"添加局部事件失败：事件 '{eventName}' 未注册或类型不匹配");
            return;
        }

        if (eventInstance.Scope != EventScope.Instance)
        {
            Debug.LogError($"添加局部事件失败：事件 '{eventName}' 不是局部事件");
            return;
        }

        Action<EventDefinition> baseListener = args => listener((TEvt)args);

        if (!_localEvents.ContainsKey(instance))
        {
            _localEvents[instance] = new Dictionary<string, List<Action<EventDefinition>>>();
        }

        var instanceEventDict = _localEvents[instance];
        if (!instanceEventDict.ContainsKey(eventName))
        {
            instanceEventDict[eventName] = new List<Action<EventDefinition>>();
        }

        if (!instanceEventDict[eventName].Contains(baseListener))
        {
            instanceEventDict[eventName].Add(baseListener);
        }
    }

    /// <summary>
    /// 从指定实例移除事件监听器
    /// </summary>
    public void RemoveListener<TEvt>(object instance, Action<TEvt> listener) where TEvt : EventDefinition, new()
    {
        if (instance == null) return;

        var eventInstance = new TEvt();
        var eventName = eventInstance.EventName;

        Action<EventDefinition> baseListener = args => listener((TEvt)args);

        if (_localEvents.TryGetValue(instance, out var instanceEventDict) &&
            instanceEventDict.TryGetValue(eventName, out var listeners))
        {
            listeners.Remove(baseListener);

            if (listeners.Count == 0)
            {
                instanceEventDict.Remove(eventName);
                if (instanceEventDict.Count == 0)
                {
                    _localEvents.Remove(instance);
                }
            }
        }
    }

    /// <summary>
    /// 移除指定实例的所有事件监听器
    /// </summary>
    public void RemoveAllListeners(object instance)
    {
        if (instance != null && _localEvents.ContainsKey(instance))
        {
            _localEvents.Remove(instance);
        }
    }

    /// <summary>
    /// 向指定目标实例广播局部事件
    /// </summary>
    public void Broadcast<TEvt>(object instance, TEvt eventData) where TEvt : EventDefinition
    {
        if (instance == null)
        {
            Debug.LogError("广播局部事件失败：实例不能为null");
            return;
        }

        if (eventData == null)
        {
            Debug.LogError("广播局部事件失败：事件数据不能为null");
            return;
        }

        var eventName = eventData.EventName;

        if (!EventDefinitionCache.TryGetEventType(eventName, out var eventType) || eventType != typeof(TEvt))
        {
            Debug.LogError($"广播局部事件失败：事件 '{eventName}' 未注册或类型不匹配");
            return;
        }

        if (eventData.Scope != EventScope.Instance)
        {
            Debug.LogError($"广播局部事件失败：事件 '{eventName}' 不是局部事件");
            return;
        }

        if (_localEvents.TryGetValue(instance, out var instanceEventDict) &&
            instanceEventDict.TryGetValue(eventName, out var listeners))
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
                    Debug.LogError($"执行局部事件 '{eventName}' 时出错: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }
}

/// <summary>
/// 局部事件扩展方法（需传入具体管理器实例）
/// </summary>
public static class LocalEventExtensions
{
    /// <summary>
    /// 为当前实例添加局部事件监听器
    /// </summary>
    public static void AddLocalListener<TEvt>(this object instance, LocalEventManager manager, Action<TEvt> listener)
        where TEvt : EventDefinition, new()
    {
        manager.AddListener(instance, listener);
    }

    /// <summary>
    /// 为当前实例移除局部事件监听器
    /// </summary>
    public static void RemoveLocalListener<TEvt>(this object instance, LocalEventManager manager, Action<TEvt> listener)
        where TEvt : EventDefinition, new()
    {
        manager.RemoveListener(instance, listener);
    }

    /// <summary>
    /// 移除当前实例的所有局部事件监听器
    /// </summary>
    public static void RemoveAllLocalListeners(this object instance, LocalEventManager manager)
    {
        manager.RemoveAllListeners(instance);
    }

    /// <summary>
    /// 从当前实例广播局部事件
    /// </summary>
    public static void BroadcastLocalEvent<TEvt>(this object instance, LocalEventManager manager, TEvt eventData)
        where TEvt : EventDefinition
    {
        manager.Broadcast(instance, eventData);
    }
}

/*
// 事件基类（示例，需提前定义）
public enum EventScope { Instance, Global }
public abstract class EventDefinition
{
    public abstract string EventName { get; }
    public abstract EventScope Scope { get; }
}

// 自定义事件示例：玩家得分事件
public class PlayerScoreEvent : EventDefinition
{
    public int Score { get; set; } // 事件携带的数据
    public override string EventName => "PlayerScoreEvent";
    public override EventScope Scope => EventScope.Instance; // 实例级事件
}

private LocalEventManager _eventManager;

void Awake()
{
    _eventManager = new LocalEventManager(); // 初始化时会自动初始化事件缓存
}

// 监听者类示例
public class UIManager
{
    public UIManager(LocalEventManager eventManager)
    {
        // 方式1：直接调用管理器方法注册
        eventManager.AddListener<PlayerScoreEvent>(this, OnPlayerScoreChanged);

        // 方式2：使用扩展方法注册（更简洁）
        this.AddLocalListener(eventManager, OnPlayerScoreChanged);
    }

    // 事件处理方法
    private void OnPlayerScoreChanged(PlayerScoreEvent evt)
    {
        Debug.Log($"玩家得分更新：{evt.Score}");
    }
}

public class PlayerController
{
    private LocalEventManager _eventManager;
    private UIManager _uiManager; // 需要监听事件的实例

    public PlayerController(LocalEventManager eventManager, UIManager uiManager)
    {
        _eventManager = eventManager;
        _uiManager = uiManager;
    }

    // 得分时广播事件
    public void AddScore(int score)
    {
        var eventData = new PlayerScoreEvent { Score = score };
        
        // 方式1：直接调用管理器广播
        _eventManager.Broadcast(_uiManager, eventData);

        // 方式2：使用扩展方法广播
        _uiManager.BroadcastLocalEvent(_eventManager, eventData);
    }
}

//移除指定事件的监听
// 方式1：直接调用管理器方法
_eventManager.RemoveListener<PlayerScoreEvent>(this, OnPlayerScoreChanged);

// 方式2：使用扩展方法
this.RemoveLocalListener(_eventManager, OnPlayerScoreChanged);

//移除实例的所有事件监听
// 方式1：直接调用管理器方法
_eventManager.RemoveAllListeners(this);

// 方式2：使用扩展方法
this.RemoveAllLocalListeners(_eventManager);
 */
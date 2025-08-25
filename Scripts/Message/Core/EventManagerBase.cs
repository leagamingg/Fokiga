//using System;
//using System.Collections.Generic;
//using System.Reflection;
//using UnityEngine;

///// <summary>
///// 事件作用域枚举
///// </summary>
//public enum EventScope
//{
//    Global,   // 全局事件，所有对象都能监听
//    Instance  // 实例事件，仅特定对象实例响应
//}

///// <summary>
///// 事件定义基类，包含事件元信息和参数数据
///// </summary>
//public abstract class EventDefinition
//{
//    /// <summary>
//    /// 事件名称（子类必须定义）
//    /// </summary>
//    public abstract string EventName { get; }

//    /// <summary>
//    /// 事件作用域（子类必须定义）
//    /// </summary>
//    public abstract EventScope Scope { get; }
//}

///// <summary>
///// 事件管理器（单例），负责事件的订阅、广播和管理
///// </summary>
//public class EventManager
//{
//    // 单例实例
//    private static EventManager _instance;
//    private static readonly object _lock = new object();

//    // 全局事件字典：事件名称 -> 处理方法列表
//    private readonly Dictionary<string, List<Action<EventDefinition>>> _globalEvents =
//        new Dictionary<string, List<Action<EventDefinition>>>();

//    // 实例事件字典：对象实例 -> (事件名称 -> 处理方法列表)
//    private readonly Dictionary<object, Dictionary<string, List<Action<EventDefinition>>>> _instanceEvents =
//        new Dictionary<object, Dictionary<string, List<Action<EventDefinition>>>>();

//    // 事件定义缓存：事件名称 -> 事件类型
//    private readonly Dictionary<string, Type> _allEventTypes =
//        new Dictionary<string, Type>();

//    public static EventManager Instance
//    {
//        get
//        {
//            if (_instance == null)
//            {
//                lock (_lock)
//                {
//                    if (_instance == null)
//                    {
//                        _instance = new EventManager();
//                        _instance.Initialize();
//                    }
//                }
//            }
//            return _instance;
//        }
//    }

//    private EventManager() { }

//    private void Initialize()
//    {
//        DiscoverEventDefinitions();
//        Debug.Log($"事件管理器初始化完成，共发现 {_allEventTypes.Count} 个事件定义");
//    }

//    /// <summary>
//    /// 自动发现所有继承自EventDefinition的事件定义类
//    /// </summary>
//    private void DiscoverEventDefinitions()
//    {
//        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
//        {
//            try
//            {
//                foreach (var type in assembly.GetTypes())
//                {
//                    if (type.IsSubclassOf(typeof(EventDefinition)) && !type.IsAbstract)
//                    {
//                        var instance = Activator.CreateInstance(type) as EventDefinition;
//                        if (instance != null && !_allEventTypes.ContainsKey(instance.EventName))
//                        {
//                            _allEventTypes.Add(instance.EventName, type);
//                        }
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                Debug.LogWarning($"反射获取事件定义时出错: {ex.Message}");
//            }
//        }
//    }

//    /// <summary>
//    /// 添加全局事件监听
//    /// </summary>
//    public void AddGlobalDelegate<TEvt>(Action<TEvt> listener) where TEvt : EventDefinition, new()
//    {
//        var eventInstance = new TEvt();
//        var eventName = eventInstance.EventName;

//        if (!_allEventTypes.TryGetValue(eventName, out var eventType) || eventType != typeof(TEvt))
//        {
//            Debug.LogError($"添加全局事件失败：事件 '{eventName}' 未定义或类型不匹配");
//            return;
//        }

//        if (eventInstance.Scope != EventScope.Global)
//        {
//            Debug.LogError($"添加全局事件失败：事件 '{eventName}' 不是全局事件");
//            return;
//        }

//        Action<EventDefinition> baseListener = args => listener((TEvt)args);

//        if (!_globalEvents.ContainsKey(eventName))
//        {
//            _globalEvents[eventName] = new List<Action<EventDefinition>>();
//        }

//        if (!_globalEvents[eventName].Contains(baseListener))
//        {
//            _globalEvents[eventName].Add(baseListener);
//        }
//    }

//    /// <summary>
//    /// 移除全局事件监听
//    /// </summary>
//    public void RemoveGlobalDelegate<TEvt>(Action<TEvt> listener) where TEvt : EventDefinition, new()
//    {
//        var eventInstance = new TEvt();
//        var eventName = eventInstance.EventName;

//        Action<EventDefinition> baseListener = args => listener((TEvt)args);

//        if (_globalEvents.ContainsKey(eventName) && _globalEvents[eventName].Contains(baseListener))
//        {
//            _globalEvents[eventName].Remove(baseListener);

//            if (_globalEvents[eventName].Count == 0)
//            {
//                _globalEvents.Remove(eventName);
//            }
//        }
//    }

//    /// <summary>
//    /// 广播全局事件
//    /// </summary>
//    public void BroadcastGlobalEvent<TEvt>(TEvt eventData) where TEvt : EventDefinition
//    {
//        if (eventData == null)
//        {
//            Debug.LogError("广播全局事件失败：事件数据不能为null");
//            return;
//        }

//        var eventName = eventData.EventName;

//        if (!_allEventTypes.TryGetValue(eventName, out var eventType) || eventType != typeof(TEvt))
//        {
//            Debug.LogError($"广播全局事件失败：事件 '{eventName}' 未定义或类型不匹配");
//            return;
//        }

//        if (eventData.Scope != EventScope.Global)
//        {
//            Debug.LogError($"广播全局事件失败：事件 '{eventName}' 不是全局事件");
//            return;
//        }

//        if (_globalEvents.TryGetValue(eventName, out var listeners))
//        {
//            var listenersCopy = new List<Action<EventDefinition>>(listeners);
//            foreach (var listener in listenersCopy)
//            {
//                try
//                {
//                    listener?.Invoke(eventData);
//                }
//                catch (Exception ex)
//                {
//                    Debug.LogError($"处理事件 '{eventName}' 时出错: {ex.Message}\n{ex.StackTrace}");
//                }
//            }
//        }
//    }

//    /// <summary>
//    /// 添加实例事件监听（需指定关联实例）
//    /// </summary>
//    public void AddInstanceDelegate<TEvt>(object instance, Action<TEvt> listener) where TEvt : EventDefinition, new()
//    {
//        if (instance == null)
//        {
//            Debug.LogError("添加实例事件失败：实例不能为null");
//            return;
//        }

//        var eventInstance = new TEvt();
//        var eventName = eventInstance.EventName;

//        if (!_allEventTypes.TryGetValue(eventName, out var eventType) || eventType != typeof(TEvt))
//        {
//            Debug.LogError($"添加实例事件失败：事件 '{eventName}' 未定义或类型不匹配");
//            return;
//        }

//        if (eventInstance.Scope != EventScope.Instance)
//        {
//            Debug.LogError($"添加实例事件失败：事件 '{eventName}' 不是实例事件");
//            return;
//        }

//        Action<EventDefinition> baseListener = args => listener((TEvt)args);

//        if (!_instanceEvents.ContainsKey(instance))
//        {
//            _instanceEvents[instance] = new Dictionary<string, List<Action<EventDefinition>>>();
//        }

//        var instanceEventDict = _instanceEvents[instance];
//        if (!instanceEventDict.ContainsKey(eventName))
//        {
//            instanceEventDict[eventName] = new List<Action<EventDefinition>>();
//        }

//        if (!instanceEventDict[eventName].Contains(baseListener))
//        {
//            instanceEventDict[eventName].Add(baseListener);
//        }
//    }

//    /// <summary>
//    /// 移除实例事件监听（需指定关联实例）
//    /// </summary>
//    public void RemoveInstanceDelegate<TEvt>(object instance, Action<TEvt> listener) where TEvt : EventDefinition, new()
//    {
//        if (instance == null) return;

//        var eventInstance = new TEvt();
//        var eventName = eventInstance.EventName;

//        Action<EventDefinition> baseListener = args => listener((TEvt)args);

//        if (_instanceEvents.TryGetValue(instance, out var instanceEventDict) &&
//            instanceEventDict.TryGetValue(eventName, out var listeners))
//        {
//            listeners.Remove(baseListener);

//            if (listeners.Count == 0)
//            {
//                instanceEventDict.Remove(eventName);
//                if (instanceEventDict.Count == 0)
//                {
//                    _instanceEvents.Remove(instance);
//                }
//            }
//        }
//    }

//    /// <summary>
//    /// 移除实例的所有事件监听
//    /// </summary>
//    public void RemoveAllInstanceDelegates(object instance)
//    {
//        if (instance != null && _instanceEvents.ContainsKey(instance))
//        {
//            _instanceEvents.Remove(instance);
//        }
//    }

//    /// <summary>
//    /// 广播实例事件（需指定目标实例）
//    /// </summary>
//    public void BroadcastInstanceEvent<TEvt>(object instance, TEvt eventData) where TEvt : EventDefinition
//    {
//        if (instance == null)
//        {
//            Debug.LogError("广播实例事件失败：实例不能为null");
//            return;
//        }

//        if (eventData == null)
//        {
//            Debug.LogError("广播实例事件失败：事件数据不能为null");
//            return;
//        }

//        var eventName = eventData.EventName;

//        if (!_allEventTypes.TryGetValue(eventName, out var eventType) || eventType != typeof(TEvt))
//        {
//            Debug.LogError($"广播实例事件失败：事件 '{eventName}' 未定义或类型不匹配");
//            return;
//        }

//        if (eventData.Scope != EventScope.Instance)
//        {
//            Debug.LogError($"广播实例事件失败：事件 '{eventName}' 不是实例事件");
//            return;
//        }

//        if (_instanceEvents.TryGetValue(instance, out var instanceEventDict) &&
//            instanceEventDict.TryGetValue(eventName, out var listeners))
//        {
//            var listenersCopy = new List<Action<EventDefinition>>(listeners);
//            foreach (var listener in listenersCopy)
//            {
//                try
//                {
//                    listener?.Invoke(eventData);
//                }
//                catch (Exception ex)
//                {
//                    Debug.LogError($"处理实例事件 '{eventName}' 时出错: {ex.Message}\n{ex.StackTrace}");
//                }
//            }
//        }
//    }

//    /// <summary>
//    /// 检查事件是否已定义
//    /// </summary>
//    public bool IsEventDefined(string eventName)
//    {
//        return _allEventTypes.ContainsKey(eventName);
//    }

//    /// <summary>
//    /// 获取所有已注册的事件名称
//    /// </summary>
//    public IEnumerable<string> GetAllEventNames()
//    {
//        return _allEventTypes.Keys;
//    }
//}

///// <summary>
///// 事件管理器扩展方法（简化事件调用）
///// </summary>
//public static class EventManagerExtensions
//{
//    // 实例事件扩展方法
//    /// <summary>
//    /// 为当前实例添加事件监听（自动将当前对象作为instance）
//    /// </summary>
//    public static void AddInstanceDelegate<TEvt>(this object instance, Action<TEvt> listener) where TEvt : EventDefinition, new()
//    {
//        EventManager.Instance.AddInstanceDelegate(instance, listener);
//    }

//    /// <summary>
//    /// 为当前实例移除事件监听（自动将当前对象作为instance）
//    /// </summary>
//    public static void RemoveInstanceDelegate<TEvt>(this object instance, Action<TEvt> listener) where TEvt : EventDefinition, new()
//    {
//        EventManager.Instance.RemoveInstanceDelegate(instance, listener);
//    }

//    /// <summary>
//    /// 移除当前实例的所有事件监听
//    /// </summary>
//    public static void RemoveAllInstanceDelegates(this object instance)
//    {
//        EventManager.Instance.RemoveAllInstanceDelegates(instance);
//    }

//    /// <summary>
//    /// 向当前实例广播事件（自动将当前对象作为instance）
//    /// </summary>
//    public static void BroadcastInstanceEvent<TEvt>(this object instance, TEvt eventData) where TEvt : EventDefinition
//    {
//        EventManager.Instance.BroadcastInstanceEvent(instance, eventData);
//    }

//    // 全局事件扩展方法
//    /// <summary>
//    /// 添加全局事件监听（简化调用）
//    /// </summary>
//    public static void AddGlobalDelegate<TEvt>(this Action<TEvt> listener) where TEvt : EventDefinition, new()
//    {
//        EventManager.Instance.AddGlobalDelegate(listener);
//    }

//    /// <summary>
//    /// 移除全局事件监听（简化调用）
//    /// </summary>
//    public static void RemoveGlobalDelegate<TEvt>(this Action<TEvt> listener) where TEvt : EventDefinition, new()
//    {
//        EventManager.Instance.RemoveGlobalDelegate(listener);
//    }

//    /// <summary>
//    /// 广播全局事件（简化调用）
//    /// </summary>
//    public static void BroadcastGlobalEvent<TEvt>(this TEvt eventData) where TEvt : EventDefinition
//    {
//        EventManager.Instance.BroadcastGlobalEvent(eventData);
//    }
//}


///*
// * // 全局事件示例
//public class PlayerLevelUpEvent : EventDefinition
//{
//    public int NewLevel { get; set; } // 事件参数
//    public override string EventName => "PlayerLevelUp";
//    public override EventScope Scope => EventScope.Global;
//}

//// 实例事件示例（仅特定实例响应）
//public class HealthChangedEvent : EventDefinition
//{
//    public int Delta { get; set; } // 事件参数（变化量）
//    public override string EventName => "HealthChanged";
//    public override EventScope Scope => EventScope.Instance;
//}




//public class Player : MonoBehaviour
//{
//    private int _health;

//    private void OnEnable()
//    {
//        // 订阅全局事件（通过扩展方法简化，无需显式调用Instance）
//        OnPlayerLevelUp.AddGlobalDelegate<PlayerLevelUpEvent>();

//        // 订阅实例事件（通过扩展方法，自动将this作为instance）
//        this.AddInstanceDelegate<HealthChangedEvent>(OnHealthChanged);
//    }

//    private void OnDisable()
//    {
//        // 移除全局事件监听（通过扩展方法简化）
//        OnPlayerLevelUp.RemoveGlobalDelegate<PlayerLevelUpEvent>();

//        // 移除实例事件监听（自动关联this）
//        this.RemoveInstanceDelegate<HealthChangedEvent>(OnHealthChanged);
//    }

//    // 全局事件处理方法
//    private void OnPlayerLevelUp(PlayerLevelUpEvent evt)
//    {
//        Debug.Log($"玩家升级到 {evt.NewLevel} 级！");
//    }

//    // 实例事件处理方法（仅当前Player实例响应）
//    private void OnHealthChanged(HealthChangedEvent evt)
//    {
//        _health += evt.Delta;
//        Debug.Log($"当前实例生命值变化：{evt.Delta}，新值：{_health}");
//    }

//    // 广播实例事件（通过扩展方法，自动将this作为目标实例）
//    public void TakeDamage(int damage)
//    {
//        this.BroadcastInstanceEvent(new HealthChangedEvent { Delta = -damage });
//    }
//}

//public class GameManager : MonoBehaviour
//{
//    // 广播全局事件（通过扩展方法简化）
//    public void LevelUpPlayer(int newLevel)
//    {
//        new PlayerLevelUpEvent { NewLevel = newLevel }.BroadcastGlobalEvent();
//    }
//}
// */
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 事件作用域枚举
/// </summary>
public enum EventScope
{
    Global,   // 全局事件，所有对象都能监听
    Instance  // 实例事件，仅特定对象实例响应
}

/// <summary>
/// 事件参数基类
/// </summary>
public abstract class EventArgsBase { }

/// <summary>
/// 标记事件定义类的特性
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class EventDefinitionAttribute : Attribute { }

/// <summary>
/// 泛型事件定义类，将事件信息与参数类型绑定
/// </summary>
/// <typeparam name="T">事件参数类型，必须是EventArgsBase的子类</typeparam>
public class EventInfo<T> where T : EventArgsBase
{
    public string Name { get; }
    public EventScope Scope { get; }
    public Type ArgsType { get; }

    public EventInfo(string name, EventScope scope)
    {
        Name = name;
        Scope = scope;
        ArgsType = typeof(T);
    }
}

/// <summary>
/// 事件管理器，不依赖MonoBehaviour的单例实现
/// </summary>
public class EventManager
{
    // 单例实例
    private static EventManager _instance;

    // 线程安全的单例锁
    private static readonly object _lock = new object();

    // 全局事件字典：事件名称 -> 事件处理方法列表
    private readonly Dictionary<string, List<Action<EventArgsBase>>> _globalEvents =
        new Dictionary<string, List<Action<EventArgsBase>>>();

    // 实例事件字典：对象实例 -> (事件名称 -> 事件处理方法列表)
    private readonly Dictionary<object, Dictionary<string, List<Action<EventArgsBase>>>> _instanceEvents =
        new Dictionary<object, Dictionary<string, List<Action<EventArgsBase>>>>();

    // 所有事件定义的缓存
    private readonly Dictionary<string, EventInfo<EventArgsBase>> _allEvents =
        new Dictionary<string, EventInfo<EventArgsBase>>();

    /// <summary>
    /// 单例实例访问器
    /// </summary>
    public static EventManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new EventManager();
                        _instance.Initialize();
                    }
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// 私有构造函数，防止外部实例化
    /// </summary>
    private EventManager() { }

    /// <summary>
    /// 初始化事件管理器，自动发现所有事件定义
    /// </summary>
    private void Initialize()
    {
        DiscoverEventDefinitions();
        Debug.Log($"事件管理器初始化完成，共发现 {_allEvents.Count} 个事件定义");
    }

    /// <summary>
    /// 自动发现并注册所有标记了EventDefinitionAttribute的事件定义类
    /// </summary>
    private void DiscoverEventDefinitions()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    // 检查类是否标记了事件定义特性
                    if (Attribute.IsDefined(type, typeof(EventDefinitionAttribute)))
                    {
                        // 反射获取所有静态字段（事件定义）
                        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
                        foreach (var field in fields)
                        {
                            // 检查字段类型是否为泛型EventInfo<>
                            if (field.FieldType.IsGenericType &&
                                field.FieldType.GetGenericTypeDefinition() == typeof(EventInfo<>))
                            {
                                var eventInfo = field.GetValue(null) as EventInfo<EventArgsBase>;
                                if (eventInfo != null && !_allEvents.ContainsKey(eventInfo.Name))
                                {
                                    _allEvents.Add(eventInfo.Name, eventInfo);
                                }
                            }
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

    /// <summary>
    /// 添加全局事件监听（泛型版本）
    /// </summary>
    public void AddGlobalListener<T>(EventInfo<T> eventInfo, Action<T> listener) where T : EventArgsBase
    {
        if (eventInfo == null)
        {
            Debug.LogError("添加全局事件监听失败：事件信息不能为null");
            return;
        }

        if (!_allEvents.ContainsKey(eventInfo.Name))
        {
            Debug.LogError($"添加全局事件监听失败：事件 '{eventInfo.Name}' 未定义");
            return;
        }

        if (_allEvents[eventInfo.Name].Scope != EventScope.Global)
        {
            Debug.LogError($"添加全局事件监听失败：事件 '{eventInfo.Name}' 不是全局事件");
            return;
        }

        // 将泛型监听器转换为基类监听器
        Action<EventArgsBase> baseListener = args => listener((T)args);

        if (!_globalEvents.ContainsKey(eventInfo.Name))
        {
            _globalEvents[eventInfo.Name] = new List<Action<EventArgsBase>>();
        }

        if (!_globalEvents[eventInfo.Name].Contains(baseListener))
        {
            _globalEvents[eventInfo.Name].Add(baseListener);
        }
    }

    /// <summary>
    /// 移除全局事件监听（泛型版本）
    /// </summary>
    public void RemoveGlobalListener<T>(EventInfo<T> eventInfo, Action<T> listener) where T : EventArgsBase
    {
        if (eventInfo == null) return;

        // 创建对应的基类监听器
        Action<EventArgsBase> baseListener = args => listener((T)args);

        if (_globalEvents.ContainsKey(eventInfo.Name) && _globalEvents[eventInfo.Name].Contains(baseListener))
        {
            _globalEvents[eventInfo.Name].Remove(baseListener);

            if (_globalEvents[eventInfo.Name].Count == 0)
            {
                _globalEvents.Remove(eventInfo.Name);
            }
        }
    }

    /// <summary>
    /// 触发全局事件（泛型版本）
    /// </summary>
    public void TriggerGlobalEvent<T>(EventInfo<T> eventInfo, T args) where T : EventArgsBase
    {
        if (eventInfo == null)
        {
            Debug.LogError("触发全局事件失败：事件信息不能为null");
            return;
        }

        if (!_allEvents.ContainsKey(eventInfo.Name))
        {
            Debug.LogError($"触发全局事件失败：事件 '{eventInfo.Name}' 未定义");
            return;
        }

        var storedEventInfo = _allEvents[eventInfo.Name];

        if (storedEventInfo.Scope != EventScope.Global)
        {
            Debug.LogError($"触发全局事件失败：事件 '{eventInfo.Name}' 不是全局事件");
            return;
        }

        if (args != null && args.GetType() != storedEventInfo.ArgsType)
        {
            Debug.LogError($"触发事件 '{eventInfo.Name}' 失败：参数类型不匹配，预期 {storedEventInfo.ArgsType.Name}，实际 {args.GetType().Name}");
            return;
        }

        if (_globalEvents.TryGetValue(eventInfo.Name, out var listeners))
        {
            var listenersCopy = new List<Action<EventArgsBase>>(listeners);
            foreach (var listener in listenersCopy)
            {
                try
                {
                    listener?.Invoke(args);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"处理事件 '{eventInfo.Name}' 时出错: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }

    /// <summary>
    /// 添加实例事件监听（泛型版本）
    /// </summary>
    public void AddInstanceListener<T>(object instance, EventInfo<T> eventInfo, Action<T> listener) where T : EventArgsBase
    {
        if (instance == null)
        {
            Debug.LogError("添加实例事件监听失败：实例不能为null");
            return;
        }

        if (eventInfo == null)
        {
            Debug.LogError("添加实例事件监听失败：事件信息不能为null");
            return;
        }

        if (!_allEvents.ContainsKey(eventInfo.Name))
        {
            Debug.LogError($"添加实例事件监听失败：事件 '{eventInfo.Name}' 未定义");
            return;
        }

        if (_allEvents[eventInfo.Name].Scope != EventScope.Instance)
        {
            Debug.LogError($"添加实例事件监听失败：事件 '{eventInfo.Name}' 不是实例事件");
            return;
        }

        // 将泛型监听器转换为基类监听器
        Action<EventArgsBase> baseListener = args => listener((T)args);

        // 确保实例在字典中存在
        if (!_instanceEvents.ContainsKey(instance))
        {
            _instanceEvents[instance] = new Dictionary<string, List<Action<EventArgsBase>>>();
        }

        var instanceEventDict = _instanceEvents[instance];

        // 确保事件名称在实例的事件字典中存在
        if (!instanceEventDict.ContainsKey(eventInfo.Name))
        {
            instanceEventDict[eventInfo.Name] = new List<Action<EventArgsBase>>();
        }

        // 避免添加重复的监听器
        if (!instanceEventDict[eventInfo.Name].Contains(baseListener))
        {
            instanceEventDict[eventInfo.Name].Add(baseListener);
        }
    }

    /// <summary>
    /// 移除实例事件监听（泛型版本）
    /// </summary>
    public void RemoveInstanceListener<T>(object instance, EventInfo<T> eventInfo, Action<T> listener) where T : EventArgsBase
    {
        if (instance == null || eventInfo == null) return;

        // 创建对应的基类监听器
        Action<EventArgsBase> baseListener = args => listener((T)args);

        if (_instanceEvents.TryGetValue(instance, out var instanceEventDict))
        {
            if (instanceEventDict.TryGetValue(eventInfo.Name, out var listeners))
            {
                listeners.Remove(baseListener);

                // 清理空列表
                if (listeners.Count == 0)
                {
                    instanceEventDict.Remove(eventInfo.Name);

                    // 如果实例没有任何事件监听了，从字典中移除该实例
                    if (instanceEventDict.Count == 0)
                    {
                        _instanceEvents.Remove(instance);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 移除实例的所有事件监听
    /// </summary>
    public void RemoveAllInstanceListeners(object instance)
    {
        if (instance != null && _instanceEvents.ContainsKey(instance))
        {
            _instanceEvents.Remove(instance);
        }
    }

    /// <summary>
    /// 触发实例事件（泛型版本）
    /// </summary>
    public void TriggerInstanceEvent<T>(object instance, EventInfo<T> eventInfo, T args) where T : EventArgsBase
    {
        if (instance == null)
        {
            Debug.LogError("触发实例事件失败：实例不能为null");
            return;
        }

        if (eventInfo == null)
        {
            Debug.LogError("触发实例事件失败：事件信息不能为null");
            return;
        }

        if (!_allEvents.ContainsKey(eventInfo.Name))
        {
            Debug.LogError($"触发实例事件失败：事件 '{eventInfo.Name}' 未定义");
            return;
        }

        var storedEventInfo = _allEvents[eventInfo.Name];

        if (storedEventInfo.Scope != EventScope.Instance)
        {
            Debug.LogError($"触发实例事件失败：事件 '{eventInfo.Name}' 不是实例事件");
            return;
        }

        if (args != null && args.GetType() != storedEventInfo.ArgsType)
        {
            Debug.LogError($"触发事件 '{eventInfo.Name}' 失败：参数类型不匹配，预期 {storedEventInfo.ArgsType.Name}，实际 {args.GetType().Name}");
            return;
        }

        if (_instanceEvents.TryGetValue(instance, out var instanceEventDict) &&
            instanceEventDict.TryGetValue(eventInfo.Name, out var listeners))
        {
            var listenersCopy = new List<Action<EventArgsBase>>(listeners);
            foreach (var listener in listenersCopy)
            {
                try
                {
                    listener?.Invoke(args);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"处理实例事件 '{eventInfo.Name}' 时出错: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }

    /// <summary>
    /// 获取事件信息
    /// </summary>
    public EventInfo<EventArgsBase> GetEventInfo(string eventName)
    {
        _allEvents.TryGetValue(eventName, out var eventInfo);
        return eventInfo;
    }

    /// <summary>
    /// 检查事件是否已定义
    /// </summary>
    public bool IsEventDefined(string eventName)
    {
        return _allEvents.ContainsKey(eventName);
    }

    /// <summary>
    /// 获取所有已注册的事件名称
    /// </summary>
    public IEnumerable<string> GetAllEventNames()
    {
        return _allEvents.Keys;
    }
}

/// <summary>
/// 事件管理器的扩展方法，简化实例事件的使用
/// </summary>
public static class EventManagerExtensions
{
    /// <summary>
    /// 为对象实例添加事件监听（扩展方法，泛型版本）
    /// </summary>
    public static void AddInstanceListener<T>(this object instance, EventInfo<T> eventInfo, Action<T> listener) where T : EventArgsBase
    {
        EventManager.Instance.AddInstanceListener(instance, eventInfo, listener);
    }

    /// <summary>
    /// 移除对象实例的事件监听（扩展方法，泛型版本）
    /// </summary>
    public static void RemoveInstanceListener<T>(this object instance, EventInfo<T> eventInfo, Action<T> listener) where T : EventArgsBase
    {
        EventManager.Instance.RemoveInstanceListener(instance, eventInfo, listener);
    }

    /// <summary>
    /// 触发对象实例的事件（扩展方法，泛型版本）
    /// </summary>
    public static void TriggerInstanceEvent<T>(this object instance, EventInfo<T> eventInfo, T args) where T : EventArgsBase
    {
        EventManager.Instance.TriggerInstanceEvent(instance, eventInfo, args);
    }
}

using Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using Event;

/// <summary>
/// Actor接口，定义Actor的核心功能以及生命周期方法
/// </summary>
public interface IActor
{
    GameObject RealObject { get; }
    LocalEventManager EventManager { get; }
    bool IsActive { get; }
    bool IsDestroyed { get; }
    bool IsEditor { get; set; }

    void CreateFromPrefab(GameObject prefab, Transform parent = null);
    void AttachToExistingObject(GameObject existingObject);

    T AddComponent<T>() where T : ComponentBase, new();
    T GetComponent<T>() where T : ComponentBase;
    void RemoveComponent<T>() where T : ComponentBase;

    void BroadcastEvent<TEvt>(TEvt eventData) where TEvt : EventDefinition;

    void Activate();
    void Deactivate();

    // 添加生命周期方法声明
    void OnAwake();
    void OnStart();
    void OnUpdate(float deltaTime);
    void OnFixedUpdate(float fixedDeltaTime);
    void OnLateUpdate(float deltaTime);
    void OnDestroy();
}

/// <summary>
/// 本地事件管理器实现
/// </summary>
public class LocalEventManager
{
    // 保持原有实现不变...
    private readonly object _eventLock = new object();
    private readonly Dictionary<object, Dictionary<string, List<Action<EventDefinition>>>> _instanceEvents = new();
    private readonly Dictionary<string, List<Action<EventDefinition>>> _globalEvents = new();
    private readonly Dictionary<(Type, object, Delegate), Action<EventDefinition>> _listenerMap = new();

    public void AddListener<TEvt>(object instance, Action<TEvt> listener) where TEvt : EventDefinition, new()
    {
        if (listener == null || instance == null) return;

        var evt = new TEvt();
        var eventName = evt.EventName;
        var key = (typeof(TEvt), instance, listener);

        lock (_eventLock)
        {
            if (!_listenerMap.ContainsKey(key))
            {
                Action<EventDefinition> baseListener = args => listener((TEvt)args);
                _listenerMap[key] = baseListener;

                if (evt.Scope == EventScope.Global)
                {
                    if (!_globalEvents.ContainsKey(eventName))
                    {
                        _globalEvents[eventName] = new List<Action<EventDefinition>>();
                    }
                    _globalEvents[eventName].Add(baseListener);
                }
                else
                {
                    if (!_instanceEvents.ContainsKey(instance))
                    {
                        _instanceEvents[instance] = new Dictionary<string, List<Action<EventDefinition>>>();
                    }
                    var instanceDict = _instanceEvents[instance];
                    if (!instanceDict.ContainsKey(eventName))
                    {
                        instanceDict[eventName] = new List<Action<EventDefinition>>();
                    }
                    instanceDict[eventName].Add(baseListener);
                }
            }
        }
    }

    public void RemoveListener<TEvt>(object instance, Action<TEvt> listener) where TEvt : EventDefinition, new()
    {
        if (listener == null || instance == null) return;

        var evt = new TEvt();
        var eventName = evt.EventName;
        var key = (typeof(TEvt), instance, listener);

        lock (_eventLock)
        {
            if (_listenerMap.TryGetValue(key, out var baseListener))
            {
                if (evt.Scope == EventScope.Global)
                {
                    if (_globalEvents.TryGetValue(eventName, out var globalListeners))
                    {
                        globalListeners.Remove(baseListener);
                    }
                }
                else
                {
                    if (_instanceEvents.TryGetValue(instance, out var instanceDict) &&
                        instanceDict.TryGetValue(eventName, out var instanceListeners))
                    {
                        instanceListeners.Remove(baseListener);
                    }
                }
                _listenerMap.Remove(key);
            }
        }
    }

    public void Broadcast<TEvt>(TEvt eventData) where TEvt : EventDefinition
    {
        if (eventData == null) return;

        var eventName = eventData.EventName;
        var listenersCopy = new List<Action<EventDefinition>>();

        lock (_eventLock)
        {
            if (eventData.Scope == EventScope.Global)
            {
                if (_globalEvents.TryGetValue(eventName, out var globalListeners))
                {
                    listenersCopy.AddRange(globalListeners);
                }
            }
            else
            {
                foreach (var instanceDict in _instanceEvents.Values)
                {
                    if (instanceDict.TryGetValue(eventName, out var instanceListeners))
                    {
                        listenersCopy.AddRange(instanceListeners);
                    }
                }
            }
        }

        foreach (var listener in listenersCopy)
        {
            try
            {
                listener?.Invoke(eventData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"事件广播失败 {eventName}: {ex.Message}");
            }
        }
    }

    public void RemoveAllListeners(object instance)
    {
        if (instance == null) return;

        lock (_eventLock)
        {
            if (_instanceEvents.TryGetValue(instance, out var instanceDict))
            {
                var keysToRemove = _listenerMap.Where(kv => kv.Key.Item2 == instance).Select(kv => kv.Key).ToList();
                foreach (var key in keysToRemove)
                {
                    _listenerMap.Remove(key);
                }
                _instanceEvents.Remove(instance);
            }

            foreach (var eventListeners in _globalEvents.Values)
            {
                var listenersToRemove = eventListeners.Where(l => l.Target == instance).ToList();
                foreach (var listener in listenersToRemove)
                {
                    eventListeners.Remove(listener);
                }
            }
        }
    }
}

/// <summary>
/// Actor基类，实现IActor接口并关联MonoBehaviour生命周期
/// </summary>
public class ActorBase : IActor
{
    protected GameObject _realObject;
    public GameObject RealObject => _realObject;

    private readonly ComponentCollection _components = new ComponentCollection();
    private readonly object _componentLock = new object();

    public LocalEventManager EventManager { get; } = new LocalEventManager();

    public bool IsActive { get; protected set; } = true;
    public bool IsDestroyed { get; private set; } = false;

    private bool _isEditor;
    public bool IsEditor
    {
        get => _isEditor;
        set
        {
            if (_isEditor == value || IsDestroyed) return;
            _isEditor = value;
            lock (_componentLock)
            {
                foreach (var component in _components)
                {
                    ((IComponent)component).IsEditor = value;
                }
            }
        }
    }

    private bool _isAwakeCalled = false;
    private bool _isStartCalled = false;

    private ComponentBase[] _sortedComponentsCache;
    private bool _needsSort = true;

    public void CreateFromPrefab(GameObject prefab, Transform parent = null)
    {
        if (IsDestroyed) return;
        if (prefab == null)
        {
            Debug.LogError("创建Actor失败，预制体不能为空");
            return;
        }

        BeforeGetRealObject();
        _realObject = UnityEngine.Object.Instantiate(prefab, parent);
        AfterGetPrefab(prefab);

    }

    public void AttachToExistingObject(GameObject existingObject)
    {
        if (IsDestroyed || existingObject == null) return;

        BeforeGetRealObject();
        _realObject = existingObject;
        AfterGetPrefab(null);
    }

    protected virtual void BeforeGetRealObject()
    {
        Debug.Log($"[{GetType().Name}] 准备获取真实对象");

        lock (_componentLock)
        {
            foreach (var component in _components)
            {
                component.InternalBeforeGetRealObject();
            }
        }
    }

    protected virtual void AfterGetPrefab(GameObject prefab)
    {
        Debug.Log($"[{GetType().Name}] 已获取{(prefab != null ? "预制体" : "真实对象")}");

        lock (_componentLock)
        {
            foreach (var component in _components)
            {
                component.InternalAfterGetPrefab(prefab);
            }
        }
    }

    protected virtual void BeforeDestroyRealObject()
    {
        if (_realObject != null)
        {
            Debug.Log($"[{GetType().Name}] 准备销毁真实对象: {_realObject.name}");
        }

        lock (_componentLock)
        {
            foreach (var component in _components)
            {
                component.InternalBeforeDestroyRealObject();
            }
        }
    }

    protected virtual void AfterDestroyRealObject()
    {
        Debug.Log($"[{GetType().Name}] 真实对象销毁完成");

        lock (_componentLock)
        {
            foreach (var component in _components)
            {
                component.InternalAfterDestroyRealObject();
            }
        }
    }

    public T AddComponent<T>() where T : ComponentBase, new()
    {
        if (IsDestroyed)
        {
            Debug.LogError("无法向已销毁的Actor添加组件");
            return null;
        }

        lock (_componentLock)
        {
            var componentType = typeof(T);
            if (_components.Contains(componentType))
            {
                Debug.LogWarning($"组件 {componentType.Name} 已添加到Actor");
                return (T)_components[componentType];
            }

            var component = new T();
            ((IComponent)component).Owner = this;
            ((IComponent)component).IsEditor = _isEditor;
            _components.Add(component);
            component.OnAddedToActor();

            if (_isAwakeCalled) component.InternalAwake();
            if (_isStartCalled) component.InternalStart();

            _needsSort = true;
            return component;
        }
    }

    public T GetComponent<T>() where T : ComponentBase
    {
        lock (_componentLock)
        {
            var componentType = typeof(T);
            return _components.TryGetComponent(componentType, out var component) ? component as T : null;
        }
    }

    public void RemoveComponent<T>() where T : ComponentBase
    {
        if (IsDestroyed) return;

        lock (_componentLock)
        {
            var componentType = typeof(T);
            if (_components.TryGetComponent(componentType, out var component))
            {
                component.OnRemovedFromActor();
                EventManager.RemoveAllListeners(component);
                component.InternalDestroy();
                _components.Remove(componentType);
                ((IComponent)component).Owner = null;
                _needsSort = true;
            }
        }
    }

    public void BroadcastEvent<TEvt>(TEvt eventData) where TEvt : EventDefinition
    {
        if (!IsActive || IsDestroyed || eventData == null) return;
        EventManager.Broadcast(eventData);
    }

    public virtual void Activate()
    {
        if (IsActive || IsDestroyed) return;

        IsActive = true;
        lock (_componentLock)
        {
            foreach (var component in _components)
            {
                component.Activate();
            }
        }
    }

    public virtual void Deactivate()
    {
        if (!IsActive || IsDestroyed) return;

        IsActive = false;
        lock (_componentLock)
        {
            foreach (var component in _components)
            {
                component.Deactivate();
            }
        }
    }

    // 实现IActor接口的生命周期方法
    public virtual void OnAwake()
    {
        if (_isAwakeCalled || IsDestroyed) return;
        lock (_componentLock)
        {
            foreach (var component in _components)
            {
                component.InternalAwake();
            }
        }
        _isAwakeCalled = true;
    }

    public virtual void OnStart()
    {
        if (_isStartCalled || !_isAwakeCalled || IsDestroyed) return;
        lock (_componentLock)
        {
            foreach (var component in _components)
            {
                component.InternalStart();
            }
        }
        _isStartCalled = true;
    }

    public virtual void OnUpdate(float deltaTime)
    {
        if (!IsActive || IsDestroyed || !_isStartCalled) return;

        foreach (var component in GetSortedComponents())
        {
            component.InternalUpdate(Time.deltaTime);
        }
    }

    public virtual void OnFixedUpdate(float fixedDeltaTime)
    {
        if (!IsActive || IsDestroyed || !_isStartCalled) return;
        foreach (var component in GetSortedComponents())
        {
            component.InternalFixedUpdate(Time.fixedDeltaTime);
        }
    }

    public virtual void OnLateUpdate(float deltaTime)
    {
        if (!IsActive || IsDestroyed || !_isStartCalled) return;
        foreach (var component in GetSortedComponents())
        {
            component.InternalLateUpdate(Time.deltaTime);
        }
    }

    public virtual void OnDestroy()
    {
        if (IsDestroyed) return;

        BeforeDestroyRealObject();

        IsDestroyed = true;
        IsActive = false;

        lock (_componentLock)
        {
            foreach (var component in _components)
            {
                component.InternalDestroy();
                ((IComponent)component).Owner = null;
            }
            _components.Clear();
        }

        if (_realObject != null)
        {
            if (IsEditor)
                UnityEngine.Object.DestroyImmediate(_realObject);
            else
                UnityEngine.Object.Destroy(_realObject);
            _realObject = null;
        }

        AfterDestroyRealObject();
        EventManager.RemoveAllListeners(this);
    }

    private ComponentBase[] GetSortedComponents()
    {
        lock (_componentLock)
        {
            if (_needsSort || _sortedComponentsCache == null)
            {
                _sortedComponentsCache = _components
                    .OrderByDescending(c => c.UpdatePriority)
                    .ToArray();
                _needsSort = false;
            }
            return _sortedComponentsCache;
        }
    }

    private class ComponentCollection : KeyedCollection<Type, ComponentBase>
    {
        protected override Type GetKeyForItem(ComponentBase item)
        {
            return item.GetType();
        }

        public bool TryGetComponent(Type type, out ComponentBase component)
        {
            if (Contains(type))
            {
                component = this[type];
                return true;
            }
            component = null;
            return false;
        }
    }
}
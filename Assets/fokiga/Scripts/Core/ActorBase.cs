using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using Fokiga.Runtime.Core;

namespace Fokiga.Runtime.Core
{
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
        private readonly object mEventLock = new object();
        private readonly Dictionary<object, Dictionary<string, List<Action<EventDefinition>>>> mInstanceEvents = new();
        private readonly Dictionary<string, List<Action<EventDefinition>>> mGlobalEvents = new();
        private readonly Dictionary<(Type, object, Delegate), Action<EventDefinition>> mListenerMap = new();

        public void AddListener<TEvt>(object instance, Action<TEvt> listener) where TEvt : EventDefinition, new()
        {
            if (listener == null || instance == null) return;

            var evt = new TEvt();
            var eventName = evt.EventName;
            var key = (typeof(TEvt), instance, listener);

            lock (mEventLock)
            {
                if (!mListenerMap.ContainsKey(key))
                {
                    Action<EventDefinition> baseListener = args => listener((TEvt)args);
                    mListenerMap[key] = baseListener;

                    if (evt.Scope == EventScope.Global)
                    {
                        if (!mGlobalEvents.ContainsKey(eventName))
                        {
                            mGlobalEvents[eventName] = new List<Action<EventDefinition>>();
                        }
                        mGlobalEvents[eventName].Add(baseListener);
                    }
                    else
                    {
                        if (!mInstanceEvents.ContainsKey(instance))
                        {
                            mInstanceEvents[instance] = new Dictionary<string, List<Action<EventDefinition>>>();
                        }
                        var instanceDict = mInstanceEvents[instance];
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

            lock (mEventLock)
            {
                if (mListenerMap.TryGetValue(key, out var baseListener))
                {
                    if (evt.Scope == EventScope.Global)
                    {
                        if (mGlobalEvents.TryGetValue(eventName, out var globalListeners))
                        {
                            globalListeners.Remove(baseListener);
                        }
                    }
                    else
                    {
                        if (mInstanceEvents.TryGetValue(instance, out var instanceDict) &&
                        instanceDict.TryGetValue(eventName, out var instanceListeners))
                        {
                            instanceListeners.Remove(baseListener);
                        }
                    }
                    mListenerMap.Remove(key);
                }
            }
        }

        public void Broadcast<TEvt>(TEvt eventData) where TEvt : EventDefinition
        {
            if (eventData == null) return;

            var eventName = eventData.EventName;
            var listenersCopy = new List<Action<EventDefinition>>();

            lock (mEventLock)
            {
                if (eventData.Scope == EventScope.Global)
                {
                    if (mGlobalEvents.TryGetValue(eventName, out var globalListeners))
                    {
                        listenersCopy.AddRange(globalListeners);
                    }
                }
                else
                {
                    foreach (var instanceDict in mInstanceEvents.Values)
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

            lock (mEventLock)
            {
                if (mInstanceEvents.TryGetValue(instance, out var instanceDict))
                {
                    var keysToRemove = mListenerMap.Where(kv => kv.Key.Item2 == instance).Select(kv => kv.Key).ToList();
                    foreach (var key in keysToRemove)
                    {
                        mListenerMap.Remove(key);
                    }
                    mInstanceEvents.Remove(instance);
                }

                foreach (var eventListeners in mGlobalEvents.Values)
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
        protected GameObject mRealObject;
        public GameObject RealObject => mRealObject;

        private readonly ComponentCollection mComponents = new ComponentCollection();
        private readonly object mComponentLock = new object();

        public LocalEventManager EventManager { get; } = new LocalEventManager();

        public bool IsActive { get; protected set; } = true;
        public bool IsDestroyed { get; private set; } = false;

        private bool mIsEditor;
        public bool IsEditor
        {
            get => mIsEditor;
            set
            {
                if (mIsEditor == value || IsDestroyed) return;
                mIsEditor = value;
                lock (mComponentLock)
                {
                    foreach (var component in mComponents)
                    {
                        ((IComponent)component).IsEditor = value;
                    }
                }
            }
        }

        private bool mIsAwakeCalled = false;
        private bool mIsStartCalled = false;

        private ComponentBase[] mSortedComponentsCache;
        private bool mNeedsSort = true;

        public void CreateFromPrefab(GameObject prefab, Transform parent = null)
        {
            if (IsDestroyed) return;
            if (prefab == null)
            {
                Debug.LogError("创建Actor失败，预制体不能为空");
                return;
            }

            BeforeGetRealObject();
            mRealObject = UnityEngine.Object.Instantiate(prefab, parent);
            AfterGetPrefab(prefab);

        }

        public void AttachToExistingObject(GameObject existingObject)
        {
            if (IsDestroyed || existingObject == null) return;

            BeforeGetRealObject();
            mRealObject = existingObject;
            AfterGetPrefab(null);
        }

        protected virtual void BeforeGetRealObject()
        {
            Debug.Log($"[{GetType().Name}] 准备获取真实对象");

            lock (mComponentLock)
            {
                foreach (var component in mComponents)
                {
                    component.InternalBeforeGetRealObject();
                }
            }
        }

        protected virtual void AfterGetPrefab(GameObject prefab)
        {
            Debug.Log($"[{GetType().Name}] 已获取{(prefab != null ? "预制体" : "真实对象")}");

            lock (mComponentLock)
            {
                foreach (var component in mComponents)
                {
                    component.InternalAfterGetPrefab(prefab);
                }
            }
        }

        protected virtual void BeforeDestroyRealObject()
        {
            if (mRealObject != null)
            {
                Debug.Log($"[{GetType().Name}] 准备销毁真实对象: {mRealObject.name}");
            }

            lock (mComponentLock)
            {
                foreach (var component in mComponents)
                {
                    component.InternalBeforeDestroyRealObject();
                }
            }
        }

        protected virtual void AfterDestroyRealObject()
        {
            Debug.Log($"[{GetType().Name}] 真实对象销毁完成");

            lock (mComponentLock)
            {
                foreach (var component in mComponents)
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

            lock (mComponentLock)
            {
                var componentType = typeof(T);
                if (mComponents.Contains(componentType))
                {
                    Debug.LogWarning($"组件 {componentType.Name} 已添加到Actor");
                    return (T)mComponents[componentType];
                }

                var component = new T();
                ((IComponent)component).Owner = this;
                ((IComponent)component).IsEditor = mIsEditor;
                mComponents.Add(component);
                component.OnAddedToActor();

                if (mIsAwakeCalled) component.InternalAwake();
                if (mIsStartCalled) component.InternalStart();

                mNeedsSort = true;
                return component;
            }
        }

        public T GetComponent<T>() where T : ComponentBase
        {
            lock (mComponentLock)
            {
                var componentType = typeof(T);
                return mComponents.TryGetComponent(componentType, out var component) ? component as T : null;
            }
        }

        public void RemoveComponent<T>() where T : ComponentBase
        {
            if (IsDestroyed) return;

            lock (mComponentLock)
            {
                var componentType = typeof(T);
                if (mComponents.TryGetComponent(componentType, out var component))
                {
                    component.OnRemovedFromActor();
                    EventManager.RemoveAllListeners(component);
                    component.InternalDestroy();
                    mComponents.Remove(componentType);
                    ((IComponent)component).Owner = null;
                    mNeedsSort = true;
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
            lock (mComponentLock)
            {
                foreach (var component in mComponents)
                {
                    component.Activate();
                }
            }
        }

        public virtual void Deactivate()
        {
            if (!IsActive || IsDestroyed) return;

            IsActive = false;
            lock (mComponentLock)
            {
                foreach (var component in mComponents)
                {
                    component.Deactivate();
                }
            }
        }

        // 实现IActor接口的生命周期方法
        public virtual void OnAwake()
        {
            if (mIsAwakeCalled || IsDestroyed) return;
            lock (mComponentLock)
            {
                foreach (var component in mComponents)
                {
                    component.InternalAwake();
                }
            }
            mIsAwakeCalled = true;
        }

        public virtual void OnStart()
        {
            if (mIsStartCalled || !mIsAwakeCalled || IsDestroyed) return;
            lock (mComponentLock)
            {
                foreach (var component in mComponents)
                {
                    component.InternalStart();
                }
            }
            mIsStartCalled = true;
        }

        public virtual void OnUpdate(float deltaTime)
        {
            if (!IsActive || IsDestroyed || !mIsStartCalled) return;

            foreach (var component in GetSortedComponents())
            {
                component.InternalUpdate(Time.deltaTime);
            }
        }

        public virtual void OnFixedUpdate(float fixedDeltaTime)
        {
            if (!IsActive || IsDestroyed || !mIsStartCalled) return;
            foreach (var component in GetSortedComponents())
            {
                component.InternalFixedUpdate(Time.fixedDeltaTime);
            }
        }

        public virtual void OnLateUpdate(float deltaTime)
        {
            if (!IsActive || IsDestroyed || !mIsStartCalled) return;
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

            lock (mComponentLock)
            {
                foreach (var component in mComponents)
                {
                    component.InternalDestroy();
                    ((IComponent)component).Owner = null;
                }
                mComponents.Clear();
            }

            if (mRealObject != null)
            {
                if (IsEditor)
                    UnityEngine.Object.DestroyImmediate(mRealObject);
                else
                    UnityEngine.Object.Destroy(mRealObject);
                mRealObject = null;
            }

            AfterDestroyRealObject();
            EventManager.RemoveAllListeners(this);
        }

        private ComponentBase[] GetSortedComponents()
        {
            lock (mComponentLock)
            {
                if (mNeedsSort || mSortedComponentsCache == null)
                {
                    mSortedComponentsCache = mComponents
                    .OrderByDescending(c => c.UpdatePriority)
                    .ToArray();
                    mNeedsSort = false;
                }
                return mSortedComponentsCache;
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
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Event
{
    /// <summary>
    /// 局部事件管理器实现
    /// </summary>
    public class LocalEventManager
    {
        // 使用更高效的数据结构：实例 -> (事件名称 -> 监听器列表)
        private readonly Dictionary<object, Dictionary<string, List<Action<EventDefinition>>>> _localEvents =
            new Dictionary<object, Dictionary<string, List<Action<EventDefinition>>>>();

        // 缓存事件类型信息以避免重复创建实例
        private static readonly Dictionary<Type, (string EventName, EventScope Scope)> EventTypeCache =
            new Dictionary<Type, (string, EventScope)>();

        // 构造函数
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
        /// 获取事件类型的元数据（名称和范围）
        /// </summary>
        private static (string eventName, EventScope scope) GetEventTypeMetadata<TEvt>() where TEvt : EventDefinition, new()
        {
            Type eventType = typeof(TEvt);

            if (!EventTypeCache.TryGetValue(eventType, out var metadata))
            {
                var eventInstance = new TEvt();
                metadata = (eventInstance.EventName, eventInstance.Scope);
                EventTypeCache[eventType] = metadata;
            }

            return metadata;
        }

        /// <summary>
        /// 为指定实例添加事件监听器
        /// </summary>
        public void AddListener<TEvt>(object instance, Action<TEvt> listener) where TEvt : EventDefinition, new()
        {
            if (instance == null)
            {
                Debug.LogError("添加局部事件失败，实例不能为null");
                return;
            }

            if (listener == null)
            {
                Debug.LogError("添加局部事件失败，监听器不能为null");
                return;
            }

            var (eventName, scope) = GetEventTypeMetadata<TEvt>();

            if (!EventDefinitionCache.TryGetEventType(eventName, out var eventType) || eventType != typeof(TEvt))
            {
                Debug.LogError($"添加局部事件失败，事件 '{eventName}' 未注册或类型不匹配");
                return;
            }

            if (scope != EventScope.Instance)
            {
                Debug.LogError($"添加局部事件失败，事件 '{eventName}' 不是局部事件");
                return;
            }

            Action<EventDefinition> baseListener = args => listener((TEvt)args);

            // 使用更简洁的字典访问方式
            if (!_localEvents.TryGetValue(instance, out var instanceEventDict))
            {
                instanceEventDict = new Dictionary<string, List<Action<EventDefinition>>>();
                _localEvents[instance] = instanceEventDict;
            }

            if (!instanceEventDict.TryGetValue(eventName, out var listeners))
            {
                listeners = new List<Action<EventDefinition>>();
                instanceEventDict[eventName] = listeners;
            }

            // 避免重复添加相同的监听器
            if (!listeners.Contains(baseListener))
            {
                listeners.Add(baseListener);
            }
            else
            {
                Debug.LogWarning($"已存在相同的事件监听器，事件: {eventName}, 实例: {instance}");
            }
        }

        /// <summary>
        /// 从指定实例移除事件监听器
        /// </summary>
        public void RemoveListener<TEvt>(object instance, Action<TEvt> listener) where TEvt : EventDefinition, new()
        {
            if (instance == null || listener == null) return;

            var (eventName, _) = GetEventTypeMetadata<TEvt>();
            Action<EventDefinition> baseListener = args => listener((TEvt)args);

            if (_localEvents.TryGetValue(instance, out var instanceEventDict) &&
                instanceEventDict.TryGetValue(eventName, out var listeners))
            {
                int removedCount = listeners.RemoveAll(l => l == baseListener);

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
            if (instance != null)
            {
                _localEvents.Remove(instance);
            }
        }

        /// <summary>
        /// 向指定的目标实例广播局部事件
        /// </summary>
        public void Broadcast<TEvt>(object instance, TEvt eventData) where TEvt : EventDefinition
        {
            if (instance == null)
            {
                Debug.LogError("广播局部事件失败，实例不能为null");
                return;
            }

            if (eventData == null)
            {
                Debug.LogError("广播局部事件失败，事件数据不能为null");
                return;
            }

            string eventName = eventData.EventName;

            if (!EventDefinitionCache.TryGetEventType(eventName, out var eventType) || eventType != typeof(TEvt))
            {
                Debug.LogError($"广播局部事件失败，事件 '{eventName}' 未注册或类型不匹配");
                return;
            }

            if (eventData.Scope != EventScope.Instance)
            {
                Debug.LogError($"广播局部事件失败，事件 '{eventName}' 不是局部事件");
                return;
            }

            if (_localEvents.TryGetValue(instance, out var instanceEventDict) &&
                instanceEventDict.TryGetValue(eventName, out var listeners))
            {
                // 创建副本以避免在迭代过程中修改集合
                var listenersCopy = listeners.ToArray();

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

        /// <summary>
        /// 清除所有事件监听器
        /// </summary>
        public void Clear()
        {
            _localEvents.Clear();
        }

        /// <summary>
        /// 获取指定实例的事件监听器数量
        /// </summary>
        public int GetListenerCount(object instance, string eventName = null)
        {
            if (instance == null || !_localEvents.TryGetValue(instance, out var instanceEventDict))
                return 0;

            if (string.IsNullOrEmpty(eventName))
            {
                int total = 0;
                foreach (var listener in instanceEventDict.Values)
                {
                    total += listener.Count;
                }
                return total;
            }

            return instanceEventDict.TryGetValue(eventName, out var listeners) ? listeners.Count : 0;
        }
    }

    /// <summary>
    /// 局部事件扩展方法（方便使用实例调用）
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

        /// <summary>
        /// 获取当前实例的事件监听器数量
        /// </summary>
        public static int GetLocalListenerCount(this object instance, LocalEventManager manager, string eventName = null)
        {
            return manager.GetListenerCount(instance, eventName);
        }
    }
}
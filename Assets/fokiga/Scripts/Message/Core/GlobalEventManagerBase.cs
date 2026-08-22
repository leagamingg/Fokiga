using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fokiga.Runtime.Core
{
    /// <summary>
    /// 全局事件管理器实现
    /// </summary>
    public class GlobalEventManager
    {
        // 单例实例
        private static GlobalEventManager mInstance;
        private static readonly object mLock = new object();

        // 全局事件存储：事件名称 -> 监听器列表
        private readonly Dictionary<string, List<Action<EventDefinition>>> mGlobalEvents =
        new Dictionary<string, List<Action<EventDefinition>>>();

        public static GlobalEventManager Instance
        {
            get
            {
                if (mInstance == null)
                {
                    lock (mLock)
                    {
                        if (mInstance == null)
                        {
                            mInstance = new GlobalEventManager();
                            mInstance.Initialize();
                        }
                    }
                }
                return mInstance;
            }
        }

        private GlobalEventManager() { }

        private void Initialize()
        {
            EventDefinitionCache.Initialize();
            Debug.Log("全局事件管理器初始化完成");
        }

        /// <summary>
        /// 添加全局事件监听器
        /// </summary>
        public void AddListener<TEvt>(Action<TEvt> listener) where TEvt : EventDefinition, new()
        {
            var eventInstance = new TEvt();
            var eventName = eventInstance.EventName;

            if (!EventDefinitionCache.TryGetEventType(eventName, out var eventType) || eventType != typeof(TEvt))
            {
                Debug.LogError($"添加全局事件失败，事件 '{eventName}' 未注册或类型不匹配");
                return;
            }

            if (eventInstance.Scope != EventScope.Global)
            {
                Debug.LogError($"添加全局事件失败，事件 '{eventName}' 不是全局事件");
                return;
            }

            Action<EventDefinition> baseListener = args => listener((TEvt)args);

            if (!mGlobalEvents.ContainsKey(eventName))
            {
                mGlobalEvents[eventName] = new List<Action<EventDefinition>>();
            }

            if (!mGlobalEvents[eventName].Contains(baseListener))
            {
                mGlobalEvents[eventName].Add(baseListener);
            }
        }

        /// <summary>
        /// 移除全局事件监听器
        /// </summary>
        public void RemoveListener<TEvt>(Action<TEvt> listener) where TEvt : EventDefinition, new()
        {
            var eventInstance = new TEvt();
            var eventName = eventInstance.EventName;

            Action<EventDefinition> baseListener = args => listener((TEvt)args);

            if (mGlobalEvents.ContainsKey(eventName) && mGlobalEvents[eventName].Contains(baseListener))
            {
                mGlobalEvents[eventName].Remove(baseListener);

                if (mGlobalEvents[eventName].Count == 0)
                {
                    mGlobalEvents.Remove(eventName);
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
                Debug.LogError("广播全局事件失败，事件数据不能为null");
                return;
            }

            var eventName = eventData.EventName;

            if (!EventDefinitionCache.TryGetEventType(eventName, out var eventType) || eventType != typeof(TEvt))
            {
                Debug.LogError($"广播全局事件失败，事件 '{eventName}' 未注册或类型不匹配");
                return;
            }

            if (eventData.Scope != EventScope.Global)
            {
                Debug.LogError($"广播全局事件失败，事件 '{eventName}' 不是全局事件");
                return;
            }

            if (mGlobalEvents.TryGetValue(eventName, out var listeners))
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
                        Debug.LogError($"执行全局事件 '{eventName}' 时出错: {ex.Message}\n{ex.StackTrace}");
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
    /// 全局事件扩展方法（语法糖）
    /// </summary>
    public static class GlobalEventExtensions
    {
        // 原有的扩展方法实现
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

        // 新增扩展方法：直接将 MonoBehaviour 作为订阅者对象
        public static void AddGlobalListener<TEvt>(this MonoBehaviour sender, Action<TEvt> listener)
        where TEvt : EventDefinition, new()
        {
            GlobalEventManager.Instance.AddListener(listener);
        }

        // 新增扩展方法：移除订阅
        public static void RemoveGlobalListener<TEvt>(this MonoBehaviour sender, Action<TEvt> listener)
        where TEvt : EventDefinition, new()
        {
            GlobalEventManager.Instance.RemoveListener(listener);
        }
    }
}

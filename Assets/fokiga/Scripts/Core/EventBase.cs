using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fokiga.Runtime.Core
{
    /// <summary>
    /// 事件作用域枚举
    /// </summary>
    public enum EventScope
    {
        Global,   // 全局事件，所有订阅者都能接收
        Instance  // 实例事件，只针对特定实例响应
    }

    /// <summary>
    /// 事件定义基类
    /// </summary>
    public abstract class EventDefinition
    {
        /// <summary>
        /// 事件名称，需保证唯一
        /// </summary>
        public abstract string EventName { get; }

        /// <summary>
        /// 事件作用域
        /// </summary>
        public abstract EventScope Scope { get; }
    }

    /// <summary>
    /// 事件定义缓存（内部使用）
    /// </summary>
    internal static class EventDefinitionCache
    {
        private static readonly Dictionary<string, Type> _allEventTypes = new Dictionary<string, Type>();
        private static bool _isInitialized;

        internal static void Initialize()
        {
            if (_isInitialized) return;

            DiscoverEventDefinitions();

            // 使用条件编译来处理不同环境下的日志输出
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
                    // 使用条件编译来处理不同环境下的日志输出
                    Debug.LogWarning($"扫描获取事件定义时出错: {ex.Message}");

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
}

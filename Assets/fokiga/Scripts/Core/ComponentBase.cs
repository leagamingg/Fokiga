using System;
using System.Collections.Generic;
using UnityEngine;
using Fokiga.Runtime.Core;

namespace Fokiga.Runtime.Core
{
    /// <summary>
    /// 组件接口，定义组件的核心行为和生命周期
    /// </summary>
    public interface IComponent
    {
        // 所属Actor
        ActorBase Owner { get; internal set; }

        // 状态标识
        bool IsActive { get; }
        bool Enabled { get; set; }
        bool IsEditor { get; internal set; }
        bool RunInEditor { get; set; }
        bool AutoActivate { get; set; }
        bool EnableUpdate { get; set; }
        bool EnableFixedUpdate { get; set; }
        bool EnableLateUpdate { get; set; }

        // 初始化状态
        bool IsInitialized { get; }
        bool IsUpdating { get; }

        // 生命周期方法
        void OnAddedToActor();
        void OnRemovedFromActor();
        void OnEditorModeChanged(bool isEditorMode);
        void OnAwake();
        void OnStart();
        void OnUpdate(float deltaTime);
        void OnFixedUpdate(float fixedDeltaTime);
        void OnLateUpdate(float deltaTime);
        void OnDestroy();
        void OnEnable();
        void OnDisable();

        // RealObject相关回调
        void BeforeGetRealObject();
        void AfterGetPrefab(GameObject prefab);
        void BeforeDestroyRealObject();
        void AfterDestroyRealObject();

        // 回调管理
        Guid AddUpdate(Action<float> updateFunction, int priority = 0, bool isOneShot = false);
        Guid AddFixedUpdate(Action<float> fixedUpdateFunction, int priority = 0, bool isOneShot = false);
        Guid AddLateUpdate(Action<float> lateUpdateFunction, int priority = 0, bool isOneShot = false);
        void RemoveUpdate(Guid callbackId);
        void ClearAllUpdates();

        // 激活/停止
        void Activate();
        void Deactivate();

        // 事件监听
        void AddListener<TEvt>(Action<TEvt> listener) where TEvt : EventDefinition, new();
        void RemoveListener<TEvt>(Action<TEvt> listener) where TEvt : EventDefinition, new();
    }
}


namespace Fokiga.Runtime.Core
{
    /// <summary>
    /// 组件基类，实现IComponent接口，模拟Unity的MonoBehaviour或UE的ActorComponent
    /// 提供生命周期、回调管理等基础功能
    /// </summary>
    public abstract class ComponentBase : IComponent
    {
        // 所属Actor的私有字段
        private ActorBase mOwner;

        // 显式实现IComponent的Owner属性，满足接口的internal set需求
        ActorBase IComponent.Owner
        {
            get => mOwner;
            set => mOwner = value;
        }

        // 公开的Owner属性，供外部只读访问
        public ActorBase Owner => mOwner;

        // 状态标识
        public bool IsActive { get; protected set; } = true;
        public bool Enabled { get; set; } = true;

        // 编辑模式标识
        private bool mIsEditor;

        // 显式实现IComponent的IsEditor属性，满足接口的internal set需求
        bool IComponent.IsEditor
        {
            get => mIsEditor;
            set
            {
                if (mIsEditor != value)
                {
                    mIsEditor = value;
                    OnEditorModeChanged(value);
                }
            }
        }

        // 公开的IsEditor属性，供外部只读访问
        public bool IsEditor => mIsEditor;

        public bool RunInEditor { get; set; } = false;

        // 自动激活标识
        public bool AutoActivate { get; set; } = true;

        // 更新开关
        public bool EnableUpdate { get; set; } = true;
        public bool EnableFixedUpdate { get; set; } = true;
        public bool EnableLateUpdate { get; set; } = true;

        // 组件的更新优先级，影响在Actor中的执行顺序
        public int UpdatePriority { get; set; } = 0;

        // 回调结构(包含优先级和标识等信息)
        private struct UpdateCallback
        {
            public Action<float> Function;
            public int Priority;
            public Guid Id;
            public string CallStack; // 调试时的调用栈

            public UpdateCallback(Action<float> function, int priority, Guid id, string callStack)
            {
                Function = function;
                Priority = priority;
                Id = id;
                CallStack = callStack;
            }
        }

        // 线程安全锁
        private readonly object mCallbackLock = new object();

        // 持久化回调字典(哈希表存储保证O(1)的查询效率)
        private Dictionary<Guid, UpdateCallback> mPersistentUpdates = new Dictionary<Guid, UpdateCallback>();
        private Dictionary<Guid, UpdateCallback> mPersistentFixedUpdates = new Dictionary<Guid, UpdateCallback>();
        private Dictionary<Guid, UpdateCallback> mPersistentLateUpdates = new Dictionary<Guid, UpdateCallback>();

        // 一次性回调列表(预先分配空间减少gc)
        private List<UpdateCallback> mOneShotUpdates = new List<UpdateCallback>(16);
        private List<UpdateCallback> mOneShotFixedUpdates = new List<UpdateCallback>(16);
        private List<UpdateCallback> mOneShotLateUpdates = new List<UpdateCallback>(16);

        // 排序后的回调列表(按优先级排序)
        private List<UpdateCallback> mSortedPersistentUpdates = new List<UpdateCallback>();
        private List<UpdateCallback> mSortedPersistentFixedUpdates = new List<UpdateCallback>();
        private List<UpdateCallback> mSortedPersistentLateUpdates = new List<UpdateCallback>();
        private bool mNeedsSortingUpdates = false;
        private bool mNeedsSortingFixedUpdates = false;
        private bool mNeedsSortingLateUpdates = false;

        // 初始化状态标识
        private bool mIsAwakeCalled = false;
        private bool mIsStartCalled = false;
        private bool mIsDestroyed = false;

        /// <summary>
        /// 标识是否已经完成初始化(OnAwake和OnStart均已执行)
        /// </summary>
        public bool IsInitialized => mIsAwakeCalled && mIsStartCalled;

        /// <summary>
        /// 当前是否处于更新状态
        /// </summary>
        public bool IsUpdating => mShouldUpdate && IsActive && Enabled;

        /// <summary>
        /// 当组件被添加到Actor时调用
        /// </summary>
        public virtual void OnAddedToActor()
        {
            if (AutoActivate && mOwner != null && !mOwner.IsDestroyed)
            {
                Activate();
            }

            // 补充逻辑：检查Actor是否已加载完成RealObject，若已加载则手动触发AfterGetPrefab
            if (mOwner != null && !mOwner.IsDestroyed && mOwner.RealObject != null)
            {
                // 模拟Actor已加载完prefab的场景，此时prefab参数已无实际意义，传null保持一致性
                AfterGetPrefab(null);
            }

            // 保证：在添加时补全未执行的初始化步骤
            if (mOwner != null && !mIsDestroyed)
            {
                // 补全Awake
                if (!mIsAwakeCalled)
                {
                    InternalAwake();
                }

                // 补全Start
                if (mIsAwakeCalled && !mIsStartCalled)
                {
                    InternalStart();
                }
            }
        }

        /// <summary>
        /// 当组件从Actor移除时调用
        /// </summary>
        public virtual void OnRemovedFromActor()
        {
            mOwner?.EventManager.RemoveAllListeners(this);
            ClearAllUpdates();
        }

        /// <summary>
        /// 编辑模式改变时调用
        /// </summary>
        public virtual void OnEditorModeChanged(bool isEditorMode) { }

        #region 生命周期方法

        public virtual void OnAwake() { }

        public virtual void OnStart() { }

        public virtual void OnUpdate(float deltaTime) { }

        public virtual void OnFixedUpdate(float fixedDeltaTime) { }

        public virtual void OnLateUpdate(float deltaTime) { }

        public virtual void OnDestroy() { }

        public virtual void OnEnable() { }

        public virtual void OnDisable() { }

        #endregion

        #region RealObject相关回调

        /// <summary>
        /// 在获取RealObject之前执行
        /// 用于准备获取真实对象前的初始化工作和资源预加载
        /// </summary>
        public virtual void BeforeGetRealObject() { }

        /// <summary>
        /// 当RealObject实例化完成后执行
        /// 当RealObject通过prefab创建时prefab不为null，直接附加时为null
        /// </summary>
        /// <param name="prefab">实例化使用的预制体，直接附加时为null</param>
        public virtual void AfterGetPrefab(GameObject prefab) { }

        /// <summary>
        /// 在RealObject销毁前执行
        /// 用于清理RealObject相关的状态和释放资源
        /// </summary>
        public virtual void BeforeDestroyRealObject() { }

        /// <summary>
        /// 在RealObject销毁后执行
        /// 用于处理销毁后的收尾工作和通知系统释放剩余资源
        /// </summary>
        public virtual void AfterDestroyRealObject() { }

        #endregion

        #region 动态更新回调管理

        /// <summary>
        /// 添加帧更新回调
        /// </summary>
        /// <param name="updateFunction">更新函数</param>
        /// <param name="priority">优先级(值越大越先执行)</param>
        /// <param name="isOneShot">是否只执行一次</param>
        /// <returns>回调ID，用于移除</returns>
        public Guid AddUpdate(Action<float> updateFunction, int priority = 0, bool isOneShot = false)
        {
            return AddCallback(
            updateFunction,
            priority,
            isOneShot,
            mPersistentUpdates,
            mOneShotUpdates,
            ref mNeedsSortingUpdates
            );
        }

        /// <summary>
        /// 添加固定时间间隔更新回调
        /// </summary>
        public Guid AddFixedUpdate(Action<float> fixedUpdateFunction, int priority = 0, bool isOneShot = false)
        {
            return AddCallback(
            fixedUpdateFunction,
            priority,
            isOneShot,
            mPersistentFixedUpdates,
            mOneShotFixedUpdates,
            ref mNeedsSortingFixedUpdates
            );
        }

        /// <summary>
        /// 添加延迟更新回调
        /// </summary>
        public Guid AddLateUpdate(Action<float> lateUpdateFunction, int priority = 0, bool isOneShot = false)
        {
            return AddCallback(
            lateUpdateFunction,
            priority,
            isOneShot,
            mPersistentLateUpdates,
            mOneShotLateUpdates,
            ref mNeedsSortingLateUpdates
            );
        }

        /// <summary>
        /// 通过ID移除更新回调
        /// </summary>
        public void RemoveUpdate(Guid callbackId)
        {
            lock (mCallbackLock)
            {
                mPersistentUpdates.Remove(callbackId);
                mPersistentFixedUpdates.Remove(callbackId);
                mPersistentLateUpdates.Remove(callbackId);

                mNeedsSortingUpdates = true;
                mNeedsSortingFixedUpdates = true;
                mNeedsSortingLateUpdates = true;
            }
        }

        /// <summary>
        /// 清除所有更新回调
        /// </summary>
        public void ClearAllUpdates()
        {
            lock (mCallbackLock)
            {
                mPersistentUpdates.Clear();
                mPersistentFixedUpdates.Clear();
                mPersistentLateUpdates.Clear();

                mOneShotUpdates.Clear();
                mOneShotFixedUpdates.Clear();
                mOneShotLateUpdates.Clear();

                mSortedPersistentUpdates.Clear();
                mSortedPersistentFixedUpdates.Clear();
                mSortedPersistentLateUpdates.Clear();

                mNeedsSortingUpdates = false;
                mNeedsSortingFixedUpdates = false;
                mNeedsSortingLateUpdates = false;
            }
        }

        #endregion

        #region 激活/停止控制

        public virtual void Activate()
        {
            if (IsActive) return;

            IsActive = true;
            OnEnable();
        }

        public virtual void Deactivate()
        {
            if (!IsActive) return;

            IsActive = false;
            OnDisable();
        }

        #endregion

        #region 事件监听

        public void AddListener<TEvt>(Action<TEvt> listener) where TEvt : EventDefinition, new()
        {
            mOwner?.EventManager.AddListener(this, listener);
        }

        public void RemoveListener<TEvt>(Action<TEvt> listener) where TEvt : EventDefinition, new()
        {
            mOwner?.EventManager.RemoveListener(this, listener);
        }

        #endregion

        #region 内部使用的生命周期方法

        internal void InternalAwake()
        {
            // 强制检查所属Actor和组件状态
            if (!mIsAwakeCalled && !mIsDestroyed && mOwner != null && !mOwner.IsDestroyed)
            {
                OnAwake();
                mIsAwakeCalled = true;
            }
        }

        internal void InternalStart()
        {
            // 强制检查所属Actor和组件状态
            if (!mIsStartCalled && mIsAwakeCalled && !mIsDestroyed && mOwner != null && !mOwner.IsDestroyed)
            {
                OnStart();
                mIsStartCalled = true;
            }
        }

        internal void InternalUpdate(float deltaTime)
        {
            if (!EnableUpdate) return;

            mShouldUpdate = ShouldExecuteUpdate();
            if (mShouldUpdate)
            {
                OnUpdate(deltaTime);
                ExecuteCallbacks(
                mPersistentUpdates,
                mOneShotUpdates,
                mSortedPersistentUpdates,
                ref mNeedsSortingUpdates,
                deltaTime
                );
            }
        }

        internal void InternalFixedUpdate(float fixedDeltaTime)
        {
            if (!EnableFixedUpdate) return;

            mShouldUpdate = ShouldExecuteUpdate();
            if (mShouldUpdate)
            {
                OnFixedUpdate(fixedDeltaTime);
                ExecuteCallbacks(
                mPersistentFixedUpdates,
                mOneShotFixedUpdates,
                mSortedPersistentFixedUpdates,
                ref mNeedsSortingFixedUpdates,
                fixedDeltaTime
                );
            }
        }

        internal void InternalLateUpdate(float deltaTime)
        {
            if (!EnableLateUpdate) return;

            mShouldUpdate = ShouldExecuteUpdate();
            if (mShouldUpdate)
            {
                OnLateUpdate(deltaTime);
                ExecuteCallbacks(
                mPersistentLateUpdates,
                mOneShotLateUpdates,
                mSortedPersistentLateUpdates,
                ref mNeedsSortingLateUpdates,
                deltaTime
                );
            }
        }

        internal void InternalDestroy()
        {
            if (!mIsDestroyed)
            {
                OnDestroy();

                // 清理并释放资源，防止内存泄漏
                ClearAllUpdates();
                mPersistentUpdates = null;
                mPersistentFixedUpdates = null;
                mPersistentLateUpdates = null;
                mOneShotUpdates = null;
                mOneShotFixedUpdates = null;
                mOneShotLateUpdates = null;
                mSortedPersistentUpdates = null;
                mSortedPersistentFixedUpdates = null;
                mSortedPersistentLateUpdates = null;

                mIsDestroyed = true;
                mIsAwakeCalled = false;
                mIsStartCalled = false;
                mOwner = null;
            }
        }

        #endregion

        #region RealObject回调的内部实现

        internal void InternalBeforeGetRealObject()
        {
            if (!mIsDestroyed && IsActive && mOwner != null && !mOwner.IsDestroyed)
            {
                BeforeGetRealObject();
            }
        }

        internal void InternalAfterGetPrefab(GameObject prefab)
        {
            if (!mIsDestroyed && IsActive && mOwner != null && !mOwner.IsDestroyed)
            {
                AfterGetPrefab(prefab);
            }
        }

        internal void InternalBeforeDestroyRealObject()
        {
            if (!mIsDestroyed && IsActive && mOwner != null && !mOwner.IsDestroyed)
            {
                BeforeDestroyRealObject();
            }
        }

        internal void InternalAfterDestroyRealObject()
        {
            if (!mIsDestroyed) // 销毁后可能还需要执行的收尾工作，不检查IsActive
            {
                AfterDestroyRealObject();
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 判断是否应该执行更新方法
        /// 强制检查依赖的Owner状态和组件自身状态
        /// </summary>
        private bool ShouldExecuteUpdate()
        {
            // 编辑模式过滤
            if (mIsEditor && !RunInEditor)
                return false;

            // 组件状态过滤
            if (!IsActive || !Enabled || !mIsStartCalled || mIsDestroyed)
                return false;

            // 所属Actor状态过滤
            if (mOwner == null || !mOwner.IsActive || mOwner.IsDestroyed)
                return false;

            return true;
        }

        /// <summary>
        /// 添加回调的通用方法
        /// </summary>
        private Guid AddCallback(
        Action<float> function,
        int priority,
        bool isOneShot,
        Dictionary<Guid, UpdateCallback> persistentCallbacks,
        List<UpdateCallback> oneShotCallbacks,
        ref bool needsSorting)
        {
            if (function == null || mIsDestroyed || mOwner?.IsDestroyed == true)
                return Guid.Empty;

            var id = Guid.NewGuid();
            string callStack = string.Empty;

            // 调试模式下记录调用栈，方便定位问题
#if DEBUG
            callStack = Environment.StackTrace;
#endif

            var callback = new UpdateCallback(function, priority, id, callStack);

            lock (mCallbackLock)
            {
                if (isOneShot)
                {
                    oneShotCallbacks.Add(callback);
                    // 一次性回调立即排序
                    oneShotCallbacks.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                }
                else
                {
                    persistentCallbacks[id] = callback;
                    needsSorting = true;
                }
            }

            return id;
        }

        /// <summary>
        /// 执行回调的通用方法
        /// </summary>
        private void ExecuteCallbacks(
        Dictionary<Guid, UpdateCallback> persistentCallbacks,
        List<UpdateCallback> oneShotCallbacks,
        List<UpdateCallback> sortedPersistentCallbacks,
        ref bool needsSorting,
        float deltaTime)
        {
            // 执行持久化回调
            if (persistentCallbacks?.Count > 0)
            {
                lock (mCallbackLock)
                {
                    // 需要时进行排序(降低排序频率)
                    if (needsSorting)
                    {
                        sortedPersistentCallbacks.Clear();
                        sortedPersistentCallbacks.AddRange(persistentCallbacks.Values);
                        sortedPersistentCallbacks.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                        needsSorting = false;
                    }
                }

                // 执行排序后的持久化回调
                for (int i = 0; i < sortedPersistentCallbacks.Count; i++)
                {
                    var callback = sortedPersistentCallbacks[i];
                    // 检查回调是否仍然存在(可能已被移除)
                    if (persistentCallbacks.TryGetValue(callback.Id, out var existing) &&
                    existing.Function == callback.Function)
                    {
                        try
                        {
                            callback.Function?.Invoke(deltaTime);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Persistent callback error: {ex.Message}\nRegistered at: {callback.CallStack}");
                        }
                    }
                }
            }

            // 执行一次性回调(倒序遍历避免删除元素时影响索引)
            if (oneShotCallbacks?.Count > 0)
            {
                lock (mCallbackLock)
                {
                    for (int i = oneShotCallbacks.Count - 1; i >= 0; i--)
                    {
                        var callback = oneShotCallbacks[i];
                        try
                        {
                            callback.Function?.Invoke(deltaTime);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"One-shot callback error: {ex.Message}\nRegistered at: {callback.CallStack}");
                        }
                        oneShotCallbacks.RemoveAt(i);
                    }
                }
            }
        }

        #endregion

        // 用于IsUpdating属性的内部标识
        private bool mShouldUpdate;
    }
}

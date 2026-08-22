using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fokiga.Runtime.Core
{
    /// <summary>
    /// Actor管理接口，定义管理Actor的通用操作
    /// </summary>
    public interface IManager
    {
        TActor CreateActorFromPrefab<TActor>(GameObject prefab, Transform parent = null, string actorId = null)
        where TActor : ActorBase, new();

        TActor CreateActorFromExisting<TActor>(GameObject existingObject, string actorId = null)
        where TActor : ActorBase, new();

        TActor GetActor<TActor>(string actorId) where TActor : ActorBase;

        List<TActor> GetActorsOfType<TActor>() where TActor : ActorBase;

        bool RemoveActor(string actorId);

        void ClearAllActors();
    }

    /// <summary>
    /// Actor管理基类，继承MonoBehaviour并实现IManager接口，单例模式
    /// </summary>
    public class ManagerBase : MonoBehaviour, IManager
    {
        // 单例实例
        public static ManagerBase Instance { get; private set; }

        // 线程安全锁
        private readonly object mActorLock = new object();

        // 存储所有Actor，以唯一标识ID为键
        private readonly Dictionary<string, ActorBase> mManagedActors = new Dictionary<string, ActorBase>();

        /// <summary>
        /// 确保单例唯一性
        /// </summary>
        protected virtual void Awake()
        {
            // 单例模式：如果已有实例则销毁当前对象
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // 设置单例实例并使其在场景切换时不销毁
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 执行自定义初始化逻辑
            OnAwake();

            // 触发所有已存在Actor的OnAwake
            InvokeActorAwake();
        }

        /// <summary>
        /// 自定义初始化方法，供子类重写
        /// </summary>
        protected virtual void OnAwake()
        {
            // 子类可在此实现初始化逻辑
        }

        /// <summary>
        /// Unity生命周期：第一帧更新前调用
        /// </summary>
        protected virtual void Start()
        {
            // 触发所有已存在Actor的OnStart
            InvokeActorStart();
        }

        /// <summary>
        /// Unity生命周期：每帧更新时调用
        /// </summary>
        protected virtual void Update()
        {
            float deltaTime = Time.deltaTime;
            // 触发所有Actor的OnUpdate
            InvokeActorUpdate(deltaTime);
        }

        /// <summary>
        /// Unity生命周期：固定时间间隔更新时调用
        /// </summary>
        protected virtual void FixedUpdate()
        {
            float fixedDeltaTime = Time.fixedDeltaTime;
            // 触发所有Actor的OnFixedUpdate
            InvokeActorFixedUpdate(fixedDeltaTime);
        }

        /// <summary>
        /// Unity生命周期：每帧更新后调用
        /// </summary>
        protected virtual void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            // 触发所有Actor的OnLateUpdate
            InvokeActorLateUpdate(deltaTime);
        }

        /// <summary>
        /// 生成唯一Actor ID
        /// </summary>
        protected virtual string GenerateUniqueId()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 通过预制体创建Actor实例
        /// </summary>
        public virtual TActor CreateActorFromPrefab<TActor>(GameObject prefab, Transform parent = null, string actorId = null)
        where TActor : ActorBase, new()
        {
            if (prefab == null)
            {
                Debug.LogError("创建Actor失败：预制体为空");
                return null;
            }

            var id = string.IsNullOrEmpty(actorId) ? GenerateUniqueId() : actorId;
            lock (mActorLock)
            {
                if (mManagedActors.ContainsKey(id))
                {
                    Debug.LogError($"创建Actor失败：ID {id} 已存在");
                    return null;
                }
            }

            var actor = new TActor();
            actor.CreateFromPrefab(prefab, parent);

            lock (mActorLock)
            {
                mManagedActors[id] = actor;
            }

            // 如果Manager已完成初始化，立即触发Actor的Awake
            if (Instance != null)
            {
                actor.OnAwake();
            }

            Debug.Log($"通过预制体创建Actor成功：ID={id}, 类型={typeof(TActor).Name}");
            return actor;
        }

        /// <summary>
        /// 通过现有GameObject创建Actor实例
        /// </summary>
        public virtual TActor CreateActorFromExisting<TActor>(GameObject existingObject, string actorId = null)
        where TActor : ActorBase, new()
        {
            if (existingObject == null)
            {
                Debug.LogError("创建Actor失败：目标游戏对象为空");
                return null;
            }

            var id = string.IsNullOrEmpty(actorId) ? GenerateUniqueId() : actorId;
            lock (mActorLock)
            {
                if (mManagedActors.ContainsKey(id))
                {
                    Debug.LogError($"创建Actor失败：ID {id} 已存在");
                    return null;
                }
            }

            var actor = new TActor();
            actor.AttachToExistingObject(existingObject);

            lock (mActorLock)
            {
                mManagedActors[id] = actor;
            }

            // 如果Manager已完成初始化，立即触发Actor的Awake
            if (Instance != null)
            {
                actor.OnAwake();
            }

            Debug.Log($"通过现有对象创建Actor成功：ID={id}, 类型={typeof(TActor).Name}");
            return actor;
        }

        /// <summary>
        /// 通过ID获取Actor
        /// </summary>
        public virtual TActor GetActor<TActor>(string actorId) where TActor : ActorBase
        {
            if (string.IsNullOrEmpty(actorId)) return null;

            lock (mActorLock)
            {
                if (mManagedActors.TryGetValue(actorId, out var actor))
                {
                    return actor as TActor;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取指定类型的所有Actor
        /// </summary>
        public virtual List<TActor> GetActorsOfType<TActor>() where TActor : ActorBase
        {
            lock (mActorLock)
            {
                return mManagedActors.Values
                .OfType<TActor>()
                .Where(actor => !actor.IsDestroyed)
                .ToList();
            }
        }

        /// <summary>
        /// 移除指定ID的Actor
        /// </summary>
        public virtual bool RemoveActor(string actorId)
        {
            if (string.IsNullOrEmpty(actorId)) return false;

            lock (mActorLock)
            {
                if (mManagedActors.TryGetValue(actorId, out var actor))
                {
                    actor.OnDestroy(); // 触发Actor的销毁逻辑
                    mManagedActors.Remove(actorId);
                    Debug.Log($"移除Actor成功：ID={actorId}");
                    return true;
                }
            }

            Debug.LogWarning($"移除Actor失败：未找到ID={actorId}的Actor");
            return false;
        }

        /// <summary>
        /// 清除所有Actor
        /// </summary>
        public virtual void ClearAllActors()
        {
            lock (mActorLock)
            {
                var actorIds = mManagedActors.Keys.ToList();
                foreach (var id in actorIds)
                {
                    if (mManagedActors.TryGetValue(id, out var actor))
                    {
                        actor.OnDestroy(); // 触发Actor的销毁逻辑
                    }
                }
                mManagedActors.Clear();
            }

            Debug.Log("所有Actor已清除");
        }

        /// <summary>
        /// 销毁时释放资源
        /// </summary>
        protected virtual void OnDestroy()
        {
            // 只有单例实例销毁时才执行清理
            if (Instance == this)
            {
                ClearAllActors();
                Instance = null;
            }
        }

        #region 生命周期分发方法

        /// <summary>
        /// 触发所有Actor的OnAwake
        /// </summary>
        private void InvokeActorAwake()
        {
            lock (mActorLock)
            {
                var currentActors = mManagedActors.Values
                .Where(actor => !actor.IsDestroyed)
                .ToList();

                foreach (var actor in currentActors)
                {
                    actor.OnAwake();
                }
            }
        }

        /// <summary>
        /// 触发所有Actor的OnStart
        /// </summary>
        private void InvokeActorStart()
        {
            lock (mActorLock)
            {
                var currentActors = mManagedActors.Values
                .Where(actor => !actor.IsDestroyed)
                .ToList();

                foreach (var actor in currentActors)
                {
                    actor.OnStart();
                }
            }
        }

        /// <summary>
        /// 触发所有Actor的OnUpdate
        /// </summary>
        private void InvokeActorUpdate(float deltaTime)
        {
            lock (mActorLock)
            {
                var currentActors = mManagedActors.Values
                .Where(actor => !actor.IsDestroyed)
                .ToList();

                foreach (var actor in currentActors)
                {
                    actor.OnUpdate(deltaTime);
                }
            }
        }

        /// <summary>
        /// 触发所有Actor的OnFixedUpdate
        /// </summary>
        private void InvokeActorFixedUpdate(float fixedDeltaTime)
        {
            lock (mActorLock)
            {
                var currentActors = mManagedActors.Values
                .Where(actor => !actor.IsDestroyed)
                .ToList();

                foreach (var actor in currentActors)
                {
                    actor.OnFixedUpdate(fixedDeltaTime);
                }
            }
        }

        /// <summary>
        /// 触发所有Actor的OnLateUpdate
        /// </summary>
        private void InvokeActorLateUpdate(float deltaTime)
        {
            lock (mActorLock)
            {
                var currentActors = mManagedActors.Values
                .Where(actor => !actor.IsDestroyed)
                .ToList();

                foreach (var actor in currentActors)
                {
                    actor.OnLateUpdate(deltaTime);
                }
            }
        }

        #endregion
    }
}

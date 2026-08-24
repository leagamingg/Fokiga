using Fokiga.Runtime.Core;
using UnityEngine;

namespace Fokiga.Runtime.Gameplay
{
    public class CharacterActor : ActorBase
    {

        /// <summary>
        /// 初始化阶段（在创建实体后调用，用于初始化基础数据）
        /// </summary>
        protected override void BeforeGetRealObject()
        {
            base.BeforeGetRealObject();
            // 角色获取实体前的准备工作（如基础数据初始化）
            Debug.Log($"{GetType().Name} 准备获取实体");
        }

        /// <summary>
        /// 实体创建/绑定后调用（用于处理实体相关初始化）
        /// </summary>
        /// <param name="prefab">如果是通过预制体创建，传入预制体；否则为null</param>
        protected override void AfterGetPrefab(GameObject prefab)
        {
            base.AfterGetPrefab(prefab);
            // 实体获取后的处理（如关联实体组件）
            Debug.Log($"{GetType().Name} 已获取{(prefab != null ? "预制体" : "现有实体")}");
        }

        /// <summary>
        /// 唤醒阶段（类似Unity的Awake，用于初始化组件引用）
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();
            // 初始化组件引用或基础资源
            Debug.Log($"{GetType().Name} Awake");
            AddComponent<PlayerInputComponent>();
            AddComponent<CameraComponent>();
            AddComponent<CharacterMovementComponent>();
        }

        /// <summary>
        /// 启动阶段（类似Unity的Start，在Awake后调用，用于业务初始化）
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            // 启动业务逻辑（如注册初始状态）
            Debug.Log($"{GetType().Name} Start");
        }

        /// <summary>
        /// 帧更新（每帧调用，处理动态逻辑）
        /// </summary>
        /// <param name="deltaTime">帧间隔时间</param>
        public override void OnUpdate(float deltaTime)
        {
            base.OnUpdate(deltaTime);
            // 处理角色帧更新逻辑（如移动、状态检测）
            //Debug.Log($"{GetType().Name} Update: {deltaTime}");
        }

        /// <summary>
        /// 固定帧更新（物理相关逻辑）
        /// </summary>
        /// <param name="fixedDeltaTime">固定帧间隔时间</param>
        public override void OnFixedUpdate(float fixedDeltaTime)
        {
            base.OnFixedUpdate(fixedDeltaTime);
            // 处理物理相关更新（如碰撞检测）
            //Debug.Log($"{GetType().Name} FixedUpdate: {fixedDeltaTime}");
        }

        public override void OnLateUpdate(float deltaTime)
        {
            base.OnLateUpdate(deltaTime);
            // 处理延迟更新逻辑（如相机跟随）
            // Debug.Log($"{GetType().Name} LateUpdate: {deltaTime}");
        }

        /// <summary>
        /// 激活角色（恢复活跃状态）
        /// </summary>
        public override void Activate()
        {
            base.Activate();
            // 激活角色逻辑（如显示模型、启用输入）
            Debug.Log($"{GetType().Name} Activated");
        }

        /// <summary>
        /// 停用角色（暂时禁用）
        /// </summary>
        public override void Deactivate()
        {
            base.Deactivate();
            // 停用角色逻辑（如隐藏模型、禁用输入）
            Debug.Log($"{GetType().Name} Deactivated");
        }

        /// <summary>
        /// 销毁前处理（释放资源前调用）
        /// </summary>
        protected override void BeforeDestroyRealObject()
        {
            base.BeforeDestroyRealObject();
            // 销毁前清理（如保存数据、移除监听）
            Debug.Log($"{GetType().Name} 准备销毁实体");
        }

        /// <summary>
        /// 销毁后处理（资源释放后调用）
        /// </summary>
        protected override void AfterDestroyRealObject()
        {
            base.AfterDestroyRealObject();
            // 销毁后收尾（如通知管理器移除角色）
            Debug.Log($"{GetType().Name} 实体已销毁");
        }

        /// <summary>
        /// 销毁角色（释放所有资源）
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();
            // 最终销毁逻辑（确保资源完全释放）
            Debug.Log($"{GetType().Name} Destroyed");
        }
    }
}

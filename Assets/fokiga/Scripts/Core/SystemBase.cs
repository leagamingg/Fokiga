using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 系统基类，仅定义系统基础功能和生命周期
/// 具体注册管理由SystemManager负责
/// </summary>
public abstract class SystemBase
{
    /// <summary>
    /// 系统是否已初始化
    /// </summary>
    public bool IsInitialized { get; private set; } = false;

    /// <summary>
    /// 系统是否处于活跃状态
    /// </summary>
    public bool IsActive { get; protected set; } = true;

    /// <summary>
    /// 系统优先级（更新顺序，值越大越先更新）
    /// </summary>
    public virtual int UpdatePriority => 0;

    /// <summary>
    /// 初始化系统
    /// </summary>
    internal void Init()
    {
        if (IsInitialized) return;

        try
        {
            OnInitialize();
            IsInitialized = true;
            Debug.Log($"系统初始化完成: {GetType().Name}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"系统初始化失败 {GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// 激活系统
    /// </summary>
    public void Activate()
    {
        if (IsActive || !IsInitialized) return;

        IsActive = true;
        OnActivated();
        Debug.Log($"系统已激活: {GetType().Name}");
    }

    /// <summary>
    /// 暂停系统
    /// </summary>
    public void Deactivate()
    {
        if (!IsActive || !IsInitialized) return;

        IsActive = false;
        OnDeactivated();
        Debug.Log($"系统已暂停: {GetType().Name}");
    }

    /// <summary>
    /// 销毁系统
    /// </summary>
    internal void Destroy()
    {
        if (!IsInitialized) return;

        try
        {
            OnDestroy();
            IsInitialized = false;
            IsActive = false;
            Debug.Log($"系统已销毁: {GetType().Name}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"系统销毁出错 {GetType().Name}: {ex.Message}");
        }
    }

    // 生命周期抽象方法
    protected abstract void OnInitialize();
    protected abstract void OnUpdate(float deltaTime);
    protected abstract void OnFixedUpdate(float fixedDeltaTime);
    protected abstract void OnLateUpdate(float deltaTime);
    protected virtual void OnActivated() { }
    protected virtual void OnDeactivated() { }
    protected abstract void OnDestroy();

    // 内部更新方法（由管理器调用）
    internal void InternalUpdate(float deltaTime)
    {
        if (IsActive && IsInitialized)
        {
            OnUpdate(deltaTime);
        }
    }

    internal void InternalFixedUpdate(float fixedDeltaTime)
    {
        if (IsActive && IsInitialized)
        {
            OnFixedUpdate(fixedDeltaTime);
        }
    }

    internal void InternalLateUpdate(float deltaTime)
    {
        if (IsActive && IsInitialized)
        {
            OnLateUpdate(deltaTime);
        }
    }
}
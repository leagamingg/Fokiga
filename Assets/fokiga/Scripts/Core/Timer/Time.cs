using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fokiga.Runtime.Core
{
    /// <summary>
    /// 计时器句柄，用于安全操作计时器（替代int ID，支持复用）
    /// </summary>
    public struct TimerHandle : IEquatable<TimerHandle>
    {
        internal int Id;
        internal TimerManager Manager;

        public TimerHandle(int id, TimerManager manager)
        {
            Id = id;
            Manager = manager;
        }

        /// <summary>
        /// 取消计时器
        /// </summary>
        public void Cancel()
        {
            Manager?.RemoveTimer(this);
        }

        /// <summary>
        /// 暂停计时器
        /// </summary>
        public void Pause()
        {
            Manager?.PauseTimer(this);
        }

        /// <summary>
        /// 恢复计时器
        /// </summary>
        public void Resume()
        {
            Manager?.ResumeTimer(this);
        }

        /// <summary>
        /// 重置计时器（重新开始计时）
        /// </summary>
        public void Reset()
        {
            Manager?.ResetTimer(this);
        }

        /// <summary>
        /// 获取剩余时间（秒）
        /// </summary>
        public float GetRemainingTime()
        {
            return Manager?.GetTimerRemaining(this) ?? 0;
        }

        /// <summary>
        /// 获取已流逝时间（秒）
        /// </summary>
        public float GetElapsedTime()
        {
            return Manager?.GetTimerElapsed(this) ?? 0;
        }

        /// <summary>
        /// 计时器是否有效
        /// </summary>
        public bool IsValid()
        {
            return Manager?.HasTimer(this) ?? false;
        }

        public bool Equals(TimerHandle other)
        {
            return Id == other.Id && ReferenceEquals(Manager, other.Manager);
        }

        public override bool Equals(object obj)
        {
            return obj is TimerHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Manager);
        }

        public static bool operator ==(TimerHandle left, TimerHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TimerHandle left, TimerHandle right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 单个计时器实例（内部使用，通过对象池复用）
    /// </summary>
    internal class Timer
    {
        // 计时器ID
        public int Id { get; private set; }
        // 总时长（秒）
        private float mDuration;
        // 已流逝时间（秒）
        private float mElapsedTime;
        // 是否循环
        private bool mIsLoop;
        // 是否使用非缩放时间（不受Time.timeScale影响）
        private bool mUseUnscaledTime;
        // 是否暂停
        private bool mIsPaused;
        // 完成回调（存储弱引用，避免强引用导致实例无法销毁）
        private WeakReference<Action> mOnCompleteWeakRef;
        // 更新回调（弱引用）
        private WeakReference<Action<float>> mOnUpdateWeakRef;
        // 时间源（根据useUnscaledTime动态获取）
        private Func<float> mGetTimeDelta;

        public Timer()
        {
            // 空构造函数，供对象池复用
        }

        /// <summary>
        /// 初始化计时器（对象池取出时调用）
        /// </summary>
        public void Init(int id, float duration, bool isLoop, Action onComplete,
        Action<float> onUpdate, bool useUnscaledTime)
        {
            Id = id;
            mDuration = duration;
            mIsLoop = isLoop;
            mUseUnscaledTime = useUnscaledTime;
            mIsPaused = false;
            mElapsedTime = 0;

            // 存储回调的弱引用（避免强引用实例）
            mOnCompleteWeakRef = onComplete != null ? new WeakReference<Action>(onComplete) : null;
            mOnUpdateWeakRef = onUpdate != null ? new WeakReference<Action<float>>(onUpdate) : null;

            // 绑定时间源
            mGetTimeDelta = useUnscaledTime ? () => Time.unscaledDeltaTime : () => Time.deltaTime;
        }

        /// <summary>
        /// 更新计时器
        /// </summary>
        /// <returns>是否需要被移除（非循环且已完成）</returns>
        public bool Update()
        {
            if (mIsPaused) return false;

            float deltaTime = mGetTimeDelta();
            mElapsedTime += deltaTime;

            // 触发更新回调（检查弱引用是否有效）
            if (mOnUpdateWeakRef != null && mOnUpdateWeakRef.TryGetTarget(out var onUpdate))
            {
                float progress = Mathf.Clamp01(mElapsedTime / mDuration);
                onUpdate.Invoke(progress);
            }

            // 检查是否完成
            if (mElapsedTime >= mDuration)
            {
                // 触发完成回调（检查弱引用是否有效）
                if (mOnCompleteWeakRef != null && mOnCompleteWeakRef.TryGetTarget(out var onComplete))
                {
                    onComplete.Invoke();
                }

                if (mIsLoop)
                {
                    // 循环模式：重置已流逝时间（支持超出部分累计，如duration=2，实际过了3秒，下次剩余1秒）
                    mElapsedTime -= mDuration;
                    return false;
                }
                else
                {
                    // 非循环模式：标记为可移除
                    return true;
                }
            }

            return false;
        }

        // 暂停
        public void Pause() => mIsPaused = true;

        // 恢复
        public void Resume() => mIsPaused = false;

        // 重置计时
        public void Reset() => mElapsedTime = 0;

        // 修改时长
        public void ChangeDuration(float newDuration) => mDuration = newDuration;

        // 获取剩余时间
        public float GetRemainingTime() => Mathf.Max(0, mDuration - mElapsedTime);

        // 获取已流逝时间
        public float GetElapsedTime() => mElapsedTime;

        /// <summary>
        /// 重置为初始状态（对象池回收时调用）
        /// </summary>
        public void ResetForPool()
        {
            mOnCompleteWeakRef = null;
            mOnUpdateWeakRef = null;
            mGetTimeDelta = null;
            mElapsedTime = 0;
            mIsPaused = false;
        }
    }
    public class TimerManager : MonoBehaviour
    {
        // 单例实例
        private static TimerManager mInstance;
        public static TimerManager Instance
        {
            get
            {
                if (mInstance == null)
                {
                    GameObject obj = new GameObject("TimerManager");
                    mInstance = obj.AddComponent<TimerManager>();
                    DontDestroyOnLoad(obj);
                }
                return mInstance;
            }
        }

        // 活跃计时器列表
        private List<Timer> mActiveTimers = new List<Timer>();
        // 待移除计时器
        private HashSet<int> mTimersToRemove = new HashSet<int>();
        // 下一个计时器ID
        private int mNextTimerId = 1;
        // Timer对象池（减少GC）
        private ObjectPool<Timer> mTimerPool = new ObjectPool<Timer>(
        createFunc: () => new Timer(),
        actionOnGet: (timer) => { },
        actionOnRelease: (timer) => timer.ResetForPool(),
        defaultCapacity: 32
        );

        private void Update()
        {
            // 清理待移除计时器
            if (mTimersToRemove.Count > 0)
            {
                for (int i = mActiveTimers.Count - 1; i >= 0; i--)
                {
                    if (mTimersToRemove.Contains(mActiveTimers[i].Id))
                    {
                        mTimerPool.Release(mActiveTimers[i]); // 回收至对象池
                        mActiveTimers.RemoveAt(i);
                    }
                }
                mTimersToRemove.Clear();
            }

            // 更新所有活跃计时器
            for (int i = 0; i < mActiveTimers.Count; i++)
            {
                if (mActiveTimers[i].Update())
                {
                    mTimersToRemove.Add(mActiveTimers[i].Id);
                }
            }
        }

        /// <summary>
        /// 添加计时器
        /// </summary>
        public TimerHandle AddTimer(float duration, bool isLoop, Action onComplete,
        Action<float> onUpdate = null, bool useUnscaledTime = false)
        {
            if (duration <= 0)
            {
                Debug.LogError("计时器时长必须大于0");
                return default;
            }

            int timerId = mNextTimerId++;
            Timer timer = mTimerPool.Get(); // 从对象池获取
            timer.Init(timerId, duration, isLoop, onComplete, onUpdate, useUnscaledTime);
            mActiveTimers.Add(timer);

            return new TimerHandle(timerId, this);
        }

        /// <summary>
        /// 通过句柄移除计时器
        /// </summary>
        internal void RemoveTimer(TimerHandle handle)
        {
            if (handle.Manager != this) return;
            mTimersToRemove.Add(handle.Id);
        }

        /// <summary>
        /// 暂停计时器
        /// </summary>
        internal void PauseTimer(TimerHandle handle)
        {
            if (handle.Manager != this) return;
            GetTimer(handle.Id)?.Pause();
        }

        /// <summary>
        /// 恢复计时器
        /// </summary>
        internal void ResumeTimer(TimerHandle handle)
        {
            if (handle.Manager != this) return;
            GetTimer(handle.Id)?.Resume();
        }

        /// <summary>
        /// 重置计时器
        /// </summary>
        internal void ResetTimer(TimerHandle handle)
        {
            if (handle.Manager != this) return;
            GetTimer(handle.Id)?.Reset();
        }

        /// <summary>
        /// 获取剩余时间
        /// </summary>
        internal float GetTimerRemaining(TimerHandle handle)
        {
            if (handle.Manager != this) return 0;
            return GetTimer(handle.Id)?.GetRemainingTime() ?? 0;
        }

        /// <summary>
        /// 获取已流逝时间
        /// </summary>
        internal float GetTimerElapsed(TimerHandle handle)
        {
            if (handle.Manager != this) return 0;
            return GetTimer(handle.Id)?.GetElapsedTime() ?? 0;
        }

        /// <summary>
        /// 检查计时器是否有效
        /// </summary>
        internal bool HasTimer(TimerHandle handle)
        {
            if (handle.Manager != this) return false;
            return GetTimer(handle.Id) != null && !mTimersToRemove.Contains(handle.Id);
        }

        /// <summary>
        /// 批量暂停所有计时器
        /// </summary>
        public void PauseAllTimers()
        {
            foreach (var timer in mActiveTimers)
            {
                timer.Pause();
            }
        }

        /// <summary>
        /// 批量恢复所有计时器
        /// </summary>
        public void ResumeAllTimers()
        {
            foreach (var timer in mActiveTimers)
            {
                timer.Resume();
            }
        }

        /// <summary>
        /// 清除所有计时器
        /// </summary>
        public void ClearAllTimers()
        {
            foreach (var timer in mActiveTimers)
            {
                mTimerPool.Release(timer);
            }
            mActiveTimers.Clear();
            mTimersToRemove.Clear();
        }

        /// <summary>
        /// 查找计时器
        /// </summary>
        private Timer GetTimer(int timerId)
        {
            for (int i = 0; i < mActiveTimers.Count; i++)
            {
                if (mActiveTimers[i].Id == timerId)
                {
                    return mActiveTimers[i];
                }
            }
            return null;
        }

        // 对象池辅助类（简化版）
        private class ObjectPool<T> where T : new()
        {
            private readonly Stack<T> mPool = new Stack<T>();
            private readonly Func<T> mCreateFunc;
            private readonly Action<T> mActionOnGet;
            private readonly Action<T> mActionOnRelease;

            public ObjectPool(Func<T> createFunc, Action<T> actionOnGet, Action<T> actionOnRelease, int defaultCapacity)
            {
                mCreateFunc = createFunc ?? (() => new T());
                mActionOnGet = actionOnGet;
                mActionOnRelease = actionOnRelease;

                // 预创建默认数量的对象
                for (int i = 0; i < defaultCapacity; i++)
                {
                    mPool.Push(mCreateFunc());
                }
            }

            public T Get()
            {
                T item = mPool.Count > 0 ? mPool.Pop() : mCreateFunc();
                mActionOnGet?.Invoke(item);
                return item;
            }

            public void Release(T item)
            {
                mActionOnRelease?.Invoke(item);
                mPool.Push(item);
            }
        }
    }
}

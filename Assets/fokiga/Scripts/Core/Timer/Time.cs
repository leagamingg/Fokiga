using System;
using System.Collections.Generic;
using UnityEngine;

namespace FokigaTime {
    /// <summary>
    /// 计时器句柄，用于安全操作计时器（替代int ID，支持复用）
    /// </summary>
    public struct TimerHandle : IEquatable<TimerHandle> {
        internal int Id;
        internal TimerManager Manager;

        public TimerHandle(int id, TimerManager manager) {
            Id = id;
            Manager = manager;
        }

        /// <summary>
        /// 取消计时器
        /// </summary>
        public void Cancel() {
            Manager?.RemoveTimer(this);
        }

        /// <summary>
        /// 暂停计时器
        /// </summary>
        public void Pause() {
            Manager?.PauseTimer(this);
        }

        /// <summary>
        /// 恢复计时器
        /// </summary>
        public void Resume() {
            Manager?.ResumeTimer(this);
        }

        /// <summary>
        /// 重置计时器（重新开始计时）
        /// </summary>
        public void Reset() {
            Manager?.ResetTimer(this);
        }

        /// <summary>
        /// 获取剩余时间（秒）
        /// </summary>
        public float GetRemainingTime() {
            return Manager?.GetTimerRemaining(this) ?? 0;
        }

        /// <summary>
        /// 获取已流逝时间（秒）
        /// </summary>
        public float GetElapsedTime() {
            return Manager?.GetTimerElapsed(this) ?? 0;
        }

        /// <summary>
        /// 计时器是否有效
        /// </summary>
        public bool IsValid() {
            return Manager?.HasTimer(this) ?? false;
        }

        public bool Equals(TimerHandle other) {
            return Id == other.Id && ReferenceEquals(Manager, other.Manager);
        }

        public override bool Equals(object obj) {
            return obj is TimerHandle other && Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine(Id, Manager);
        }

        public static bool operator ==(TimerHandle left, TimerHandle right) {
            return left.Equals(right);
        }

        public static bool operator !=(TimerHandle left, TimerHandle right) {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 单个计时器实例（内部使用，通过对象池复用）
    /// </summary>
    internal class Timer {
        // 计时器ID
        public int Id { get; private set; }
        // 总时长（秒）
        private float _duration;
        // 已流逝时间（秒）
        private float _elapsedTime;
        // 是否循环
        private bool _isLoop;
        // 是否使用非缩放时间（不受Time.timeScale影响）
        private bool _useUnscaledTime;
        // 是否暂停
        private bool _isPaused;
        // 完成回调（存储弱引用，避免强引用导致实例无法销毁）
        private WeakReference<Action> _onCompleteWeakRef;
        // 更新回调（弱引用）
        private WeakReference<Action<float>> _onUpdateWeakRef;
        // 时间源（根据useUnscaledTime动态获取）
        private Func<float> _getTimeDelta;

        public Timer() {
            // 空构造函数，供对象池复用
        }

        /// <summary>
        /// 初始化计时器（对象池取出时调用）
        /// </summary>
        public void Init(int id, float duration, bool isLoop, Action onComplete,
                        Action<float> onUpdate, bool useUnscaledTime) {
            Id = id;
            _duration = duration;
            _isLoop = isLoop;
            _useUnscaledTime = useUnscaledTime;
            _isPaused = false;
            _elapsedTime = 0;

            // 存储回调的弱引用（避免强引用实例）
            _onCompleteWeakRef = onComplete != null ? new WeakReference<Action>(onComplete) : null;
            _onUpdateWeakRef = onUpdate != null ? new WeakReference<Action<float>>(onUpdate) : null;

            // 绑定时间源
            _getTimeDelta = useUnscaledTime ? () => Time.unscaledDeltaTime : () => Time.deltaTime;
        }

        /// <summary>
        /// 更新计时器
        /// </summary>
        /// <returns>是否需要被移除（非循环且已完成）</returns>
        public bool Update() {
            if (_isPaused) return false;

            float deltaTime = _getTimeDelta();
            _elapsedTime += deltaTime;

            // 触发更新回调（检查弱引用是否有效）
            if (_onUpdateWeakRef != null && _onUpdateWeakRef.TryGetTarget(out var onUpdate)) {
                float progress = Mathf.Clamp01(_elapsedTime / _duration);
                onUpdate.Invoke(progress);
            }

            // 检查是否完成
            if (_elapsedTime >= _duration) {
                // 触发完成回调（检查弱引用是否有效）
                if (_onCompleteWeakRef != null && _onCompleteWeakRef.TryGetTarget(out var onComplete)) {
                    onComplete.Invoke();
                }

                if (_isLoop) {
                    // 循环模式：重置已流逝时间（支持超出部分累计，如duration=2，实际过了3秒，下次剩余1秒）
                    _elapsedTime -= _duration;
                    return false;
                } else {
                    // 非循环模式：标记为可移除
                    return true;
                }
            }

            return false;
        }

        // 暂停
        public void Pause() => _isPaused = true;

        // 恢复
        public void Resume() => _isPaused = false;

        // 重置计时
        public void Reset() => _elapsedTime = 0;

        // 修改时长
        public void ChangeDuration(float newDuration) => _duration = newDuration;

        // 获取剩余时间
        public float GetRemainingTime() => Mathf.Max(0, _duration - _elapsedTime);

        // 获取已流逝时间
        public float GetElapsedTime() => _elapsedTime;

        /// <summary>
        /// 重置为初始状态（对象池回收时调用）
        /// </summary>
        public void ResetForPool() {
            _onCompleteWeakRef = null;
            _onUpdateWeakRef = null;
            _getTimeDelta = null;
            _elapsedTime = 0;
            _isPaused = false;
        }
    }
    public class TimerManager : MonoBehaviour {
        // 单例实例
        private static TimerManager _instance;
        public static TimerManager Instance {
            get {
                if (_instance == null) {
                    GameObject obj = new GameObject("TimerManager");
                    _instance = obj.AddComponent<TimerManager>();
                    DontDestroyOnLoad(obj);
                }
                return _instance;
            }
        }

        // 活跃计时器列表
        private List<Timer> _activeTimers = new List<Timer>();
        // 待移除计时器
        private HashSet<int> _timersToRemove = new HashSet<int>();
        // 下一个计时器ID
        private int _nextTimerId = 1;
        // Timer对象池（减少GC）
        private ObjectPool<Timer> _timerPool = new ObjectPool<Timer>(
            createFunc: () => new Timer(),
            actionOnGet: (timer) => { },
            actionOnRelease: (timer) => timer.ResetForPool(),
            defaultCapacity: 32
        );

        private void Update() {
            // 清理待移除计时器
            if (_timersToRemove.Count > 0) {
                for (int i = _activeTimers.Count - 1; i >= 0; i--) {
                    if (_timersToRemove.Contains(_activeTimers[i].Id)) {
                        _timerPool.Release(_activeTimers[i]); // 回收至对象池
                        _activeTimers.RemoveAt(i);
                    }
                }
                _timersToRemove.Clear();
            }

            // 更新所有活跃计时器
            for (int i = 0; i < _activeTimers.Count; i++) {
                if (_activeTimers[i].Update()) {
                    _timersToRemove.Add(_activeTimers[i].Id);
                }
            }
        }

        /// <summary>
        /// 添加计时器
        /// </summary>
        public TimerHandle AddTimer(float duration, bool isLoop, Action onComplete,
                                  Action<float> onUpdate = null, bool useUnscaledTime = false) {
            if (duration <= 0) {
                Debug.LogError("计时器时长必须大于0");
                return default;
            }

            int timerId = _nextTimerId++;
            Timer timer = _timerPool.Get(); // 从对象池获取
            timer.Init(timerId, duration, isLoop, onComplete, onUpdate, useUnscaledTime);
            _activeTimers.Add(timer);

            return new TimerHandle(timerId, this);
        }

        /// <summary>
        /// 通过句柄移除计时器
        /// </summary>
        internal void RemoveTimer(TimerHandle handle) {
            if (handle.Manager != this) return;
            _timersToRemove.Add(handle.Id);
        }

        /// <summary>
        /// 暂停计时器
        /// </summary>
        internal void PauseTimer(TimerHandle handle) {
            if (handle.Manager != this) return;
            GetTimer(handle.Id)?.Pause();
        }

        /// <summary>
        /// 恢复计时器
        /// </summary>
        internal void ResumeTimer(TimerHandle handle) {
            if (handle.Manager != this) return;
            GetTimer(handle.Id)?.Resume();
        }

        /// <summary>
        /// 重置计时器
        /// </summary>
        internal void ResetTimer(TimerHandle handle) {
            if (handle.Manager != this) return;
            GetTimer(handle.Id)?.Reset();
        }

        /// <summary>
        /// 获取剩余时间
        /// </summary>
        internal float GetTimerRemaining(TimerHandle handle) {
            if (handle.Manager != this) return 0;
            return GetTimer(handle.Id)?.GetRemainingTime() ?? 0;
        }

        /// <summary>
        /// 获取已流逝时间
        /// </summary>
        internal float GetTimerElapsed(TimerHandle handle) {
            if (handle.Manager != this) return 0;
            return GetTimer(handle.Id)?.GetElapsedTime() ?? 0;
        }

        /// <summary>
        /// 检查计时器是否有效
        /// </summary>
        internal bool HasTimer(TimerHandle handle) {
            if (handle.Manager != this) return false;
            return GetTimer(handle.Id) != null && !_timersToRemove.Contains(handle.Id);
        }

        /// <summary>
        /// 批量暂停所有计时器
        /// </summary>
        public void PauseAllTimers() {
            foreach (var timer in _activeTimers) {
                timer.Pause();
            }
        }

        /// <summary>
        /// 批量恢复所有计时器
        /// </summary>
        public void ResumeAllTimers() {
            foreach (var timer in _activeTimers) {
                timer.Resume();
            }
        }

        /// <summary>
        /// 清除所有计时器
        /// </summary>
        public void ClearAllTimers() {
            foreach (var timer in _activeTimers) {
                _timerPool.Release(timer);
            }
            _activeTimers.Clear();
            _timersToRemove.Clear();
        }

        /// <summary>
        /// 查找计时器
        /// </summary>
        private Timer GetTimer(int timerId) {
            for (int i = 0; i < _activeTimers.Count; i++) {
                if (_activeTimers[i].Id == timerId) {
                    return _activeTimers[i];
                }
            }
            return null;
        }

        // 对象池辅助类（简化版）
        private class ObjectPool<T> where T : new() {
            private readonly Stack<T> _pool = new Stack<T>();
            private readonly Func<T> _createFunc;
            private readonly Action<T> _actionOnGet;
            private readonly Action<T> _actionOnRelease;

            public ObjectPool(Func<T> createFunc, Action<T> actionOnGet, Action<T> actionOnRelease, int defaultCapacity) {
                _createFunc = createFunc ?? (() => new T());
                _actionOnGet = actionOnGet;
                _actionOnRelease = actionOnRelease;

                // 预创建默认数量的对象
                for (int i = 0; i < defaultCapacity; i++) {
                    _pool.Push(_createFunc());
                }
            }

            public T Get() {
                T item = _pool.Count > 0 ? _pool.Pop() : _createFunc();
                _actionOnGet?.Invoke(item);
                return item;
            }

            public void Release(T item) {
                _actionOnRelease?.Invoke(item);
                _pool.Push(item);
            }
        }
    }
}
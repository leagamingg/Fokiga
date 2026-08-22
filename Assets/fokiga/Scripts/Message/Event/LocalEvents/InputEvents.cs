using Event;
using UnityEngine;

public class InputEvents
{
    /// <summary>
    /// 移动输入变化事件（当前后左右输入发生变化时触发）
    /// </summary>
    public class MoveInputChangedEvent : EventDefinition
    {
        /// <summary>
        /// 当前移动方向向量（x：左右方向，y：前后方向）
        /// </summary>
        public Vector2 MoveDirection { get; set; }

        public override string EventName => "Input.Move.Changed";
        public override EventScope Scope => EventScope.Instance;
    }

    /// <summary>
    /// 跳跃输入触发事件（按下跳跃键时触发）
    /// </summary>
    public class JumpPerformedEvent : EventDefinition
    {
        public override string EventName => "Input.Jump.Performed";
        public override EventScope Scope => EventScope.Instance;
    }

    /// <summary>
    /// 跳跃输入取消事件（松开跳跃键时触发）
    /// </summary>
    public class JumpCanceledEvent : EventDefinition
    {
        public override string EventName => "Input.Jump.Canceled";
        public override EventScope Scope => EventScope.Instance;
    }

    /// <summary>
    /// 奔跑输入触发事件（按下奔跑键时触发）
    /// </summary>
    public class RunPerformedEvent : EventDefinition
    {
        public override string EventName => "Input.Run.Performed";
        public override EventScope Scope => EventScope.Instance;
    }

    /// <summary>
    /// 奔跑输入取消事件（松开奔跑键时触发）
    /// </summary>
    public class RunCanceledEvent : EventDefinition
    {
        public override string EventName => "Input.Run.Canceled";
        public override EventScope Scope => EventScope.Instance;
    }

    /// <summary>
    /// 滑行输入触发事件（按下滑行键时触发）
    /// </summary>
    public class SlipPerformedEvent : EventDefinition
    {
        public override string EventName => "Input.Slip.Performed";
        public override EventScope Scope => EventScope.Instance;
    }

    /// <summary>
    /// 滑行输入取消事件（松开滑行键时触发）
    /// </summary>
    public class SlipCanceledEvent : EventDefinition
    {
        public override string EventName => "Input.Slip.Canceled";
        public override EventScope Scope => EventScope.Instance;
    }
}
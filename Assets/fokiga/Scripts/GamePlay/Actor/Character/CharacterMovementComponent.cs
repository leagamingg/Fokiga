using System.Collections.Generic;
using Fokiga.Runtime.Core;
using UnityEngine;

namespace Fokiga.Runtime.Gameplay
{
    public sealed class CharacterMovementComponent : ComponentBase
    {
        public const float DefaultMoveSpeed = 4f;
        public const float DefaultRunSpeed = 7f;
        public const float DefaultRotationSpeed = 720f;
        public const float DefaultWalkAnimationSpeed = 0.45f;
        public const float DefaultRunAnimationSpeed = 0.8f;
        public const float DefaultAnimationDampTime = 0.08f;

        private const float MoveThreshold = 0.0001f;
        private static readonly int SpeedParameterHash = Animator.StringToHash("Speed");
        private static readonly int DirectionParameterHash = Animator.StringToHash("Direction");
        private static readonly int MoveXParameterHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYParameterHash = Animator.StringToHash("MoveY");
        private static readonly int MoveMagnitudeParameterHash = Animator.StringToHash("MoveMagnitude");
        private static readonly int IsMovingParameterHash = Animator.StringToHash("IsMoving");
        private static readonly int IsRunningParameterHash = Animator.StringToHash("IsRunning");
        private static readonly int MoveDirectionParameterHash = Animator.StringToHash("MoveDirection");

        public float MoveSpeed { get; set; } = DefaultMoveSpeed;

        public float RunSpeed { get; set; } = DefaultRunSpeed;

        public float RotationSpeed { get; set; } = DefaultRotationSpeed;

        public float WalkAnimationSpeed { get; set; } = DefaultWalkAnimationSpeed;

        public float RunAnimationSpeed { get; set; } = DefaultRunAnimationSpeed;

        public float AnimationDampTime { get; set; } = DefaultAnimationDampTime;

        private Transform mTransform;
        private Rigidbody mRigidbody;
        private Animator mAnimator;
        private HashSet<int> mAnimatorParameterHashes;
        private Vector2 mMoveDirection;

        public override void OnAwake()
        {
            base.OnAwake();
            AddListener<InputEvents.MoveInputChangedEvent>(OnMoveInputChanged);
        }

        public override void AfterGetPrefab(GameObject prefab)
        {
            base.AfterGetPrefab(prefab);
            CacheMovementTarget();
        }

        public override void BeforeDestroyRealObject()
        {
            StopHorizontalMotion();
            base.BeforeDestroyRealObject();
        }

        public override void AfterDestroyRealObject()
        {
            mRigidbody = null;
            mAnimator = null;
            mAnimatorParameterHashes = null;
            mTransform = null;
            mMoveDirection = Vector2.zero;
            base.AfterDestroyRealObject();
        }

        public override void OnFixedUpdate(float fixedDeltaTime)
        {
            base.OnFixedUpdate(fixedDeltaTime);
            if (!TryGetMovementTarget() || fixedDeltaTime <= 0f)
            {
                return;
            }

            var inputMagnitude = Mathf.Clamp01(mMoveDirection.magnitude);
            var direction = ResolveMovementDirection();
            if (direction.sqrMagnitude > MoveThreshold)
            {
                direction.Normalize();
            }

            var inputComponent = Owner?.GetComponent<PlayerInputComponent>();
            var speed = inputComponent != null && inputComponent.IsRunning ? RunSpeed : MoveSpeed;
            var horizontalVelocity = direction * Mathf.Max(0f, speed) * inputMagnitude;

            if (mRigidbody != null && !mRigidbody.isKinematic)
            {
                var velocity = mRigidbody.velocity;
                velocity.x = horizontalVelocity.x;
                velocity.z = horizontalVelocity.z;
                mRigidbody.velocity = velocity;
            }
            else if (mRigidbody != null)
            {
                mRigidbody.MovePosition(mRigidbody.position + horizontalVelocity * fixedDeltaTime);
            }
            else
            {
                mTransform.position += horizontalVelocity * fixedDeltaTime;
            }

            if (direction.sqrMagnitude > MoveThreshold)
            {
                RotateTowards(direction, fixedDeltaTime);
            }
        }

        public override void OnUpdate(float deltaTime)
        {
            base.OnUpdate(deltaTime);
            UpdateAnimator(deltaTime);
        }

        public override void OnDisable()
        {
            StopHorizontalMotion();
            mMoveDirection = Vector2.zero;
            UpdateAnimator(0f);
            base.OnDisable();
        }

        public override void OnDestroy()
        {
            RemoveListener<InputEvents.MoveInputChangedEvent>(OnMoveInputChanged);
            StopHorizontalMotion();
            base.OnDestroy();
        }

        private void OnMoveInputChanged(InputEvents.MoveInputChangedEvent eventData)
        {
            mMoveDirection = eventData.MoveDirection;
            if (mMoveDirection.sqrMagnitude > 1f)
            {
                mMoveDirection.Normalize();
            }
        }

        private void CacheMovementTarget()
        {
            mTransform = Owner?.RealObject?.transform;
            mRigidbody = mTransform != null ? mTransform.GetComponent<Rigidbody>() : null;
            mAnimator = mTransform != null ? mTransform.GetComponent<Animator>() : null;

            if (mRigidbody != null)
            {
                mRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                mRigidbody.constraints =
                    (mRigidbody.constraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ) &
                    ~RigidbodyConstraints.FreezeRotationY;
            }

            if (mAnimator != null)
            {
                mAnimator.applyRootMotion = false;
                CacheAnimatorParameters();
            }
            else
            {
                mAnimatorParameterHashes = null;
            }
        }

        private bool TryGetMovementTarget()
        {
            if (mTransform == null)
            {
                CacheMovementTarget();
            }

            return mTransform != null;
        }

        private Vector3 ResolveMovementDirection()
        {
            var cameraComponent = Owner?.GetComponent<CameraComponent>();
            if (cameraComponent != null)
            {
                return cameraComponent.GetPlanarMovementDirection(mMoveDirection);
            }

            return new Vector3(mMoveDirection.x, 0f, mMoveDirection.y);
        }

        private void RotateTowards(Vector3 direction, float deltaTime)
        {
            var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            var rotation = Quaternion.RotateTowards(
                mRigidbody != null ? mRigidbody.rotation : mTransform.rotation,
                targetRotation,
                Mathf.Max(0f, RotationSpeed) * deltaTime);

            if (mRigidbody != null && !mRigidbody.isKinematic)
            {
                mRigidbody.MoveRotation(rotation);
            }
            else
            {
                mTransform.rotation = rotation;
            }
        }

        private void StopHorizontalMotion()
        {
            if (mRigidbody == null || mRigidbody.isKinematic)
            {
                return;
            }

            var velocity = mRigidbody.velocity;
            velocity.x = 0f;
            velocity.z = 0f;
            mRigidbody.velocity = velocity;
        }

        private void UpdateAnimator(float deltaTime)
        {
            if (!TryGetMovementTarget() || mAnimator == null)
            {
                return;
            }

            var inputMagnitude = Mathf.Clamp01(mMoveDirection.magnitude);
            var hasMovementInput = inputMagnitude > MoveThreshold;
            var normalizedInput = hasMovementInput
                ? mMoveDirection / inputMagnitude
                : Vector2.zero;
            var inputComponent = Owner?.GetComponent<PlayerInputComponent>();
            var isRunning = hasMovementInput && inputComponent != null && inputComponent.IsRunning;
            var targetAnimationSpeed = hasMovementInput
                ? Mathf.Lerp(0f, isRunning ? RunAnimationSpeed : WalkAnimationSpeed, inputMagnitude)
                : 0f;

            SetAnimatorFloat(SpeedParameterHash, targetAnimationSpeed, deltaTime);
            SetAnimatorFloat(DirectionParameterHash, normalizedInput.x, deltaTime);
            SetAnimatorFloat(MoveXParameterHash, normalizedInput.x, deltaTime);
            SetAnimatorFloat(MoveYParameterHash, normalizedInput.y, deltaTime);
            SetAnimatorFloat(MoveMagnitudeParameterHash, inputMagnitude, deltaTime);
            SetAnimatorBool(IsMovingParameterHash, hasMovementInput);
            SetAnimatorBool(IsRunningParameterHash, isRunning);
            SetAnimatorInteger(MoveDirectionParameterHash, GetEightDirectionIndex(normalizedInput, hasMovementInput));
        }

        private void CacheAnimatorParameters()
        {
            if (mAnimatorParameterHashes == null)
            {
                mAnimatorParameterHashes = new HashSet<int>();
            }
            else
            {
                mAnimatorParameterHashes.Clear();
            }

            foreach (var parameter in mAnimator.parameters)
            {
                mAnimatorParameterHashes.Add(parameter.nameHash);
            }
        }

        private bool HasAnimatorParameter(int parameterHash)
        {
            return mAnimatorParameterHashes != null && mAnimatorParameterHashes.Contains(parameterHash);
        }

        private void SetAnimatorFloat(int parameterHash, float value, float deltaTime)
        {
            if (!HasAnimatorParameter(parameterHash))
            {
                return;
            }

            if (AnimationDampTime > 0f && deltaTime > 0f)
            {
                mAnimator.SetFloat(parameterHash, value, AnimationDampTime, deltaTime);
                return;
            }

            mAnimator.SetFloat(parameterHash, value);
        }

        private void SetAnimatorBool(int parameterHash, bool value)
        {
            if (HasAnimatorParameter(parameterHash))
            {
                mAnimator.SetBool(parameterHash, value);
            }
        }

        private void SetAnimatorInteger(int parameterHash, int value)
        {
            if (HasAnimatorParameter(parameterHash))
            {
                mAnimator.SetInteger(parameterHash, value);
            }
        }

        private static int GetEightDirectionIndex(Vector2 normalizedInput, bool hasMovementInput)
        {
            if (!hasMovementInput)
            {
                return -1;
            }

            var angle = Mathf.Atan2(normalizedInput.x, normalizedInput.y) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f;
            }

            return Mathf.RoundToInt(angle / 45f) % 8;
        }
    }
}

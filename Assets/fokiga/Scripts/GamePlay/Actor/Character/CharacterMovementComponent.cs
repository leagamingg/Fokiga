using Fokiga.Runtime.Core;
using UnityEngine;

namespace Fokiga.Runtime.Gameplay
{
    public sealed class CharacterMovementComponent : ComponentBase
    {
        public const float DefaultMoveSpeed = 4f;
        public const float DefaultRunSpeed = 7f;
        public const float DefaultRotationSpeed = 720f;

        public float MoveSpeed { get; set; } = DefaultMoveSpeed;

        public float RunSpeed { get; set; } = DefaultRunSpeed;

        public float RotationSpeed { get; set; } = DefaultRotationSpeed;

        private Transform mTransform;
        private Rigidbody mRigidbody;
        private Animator mAnimator;
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

            var direction = ResolveMovementDirection();
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            var inputComponent = Owner.GetComponent<PlayerInputComponent>();
            var speed = inputComponent != null && inputComponent.IsRunning ? RunSpeed : MoveSpeed;
            var horizontalVelocity = direction * Mathf.Max(0f, speed);

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

            if (direction.sqrMagnitude > 0.0001f)
            {
                RotateTowards(direction, fixedDeltaTime);
            }
        }

        public override void OnDisable()
        {
            StopHorizontalMotion();
            mMoveDirection = Vector2.zero;
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
    }
}

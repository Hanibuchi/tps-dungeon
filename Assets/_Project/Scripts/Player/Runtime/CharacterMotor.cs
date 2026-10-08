using UnityEngine;

namespace TpsDungeon.Player
{
    /// <summary>
    /// 操作しているキャラの歩き・走り・ジャンプ・重力と、カメラの注視点の回転。Starter Assets の ThirdPersonController を写したもの。
    /// 元との違い:
    /// - 入力は同じ GameObject からではなく <see cref="Input"/>（パーティーの操作台の <see cref="CharacterInput"/>）から読む。
    ///   全員が同じプレハブなので、キャラごとに PlayerInput を持たせない（持たせると別々のプレイヤー扱いになる）。
    /// - パーティーの先頭のときだけ有効にする（後ろの仲間は NavMeshAgent が動かす）。
    /// - 先頭を替えてもカメラが跳ねないよう、注視点の向きを <see cref="CameraYaw"/> / <see cref="CameraPitch"/> で読み書きできる。
    /// 足音と着地の音はアニメーションのイベント（OnFootstep / OnLand）で鳴らす。無効でも呼ばれるので、仲間の足音もここで鳴る。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [AddComponentMenu("TPS Dungeon/Character Motor")]
    public sealed class CharacterMotor : MonoBehaviour
    {
        [Header("歩き")]
        [Tooltip("歩く速さ（m/秒）。")]
        public float MoveSpeed = 2.0f;

        [Tooltip("走る速さ（m/秒）。")]
        public float SprintSpeed = 5.335f;

        [Tooltip("歩く向きへ体を回す速さ（秒）。")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("速さの追従の速さ。")]
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("ジャンプの高さ（m）。")]
        public float JumpHeight = 1.2f;

        [Tooltip("重力。")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("次にジャンプできるまでの秒数。")]
        public float JumpTimeout = 0.50f;

        [Tooltip("落ちていると見なすまでの秒数。階段を下りるとき用。")]
        public float FallTimeout = 0.15f;

        [Header("接地")]
        [Tooltip("接地しているか（CharacterController の判定とは別）。")]
        public bool Grounded = true;

        [Tooltip("接地の判定の高さのずれ。")]
        public float GroundedOffset = -0.14f;

        [Tooltip("接地の判定の半径。CharacterController の半径に合わせる。")]
        public float GroundedRadius = 0.28f;

        [Tooltip("地面とみなすレイヤー。")]
        public LayerMask GroundLayers = 1;

        [Header("カメラ")]
        [Tooltip("カメラが追う注視点。")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("上を向ける角度。")]
        public float TopClamp = 70.0f;

        [Tooltip("下を向ける角度。")]
        public float BottomClamp = -30.0f;

        [Tooltip("注視点の向きに足す角度。")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("カメラの向きを固定するか。")]
        public bool LockCameraPosition = false;

        private const float Threshold = 0.01f;
        private const float TerminalVelocity = 53.0f;

        private static readonly int AnimIdSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimIdGrounded = Animator.StringToHash("Grounded");
        private static readonly int AnimIdJump = Animator.StringToHash("Jump");
        private static readonly int AnimIdFreeFall = Animator.StringToHash("FreeFall");
        private static readonly int AnimIdMotionSpeed = Animator.StringToHash("MotionSpeed");

        private float cameraYaw;
        private float cameraPitch;

        private float speed;
        private float animationBlend;
        private float targetRotation;
        private float rotationVelocity;
        private float verticalVelocity;

        private float jumpTimeoutDelta;
        private float fallTimeoutDelta;

        private Animator animator;
        private CharacterController controller;
        private Transform mainCamera;
        private bool cameraInitialized;

        /// <summary>入力の値。未設定ならシーンから探す。</summary>
        public CharacterInput Input { get; set; }

        /// <summary>カメラの注視点の左右の向き（度）。</summary>
        public float CameraYaw
        {
            get { EnsureCameraAngles(); return cameraYaw; }
            set { cameraInitialized = true; cameraYaw = value; }
        }

        /// <summary>カメラの注視点の上下の向き（度）。</summary>
        public float CameraPitch
        {
            get { EnsureCameraAngles(); return cameraPitch; }
            set { cameraInitialized = true; cameraPitch = value; }
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            TryGetComponent(out animator);
        }

        private void OnEnable()
        {
            // 前に先頭だったときの勢いを持ち越さない。
            speed = 0f;
            animationBlend = 0f;
            rotationVelocity = 0f;
            verticalVelocity = 0f;
            targetRotation = transform.eulerAngles.y;
            jumpTimeoutDelta = JumpTimeout;
            fallTimeoutDelta = FallTimeout;
        }

        private void Update()
        {
            if (Input == null) Input = FindAnyObjectByType<CharacterInput>();
            if (Input == null) return;
            if (mainCamera == null && Camera.main != null) mainCamera = Camera.main.transform;

            JumpAndGravity();
            GroundedCheck();
            Move();
        }

        private void LateUpdate()
        {
            if (Input == null) return;
            CameraRotation();
        }

        private void EnsureCameraAngles()
        {
            if (cameraInitialized || CinemachineCameraTarget == null) return;
            cameraInitialized = true;
            cameraYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
            cameraPitch = 0f;
        }

        private void GroundedCheck()
        {
            Vector3 position = transform.position;
            var spherePosition = new Vector3(position.x, position.y - GroundedOffset, position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
            if (animator != null) animator.SetBool(AnimIdGrounded, Grounded);
        }

        private void CameraRotation()
        {
            EnsureCameraAngles();
            if (CinemachineCameraTarget == null) return;

            if (Input.look.sqrMagnitude >= Threshold && !LockCameraPosition)
            {
                // マウスの動きには deltaTime を掛けない。
                float deltaTimeMultiplier = Input.IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
                cameraYaw += Input.look.x * deltaTimeMultiplier;
                cameraPitch += Input.look.y * deltaTimeMultiplier;
            }

            cameraYaw = ClampAngle(cameraYaw, float.MinValue, float.MaxValue);
            cameraPitch = ClampAngle(cameraPitch, BottomClamp, TopClamp);
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(cameraPitch + CameraAngleOverride, cameraYaw, 0.0f);
        }

        private void Move()
        {
            float targetSpeed = Input.sprint ? SprintSpeed : MoveSpeed;
            if (Input.move == Vector2.zero) targetSpeed = 0.0f;

            Vector3 velocity = controller.velocity;
            float currentHorizontalSpeed = new Vector3(velocity.x, 0.0f, velocity.z).magnitude;

            const float speedOffset = 0.1f;
            float inputMagnitude = Input.analogMovement ? Input.move.magnitude : 1f;

            if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
                speed = Mathf.Round(speed * 1000f) / 1000f;
            }
            else
            {
                speed = targetSpeed;
            }

            animationBlend = Mathf.Lerp(animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (animationBlend < 0.01f) animationBlend = 0f;

            Vector3 inputDirection = new Vector3(Input.move.x, 0.0f, Input.move.y).normalized;
            if (Input.move != Vector2.zero)
            {
                float cameraYawNow = mainCamera != null ? mainCamera.eulerAngles.y : CameraYaw;
                targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraYawNow;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation, ref rotationVelocity, RotationSmoothTime);
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }

            Vector3 targetDirection = Quaternion.Euler(0.0f, targetRotation, 0.0f) * Vector3.forward;
            controller.Move(targetDirection.normalized * (speed * Time.deltaTime) + new Vector3(0.0f, verticalVelocity, 0.0f) * Time.deltaTime);

            if (animator != null)
            {
                animator.SetFloat(AnimIdSpeed, animationBlend);
                animator.SetFloat(AnimIdMotionSpeed, inputMagnitude);
            }
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                fallTimeoutDelta = FallTimeout;

                if (animator != null)
                {
                    animator.SetBool(AnimIdJump, false);
                    animator.SetBool(AnimIdFreeFall, false);
                }

                if (verticalVelocity < 0.0f) verticalVelocity = -2f;

                if (Input.jump && jumpTimeoutDelta <= 0.0f)
                {
                    verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                    if (animator != null) animator.SetBool(AnimIdJump, true);
                }

                if (jumpTimeoutDelta >= 0.0f) jumpTimeoutDelta -= Time.deltaTime;
            }
            else
            {
                jumpTimeoutDelta = JumpTimeout;

                if (fallTimeoutDelta >= 0.0f) fallTimeoutDelta -= Time.deltaTime;
                else if (animator != null) animator.SetBool(AnimIdFreeFall, true);

                Input.jump = false;
            }

            if (verticalVelocity < TerminalVelocity) verticalVelocity += Gravity * Time.deltaTime;
        }

        private static float ClampAngle(float angle, float min, float max)
        {
            if (angle < -360f) angle += 360f;
            if (angle > 360f) angle -= 360f;
            return Mathf.Clamp(angle, min, max);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Grounded ? new Color(0.0f, 1.0f, 0.0f, 0.35f) : new Color(1.0f, 0.0f, 0.0f, 0.35f);
            Vector3 position = transform.position;
            Gizmos.DrawSphere(new Vector3(position.x, position.y - GroundedOffset, position.z), GroundedRadius);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight <= 0.5f || FootstepAudioClips == null || FootstepAudioClips.Length == 0) return;

            int index = Random.Range(0, FootstepAudioClips.Length);
            AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(Center()), FootstepAudioVolume);
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight <= 0.5f || LandingAudioClip == null) return;
            AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(Center()), FootstepAudioVolume);
        }

        private Vector3 Center() => controller != null ? controller.center : Vector3.up;
    }
}

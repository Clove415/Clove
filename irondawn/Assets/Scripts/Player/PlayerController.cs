using UnityEngine;

namespace IronDawn
{
    /// <summary>
    /// 第三人称移动。
    /// C 下蹲（按一次切换，头顶有障碍时站不起来），Q 左探头，E 右探头（按住）。
    /// 探头只移动相机、不移动身体——这是越肩视角下最省事也最不容易穿模的做法。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float walkSpeed = 6.4f;
        public float aimSpeed = 3.3f;

        public float crouchSpeed = 2.9f;
        public float crouchAimSpeed = 1.6f;

        public float acceleration = 16f;
        public float airControl = 0.35f;

        public float gravity = -24f;
        public float jumpHeight = 1.05f;

        public float lookSensitivity = 2.4f;
        public float minPitch = -52f;
        public float maxPitch = 68f;

        public float standHeight = 1.8f;
        public float crouchHeight = 1.15f;
        public float heightSmooth = 12f;
        public float eyeOffset = 0.22f;

        public ThirdPersonCamera cameraRig;
        public Transform weaponPivot;
        public Transform head;
        public PlayerVisual visual;

        /// <summary>下蹲时相机锚点额外下沉的高度。默认 0：镜头不跟着蹲，只有人物蹲下。</summary>
        public float crouchCameraDrop = 0f;

        public Health health;
        public bool InputLocked { get; set; }
        public bool IsAiming { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsGrounded { get { return _controller != null && _controller.isGrounded; } }

        /// <summary>当前探头方向：-1 左，+1 右，0 居中。给 HUD 用。</summary>
        public float LeanAmount { get { return cameraRig != null ? cameraRig.Lean : 0f; } }

        private CharacterController _controller;
        private float _yaw;
        private float _pitch;
        private float _verticalVelocity;
        private Vector3 _horizontalVelocity;

        /// <summary>身体朝向。可以与 <see cref="_yaw"/>（视线朝向）不同：平移时身体转向移动方向。</summary>
        private float _bodyYaw;
        private float _fireFacingUntil;

        /// <summary>身体转向的角速度（度/秒）。</summary>
        public float turnSpeed = 720f;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (health == null) health = GetComponent<Health>();
            if (health == null) health = gameObject.AddComponent<Health>();
            _yaw = transform.eulerAngles.y;
            _bodyYaw = _yaw;
        }

        void Start()
        {
            LockCursor(true);
        }

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) LockCursor(false);

            // 这里刻意不再"未锁定 + 点左键就重新锁定鼠标"。
            // 暂停面板的按钮本身就是鼠标点击，而 Update 早于 OnGUI：
            // 一旦在这里抢先锁定，鼠标位置会被固定在屏幕中心，点击就永远落不到按钮上，
            // 连面板都会因为"已锁定"而不绘制——表现为点「离开对局」没反应。
            // 重新进入游戏改由 HUD 的「继续游戏」按钮 / 点击面板外的画面负责。

            bool dead = health != null && !health.IsAlive;
            if (InputLocked || dead)
            {
                // 死亡或失能时退出瞄准与探头，否则镜头会一直偏着
                if (cameraRig != null)
                {
                    cameraRig.IsAiming = false;
                    cameraRig.Lean = 0f;
                }

                _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, Vector3.zero, acceleration * Time.deltaTime);
                FeedVisual();
                ApplyGravityAndMove();
                return;
            }

            Look();
            HandleStance();
            HandleLean();
            Move();
            FeedVisual();
        }

        /// <summary>把移动状态喂给模型，走路摆臂摆腿全靠这个速度。</summary>
        private void FeedVisual()
        {
            if (visual == null) return;

            visual.Speed = _horizontalVelocity.magnitude;
            visual.Grounded = _controller != null && _controller.isGrounded;
            visual.Aiming = IsAiming;
        }

        private void Look()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;

            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");

            _yaw += mx * lookSensitivity;
            _pitch = Mathf.Clamp(_pitch - my * lookSensitivity, minPitch, maxPitch);

            // 身体朝向不在这里改：由 UpdateFacing 按"是否瞄准/移动"决定
            if (cameraRig != null)
            {
                cameraRig.Pitch = _pitch;
                cameraRig.Yaw = _yaw;
            }
        }

        public void AddRecoil(float degrees)
        {
            _pitch = Mathf.Clamp(_pitch - degrees, minPitch, maxPitch);
            if (cameraRig != null) cameraRig.Pitch = _pitch;
        }

        /// <summary>重生/传送时把朝向和速度一起按死，避免带着上一局的动量飞出去。</summary>
        public void SnapRotation(float yaw, float pitch)
        {
            _yaw = yaw;
            _bodyYaw = yaw;
            _pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(0f, _bodyYaw, 0f);
            if (cameraRig != null)
            {
                cameraRig.Pitch = _pitch;
                cameraRig.Yaw = _yaw;
            }

            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
        }

        // ---------------- 下蹲 ----------------

        private void HandleStance()
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                if (IsCrouching)
                {
                    // 头顶有东西就站不起来，避免把人塞进天花板
                    if (CanStandUp()) IsCrouching = false;
                }
                else
                {
                    IsCrouching = true;
                }
            }

            float targetHeight = IsCrouching ? crouchHeight : standHeight;
            float k = 1f - Mathf.Exp(-heightSmooth * Time.deltaTime);

            // 碰撞体仍然要变矮，否则钻不过低矮空间
            float h = Mathf.Lerp(_controller.height, targetHeight, k);
            _controller.height = h;
            _controller.center = new Vector3(0f, h * 0.5f, 0f);

            // 人物模型自己蹲下：腿缩短、上半身下沉
            if (visual != null) visual.SetCrouch(IsCrouching);

            // 相机锚点默认不动。只有把 crouchCameraDrop 调大于 0 才会跟着降一点
            if (crouchCameraDrop > 0f && head != null)
            {
                float targetEye = standHeight - eyeOffset - (IsCrouching ? crouchCameraDrop : 0f);
                Vector3 lp = head.localPosition;
                lp.y = Mathf.Lerp(lp.y, targetEye, k);
                head.localPosition = lp;
            }
        }

        private bool CanStandUp()
        {
            float distance = standHeight - _controller.height;
            if (distance <= 0.02f) return true;

            float radius = _controller.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (_controller.height - radius);

            RaycastHit[] hits = Physics.SphereCastAll(
                origin, radius, Vector3.up, distance, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null) continue;
                if (col.transform.IsChildOf(transform)) continue;
                return false;
            }

            return true;
        }

        // ---------------- 探头 ----------------

        private void HandleLean()
        {
            float target = 0f;
            if (Input.GetKey(KeyCode.Q)) target -= 1f;
            if (Input.GetKey(KeyCode.E)) target += 1f;

            // 相机横移 + 模型侧倾，两边用同一个值，动作才对得上
            if (cameraRig != null) cameraRig.Lean = target;
            if (visual != null) visual.SetLean(target);
        }

        // ---------------- 移动 ----------------

        private void Move()
        {
            IsAiming = Input.GetMouseButton(1);

            // 关键：把瞄准状态同步给相机，视野缩放和肩位切换都依赖它
            if (cameraRig != null) cameraRig.IsAiming = IsAiming;

            float h = 0f;
            float v = 0f;
            if (Input.GetKey(KeyCode.W)) v += 1f;
            if (Input.GetKey(KeyCode.S)) v -= 1f;
            if (Input.GetKey(KeyCode.D)) h += 1f;
            if (Input.GetKey(KeyCode.A)) h -= 1f;

            // 输入方向必须以视线（相机）为基准。
            // 如果拿身体朝向当基准，身体一转 WASD 的参考系就跟着转，会原地打转。
            Quaternion viewRot = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 wish = viewRot * (Vector3.forward * v + Vector3.right * h);
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            float speed;
            if (IsCrouching) speed = IsAiming ? crouchAimSpeed : crouchSpeed;
            else speed = IsAiming ? aimSpeed : walkSpeed;

            UpdateFacing(wish);

            // 手里的枪：瞄准时指向准心（带俯仰），平时跟着身体走
            if (weaponPivot != null)
            {
                float weaponYaw = IsAiming ? _yaw : transform.eulerAngles.y;
                float weaponPitch = IsAiming ? _pitch : 0f;
                weaponPivot.rotation = Quaternion.Euler(weaponPitch, weaponYaw, 0f);
            }

            float accel = acceleration * (_controller.isGrounded ? 1f : airControl);
            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity, wish * speed, accel * Time.deltaTime);

            // 蹲着不能跳
            if (_controller.isGrounded && !IsCrouching && Input.GetKeyDown(KeyCode.Space))
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            ApplyGravityAndMove();
        }

        /// <summary>
        /// 决定身体朝哪转：
        ///   · 瞄准中、或刚开过枪 → 正面朝准心（视线方向）；
        ///   · 否则在移动 → 正面朝移动方向（左右平移就侧身对着移动方向）；
        ///   · 站着不动且没开枪 → 保持当前朝向，不会自己乱转。
        /// </summary>
        private void UpdateFacing(Vector3 wish)
        {
            bool faceAim = IsAiming || Time.time < _fireFacingUntil;

            float targetYaw;

            if (faceAim)
            {
                targetYaw = _yaw;
            }
            else if (wish.sqrMagnitude > 0.0001f)
            {
                targetYaw = Mathf.Atan2(wish.x, wish.z) * Mathf.Rad2Deg;
            }
            else
            {
                targetYaw = _bodyYaw;
            }

            _bodyYaw = Mathf.MoveTowardsAngle(_bodyYaw, targetYaw, turnSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, _bodyYaw, 0f);
        }

        /// <summary>开枪时让身体立刻正对准心，哪怕没有按右键瞄准。</summary>
        public void ForceFacingAim(float seconds)
        {
            _fireFacingUntil = Mathf.Max(_fireFacingUntil, Time.time + Mathf.Max(0f, seconds));
        }

        private void ApplyGravityAndMove()
        {
            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;

            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = _horizontalVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }
    }
}

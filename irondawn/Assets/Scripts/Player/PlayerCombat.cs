using IronDawn.Multiplayer;
using UnityEngine;

namespace IronDawn
{
    /// <summary>
    /// 玩家的武器控制。四个槽位对应键盘 1/2/3/4：
    ///   1 步枪（全自动）、2 手枪（半自动）、3 匕首（近战）、4 投掷物（在四种之间循环）。
    ///
    /// 命中判定的关键点是"不让子弹穿过墙"：
    /// 先用相机射线求出玩家真正瞄的那个点（准星看到的位置），
    /// 再从人物眼睛向那个点补一条射线作为真实弹道——
    /// 相机在第三人称下位于人物身后，如果只从相机打，墙挡在相机和人物之间时就会被整段跳过。
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        /// <summary>单发结算结果。</summary>
        public struct ShotInfo
        {
            public Vector3 from;
            public Vector3 to;
            public byte hitId;
            public float damage;
            public byte kind;   // 0 = 子弹，1 = 近战
        }

        public event System.Action<ShotInfo> ShotResolved;
        public event System.Action<ThrowableData> ThrowResolved;

        public Camera viewCamera;
        public Transform muzzle;
        public Transform head;
        public PlayerController controller;
        public WeaponModels models;

        public LayerMask hitMask = ~0;

        private static readonly System.Comparison<RaycastHit> ByDistance =
            (a, b) => a.distance.CompareTo(b.distance);

        public WeaponSlot Slot { get; private set; }
        public ThrowableKind Throwable { get; private set; }
        public int RifleAmmo { get; private set; }
        public int PistolAmmo { get; private set; }
        public int FireCount { get; private set; }
        public int SmokeCount { get; private set; }
        public int HeCount { get; private set; }
        public int FlashCount { get; private set; }

        public bool Reloading { get; private set; }

        public float ReloadProgress
        {
            get
            {
                if (!Reloading) return 1f;
                return Mathf.Clamp01(1f - (_reloadEnd - Time.time) / _reloadLength);
            }
        }

        public bool IsGun { get { return Slot == WeaponSlot.Primary || Slot == WeaponSlot.Secondary; } }

        public string WeaponName
        {
            get
            {
                if (Slot == WeaponSlot.Throwable) return WeaponDefs.ThrowableName(Throwable);
                return WeaponDefs.Spec(Slot).name;
            }
        }

        public string AmmoLabel
        {
            get
            {
                switch (Slot)
                {
                    case WeaponSlot.Primary: return RifleAmmo + " / " + WeaponDefs.Rifle.magazine;
                    case WeaponSlot.Secondary: return PistolAmmo + " / " + WeaponDefs.Pistol.magazine;
                    case WeaponSlot.Melee: return "—";
                    default: return "× " + CountOf(Throwable);
                }
            }
        }

        public int CountOf(ThrowableKind kind)
        {
            switch (kind)
            {
                case ThrowableKind.Smoke: return SmokeCount;
                case ThrowableKind.He: return HeCount;
                case ThrowableKind.Fire: return FireCount;
                default: return FlashCount;
            }
        }

        private float _nextFire;
        private float _fireBlockedUntil;
        private float _reloadEnd;
        private float _reloadLength = 1f;
        private byte _lastHitId;

        void Awake()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            ResetLoadout();
        }

        /// <summary>出生/重生时把弹药和投掷物补满。</summary>
        public void ResetLoadout()
        {
            RifleAmmo = WeaponDefs.Rifle.magazine;
            PistolAmmo = WeaponDefs.Pistol.magazine;
            SmokeCount = WeaponDefs.ThrowableCapacity[(int)ThrowableKind.Smoke];
            HeCount = WeaponDefs.ThrowableCapacity[(int)ThrowableKind.He];
            FireCount = WeaponDefs.ThrowableCapacity[(int)ThrowableKind.Fire];
            FlashCount = WeaponDefs.ThrowableCapacity[(int)ThrowableKind.Flash];

            Slot = WeaponSlot.Primary;
            Throwable = ThrowableKind.Smoke;
            Reloading = false;

            SyncHoldPose();
        }

        /// <summary>短暂禁止开火：用于从暂停面板返回游戏时，避免把"点继续"的那一下当成扣扳机。</summary>
        public void SuppressFire(float seconds)
        {
            _fireBlockedUntil = Mathf.Max(_fireBlockedUntil, Time.time + Mathf.Max(0f, seconds));
        }

        void Update()
        {
            if (Reloading && Time.time >= _reloadEnd)
            {
                Reloading = false;
                if (Slot == WeaponSlot.Primary) RifleAmmo = WeaponDefs.Rifle.magazine;
                else if (Slot == WeaponSlot.Secondary) PistolAmmo = WeaponDefs.Pistol.magazine;
            }

            if (controller != null && controller.InputLocked) return;
            if (controller != null && controller.health != null && !controller.health.IsAlive) return;

            HandleSwitch();

            if (Input.GetKeyDown(KeyCode.R)) StartReload();

            if (IsGun)
            {
                if (AmmoInMagazine() <= 0 && !Reloading) StartReload();
                if (Reloading) return;
            }

            if (!Input.GetMouseButton(0)) return;
            if (Cursor.lockState != CursorLockMode.Locked && !(controller != null && controller.IsAiming)) return;

            if (Time.time < _fireBlockedUntil) return;

            if (Slot == WeaponSlot.Throwable)
            {
                if (Input.GetMouseButtonDown(0)) TryThrow();
                return;
            }

            if (Slot == WeaponSlot.Melee)
            {
                if (Input.GetMouseButtonDown(0)) TryMelee();
                return;
            }

            ApplyTrigger();
        }

        private void ApplyTrigger()
        {
            WeaponSpec spec = WeaponDefs.Spec(Slot);

            // 半自动要求按下瞬间开火，全自动按住即可
            if (!spec.automatic && !Input.GetMouseButtonDown(0)) return;

            TryFireHitscan(spec);
        }

        private int AmmoInMagazine()
        {
            return Slot == WeaponSlot.Primary ? RifleAmmo : PistolAmmo;
        }

        // ---------------- 切枪 ----------------

        private void HandleSwitch()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectSlot(WeaponSlot.Primary);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectSlot(WeaponSlot.Secondary);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SelectSlot(WeaponSlot.Melee);
            else if (Input.GetKeyDown(KeyCode.Alpha4)) CycleThrowable();

            // 滚轮也可以切主副武器，和射击游戏的习惯一致
            float wheel = Input.GetAxisRaw("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) > 0.01f)
                SelectSlot(Slot == WeaponSlot.Primary ? WeaponSlot.Secondary : WeaponSlot.Primary);
        }

        private void SelectSlot(WeaponSlot slot)
        {
            if (Slot == slot) return;

            Slot = slot;
            Reloading = false;
            _nextFire = 0f;

            SyncHoldPose();
        }

        /// <summary>和 CS2 一样：按 4 在拥有的投掷物之间循环。</summary>
        private void CycleThrowable()
        {
            if (Slot != WeaponSlot.Throwable)
            {
                Slot = WeaponSlot.Throwable;
                Reloading = false;
                _nextFire = 0f;

                if (CountOf(Throwable) <= 0) StepThrowable();
                SyncHoldPose();
                return;
            }

            StepThrowable();
            SyncHoldPose();
        }

        private void StepThrowable()
        {
            for (int i = 0; i < WeaponDefs.ThrowableKindCount; i++)
            {
                ThrowableKind next = (ThrowableKind)(((int)Throwable + 1 + i) % WeaponDefs.ThrowableKindCount);
                if (CountOf(next) <= 0) continue;

                Throwable = next;
                return;
            }
        }

        // ---------------- 开火 ----------------

        public void StartReload()
        {
            if (!IsGun || Reloading) return;

            WeaponSpec spec = WeaponDefs.Spec(Slot);
            if (AmmoInMagazine() >= spec.magazine) return;

            Reloading = true;
            _reloadLength = spec.reloadTime;
            _reloadEnd = Time.time + _reloadLength;
        }

        /// <summary>把当前槽位同步给手臂姿势和手里的武器模型。</summary>
        private void SyncHoldPose()
        {
            if (models != null) models.Show(Slot, Throwable);

            if (controller != null && controller.visual != null)
                controller.visual.SetHoldPose(Slot);
        }

        private void TryFireHitscan(WeaponSpec spec)
        {
            if (Time.time < _nextFire) return;

            int ammo = AmmoInMagazine();
            if (ammo <= 0) return;

            _nextFire = Time.time + spec.fireInterval;

            // 开枪瞬间身体立刻正对准心，哪怕没按右键瞄准
            if (controller != null) controller.ForceFacingAim(0.45f);

            if (Slot == WeaponSlot.Primary) RifleAmmo = ammo - 1;
            else PistolAmmo = ammo - 1;

            bool aiming = controller != null && controller.IsAiming;
            float spread = aiming ? spec.aimSpread : spec.hipSpread;

            Vector3 eye = EyePosition();
            Vector3 aimPoint = ResolveAimPoint(spec.range, spread);
            Vector3 dir = aimPoint - eye;
            float distance = dir.magnitude;

            if (distance < 0.05f) return;
            dir /= distance;

            distance = Mathf.Min(distance + 0.4f, spec.range);

            Vector3 end = eye + dir * distance;
            byte hitId = 0;
            bool hitSomething = false;

            RaycastHit[] hits = Physics.RaycastAll(eye, dir, distance, hitMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, ByDistance);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null) continue;
                if (col.transform.IsChildOf(transform)) continue;

                // 眼睛在自身体积内，起点附近的自身碰撞体要跳过；
                // 第一个"不是自己"的命中就是真实弹道终点，墙挡在前面就打不到后面的人
                end = hits[i].point;
                hitSomething = true;

                IDamageable target = col.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive)
                {
                    INetHitTarget netTarget = target as INetHitTarget;
                    if (netTarget != null) hitId = netTarget.NetId;

                    target.ApplyDamage(spec.damage, hits[i].point, hits[i].normal, gameObject);
                    Tracer.Flash(hits[i].point, 0.1f, Palette.Muzzle, 0.06f);
                }

                break;
            }

            Vector3 tracerStart = muzzle != null ? muzzle.position : eye;
            Tracer.Draw(tracerStart, end, Palette.PlayerTracer, 0.032f, 0.05f);

            if (!hitSomething) end = eye + dir * distance;

            RaiseShot(tracerStart, end, hitId, spec.damage, 0);
            _lastHitId = hitId;

            if (controller != null) controller.AddRecoil(spec.recoil);
        }

        private void TryMelee()
        {
            if (Time.time < _nextFire) return;
            _nextFire = Time.time + WeaponDefs.Knife.fireInterval;

            if (controller != null) controller.ForceFacingAim(0.4f);

            Vector3 eye = EyePosition();
            Vector3 dir = viewCamera != null ? viewCamera.transform.forward : transform.forward;

            // 近战不需要精确瞄头，稍微微调一下朝向
            RaycastHit[] hits = Physics.RaycastAll(eye, dir, WeaponDefs.Knife.range, hitMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, ByDistance);

            Vector3 end = eye + dir * WeaponDefs.Knife.range;
            byte hitId = 0;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null) continue;
                if (col.transform.IsChildOf(transform)) continue;

                end = hits[i].point;

                IDamageable target = col.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive)
                {
                    INetHitTarget netTarget = target as INetHitTarget;
                    if (netTarget != null) hitId = netTarget.NetId;

                    target.ApplyDamage(WeaponDefs.Knife.damage, hits[i].point, hits[i].normal, gameObject);
                    Tracer.Flash(hits[i].point, 0.14f, new Color(1f, 0.62f, 0.55f), 0.07f);
                }

                break;
            }

            // 近战用一小段白色划痕代替弹道
            Vector3 start = muzzle != null ? muzzle.position : eye;
            Tracer.Draw(start, end, new Color(0.92f, 0.95f, 1f), 0.025f, 0.06f);

            RaiseShot(start, end, hitId, WeaponDefs.Knife.damage, 1);
        }

        private void TryThrow()
        {
            if (Time.time < _nextFire) return;
            if (CountOf(Throwable) <= 0) return;

            _nextFire = Time.time + 0.85f;

            if (controller != null) controller.ForceFacingAim(0.35f);

            switch (Throwable)
            {
                case ThrowableKind.Smoke: SmokeCount--; break;
                case ThrowableKind.He: HeCount--; break;
                case ThrowableKind.Fire: FireCount--; break;
                default: FlashCount--; break;
            }

            Vector3 eye = EyePosition();
            Vector3 dir = viewCamera != null ? viewCamera.transform.forward : transform.forward;

            Vector3 origin = eye + dir * 0.45f;
            Vector3 velocity = dir * WeaponDefs.ThrowSpeed(Throwable) + Vector3.up * 2.2f;

            byte selfId = LocalPlayerId();

            // 挂到世界根下：离开对局时随世界一起清掉，不会在场景里留下孤儿弹体
            Throwables.Spawn(Throwable, origin, velocity, WorldRoot(), selfId);

            ThrowableData data;
            data.owner = selfId;
            data.kind = (byte)Throwable;
            data.origin = origin;
            data.velocity = velocity;

            if (ThrowResolved != null) ThrowResolved(data);

            // 扔完自动跳到下一颗，符合"按一次扔一颗"的直觉
            if (CountOf(Throwable) <= 0) StepThrowable();
            SyncHoldPose();
        }

        // ---------------- 射线工具 ----------------

        private static byte LocalPlayerId()
        {
            LanNetManager net = LanNetManager.Instance;
            return net != null ? net.LocalId : (byte)0;
        }

        private static Transform WorldRoot()
        {
            ArenaRuntime runtime = ArenaRuntime.Instance;
            return runtime != null ? runtime.WorldRoot : null;
        }

        private Vector3 EyePosition()
        {
            if (head != null) return head.position;
            return transform.position + Vector3.up * 1.58f;
        }

        /// <summary>
        /// 先沿相机方向打一条求"准星落点"，再把这道射线当作瞄准点。
        /// 相机被墙挡住时会立刻停在墙上，于是后面的眼睛射线也打不到墙外的人。
        /// </summary>
        private Vector3 ResolveAimPoint(float range, float spread)
        {
            if (viewCamera == null) return transform.position + transform.forward * 20f;

            Vector3 origin = viewCamera.transform.position;
            Vector3 dir = MathUtil.Spread(viewCamera.transform.forward, spread);

            Vector3 point = origin + dir * range;

            RaycastHit hit;
            if (Physics.Raycast(origin, dir, out hit, range, hitMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform != null && !hit.transform.IsChildOf(transform))
                    point = hit.point;
            }

            return point;
        }

        private void RaiseShot(Vector3 from, Vector3 to, byte hitId, float damage, byte kind)
        {
            if (ShotResolved == null) return;

            ShotInfo info;
            info.from = from;
            info.to = to;
            info.hitId = hitId;
            info.damage = damage;
            info.kind = kind;
            ShotResolved(info);
        }

        /// <summary>给 HUD 判断准星命中反馈用。</summary>
        public bool HasRecentHit { get { return _lastHitId != 0; } }
    }
}

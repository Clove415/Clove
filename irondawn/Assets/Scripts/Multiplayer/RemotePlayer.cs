using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IronDawn.Multiplayer
{
    /// <summary>
    /// 远端玩家的本地替身。位置/姿态来自网络快照，用 100ms 插值缓冲播放，
    /// 所以在 20Hz 的更新率下看起来仍然是连续的。
    ///
    /// 它实现 IDamageable 只是为了让本地射击能"打到人"——
    /// 扣血不由射击方决定，被打的一方收到 Shot 消息后自己结算，
    /// 这里只负责让自己变成尸体。
    /// </summary>
    public sealed class RemotePlayer : MonoBehaviour, IDamageable, INetHitTarget
    {
        public PlayerVisual visual;
        public Transform weaponPivot;

        public byte NetId { get; private set; }
        public string PlayerName { get; private set; }
        public int Slot { get; private set; }
        public float Health01 { get; private set; }
        public bool Alive { get; private set; }

        private struct Snap
        {
            public float time;
            public Vector3 position;
            public float yaw;
        }

        /// <summary>渲染时间落后于最新快照的时长：用一点延迟换平滑。</summary>
        private const float InterpDelay = 0.1f;
        private const int MaxSnaps = 16;

        private readonly List<Snap> _snaps = new List<Snap>();

        private PlayerFlags _flags;
        private float _pitch;
        private bool _hasState;
        private bool _squashed;
        private Vector3 _baseScale = Vector3.one;
        private Collider _collider;

        public void Setup(byte id, string playerName, int slot, PlayerVisual playerVisual, Transform weapon)
        {
            NetId = id;
            PlayerName = playerName;
            Slot = slot;
            visual = playerVisual;
            weaponPivot = weapon;

            Health01 = 1f;
            Alive = true;
            _flags = PlayerFlags.Alive;

            _collider = GetComponent<Collider>();

            if (visual != null) _baseScale = visual.transform.localScale;
        }

        // ---------------- 网络数据 ----------------

        public void ApplyState(PlayerStateData d)
        {
            _pitch = d.pitch;
            _flags = d.flags;
            Health01 = Mathf.Clamp01(d.health);

            bool alive = (d.flags & PlayerFlags.Alive) != 0;
            if (alive != Alive)
            {
                Alive = alive;
                if (alive) RestoreVisual();
                else StartDeathVisual();
            }

            if (visual != null)
            {
                visual.SetCrouch((d.flags & PlayerFlags.Crouching) != 0);
                visual.SetLean(LeanOf(d.flags));
            }

            Snap s;
            s.time = Time.time;
            s.position = d.position;
            s.yaw = d.yaw;

            // 瞬移（重生、卡顿补齐）不走插值，否则会从场地一头滑到另一头
            if (_snaps.Count > 0)
            {
                Vector3 prev = _snaps[_snaps.Count - 1].position;
                if ((d.position - prev).sqrMagnitude > 36f) _snaps.Clear();
            }

            _snaps.Add(s);
            if (_snaps.Count > MaxSnaps) _snaps.RemoveAt(0);

            _hasState = true;
        }

        public void ApplyDeath()
        {
            Alive = false;
            Health01 = 0f;
            _flags &= ~PlayerFlags.Alive;
            StartDeathVisual();
        }

        public void ApplyRespawn(RespawnData d)
        {
            _snaps.Clear();
            transform.position = d.position;
            transform.rotation = Quaternion.Euler(0f, d.yaw, 0f);

            _hasState = true;
            Alive = true;
            Health01 = 1f;
            _flags |= PlayerFlags.Alive;

            RestoreVisual();
        }

        private static float LeanOf(PlayerFlags flags)
        {
            if ((flags & PlayerFlags.LeanLeft) != 0) return -1f;
            if ((flags & PlayerFlags.LeanRight) != 0) return 1f;
            return 0f;
        }

        // ---------------- 表现 ----------------

        void Update()
        {
            if (!_hasState) return;

            ApplyWeaponRotation();

            float renderTime = Time.time - InterpDelay;

            Vector3 pos;
            float yaw;
            if (Sample(renderTime, out pos, out yaw))
            {
                transform.position = pos;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            // 只留两个夹住渲染时刻的样本，别让列表无限长
            while (_snaps.Count > 2 && _snaps[1].time < renderTime - InterpDelay)
                _snaps.RemoveAt(0);
        }

        private void ApplyWeaponRotation()
        {
            if (weaponPivot == null) return;

            if ((_flags & PlayerFlags.Aiming) == 0)
            {
                weaponPivot.localRotation = Quaternion.identity;
                return;
            }

            weaponPivot.rotation = Quaternion.Euler(_pitch, transform.eulerAngles.y, 0f);
        }

        private bool Sample(float t, out Vector3 pos, out float yaw)
        {
            pos = transform.position;
            yaw = transform.eulerAngles.y;

            if (_snaps.Count == 0) return false;

            if (_snaps.Count == 1 || t <= _snaps[0].time)
            {
                pos = _snaps[0].position;
                yaw = _snaps[0].yaw;
                return true;
            }

            for (int i = 0; i < _snaps.Count - 1; i++)
            {
                Snap a = _snaps[i];
                Snap b = _snaps[i + 1];

                if (t < a.time || t > b.time) continue;

                float span = b.time - a.time;
                float k = span <= 0.0001f ? 1f : Mathf.Clamp01((t - a.time) / span);

                pos = Vector3.Lerp(a.position, b.position, k);
                yaw = Mathf.LerpAngle(a.yaw, b.yaw, k);
                return true;
            }

            Snap last = _snaps[_snaps.Count - 1];
            pos = last.position;
            yaw = last.yaw;
            return true;
        }

        private void StartDeathVisual()
        {
            if (_collider != null) _collider.enabled = false;
            if (_squashed) return;

            _squashed = true;
            StartCoroutine(SquashRoutine());
        }

        private void RestoreVisual()
        {
            StopAllCoroutines();
            _squashed = false;

            if (visual != null) visual.transform.localScale = _baseScale;
            if (_collider != null) _collider.enabled = true;
        }

        private IEnumerator SquashRoutine()
        {
            if (visual == null) yield break;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.35f;
                float k = Mathf.Clamp01(t);
                visual.transform.localScale = new Vector3(
                    _baseScale.x * (1f + k * 0.22f),
                    _baseScale.y * Mathf.Max(0.12f, 1f - k * 0.88f),
                    _baseScale.z * (1f + k * 0.22f));
                yield return null;
            }
        }

        // ---------------- IDamageable / INetHitTarget ----------------

        public bool IsAlive { get { return Alive; } }

        /// <summary>射击方不做权威扣血：命中只体现在弹道和命中反馈上。</summary>
        public void ApplyDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, GameObject source)
        {
        }
    }
}

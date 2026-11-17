using System;
using UnityEngine;

namespace IronDawn
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        void ApplyDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, GameObject source);
    }

    /// <summary>
    /// 通用生命值。所有会死的东西（玩家、敌人、通讯兵）都挂这一个。
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        public float maxHealth = 100f;

        [NonSerialized] public float current;

        public event Action<Health> Damaged;
        public event Action<Health> Died;

        public bool IsAlive { get { return current > 0f; } }

        public float Normalized
        {
            get { return maxHealth <= 0f ? 1f : Mathf.Clamp01(current / maxHealth); }
        }

        void Awake()
        {
            if (current <= 0f) current = maxHealth;
        }

        public void ResetToFull()
        {
            current = maxHealth;
        }

        public void ApplyDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, GameObject source)
        {
            if (!IsAlive || amount <= 0f) return;

            current -= amount;
            if (Damaged != null) Damaged(this);

            if (current <= 0f)
            {
                current = 0f;
                if (Died != null) Died(this);
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            current = Mathf.Min(maxHealth, current + amount);
        }
    }
}

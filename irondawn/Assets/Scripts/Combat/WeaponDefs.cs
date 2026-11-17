using UnityEngine;

namespace IronDawn
{
    /// <summary>武器槽位。对应键盘 1 / 2 / 3 / 4。</summary>
    public enum WeaponSlot
    {
        Primary = 0,
        Secondary = 1,
        Melee = 2,
        Throwable = 3
    }

    /// <summary>四种投掷物。数值取向参考 CS2：烟封视野、雷打爆发伤害、火封区域、闪致盲。</summary>
    public enum ThrowableKind
    {
        Smoke = 0,
        He = 1,
        Fire = 2,
        Flash = 3
    }

    public struct WeaponSpec
    {
        public string name;
        public bool hitscan;
        public bool automatic;
        public float fireInterval;
        public float damage;
        public float range;
        public int magazine;
        public float reloadTime;
        public float hipSpread;
        public float aimSpread;
        public float recoil;
    }

    public static class WeaponDefs
    {
        public const int ThrowableKindCount = 4;

        public static readonly WeaponSpec Rifle = new WeaponSpec
        {
            name = "步枪",
            hitscan = true,
            automatic = true,
            fireInterval = 0.105f,
            damage = 13f,
            range = 95f,
            magazine = 30,
            reloadTime = 1.65f,
            hipSpread = 1.7f,
            aimSpread = 0.28f,
            recoil = 0.32f
        };

        public static readonly WeaponSpec Pistol = new WeaponSpec
        {
            name = "手枪",
            hitscan = true,
            automatic = false,
            fireInterval = 0.17f,
            damage = 22f,
            range = 70f,
            magazine = 12,
            reloadTime = 1.35f,
            hipSpread = 1.15f,
            aimSpread = 0.22f,
            recoil = 0.62f
        };

        public static readonly WeaponSpec Knife = new WeaponSpec
        {
            name = "匕首",
            hitscan = false,
            automatic = false,
            fireInterval = 0.55f,
            damage = 55f,
            range = 2.1f,
            magazine = 0,
            reloadTime = 0f,
            hipSpread = 0f,
            aimSpread = 0f,
            recoil = 0f
        };

        /// <summary>出生时的投掷物携带量。</summary>
        public static readonly int[] ThrowableCapacity = { 2, 2, 1, 2 };

        public static WeaponSpec Spec(WeaponSlot slot)
        {
            switch (slot)
            {
                case WeaponSlot.Primary: return Rifle;
                case WeaponSlot.Secondary: return Pistol;
                default: return Knife;
            }
        }

        public static string SlotName(WeaponSlot slot)
        {
            switch (slot)
            {
                case WeaponSlot.Primary: return "1 步枪";
                case WeaponSlot.Secondary: return "2 手枪";
                case WeaponSlot.Melee: return "3 匕首";
                default: return "4 投掷物";
            }
        }

        public static string ThrowableName(ThrowableKind kind)
        {
            switch (kind)
            {
                case ThrowableKind.Smoke: return "烟雾弹";
                case ThrowableKind.He: return "高爆手雷";
                case ThrowableKind.Fire: return "燃烧瓶";
                default: return "闪光弹";
            }
        }

        public static Color ThrowableColor(ThrowableKind kind)
        {
            switch (kind)
            {
                case ThrowableKind.Smoke: return new Color(0.56f, 0.60f, 0.66f);
                case ThrowableKind.He: return new Color(0.35f, 0.40f, 0.24f);
                case ThrowableKind.Fire: return new Color(0.72f, 0.30f, 0.16f);
                default: return new Color(0.90f, 0.88f, 0.74f);
            }
        }

        /// <summary>引信时长。0 表示落地即触发（烟与火）。</summary>
        public static float FuseSeconds(ThrowableKind kind)
        {
            switch (kind)
            {
                case ThrowableKind.He: return 1.6f;
                case ThrowableKind.Flash: return 1.5f;
                default: return 0f;
            }
        }

        public static float ThrowSpeed(ThrowableKind kind)
        {
            switch (kind)
            {
                case ThrowableKind.He: return 17.5f;
                case ThrowableKind.Flash: return 18.5f;
                case ThrowableKind.Smoke: return 16.5f;
                default: return 15.5f;
            }
        }
    }
}

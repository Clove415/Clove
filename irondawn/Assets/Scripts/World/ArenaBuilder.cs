using UnityEngine;

namespace IronDawn
{
    /// <summary>
    /// 局域网对战场地：一个四面合围的封闭竞技场，没有街道尽头那种"跑出去"的方向。
    ///
    /// 两条硬规矩：
    ///   1. 场地全封闭——四面 12 米高墙 + 四角塔楼，玩家只能在场内打；
    ///   2. 场内所有物件都必须有碰撞体——原来那些纯装饰的废墟、瓦砾、光柱全部不再生成，
    ///      看得见的东西就是能挡子弹的东西，灰盒里不该有"假的"。
    ///
    /// 掩体按 4 重镜像摆放（x 与 z 各自翻转），所以四个方位的掩体密度完全一致，
    /// 出生点收益不会因为"站哪边"而不公平。
    /// </summary>
    public class ArenaBuilder : MonoBehaviour
    {
        public const float HalfSize = 30f;
        public const float WallHeight = 12f;
        public const int SpawnPointCount = 8;

        /// <summary>4 重镜像后共用的 8 个出生点（y 由 Spawn() 补）。</summary>
        private static readonly Vector2[] Spawns =
        {
            new Vector2(  0f, -24f),
            new Vector2( 24f,   0f),
            new Vector2(  0f,  24f),
            new Vector2(-24f,   0f),
            new Vector2( 14f, -14f),
            new Vector2( 14f,  14f),
            new Vector2(-14f,  14f),
            new Vector2(-14f, -14f)
        };

        private struct CoverSpec
        {
            public float x, z, w, h, d;

            public CoverSpec(float x, float z, float w, float h, float d)
            {
                this.x = x;
                this.z = z;
                this.w = w;
                this.h = h;
                this.d = d;
            }
        }

        /// <summary>
        /// 只写第一象限，Build 时翻转出另外三个象限。
        ///
        /// 高度约定：1.8 米的掩体会让人卡在"头皮位"——身体藏住了，脑袋露在外面，
        /// 双方都只能互相点头。所以战斗掩体统一压到 2.05 米（略高过人物），
        /// 既能完全藏住站姿，又不会让场地变成纯墙局。
        /// 例外是"站立平台"和它的台阶：平台要保持可以跳上去，台阶要能踩。
        /// </summary>
        private static readonly CoverSpec[] QuadrantCovers =
        {
            new CoverSpec( 8.5f,  6.0f, 5.5f, 2.05f, 2.0f),
            new CoverSpec(20.0f, 14.0f, 2.0f, 2.40f, 7.0f),
            new CoverSpec(11.0f, 21.0f, 7.0f, 2.05f, 2.0f),
            new CoverSpec(24.0f,  4.0f, 2.0f, 2.05f, 6.0f),
            new CoverSpec(21.0f, 25.0f, 7.0f, 1.50f, 7.0f),   // 站立平台：保持可跳上
            new CoverSpec(16.6f, 25.0f, 1.8f, 0.72f, 7.0f),   // 上台台阶
            new CoverSpec(26.5f, 20.0f, 2.0f, 2.05f, 2.6f)
        };

        public Transform root;

        public void Build()
        {
            if (root == null) root = transform;

            BuildGround();
            BuildWalls();
            BuildCovers();
            BuildCenter();
            BuildSpawnPads();
        }

        // ---------------- 外壳 ----------------

        private void BuildGround()
        {
            Prim.Box("ground", root, new Vector3(0f, -0.5f, 0f),
                new Vector3(HalfSize * 2f + 8f, 1f, HalfSize * 2f + 8f), Palette.Ground);
        }

        private void BuildWalls()
        {
            float h = WallHeight;
            float cy = h * 0.5f;
            float span = HalfSize * 2f + 4f;
            float offset = HalfSize + 1f;

            // 四面墙的内表面就落在 ±HalfSize 上，场地边界清清楚楚
            Prim.Box("wall_north", root, new Vector3(0f, cy, -offset), new Vector3(span, h, 2f), Palette.Wall);
            Prim.Box("wall_south", root, new Vector3(0f, cy, offset), new Vector3(span, h, 2f), Palette.Wall);
            Prim.Box("wall_east", root, new Vector3(offset, cy, 0f), new Vector3(2f, h, span), Palette.Wall);
            Prim.Box("wall_west", root, new Vector3(-offset, cy, 0f), new Vector3(2f, h, span), Palette.Wall);

            // 沿墙的腰线：也做实心，站在远处能立刻读出场地高度
            float trimY = 4.6f;
            float trimX = HalfSize - 0.4f;

            Prim.Box("trim_north", root, new Vector3(0f, trimY, -trimX), new Vector3(HalfSize * 2f, 1.2f, 0.8f), Palette.WallTrim);
            Prim.Box("trim_south", root, new Vector3(0f, trimY, trimX), new Vector3(HalfSize * 2f, 1.2f, 0.8f), Palette.WallTrim);
            Prim.Box("trim_east", root, new Vector3(trimX, trimY, 0f), new Vector3(0.8f, 1.2f, HalfSize * 2f), Palette.WallTrim);
            Prim.Box("trim_west", root, new Vector3(-trimX, trimY, 0f), new Vector3(0.8f, 1.2f, HalfSize * 2f), Palette.WallTrim);

            // 四角塔楼：补齐四个死角，顺便给出"这是个封闭盒子"的剪影
            float corner = HalfSize - 3.2f;
            Prim.Box("tower_nw", root, new Vector3(-corner, h * 0.5f, -corner), new Vector3(6.4f, h, 6.4f), Palette.Wall);
            Prim.Box("tower_ne", root, new Vector3(corner, h * 0.5f, -corner), new Vector3(6.4f, h, 6.4f), Palette.Wall);
            Prim.Box("tower_sw", root, new Vector3(-corner, h * 0.5f, corner), new Vector3(6.4f, h, 6.4f), Palette.Wall);
            Prim.Box("tower_se", root, new Vector3(corner, h * 0.5f, corner), new Vector3(6.4f, h, 6.4f), Palette.Wall);
        }

        // ---------------- 掩体 ----------------

        private void BuildCovers()
        {
            for (int i = 0; i < QuadrantCovers.Length; i++)
            {
                CoverSpec s = QuadrantCovers[i];

                BuildCover(i, s.x, s.z, s.w, s.h, s.d);
                BuildCover(i, -s.x, s.z, s.w, s.h, s.d);
                BuildCover(i, s.x, -s.z, s.w, s.h, s.d);
                BuildCover(i, -s.x, -s.z, s.w, s.h, s.d);
            }
        }

        private void BuildCover(int index, float x, float z, float w, float h, float d)
        {
            Prim.Box("cover_" + index, root, new Vector3(x, h * 0.5f, z), new Vector3(w, h, d), Palette.Cover);
        }

        private void BuildCenter()
        {
            // 中央高台：全场唯一的高点，站上去视野最好，但也最显眼
            Prim.Box("center_dais", root, new Vector3(0f, 0.3f, 0f), new Vector3(12f, 0.6f, 12f), Palette.Raised);
            Prim.Box("center_pillar", root, new Vector3(0f, 2.1f, 0f), new Vector3(3.2f, 3f, 3.2f), Palette.WallTrim);

            // 高台四边的短墙：同样抬到 2.05 米，避免站台边缘只剩头皮
            Prim.Box("center_ctrl_n", root, new Vector3(0f, 1.03f, 6.9f), new Vector3(2.6f, 2.05f, 1.3f), Palette.Cover);
            Prim.Box("center_ctrl_s", root, new Vector3(0f, 1.03f, -6.9f), new Vector3(2.6f, 2.05f, 1.3f), Palette.Cover);
            Prim.Box("center_ctrl_e", root, new Vector3(6.9f, 1.03f, 0f), new Vector3(1.3f, 2.05f, 2.6f), Palette.Cover);
            Prim.Box("center_ctrl_w", root, new Vector3(-6.9f, 1.03f, 0f), new Vector3(1.3f, 2.05f, 2.6f), Palette.Cover);
        }

        private void BuildSpawnPads()
        {
            for (int i = 0; i < SpawnPointCount; i++)
            {
                Vector3 p = Spawn(i);
                Prim.Box("spawn_" + i, root, new Vector3(p.x, 0.03f, p.z),
                    new Vector3(3f, 0.06f, 3f), Palette.TeamBody(i));
            }
        }

        // ---------------- 出生点 ----------------

        public static Vector3 Spawn(int index)
        {
            Vector2 p = Spawns[((index % SpawnPointCount) + SpawnPointCount) % SpawnPointCount];
            return new Vector3(p.x, 0.08f, p.y);
        }

        /// <summary>出生朝向：一律面向场地中心。</summary>
        public static float SpawnYaw(int index)
        {
            Vector3 p = Spawn(index);
            Vector3 dir = new Vector3(-p.x, 0f, -p.z);

            if (dir.sqrMagnitude < 0.01f) return 0f;
            return Quaternion.LookRotation(dir).eulerAngles.y;
        }
    }
}

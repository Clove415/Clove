using System.Collections.Generic;
using UnityEngine;

namespace IronDawn
{
    /// <summary>
    /// 局域网对战调色板。全部是纯色占位材质，不加载任何贴图与美术资源。
    /// 同时兼容 URP / Built-in 两条渲染管线，工程用哪个都不会崩。
    /// </summary>
    public static class Palette
    {
        // ---- 场地：低饱和、脏、暗，保证玩家和弹道是画面里最亮的东西 ----
        public static readonly Color Ground = new Color(0.15f, 0.155f, 0.17f);
        public static readonly Color Wall = new Color(0.23f, 0.235f, 0.25f);
        public static readonly Color WallTrim = new Color(0.31f, 0.325f, 0.35f);
        public static readonly Color Cover = new Color(0.42f, 0.37f, 0.27f);
        public static readonly Color Raised = new Color(0.32f, 0.33f, 0.35f);

        // ---- 玩家与反馈 ----
        public static readonly Color PlayerGear = new Color(0.11f, 0.13f, 0.16f);
        public static readonly Color PlayerTracer = new Color(1.00f, 0.86f, 0.48f);
        public static readonly Color Muzzle = new Color(1.00f, 0.94f, 0.66f);
        public static readonly Color Objective = new Color(0.28f, 0.82f, 0.86f);

        /// <summary>8 个对局席位色。席位号 = 出生点号，玩家和地面出生点用同一个颜色。</summary>
        private static readonly Color[] TeamBodies =
        {
            new Color(0.26f, 0.50f, 0.86f),
            new Color(0.86f, 0.30f, 0.26f),
            new Color(0.30f, 0.72f, 0.40f),
            new Color(0.88f, 0.72f, 0.24f),
            new Color(0.62f, 0.38f, 0.86f),
            new Color(0.24f, 0.76f, 0.78f),
            new Color(0.90f, 0.52f, 0.22f),
            new Color(0.82f, 0.82f, 0.86f)
        };

        public static Color TeamBody(int slot)
        {
            int i = Mathf.Abs(slot) % TeamBodies.Length;
            return TeamBodies[i];
        }

        /// <summary>同色系的高亮版，用来画面罩、出生点描边这类小面积。</summary>
        public static Color TeamAccent(int slot)
        {
            Color c = TeamBody(slot);
            return new Color(
                Mathf.Min(1f, c.r * 1.35f + 0.12f),
                Mathf.Min(1f, c.g * 1.35f + 0.12f),
                Mathf.Min(1f, c.b * 1.35f + 0.12f));
        }

        private static readonly Dictionary<Color, Material> LitCache = new Dictionary<Color, Material>();
        private static readonly Dictionary<Color, Material> LineCache = new Dictionary<Color, Material>();
        private static Shader _litShader;
        private static Shader _lineShader;

        private static Shader ResolveLit()
        {
            if (_litShader != null) return _litShader;
            _litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (_litShader == null) _litShader = Shader.Find("Standard");
            if (_litShader == null) _litShader = Shader.Find("Legacy Shaders/Diffuse");
            if (_litShader == null) _litShader = Shader.Find("Unlit/Color");
            return _litShader;
        }

        private static Shader ResolveLine()
        {
            if (_lineShader != null) return _lineShader;
            _lineShader = Shader.Find("Sprites/Default");
            if (_lineShader == null) _lineShader = Shader.Find("Unlit/Color");
            if (_lineShader == null) _lineShader = ResolveLit();
            return _lineShader;
        }

        public static Material Lit(Color c)
        {
            Material m;
            if (LitCache.TryGetValue(c, out m) && m != null) return m;
            m = new Material(ResolveLit());
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            LitCache[c] = m;
            return m;
        }

        public static Material Line(Color c)
        {
            Material m;
            if (LineCache.TryGetValue(c, out m) && m != null) return m;
            m = new Material(ResolveLine());
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            LineCache[c] = m;
            return m;
        }

        public static void Paint(GameObject go, Color c)
        {
            if (go == null) return;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = Lit(c);
        }
    }
}

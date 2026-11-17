using System.Collections.Generic;
using IronDawn.Multiplayer;
using UnityEngine;

namespace IronDawn
{
    /// <summary>
    /// 对战 HUD。全部用 OnGUI 画，不依赖任何 UI 资源。
    /// 中文字体从系统里取（微软雅黑/黑体），所以不需要导字体文件。
    ///
    /// 信息的优先级：准星与命中反馈 > 血量与弹药 > 记分板 > 网络状态。
    /// 剧情版里的字幕、任务目标、通讯兵血条都已随剧情模块一起移除。
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public ArenaRuntime runtime;

        private Font _font;
        private GUIStyle _text;
        private GUIStyle _textRight;
        private GUIStyle _textCenter;
        private GUIStyle _title;
        private GUIStyle _big;
        private GUIStyle _small;
        private GUIStyle _small_Center;
        private bool _stylesReady;

        private Texture2D _white;
        private float _crossArm = 8f;

        void Awake()
        {
            _white = Texture2D.whiteTexture;
            _font = CreateOsFont();
        }

        private static Font CreateOsFont()
        {
            string[] candidates =
            {
                "Microsoft YaHei", "微软雅黑", "SimHei", "黑体",
                "SimSun", "宋体", "Noto Sans CJK SC", "Arial Unicode MS", "Arial"
            };

            try
            {
                Font f = Font.CreateDynamicFontFromOSFont(candidates, 16);
                if (f != null) return f;
            }
            catch
            {
                // 系统里没有可用字体时静默降级到内置字体
            }

            return null;
        }

        private void EnsureStyles()
        {
            if (_stylesReady && _text != null) return;

            _text = new GUIStyle(GUI.skin.label);
            _text.fontSize = 16;
            _text.normal.textColor = Color.white;
            if (_font != null) _text.font = _font;
            _text.padding = new RectOffset(0, 0, 0, 0);

            _textRight = new GUIStyle(_text);
            _textRight.alignment = TextAnchor.UpperRight;

            _textCenter = new GUIStyle(_text);
            _textCenter.alignment = TextAnchor.UpperCenter;

            _small = new GUIStyle(_text);
            _small.fontSize = 13;
            _small.normal.textColor = new Color(0.72f, 0.76f, 0.84f);

            _small_Center = new GUIStyle(_small);
            _small_Center.alignment = TextAnchor.UpperCenter;

            _title = new GUIStyle(_text);
            _title.fontSize = 34;
            _title.alignment = TextAnchor.MiddleCenter;

            _big = new GUIStyle(_text);
            _big.fontSize = 46;
            _big.alignment = TextAnchor.MiddleCenter;

            _namePlate = new GUIStyle(_text);
            _namePlate.fontSize = 14;
            _namePlate.alignment = TextAnchor.UpperCenter;

            _stylesReady = true;
        }

        private void DrawRect(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = prev;
        }

        private void OnGUI()
        {
            if (runtime == null) return;

            EnsureStyles();

            // 正在等主机握手：这时候还没有玩家和相机，只给一句提示，别让屏幕全黑
            if (runtime.CurrentMode == ArenaRuntime.Mode.Connecting)
            {
                DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.04f, 0.045f, 0.06f, 1f));
                GUI.Label(new Rect(0f, Screen.height * 0.5f - 40f, Screen.width, 30f), "正在加入对局…", _title);
                if (!string.IsNullOrEmpty(runtime.StatusLine))
                    GUI.Label(new Rect(0f, Screen.height * 0.5f + 10f, Screen.width, 24f), runtime.StatusLine, _textCenter);
                return;
            }

            if (!runtime.InMatch) return;

            float w = Screen.width;
            float h = Screen.height;

            Health health = runtime.LocalHealth;
            bool alive = health != null && health.IsAlive;

            DrawDamageOverlay(w, h, health);
            if (alive && Cursor.lockState == CursorLockMode.Locked) DrawCrosshair(w, h);
            DrawHitmarker(w, h);

            DrawVitals(w, h, health);
            DrawAmmo(w, h);
            DrawScoreboard(w, h);
            DrawConnectionLine(w, h);
            DrawStatusLine(w, h);
            DrawNameplates(w, h);

            if (!alive) DrawDeathScreen(w, h, health);
            if (Cursor.lockState != CursorLockMode.Locked) DrawPausePanel(w, h);
        }

        // ---------------- 反馈 ----------------

        private void DrawDamageOverlay(float w, float h, Health health)
        {
            float hurt = runtime.HitFlashUntil - Time.time;
            float missing = health != null ? 1f - health.Normalized : 0f;

            float alpha = Mathf.Clamp01((missing - 0.45f) / 0.55f) * 0.42f;

            if (hurt > 0f)
                alpha = Mathf.Max(alpha, Mathf.Clamp01(hurt / 0.32f) * 0.34f);

            if (alpha <= 0.002f) return;

            Color c = new Color(0.55f, 0.06f, 0.06f, alpha);
            float band = Mathf.Lerp(24f, 80f, Mathf.Clamp01(alpha / 0.42f));

            DrawRect(new Rect(0f, 0f, w, band), c);
            DrawRect(new Rect(0f, h - band, w, band), c);
            DrawRect(new Rect(0f, 0f, band, h), c);
            DrawRect(new Rect(w - band, 0f, band, h), c);
        }

        private void DrawCrosshair(float w, float h)
        {
            float cx = w * 0.5f;
            float cy = h * 0.5f;

            PlayerController controller = runtime.LocalController;
            bool aiming = controller != null && controller.IsAiming;

            float targetArm = aiming ? 4.5f : 8f;
            _crossArm = Mathf.Lerp(_crossArm, targetArm, 1f - Mathf.Exp(-14f * Time.deltaTime));

            float thick = aiming ? 1.6f : 2f;
            Color c = new Color(1f, 1f, 1f, aiming ? 0.9f : 0.78f);

            DrawRect(new Rect(cx - thick * 0.5f, cy - _crossArm, thick, _crossArm * 2f), c);
            DrawRect(new Rect(cx - _crossArm, cy - thick * 0.5f, _crossArm * 2f, thick), c);

            Color dot = new Color(1f, 0.87f, 0.5f, aiming ? 1f : 0.9f);
            float dotR = aiming ? 1.2f : 1.5f;
            DrawRect(new Rect(cx - dotR, cy - dotR, dotR * 2f, dotR * 2f), dot);
        }

        private void DrawHitmarker(float w, float h)
        {
            if (Time.time >= runtime.HitmarkerUntil) return;

            float cx = w * 0.5f;
            float cy = h * 0.5f;

            float left = runtime.HitmarkerUntil - Time.time;
            float alpha = Mathf.Clamp01(left / 0.22f);

            Color c = new Color(1f, 0.95f, 0.85f, alpha);
            float len = 9f;
            float thick = 2f;

            DrawRect(new Rect(cx - 6f - len, cy - 6f, len, thick), c);
            DrawRect(new Rect(cx + 6f, cy - 6f, len, thick), c);
            DrawRect(new Rect(cx - 6f - len, cy + 6f, len, thick), c);
            DrawRect(new Rect(cx + 6f, cy + 6f, len, thick), c);
        }

        // ---------------- 左下角：血量 ----------------

        private void DrawVitals(float w, float h, Health health)
        {
            if (health == null) return;

            float barW = 250f;
            float barH = 15f;
            float x = 30f;
            float y = h - 62f;

            GUI.Label(new Rect(x, y - 26f, 300f, 22f), "生命值", _text);

            DrawRect(new Rect(x - 1f, y - 1f, barW + 2f, barH + 2f), new Color(0f, 0f, 0f, 0.55f));
            DrawRect(new Rect(x, y, barW, barH), new Color(0.12f, 0.12f, 0.14f, 0.9f));

            float fill = barW * health.Normalized;
            Color barColor = Color.Lerp(new Color(0.75f, 0.16f, 0.16f), new Color(0.28f, 0.72f, 0.78f), health.Normalized);
            DrawRect(new Rect(x, y, fill, barH), barColor);

            GUI.Label(new Rect(x, y + barH + 6f, 260f, 22f),
                Mathf.CeilToInt(health.current) + " / " + Mathf.CeilToInt(health.maxHealth), _text);

            // 姿态：确认蹲与探头是否真的生效
            PlayerController controller = runtime.LocalController;
            if (controller == null) return;

            string stance = "";
            if (controller.IsCrouching) stance = "蹲伏";

            float lean = controller.LeanAmount;
            if (lean < -0.35f) stance = Append(stance, "左探头");
            else if (lean > 0.35f) stance = Append(stance, "右探头");

            if (stance.Length > 0)
                GUI.Label(new Rect(x, y + barH + 30f, 300f, 22f), stance, _text);

            // 自己的战绩
            RosterEntry me = FindLocalEntry();
            if (me != null)
                GUI.Label(new Rect(x + 150f, y + barH + 6f, 200f, 22f),
                    "击杀 " + me.kills + " · 阵亡 " + me.deaths, _text);
        }

        private static string Append(string current, string add)
        {
            return string.IsNullOrEmpty(current) ? add : current + " · " + add;
        }

        // ---------------- 右下角：弹药 ----------------

        private void DrawAmmo(float w, float h)
        {
            PlayerCombat combat = runtime.LocalCombat;
            if (combat == null) return;

            float y = h - 62f;

            string ammo = combat.Reloading
                ? "换弹中…"
                : combat.Ammo + " / " + combat.magazineSize;

            GUI.Label(new Rect(w - 280f, y - 26f, 250f, 24f), "弹匣", _textRight);
            GUI.Label(new Rect(w - 280f, y + 2f, 250f, 30f), ammo, _textRight);

            if (combat.Reloading)
            {
                float barW = 140f;
                float x = w - 30f - barW;
                DrawRect(new Rect(x, y + 34f, barW, 5f), new Color(0.12f, 0.12f, 0.14f, 0.9f));
                DrawRect(new Rect(x, y + 34f, barW * combat.ReloadProgress, 5f), new Color(0.85f, 0.78f, 0.42f, 0.95f));
            }
        }

        // ---------------- 右上角：记分板 ----------------

        private void DrawScoreboard(float w, float h)
        {
            IList<RosterEntry> roster = runtime.Roster;
            if (roster == null || roster.Count == 0) return;

            byte localId = runtime.Net != null ? runtime.Net.LocalId : (byte)0;

            float panelW = 320f;
            float rowH = 24f;
            float x = w - panelW - 24f;
            float y = 22f;

            DrawRect(new Rect(x - 10f, y - 8f, panelW + 20f, roster.Count * rowH + 34f), new Color(0f, 0f, 0f, 0.34f));

            GUI.Label(new Rect(x, y - 4f, 140f, 22f), "对局记分", _text);
            GUI.Label(new Rect(x + 190f, y - 4f, 100f, 22f), "击杀 / 阵亡", _small);

            for (int i = 0; i < roster.Count; i++)
            {
                RosterEntry e = roster[i];
                float ry = y + 24f + i * rowH;

                bool isLocal = e.id == localId;
                Color tint = isLocal ? Color.white : new Color(0.82f, 0.85f, 0.9f);

                DrawRect(new Rect(x, ry + 9f, 4f, 12f), Palette.TeamBody(e.spawnIndex));

                GUI.color = tint;
                GUI.Label(new Rect(x + 12f, ry, 170f, 22f), e.name, _text);
                GUI.Label(new Rect(x + 190f, ry, 100f, 22f), e.kills + " / " + e.deaths, _text);
                GUI.color = Color.white;
            }
        }

        private void DrawConnectionLine(float w, float h)
        {
            LanNetManager net = runtime.Net;
            if (net == null) return;

            string role = net.CurrentRole == LanNetManager.Role.Host ? "主机" : "客户端";
            string line = role + " · " + net.PlayerCount + "/" + NetConfig.MaxPlayers + " 人 · 端口 " + net.GamePort;

            GUI.Label(new Rect(30f, 24f, 420f, 22f), line, _text);
        }

        private void DrawStatusLine(float w, float h)
        {
            if (string.IsNullOrEmpty(runtime.StatusLine)) return;
            if (Time.time > runtime.StatusUntil) return;

            float left = runtime.StatusUntil - Time.time;
            float alpha = Mathf.Clamp01(left / 0.6f);

            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(0f, 74f, w, 26f), runtime.StatusLine, _textCenter);
            GUI.color = prev;
        }

        // ---------------- 名字牌 ----------------

        private void DrawNameplates(float w, float h)
        {
            Camera cam = runtime.ActiveCamera;
            if (cam == null) return;

            foreach (RemotePlayer rp in runtime.Remotes)
            {
                if (rp == null) continue;

                Vector3 world = rp.transform.position + Vector3.up * 2.05f;
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0.1f) continue;

                float dist = Vector3.Distance(cam.transform.position, rp.transform.position);
                float alpha = Mathf.Clamp01(1f - (dist - 45f) / 35f);
                if (alpha <= 0.03f) continue;

                float x = screen.x;
                float y = h - screen.y;

                float boxW = 132f;
                float bx = x - boxW * 0.5f;

                Color prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);

                DrawRect(new Rect(bx, y - 2f, boxW, 20f), new Color(0f, 0f, 0f, 0.42f * alpha));
                GUI.Label(new Rect(bx, y - 3f, boxW, 20f), rp.PlayerName, _namePlate);

                Color bar = Color.Lerp(new Color(0.75f, 0.16f, 0.16f), new Color(0.30f, 0.74f, 0.42f), rp.Health01);
                bar.a = alpha;

                DrawRect(new Rect(bx + 12f, y + 20f, boxW - 24f, 4f), new Color(0f, 0f, 0f, 0.5f * alpha));
                DrawRect(new Rect(bx + 12f, y + 20f, (boxW - 24f) * (rp.Alive ? rp.Health01 : 0f), 4f), bar);

                GUI.color = prev;
            }
        }

        private GUIStyle _namePlate;

        // ---------------- 死亡 / 暂停 ----------------

        private void DrawDeathScreen(float w, float h, Health health)
        {
            DrawRect(new Rect(0f, 0f, w, h), new Color(0f, 0f, 0f, 0.66f));

            GUI.color = new Color(0.85f, 0.2f, 0.2f, 1f);
            GUI.Label(new Rect(0f, h * 0.5f - 96f, w, 60f), "已阵亡", _big);
            GUI.color = Color.white;

            float remain = Mathf.Max(0f, runtime.RespawnAt - Time.time);
            GUI.Label(new Rect(0f, h * 0.5f - 22f, w, 30f),
                "重生倒计时 " + remain.ToString("0.0") + " 秒", _textCenter);

            GUI.Label(new Rect(0f, h * 0.5f + 16f, w, 30f), "重生点会尽量远离对手", _small);
        }

        private void DrawPausePanel(float w, float h)
        {
            float panelW = 340f;
            float panelH = 178f;
            float x = (w - panelW) * 0.5f;
            float y = (h - panelH) * 0.5f;
            Rect panel = new Rect(x, y, panelW, panelH);

            // 暂停时冻结移动：面板在，人就不该还在场上走动
            runtime.PauseInput();

            // 键盘通路：就算鼠标出了问题，也一定离得开对局
            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    runtime.ResumeCursor();
                    return;
                }

                if (e.keyCode == KeyCode.Q)
                {
                    runtime.LeaveMatch(null);
                    return;
                }
            }

            // 点击面板外的画面 = 继续游戏（点面板内部不处理，避免误触离开）
            if (e != null && e.type == EventType.MouseDown && !panel.Contains(e.mousePosition))
            {
                runtime.ResumeCursor();
                return;
            }

            DrawRect(panel, new Color(0f, 0f, 0f, 0.72f));

            GUI.Label(new Rect(x, y + 14f, panelW, 24f), "已暂停", _textCenter);

            if (GUI.Button(new Rect(x + 40f, y + 50f, panelW - 80f, 36f), "继续游戏（回车）"))
                runtime.ResumeCursor();

            if (GUI.Button(new Rect(x + 40f, y + 96f, panelW - 80f, 36f), "离开对局（Q）"))
                runtime.LeaveMatch(null);

            GUI.Label(new Rect(x, y + 140f, panelW, 22f), "也可以直接点击画面继续", _small_Center);
        }

        private RosterEntry FindLocalEntry()
        {
            LanNetManager net = runtime.Net;
            if (net == null) return null;

            IList<RosterEntry> roster = net.Roster;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i].id == net.LocalId) return roster[i];

            return null;
        }
    }
}

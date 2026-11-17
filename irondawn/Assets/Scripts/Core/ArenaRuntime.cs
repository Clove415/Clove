using System;
using System.Collections.Generic;
using IronDawn.Multiplayer;
using UnityEngine;
using UnityEngine.Rendering;

namespace IronDawn
{
    /// <summary>
    /// 局域网对战运行时。整局的生命周期都在这里：
    ///   菜单（不建玩家、只有场地） → 建房/加入 → 对局 → 断线或主动离开后回到菜单。
    ///
    /// 场地由代码生成，每台机器各自搭一份，所以场景几何完全不需要同步；
    /// 需要在网络上跑的只有玩家、弹道、死亡和重生这四件事。
    /// </summary>
    public class ArenaRuntime : MonoBehaviour
    {
        public static ArenaRuntime Instance { get; private set; }

        public enum Mode { Menu, Connecting, Playing }

        private Transform _worldRoot;
        private ArenaBuilder _builder;
        private Camera _menuCamera;

        private GameObject _localPlayer;
        private PlayerController _player;
        private PlayerCombat _combat;
        private Health _playerHealth;
        private Camera _camera;

        private Hud _hud;
        private LanMenu _menu;
        private LanNetManager _net;

        private readonly Dictionary<byte, RemotePlayer> _remotes = new Dictionary<byte, RemotePlayer>();

        private float _respawnAt = -1f;
        private float _hitmarkerUntil;
        private float _hitFlashUntil;
        private float _statusUntil;
        private string _statusLine;
        private byte _lastAttackerId;

        private float _blindUntil;
        private float _blindStrength;

        public Mode CurrentMode { get; private set; }
        public string PlayerName { get; private set; }
        public int LocalSlot { get; private set; }
        public byte LastAttackerId { get { return _lastAttackerId; } }

        public Health LocalHealth { get { return _playerHealth; } }
        public PlayerCombat LocalCombat { get { return _combat; } }
        public PlayerController LocalController { get { return _player; } }
        public LanNetManager Net { get { return _net; } }
        public Transform LocalPlayerTransform { get { return _localPlayer != null ? _localPlayer.transform : null; } }

        /// <summary>世界根节点。投掷物之类的临时物体挂在这里，离开对局时一起销毁。</summary>
        public Transform WorldRoot { get { return _worldRoot; } }
        public Camera ActiveCamera { get { return CurrentMode == Mode.Menu ? _menuCamera : _camera; } }
        public float RespawnAt { get { return _respawnAt; } }
        public string StatusLine { get { return _statusLine; } }
        public float StatusUntil { get { return _statusUntil; } }
        public float HitmarkerUntil { get { return _hitmarkerUntil; } }
        public float HitFlashUntil { get { return _hitFlashUntil; } }

        /// <summary>闪光致盲强度（0..1），HUD 用它画白屏。</summary>
        /// <summary>闪光致盲强度（0..1）。被闪到时满值全白，只在最后半秒淡出。</summary>
        public float BlindStrength
        {
            get
            {
                float remaining = _blindUntil - Time.time;
                if (remaining <= 0f) return 0f;

                // 前段保持纯白（alpha = 1），最后 0.55 秒线性退白，避免硬切
                return _blindStrength * Mathf.Clamp01(remaining / 0.55f);
            }
        }
        public IList<RosterEntry> Roster { get { return _net != null ? _net.Roster : null; } }
        public IEnumerable<RemotePlayer> Remotes { get { return _remotes.Values; } }
        public bool InMatch { get { return CurrentMode == Mode.Playing; } }
        public bool IsHost { get { return _net != null && _net.CurrentRole == LanNetManager.Role.Host; } }

        void Awake()
        {
            Instance = this;
            Application.runInBackground = true;
        }

        void Start()
        {
            BuildWorld();
            EnterMenu(null);

            // 启动时打一条能对上的日志：排查"装了但没反应"时先看这行在不在
            Debug.Log("[IronDawn] 已启动 · 竞技场尺寸 " + (ArenaBuilder.HalfSize * 2f) + "×" + (ArenaBuilder.HalfSize * 2f)
                + " 米 · 端口 " + NetConfig.GamePort + " · 版本 " + Application.version);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_net != null) _net.Stop();
        }

        void Update()
        {
            if (CurrentMode == Mode.Menu) UpdateMenuCamera();
            else if (CurrentMode == Mode.Playing) UpdateMatch();

            // 死亡后按任意视角看自己的尸体也行，这里只处理重生倒计时
        }

        // ---------------- 世界 ----------------

        private void BuildWorld()
        {
            DisableSceneCameras();

            _worldRoot = new GameObject("[World]").transform;

            SetupEnvironment();

            GameObject arenaGo = new GameObject("Arena");
            arenaGo.transform.SetParent(_worldRoot, false);

            _builder = arenaGo.AddComponent<ArenaBuilder>();
            _builder.root = arenaGo.transform;
            _builder.Build();
        }

        private void DisableSceneCameras()
        {
            Camera[] cams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cams.Length; i++)
                if (cams[i] != null) cams[i].gameObject.SetActive(false);

            AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            for (int i = 0; i < listeners.Length; i++)
                if (listeners[i] != null) listeners[i].enabled = false;
        }

        private void SetupEnvironment()
        {
            GameObject sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(_worldRoot, false);
            sunGo.transform.rotation = Quaternion.Euler(46f, -36f, 0f);

            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.78f, 0.84f, 1f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.26f, 0.28f, 0.34f);
            RenderSettings.skybox = null;

            // 封闭场地里雾只用来压远处墙角，起点别太近
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.09f, 0.10f, 0.13f);
            RenderSettings.fogStartDistance = 48f;
            RenderSettings.fogEndDistance = 170f;
        }

        private void UpdateMenuCamera()
        {
            if (_menuCamera == null) return;

            _menuCamera.transform.RotateAround(new Vector3(0f, 2f, 0f), Vector3.up, 6f * Time.deltaTime);
            _menuCamera.transform.LookAt(new Vector3(0f, 3f, 0f));
        }

        private void CreateMenuCamera()
        {
            GameObject go = new GameObject("MenuCamera");
            go.transform.SetParent(_worldRoot, false);
            go.tag = "MainCamera";

            _menuCamera = go.AddComponent<Camera>();
            _menuCamera.clearFlags = CameraClearFlags.SolidColor;
            _menuCamera.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
            _menuCamera.fieldOfView = 55f;
            _menuCamera.nearClipPlane = 0.1f;
            _menuCamera.farClipPlane = 320f;

            go.AddComponent<AudioListener>();

            _menuCamera.transform.position = new Vector3(0f, 24f, -33f);
            _menuCamera.transform.LookAt(new Vector3(0f, 2f, 0f));
        }

        // ---------------- 菜单 ----------------

        public void EnterMenu(string reason)
        {
            TeardownMatch();

            CurrentMode = Mode.Menu;

            if (_menu == null)
            {
                _menu = GetComponent<LanMenu>();
                if (_menu == null) _menu = gameObject.AddComponent<LanMenu>();
                _menu.runtime = this;
            }

            _menu.Visible = true;
            if (!string.IsNullOrEmpty(reason)) _menu.Notice = reason;

            if (_menuCamera == null) CreateMenuCamera();
            _menuCamera.gameObject.SetActive(true);

            PlayerController.LockCursor(false);
        }

        private void BeginMatch()
        {
            if (_menu != null) _menu.Visible = false;

            if (_menuCamera != null)
            {
                // 先失活再销毁：Destroy 是帧末生效的，否则这一帧会有两个 AudioListener
                _menuCamera.gameObject.SetActive(false);
                Destroy(_menuCamera.gameObject);
                _menuCamera = null;
            }

            if (_hud == null)
            {
                GameObject hudGo = new GameObject("[HUD]");
                hudGo.transform.SetParent(_worldRoot, false);
                _hud = hudGo.AddComponent<Hud>();
                _hud.runtime = this;
            }

            PlayerController.LockCursor(true);
        }

        public void HostGame(string playerName)
        {
            if (CurrentMode != Mode.Menu) return;

            _net = CreateNet();
            if (!_net.StartHost(playerName, NetConfig.GamePort))
            {
                string error = _net.LastError;
                Destroy(_net.gameObject);
                _net = null;
                EnterMenu(error);
                return;
            }

            PlayerName = _net.LocalName;
            BeginMatch();

            int spawnIndex = 0;
            if (_net.Roster.Count > 0) spawnIndex = _net.Roster[0].spawnIndex;
            SpawnLocal(spawnIndex);

            ShowStatus("对局已创建 · 等待其他人加入", 3f);
        }

        public void JoinGame(string playerName, string address)
        {
            if (CurrentMode != Mode.Menu) return;

            _net = CreateNet();
            if (!_net.StartClient(playerName, address, NetConfig.GamePort))
            {
                string error = _net.LastError;
                Destroy(_net.gameObject);
                _net = null;
                EnterMenu(error);
                return;
            }

            PlayerName = _net.LocalName;
            BeginMatch();
            CurrentMode = Mode.Connecting;
            ShowStatus("正在连接 " + address + " …", 3f);
        }

        public void LeaveMatch(string reason)
        {
            EnterMenu(reason);
        }

        public void ResumeCursor()
        {
            // 点「继续游戏」或点画面继续时，鼠标键很可能还按着，
            // 放开锁定的瞬间会顺带打出一枪，这里先压住一小段时间的射击
            if (_combat != null) _combat.SuppressFire(0.25f);
            if (_player != null) _player.InputLocked = false;

            PlayerController.LockCursor(true);
        }

        /// <summary>暂停面板显示期间冻结移动，免得人还在场上走。</summary>
        public void PauseInput()
        {
            if (_player != null) _player.InputLocked = true;
        }

        private LanNetManager CreateNet()
        {
            GameObject go = new GameObject("[Net]");
            LanNetManager net = go.AddComponent<LanNetManager>();

            net.RosterChanged += HandleRosterChanged;
            net.Ready += HandleNetReady;
            net.StateArrived += HandleRemoteState;
            net.ShotArrived += HandleRemoteShot;
            net.DeathArrived += HandleRemoteDeath;
            net.RespawnArrived += HandleRemoteRespawn;
            net.ThrowableArrived += HandleRemoteThrowable;
            net.Terminated += HandleTerminated;

            return net;
        }

        private void TeardownMatch()
        {
            if (_net != null)
            {
                _net.RosterChanged -= HandleRosterChanged;
                _net.Ready -= HandleNetReady;
                _net.StateArrived -= HandleRemoteState;
                _net.ShotArrived -= HandleRemoteShot;
                _net.DeathArrived -= HandleRemoteDeath;
                _net.RespawnArrived -= HandleRemoteRespawn;
                _net.ThrowableArrived -= HandleRemoteThrowable;
                _net.Terminated -= HandleTerminated;

                _net.Stop();
                Destroy(_net.gameObject);
                _net = null;
            }

            foreach (KeyValuePair<byte, RemotePlayer> kv in _remotes)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _remotes.Clear();

            if (_playerHealth != null)
            {
                _playerHealth.Damaged -= HandleLocalDamaged;
                _playerHealth.Died -= HandleLocalDied;
            }

            if (_localPlayer != null) Destroy(_localPlayer);
            _localPlayer = null;

            if (_camera != null) Destroy(_camera.gameObject);
            _camera = null;

            _player = null;
            _combat = null;
            _playerHealth = null;

            _respawnAt = -1f;
            _lastAttackerId = 0;
            _hitmarkerUntil = 0f;
            _hitFlashUntil = 0f;
            _statusUntil = 0f;
            _statusLine = null;
            _blindUntil = 0f;
            _blindStrength = 0f;
        }

        // ---------------- 本地玩家 ----------------

        private void SpawnLocal(int spawnIndex)
        {
            if (_localPlayer != null) return;

            Vector3 position = ArenaBuilder.Spawn(spawnIndex);
            float yaw = ArenaBuilder.SpawnYaw(spawnIndex);
            LocalSlot = spawnIndex;

            PlayerFactory.LocalRig rig = PlayerFactory.CreateLocal(_worldRoot, position, yaw, spawnIndex);

            _localPlayer = rig.root;
            _player = rig.controller;
            _combat = rig.combat;
            _playerHealth = rig.health;
            _camera = rig.camera;

            PlayerNetSync sync = _localPlayer.AddComponent<PlayerNetSync>();
            sync.controller = _player;
            sync.combat = _combat;
            sync.health = _playerHealth;

            _playerHealth.Damaged += HandleLocalDamaged;
            _playerHealth.Died += HandleLocalDied;
            _combat.ShotResolved += HandleLocalShot;

            CurrentMode = Mode.Playing;
            _respawnAt = -1f;

            PlayerController.LockCursor(true);
            SyncRemotes();
        }

        private void UpdateMatch()
        {
            if (_respawnAt > 0f && Time.time >= _respawnAt) RespawnLocal();
        }

        private void HandleLocalShot(PlayerCombat.ShotInfo shot)
        {
            if (shot.hitId != 0) _hitmarkerUntil = Time.time + 0.22f;
        }

        private void HandleLocalDamaged(Health h)
        {
            _hitFlashUntil = Time.time + 0.32f;
        }

        private void HandleLocalDied(Health h)
        {
            if (_player != null) _player.InputLocked = true;

            _respawnAt = Time.time + NetConfig.RespawnDelay;

            byte killer = _lastAttackerId;
            _lastAttackerId = 0;

            if (_net != null) _net.PublishDeath(_net.LocalId, killer);

            ShowStatus(killer != 0 ? "你被 " + NameOf(killer) + " 击杀" : "你被击杀了", NetConfig.RespawnDelay);
        }

        private void RespawnLocal()
        {
            _respawnAt = -1f;
            if (_localPlayer == null || _playerHealth == null) return;

            int index = PickRespawnIndex();
            Vector3 position = ArenaBuilder.Spawn(index);
            float yaw = ArenaBuilder.SpawnYaw(index);

            TeleportLocal(position, yaw);
            _playerHealth.ResetToFull();
            if (_player != null) _player.InputLocked = false;
            if (_combat != null) _combat.ResetLoadout();

            if (_net != null)
            {
                RespawnData d;
                d.id = _net.LocalId;
                d.position = position;
                d.yaw = yaw;
                _net.PublishRespawn(d);
            }
        }

        private void TeleportLocal(Vector3 position, float yaw)
        {
            CharacterController cc = _localPlayer.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            _localPlayer.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            if (cc != null) cc.enabled = true;
            if (_player != null) _player.SnapRotation(yaw, 0f);
        }

        /// <summary>尽量在离其他活人最远的出生点重生，避免复活即被守尸。</summary>
        private int PickRespawnIndex()
        {
            int best = UnityEngine.Random.Range(0, ArenaBuilder.SpawnPointCount);
            float bestDistance = -1f;

            for (int i = 0; i < ArenaBuilder.SpawnPointCount; i++)
            {
                Vector3 spawn = ArenaBuilder.Spawn(i);
                float nearest = float.MaxValue;

                foreach (KeyValuePair<byte, RemotePlayer> kv in _remotes)
                {
                    RemotePlayer other = kv.Value;
                    if (other == null || !other.Alive) continue;

                    float d = (other.transform.position - spawn).sqrMagnitude;
                    if (d < nearest) nearest = d;
                }

                if (nearest == float.MaxValue) return i;
                if (nearest > bestDistance)
                {
                    bestDistance = nearest;
                    best = i;
                }
            }

            return best;
        }

        // ---------------- 远端玩家 ----------------

        private void HandleRosterChanged()
        {
            SyncRemotes();
        }

        private void SyncRemotes()
        {
            if (_net == null || _net.LocalId == 0) return;

            IList<RosterEntry> roster = _net.Roster;

            for (int i = 0; i < roster.Count; i++)
            {
                RosterEntry entry = roster[i];
                if (entry.id == _net.LocalId) continue;
                if (_remotes.ContainsKey(entry.id)) continue;

                CreateRemote(entry);
            }

            List<byte> stale = null;

            foreach (KeyValuePair<byte, RemotePlayer> kv in _remotes)
            {
                if (FindRoster(kv.Key) != null) continue;
                if (stale == null) stale = new List<byte>();
                stale.Add(kv.Key);
            }

            if (stale == null) return;

            for (int i = 0; i < stale.Count; i++)
            {
                RemotePlayer proxy = _remotes[stale[i]];
                _remotes.Remove(stale[i]);
                if (proxy != null) Destroy(proxy.gameObject);
            }
        }

        private void CreateRemote(RosterEntry entry)
        {
            Vector3 position = ArenaBuilder.Spawn(entry.spawnIndex);
            float yaw = ArenaBuilder.SpawnYaw(entry.spawnIndex);

            PlayerFactory.RemoteRig rig = PlayerFactory.CreateRemote(
                _worldRoot, entry.name, position, yaw, entry.spawnIndex);

            RemotePlayer proxy = rig.root.AddComponent<RemotePlayer>();
            proxy.Setup(entry.id, entry.name, entry.spawnIndex, rig.visual, rig.weaponPivot, rig.weaponModels);

            _remotes[entry.id] = proxy;
        }

        private void HandleNetReady()
        {
            if (_net == null) return;

            // 出生点来自 Welcome 包本身：名册可能还在路上，不能等它
            int spawnIndex = _net.AssignedSpawn;
            RosterEntry mine = FindRoster(_net.LocalId);
            if (mine != null) spawnIndex = mine.spawnIndex;

            SpawnLocal(spawnIndex);
            ShowStatus("已加入对局", 2.5f);
        }

        private void HandleRemoteState(PlayerStateData d)
        {
            RemotePlayer proxy;
            if (!_remotes.TryGetValue(d.id, out proxy))
            {
                SyncRemotes();
                if (!_remotes.TryGetValue(d.id, out proxy)) return;
            }

            proxy.ApplyState(d);
        }

        private void HandleRemoteShot(ShotData d)
        {
            // 近战只闪一下，子弹才画弹道
            if (d.kind == 1) Tracer.Flash(d.to, 0.14f, new Color(1f, 0.72f, 0.62f), 0.06f);
            else
            {
                Tracer.Draw(d.from, d.to, Palette.PlayerTracer, 0.03f, 0.05f);
                Tracer.Flash(d.to, 0.09f, Palette.Muzzle, 0.05f);
            }

            if (_net == null || d.hit == 0) return;

            // 只有被打的一方结算伤害，射击方不碰别人的血量
            if (d.hit != _net.LocalId) return;
            if (_playerHealth == null || !_playerHealth.IsAlive) return;

            _lastAttackerId = d.shooter;
            _playerHealth.ApplyDamage(d.damage, d.to, Vector3.up, null);
            _hitFlashUntil = Time.time + 0.32f;
        }

        private void HandleRemoteDeath(DeathData d)
        {
            if (_net == null) return;
            if (d.victim == _net.LocalId) return;   // 自己的死亡本地已经处理过

            RemotePlayer proxy;
            if (_remotes.TryGetValue(d.victim, out proxy)) proxy.ApplyDeath();

            if (d.killer == _net.LocalId) ShowStatus("击杀 " + NameOf(d.victim), 2.2f);
        }

        private void HandleRemoteRespawn(RespawnData d)
        {
            if (_net == null || d.id == _net.LocalId) return;

            RemotePlayer proxy;
            if (_remotes.TryGetValue(d.id, out proxy)) proxy.ApplyRespawn(d);
        }

        /// <summary>别人扔的道具：本地也生成一份弹体，效果各自结算。</summary>
        private void HandleRemoteThrowable(ThrowableData d)
        {
            if (_net == null || d.owner == _net.LocalId) return;

            Throwables.Spawn((ThrowableKind)d.kind, d.origin, d.velocity, _worldRoot, d.owner);
        }

        // ---------------- 投掷物伤害结算 ----------------

        /// <summary>高爆伤害由被炸的本地玩家结算。</summary>
        public void NotifyLocalExplosion(float damage, Vector3 center, byte attacker)
        {
            if (_playerHealth == null || !_playerHealth.IsAlive) return;

            // 致命伤害会触发 Health.Died → HandleLocalDied，死亡广播与记分都在那里完成
            AttributeAttack(attacker);
            _playerHealth.ApplyDamage(damage, center, Vector3.up, null);
            _hitFlashUntil = Time.time + 0.45f;
        }

        /// <summary>燃烧伤害按 tick 结算。</summary>
        public void NotifyLocalFireDamage(float damage, byte attacker)
        {
            if (_playerHealth == null || !_playerHealth.IsAlive) return;

            Vector3 point = _localPlayer != null ? _localPlayer.transform.position : Vector3.zero;
            AttributeAttack(attacker);
            _playerHealth.ApplyDamage(damage, point, Vector3.up, null);
            _hitFlashUntil = Time.time + 0.2f;
        }

        /// <summary>把自己炸死的记分记到投掷者头上，自伤不算别人的击杀。</summary>
        private void AttributeAttack(byte attacker)
        {
            if (_net == null || attacker == 0) return;
            if (attacker == _net.LocalId) return;

            _lastAttackerId = attacker;
        }

        public void ApplyBlind(float duration, float strength)
        {
            float end = Time.time + duration;
            if (end <= _blindUntil) return;

            _blindUntil = end;
            _blindStrength = Mathf.Clamp01(strength);
        }

        private void HandleTerminated(string reason)
        {
            EnterMenu(reason);
        }

        // ---------------- 工具 ----------------

        public string NameOf(byte id)
        {
            RosterEntry entry = FindRoster(id);
            return entry != null ? entry.name : "对手";
        }

        private RosterEntry FindRoster(byte id)
        {
            if (_net == null) return null;

            IList<RosterEntry> roster = _net.Roster;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i].id == id) return roster[i];

            return null;
        }

        public void ShowStatus(string text, float duration)
        {
            if (string.IsNullOrEmpty(text)) return;
            _statusLine = text;
            _statusUntil = Time.time + Mathf.Max(0.5f, duration);
        }
    }
}

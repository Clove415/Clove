using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace IronDawn.Multiplayer
{
    /// <summary>
    /// 局域网对战的传输层：一台机器当主机（同时也是一个玩家），其余机器作为客户端。
    ///
    /// 权责划分（故意做得很扁，局域网原型不需要 rollback）：
    ///   · 移动：各自本地模拟，20Hz 上报，主机负责转发；
    ///   · 命中：射击端本地判定，把「打中了谁」上报，扣血由被打的一方自己结算——
    ///     这样不需要在主机上复制整份玩家物理状态；
    ///   · 名册：主机是唯一作者，客户端只读，加入/离开/比分都靠名册刷新。
    /// </summary>
    public sealed class LanNetManager : MonoBehaviour
    {
        public enum Role { None, Host, Client }

        public static LanNetManager Instance { get; private set; }

        private sealed class Peer
        {
            public byte id;
            public IPEndPoint endPoint;
            public float lastHeard;
        }

        public Role CurrentRole { get; private set; }
        public byte LocalId { get; private set; }
        public string LocalName { get; private set; }
        public bool InMatch { get; private set; }
        public string LastError { get; private set; }
        public int GamePort { get; private set; }

        /// <summary>主机分配给自己的出生点。取自 Welcome，不依赖名册是否先到。</summary>
        public int AssignedSpawn { get; private set; }

        public event Action RosterChanged;
        public event Action Ready;
        public event Action<PlayerStateData> StateArrived;
        public event Action<ShotData> ShotArrived;
        public event Action<DeathData> DeathArrived;
        public event Action<RespawnData> RespawnArrived;
        public event Action<ThrowableData> ThrowableArrived;
        public event Action<string> Terminated;

        private readonly List<RosterEntry> _roster = new List<RosterEntry>();
        private readonly Dictionary<string, Peer> _peersByEndPoint = new Dictionary<string, Peer>();
        private readonly Dictionary<byte, Peer> _peersById = new Dictionary<byte, Peer>();

        private LanSocket _socket;
        private LanSocket _beacon;
        private IPEndPoint _hostEndPoint;

        private byte _nextId = 2;
        private float _nextHello;
        private float _nextPing;
        private float _nextBeacon;
        private float _nextTimeoutCheck;
        private float _nextRosterPush;
        private float _nextRosterKeepAlive;
        private float _clientStartedAt;
        private float _lastHostHeard;
        private bool _rosterDirty;

        public IList<RosterEntry> Roster { get { return _roster; } }
        public int PlayerCount { get { return _roster.Count; } }
        public bool IsHost { get { return CurrentRole == Role.Host; } }

        private static float Now { get { return Time.realtimeSinceStartup; } }

        void Awake()
        {
            Instance = this;
            Application.runInBackground = true;
        }

        void OnDestroy()
        {
            Stop();
            if (Instance == this) Instance = null;
        }

        // ---------------- 开局 ----------------

        public bool StartHost(string playerName, int port)
        {
            Stop();

            _socket = new LanSocket();
            if (!_socket.Open(port))
            {
                LastError = "端口 " + port + " 无法监听（可能已被占用）";
                return false;
            }

            CurrentRole = Role.Host;
            LocalName = SanitizeName(playerName, "主机");
            LocalId = 1;
            GamePort = _socket.Port;
            InMatch = true;
            _nextId = 2;
            LastError = null;

            _roster.Add(new RosterEntry(LocalId, LocalName, 0));
            StartBeacon();

            Debug.Log("[IronDawn] 已创建局域网对局，端口 " + GamePort);
            return true;
        }

        public bool StartClient(string playerName, string address, int port)
        {
            Stop();

            IPAddress ip;
            if (!TryResolve(address, out ip))
            {
                LastError = "无法解析地址：" + address;
                return false;
            }

            _socket = new LanSocket();
            if (!_socket.Open(0))
            {
                LastError = "本机无法创建 UDP 套接字";
                return false;
            }

            CurrentRole = Role.Client;
            LocalName = SanitizeName(playerName, "玩家");
            LocalId = 0;
            GamePort = port;
            InMatch = false;
            LastError = null;

            _hostEndPoint = new IPEndPoint(ip, port);
            _clientStartedAt = Now;
            _lastHostHeard = Now;
            _nextHello = 0f;
            _nextPing = 0f;

            Debug.Log("[IronDawn] 正在连接 " + ip + ":" + port);
            return true;
        }

        public void Stop()
        {
            if (_socket != null) { _socket.Close(); _socket = null; }
            if (_beacon != null) { _beacon.Close(); _beacon = null; }

            _peersByEndPoint.Clear();
            _peersById.Clear();
            _roster.Clear();
            _hostEndPoint = null;
            _rosterDirty = false;
            CurrentRole = Role.None;
            InMatch = false;
            LocalId = 0;
            AssignedSpawn = 0;
        }

        // ---------------- 主循环 ----------------

        void Update()
        {
            if (CurrentRole == Role.None || _socket == null) return;

            float now = Now;

            // 一帧最多吃 256 个包，避免突发流量把主线程钉住
            for (int i = 0; i < 256; i++)
            {
                byte[] data;
                IPEndPoint from;
                if (!_socket.Receive(out data, out from)) break;

                try
                {
                    HandlePacket(data, from, now);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[IronDawn] 丢弃异常数据包: " + e.Message);
                }
            }

            if (CurrentRole == Role.Host) UpdateHost(now);
            else if (CurrentRole == Role.Client) UpdateClient(now);
        }

        private void UpdateHost(float now)
        {
            if (now >= _nextTimeoutCheck)
            {
                _nextTimeoutCheck = now + 0.5f;
                DropTimeoutPeers(now);
            }

            if (now >= _nextBeacon)
            {
                _nextBeacon = now + 1f;
                SendBeacon();
            }

            if (_rosterDirty && now >= _nextRosterPush) BroadcastRoster();

            // 名册也会丢：每两秒重播一次，丢包的客户端不会永远看不到别人
            if (now >= _nextRosterKeepAlive)
            {
                _nextRosterKeepAlive = now + 2f;
                if (_peersById.Count > 0) Relay(NetCodec.Roster(_roster), 0);
            }
        }

        private void UpdateClient(float now)
        {
            if (!InMatch)
            {
                if (now >= _nextHello)
                {
                    _nextHello = now + 0.4f;
                    SendToHost(NetCodec.Hello(LocalName));
                }

                if (now - _clientStartedAt > NetConfig.JoinTimeout)
                    Terminate("连接超时：主机没有回应，检查 IP 与防火墙");

                return;
            }

            if (now >= _nextPing)
            {
                _nextPing = now + NetConfig.KeepAliveInterval;
                SendToHost(NetCodec.Ping());
            }

            if (now - _lastHostHeard > NetConfig.PeerTimeout)
                Terminate("与主机的连接已断开");
        }

        // ---------------- 收包 ----------------

        private void HandlePacket(byte[] data, IPEndPoint from, float now)
        {
            NetReader r = new NetReader(data);
            NetMsg type = (NetMsg)r.Byte();

            if (CurrentRole == Role.Host) HandleHostPacket(type, r, data, from, now);
            else HandleClientPacket(type, r, from, now);
        }

        private void HandleHostPacket(NetMsg type, NetReader r, byte[] raw, IPEndPoint from, float now)
        {
            if (type == NetMsg.Hello)
            {
                HandleHello(r, from, now);
                return;
            }

            Peer peer = FindPeer(from);
            if (peer == null) return;   // 陌生地址：要么刚掉线，要么在瞎发

            peer.lastHeard = now;

            switch (type)
            {
                case NetMsg.State:
                {
                    PlayerStateData d = NetCodec.ReadState(r);
                    if (d.id != peer.id) return;
                    Relay(raw, peer.id);
                    if (StateArrived != null) StateArrived(d);
                    break;
                }

                case NetMsg.Shot:
                {
                    ShotData d = NetCodec.ReadShot(r);
                    if (d.shooter != peer.id) return;
                    Relay(raw, peer.id);
                    if (ShotArrived != null) ShotArrived(d);
                    break;
                }

                case NetMsg.Death:
                {
                    DeathData d = NetCodec.ReadDeath(r);
                    if (d.victim != peer.id) return;
                    ApplyDeathScore(d);
                    Relay(raw, peer.id);
                    if (DeathArrived != null) DeathArrived(d);
                    break;
                }

                case NetMsg.Respawn:
                {
                    RespawnData d = NetCodec.ReadRespawn(r);
                    if (d.id != peer.id) return;
                    Relay(raw, peer.id);
                    if (RespawnArrived != null) RespawnArrived(d);
                    break;
                }

                case NetMsg.Throwable:
                {
                    ThrowableData d = NetCodec.ReadThrowable(r);
                    if (d.owner != peer.id) return;
                    Relay(raw, peer.id);
                    if (ThrowableArrived != null) ThrowableArrived(d);
                    break;
                }

                case NetMsg.Ping:
                    _socket.Send(NetCodec.Pong(), from);
                    break;
            }
        }

        private void HandleClientPacket(NetMsg type, NetReader r, IPEndPoint from, float now)
        {
            if (_hostEndPoint == null) return;

            // 客户端只认主机一个地址
            if (!from.Address.Equals(_hostEndPoint.Address) || from.Port != _hostEndPoint.Port) return;

            _lastHostHeard = now;

            switch (type)
            {
                case NetMsg.Welcome:
                {
                    byte id;
                    int spawn;
                    NetCodec.ReadWelcome(r, out id, out spawn);
                    LocalId = id;
                    AssignedSpawn = spawn;
                    InMatch = true;
                    _nextPing = 0f;
                    Debug.Log("[IronDawn] 已加入对局：玩家 #" + id + "，出生点 " + spawn);
                    if (Ready != null) Ready();
                    break;
                }

                case NetMsg.Reject:
                    Terminate(NetCodec.ReadReject(r));
                    break;

                case NetMsg.Roster:
                    _roster.Clear();
                    NetCodec.ReadRoster(r, _roster);
                    if (RosterChanged != null) RosterChanged();
                    break;

                case NetMsg.State:
                {
                    PlayerStateData d = NetCodec.ReadState(r);
                    if (d.id == LocalId) return;
                    if (StateArrived != null) StateArrived(d);
                    break;
                }

                case NetMsg.Shot:
                {
                    ShotData d = NetCodec.ReadShot(r);
                    if (d.shooter == LocalId) return;
                    if (ShotArrived != null) ShotArrived(d);
                    break;
                }

                case NetMsg.Death:
                {
                    DeathData d = NetCodec.ReadDeath(r);
                    if (DeathArrived != null) DeathArrived(d);
                    break;
                }

                case NetMsg.Respawn:
                {
                    RespawnData d = NetCodec.ReadRespawn(r);
                    if (d.id == LocalId) return;
                    if (RespawnArrived != null) RespawnArrived(d);
                    break;
                }

                case NetMsg.Throwable:
                {
                    ThrowableData d = NetCodec.ReadThrowable(r);
                    if (d.owner == LocalId) return;
                    if (ThrowableArrived != null) ThrowableArrived(d);
                    break;
                }
            }
        }

        private void HandleHello(NetReader r, IPEndPoint from, float now)
        {
            byte version;
            string name;
            NetCodec.ReadHello(r, out version, out name);

            if (version != NetConfig.ProtocolVersion)
            {
                _socket.Send(NetCodec.Reject("协议版本不一致，请使用同一个版本"), from);
                return;
            }

            // 已经在名册里：重发一次 Welcome，防止首次握手包丢失
            Peer existing = FindPeer(from);
            if (existing != null)
            {
                existing.lastHeard = now;
                _socket.Send(NetCodec.Welcome(existing.id, SpawnIndexOf(existing.id)), from);
                _socket.Send(NetCodec.Roster(_roster), from);
                return;
            }

            if (_roster.Count >= NetConfig.MaxPlayers)
            {
                _socket.Send(NetCodec.Reject("房间已满"), from);
                return;
            }

            byte id = AllocateId();
            if (id == 0)
            {
                _socket.Send(NetCodec.Reject("房间已满"), from);
                return;
            }

            Peer peer = new Peer();
            peer.id = id;
            peer.endPoint = from;
            peer.lastHeard = now;
            _peersByEndPoint[from.ToString()] = peer;
            _peersById[id] = peer;

            RosterEntry entry = new RosterEntry(id, SanitizeName(name, "玩家"), NextFreeSpawn());
            _roster.Add(entry);

            _socket.Send(NetCodec.Welcome(id, entry.spawnIndex), from);
            BroadcastRoster();

            Debug.Log("[IronDawn] 玩家加入：" + entry.name + " (#" + id + ")");
        }

        // ---------------- 发包 ----------------

        public void PublishState(PlayerStateData d)
        {
            if (CurrentRole == Role.None || !InMatch || _socket == null) return;

            d.id = LocalId;
            byte[] data = NetCodec.State(d);

            if (CurrentRole == Role.Host) Relay(data, LocalId);
            else SendToHost(data);
        }

        public void PublishShot(ShotData d)
        {
            if (CurrentRole == Role.None || !InMatch || _socket == null) return;

            d.shooter = LocalId;
            byte[] data = NetCodec.Shot(d);

            if (CurrentRole == Role.Host) Relay(data, LocalId);
            else SendToHost(data);
        }

        public void PublishDeath(byte victim, byte killer)
        {
            if (CurrentRole == Role.None || !InMatch || _socket == null) return;

            DeathData d;
            d.victim = victim;
            d.killer = killer;
            byte[] data = NetCodec.Death(d);

            if (CurrentRole == Role.Host)
            {
                ApplyDeathScore(d);
                Relay(data, LocalId);
            }
            else
            {
                SendToHost(data);
            }
        }

        public void PublishRespawn(RespawnData d)
        {
            if (CurrentRole == Role.None || !InMatch || _socket == null) return;

            d.id = LocalId;
            byte[] data = NetCodec.Respawn(d);

            if (CurrentRole == Role.Host) Relay(data, LocalId);
            else SendToHost(data);
        }

        public void PublishThrowable(ThrowableData d)
        {
            if (CurrentRole == Role.None || !InMatch || _socket == null) return;

            d.owner = LocalId;
            byte[] data = NetCodec.Throwable(d);

            if (CurrentRole == Role.Host) Relay(data, LocalId);
            else SendToHost(data);
        }

        private void Relay(byte[] data, byte exceptId)
        {
            if (_socket == null) return;

            foreach (KeyValuePair<byte, Peer> kv in _peersById)
                if (kv.Key != exceptId) _socket.Send(data, kv.Value.endPoint);
        }

        private void SendToHost(byte[] data)
        {
            if (_socket == null || _hostEndPoint == null) return;
            _socket.Send(data, _hostEndPoint);
        }

        private void BroadcastRoster()
        {
            _rosterDirty = false;
            _nextRosterPush = Now + 0.15f;
            Relay(NetCodec.Roster(_roster), 0);

            // 主机自己也走同一条刷新路径：远端替身的增删只看名册
            if (RosterChanged != null) RosterChanged();
        }

        // ---------------- 局域网广播 ----------------

        private void StartBeacon()
        {
            _beacon = new LanSocket();
            if (_beacon.Open(0)) return;

            _beacon = null;
        }

        private void SendBeacon()
        {
            if (_beacon == null || !_beacon.IsOpen) return;

            byte[] payload = NetCodec.Beacon(LocalName, _roster.Count, GamePort);
            _beacon.Send(payload, new IPEndPoint(IPAddress.Broadcast, NetConfig.BeaconPort));
        }

        // ---------------- 内部工具 ----------------

        private void ApplyDeathScore(DeathData d)
        {
            RosterEntry victim = FindEntry(d.victim);
            if (victim != null) victim.deaths++;

            if (d.killer != 0 && d.killer != d.victim)
            {
                RosterEntry killer = FindEntry(d.killer);
                if (killer != null) killer.kills++;
            }

            _rosterDirty = true;
        }

        private void DropTimeoutPeers(float now)
        {
            List<Peer> gone = null;

            foreach (KeyValuePair<byte, Peer> kv in _peersById)
            {
                if (now - kv.Value.lastHeard <= NetConfig.PeerTimeout) continue;
                if (gone == null) gone = new List<Peer>();
                gone.Add(kv.Value);
            }

            if (gone == null) return;

            for (int i = 0; i < gone.Count; i++)
            {
                Peer p = gone[i];
                _peersById.Remove(p.id);
                _peersByEndPoint.Remove(p.endPoint.ToString());

                RosterEntry entry = FindEntry(p.id);
                if (entry != null) _roster.Remove(entry);

                Debug.Log("[IronDawn] 玩家超时离开 #" + p.id);
            }

            _rosterDirty = true;
            BroadcastRoster();
        }

        private void Terminate(string reason)
        {
            if (CurrentRole == Role.None) return;

            LastError = reason;
            Stop();
            Debug.LogWarning("[IronDawn] 联机结束：" + reason);

            if (Terminated != null) Terminated(reason);
        }

        private byte AllocateId()
        {
            for (int i = 0; i < 250; i++)
            {
                _nextId++;
                if (_nextId < 2) _nextId = 2;
                if (!_peersById.ContainsKey(_nextId)) return _nextId;
            }

            return 0;
        }

        private RosterEntry FindEntry(byte id)
        {
            for (int i = 0; i < _roster.Count; i++)
                if (_roster[i].id == id) return _roster[i];

            return null;
        }

        private int SpawnIndexOf(byte id)
        {
            RosterEntry e = FindEntry(id);
            return e != null ? e.spawnIndex : 0;
        }

        private int NextFreeSpawn()
        {
            for (int candidate = 0; candidate < ArenaBuilder.SpawnPointCount; candidate++)
            {
                bool used = false;

                for (int i = 0; i < _roster.Count; i++)
                {
                    if (_roster[i].spawnIndex != candidate) continue;
                    used = true;
                    break;
                }

                if (!used) return candidate;
            }

            return UnityEngine.Random.Range(0, ArenaBuilder.SpawnPointCount);
        }

        private Peer FindPeer(IPEndPoint ep)
        {
            if (ep == null) return null;

            Peer peer;
            _peersByEndPoint.TryGetValue(ep.ToString(), out peer);
            return peer;
        }

        private static string SanitizeName(string name, string fallback)
        {
            string t = name == null ? string.Empty : name.Trim();
            if (t.Length == 0) t = fallback;
            if (t.Length > NetConfig.MaxNameLength) t = t.Substring(0, NetConfig.MaxNameLength);
            return t;
        }

        private static bool TryResolve(string address, out IPAddress ip)
        {
            ip = null;

            if (string.IsNullOrEmpty(address)) return false;

            string t = address.Trim();
            if (t.Length == 0) return false;

            if (IPAddress.TryParse(t, out ip)) return true;

            try
            {
                IPAddress[] all = Dns.GetHostAddresses(t);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].AddressFamily != AddressFamily.InterNetwork) continue;
                    ip = all[i];
                    return true;
                }
            }
            catch (Exception)
            {
            }

            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace IronDawn.Multiplayer
{
    /// <summary>
    /// 局域网消息类型。UDP 单包一消息，够用且不需要额外框架。
    /// </summary>
    public enum NetMsg : byte
    {
        Hello = 1,
        Welcome = 2,
        Reject = 3,
        Roster = 4,
        State = 5,
        Shot = 6,
        Death = 7,
        Respawn = 8,
        Ping = 9,
        Pong = 10
    }

    [Flags]
    public enum PlayerFlags : byte
    {
        None = 0,
        Crouching = 1 << 0,
        Aiming = 1 << 1,
        LeanLeft = 1 << 2,
        LeanRight = 1 << 3,
        Alive = 1 << 4
    }

    /// <summary>所有联机常量。两端都读这里，改一处就同步。</summary>
    public static class NetConfig
    {
        public const byte ProtocolVersion = 1;
        public const int GamePort = 47800;
        public const int BeaconPort = 47801;
        public const int MaxPlayers = 8;

        /// <summary>位置状态发送频率。</summary>
        public const float StateHz = 20f;

        public const float KeepAliveInterval = 1f;
        public const float PeerTimeout = 6f;
        public const float JoinTimeout = 8f;
        public const float RespawnDelay = 3f;
        public const int MaxNameLength = 12;
    }

    public struct PlayerStateData
    {
        public byte id;
        public Vector3 position;
        public float yaw;
        public float pitch;
        public PlayerFlags flags;
        public float health;
    }

    public struct ShotData
    {
        public byte shooter;
        public Vector3 from;
        public Vector3 to;
        public byte hit;
        public float damage;
    }

    public struct DeathData
    {
        public byte victim;
        public byte killer;
    }

    public struct RespawnData
    {
        public byte id;
        public Vector3 position;
        public float yaw;
    }

    /// <summary>名册条目。主机是唯一作者，客户端只读。</summary>
    public sealed class RosterEntry
    {
        public byte id;
        public string name;
        public int spawnIndex;
        public int kills;
        public int deaths;
        public bool connected;

        public RosterEntry(byte id, string name, int spawnIndex)
        {
            this.id = id;
            this.name = name;
            this.spawnIndex = spawnIndex;
            this.connected = true;
        }
    }

    /// <summary>射线打中的远端玩家。射击端只上报 id，扣血由被打的一方裁决。</summary>
    public interface INetHitTarget
    {
        byte NetId { get; }
    }

    public sealed class NetWriter
    {
        private readonly MemoryStream _stream;
        private readonly BinaryWriter _writer;

        public NetWriter(NetMsg type)
        {
            _stream = new MemoryStream(128);
            _writer = new BinaryWriter(_stream, Encoding.UTF8);
            _writer.Write((byte)type);
        }

        public void Byte(byte v) { _writer.Write(v); }
        public void UShort(ushort v) { _writer.Write(v); }
        public void Float(float v) { _writer.Write(v); }

        public void Points(Vector3 v)
        {
            _writer.Write(v.x);
            _writer.Write(v.y);
            _writer.Write(v.z);
        }

        public void Text(string s)
        {
            string t = s ?? string.Empty;
            if (t.Length > NetConfig.MaxNameLength) t = t.Substring(0, NetConfig.MaxNameLength);

            byte[] bytes = Encoding.UTF8.GetBytes(t);
            if (bytes.Length > 255)
            {
                byte[] cut = new byte[255];
                Array.Copy(bytes, cut, 255);
                bytes = cut;
            }

            _writer.Write((byte)bytes.Length);
            _writer.Write(bytes);
        }

        public byte[] Done()
        {
            _writer.Flush();
            return _stream.ToArray();
        }
    }

    public sealed class NetReader
    {
        private readonly BinaryReader _reader;

        public NetReader(byte[] data)
        {
            _reader = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
        }

        public bool HasData { get { return _reader.BaseStream.Position < _reader.BaseStream.Length; } }

        public byte Byte() { return _reader.ReadByte(); }
        public ushort UShort() { return _reader.ReadUInt16(); }
        public float Float() { return _reader.ReadSingle(); }

        public Vector3 Points()
        {
            float x = _reader.ReadSingle();
            float y = _reader.ReadSingle();
            float z = _reader.ReadSingle();
            return new Vector3(x, y, z);
        }

        public string Text()
        {
            int len = _reader.ReadByte();
            return Encoding.UTF8.GetString(_reader.ReadBytes(len));
        }
    }

    /// <summary>消息编解码。收发两端共用，避免两处手写字节序。</summary>
    public static class NetCodec
    {
        private const string BeaconTag = "IRONDAWN";

        public static byte[] Hello(string name)
        {
            NetWriter w = new NetWriter(NetMsg.Hello);
            w.Byte(NetConfig.ProtocolVersion);
            w.Text(name);
            return w.Done();
        }

        public static void ReadHello(NetReader r, out byte version, out string name)
        {
            version = r.Byte();
            name = r.Text();
        }

        public static byte[] Welcome(byte id, int spawnIndex)
        {
            NetWriter w = new NetWriter(NetMsg.Welcome);
            w.Byte(id);
            w.Byte((byte)spawnIndex);
            w.Byte(NetConfig.ProtocolVersion);
            return w.Done();
        }

        public static void ReadWelcome(NetReader r, out byte id, out int spawnIndex)
        {
            id = r.Byte();
            spawnIndex = r.Byte();
        }

        public static byte[] Reject(string reason)
        {
            NetWriter w = new NetWriter(NetMsg.Reject);
            w.Byte(NetConfig.ProtocolVersion);
            w.Text(reason);
            return w.Done();
        }

        public static string ReadReject(NetReader r)
        {
            r.Byte();
            return r.Text();
        }

        public static byte[] Roster(IList<RosterEntry> list)
        {
            NetWriter w = new NetWriter(NetMsg.Roster);
            w.Byte((byte)Mathf.Min(list.Count, 255));

            for (int i = 0; i < list.Count; i++)
            {
                RosterEntry e = list[i];
                w.Byte(e.id);
                w.Text(e.name);
                w.Byte((byte)Mathf.Clamp(e.spawnIndex, 0, 255));
                w.UShort((ushort)Mathf.Clamp(e.kills, 0, 65535));
                w.UShort((ushort)Mathf.Clamp(e.deaths, 0, 65535));
                w.Byte((byte)(e.connected ? 1 : 0));
            }

            return w.Done();
        }

        public static void ReadRoster(NetReader r, List<RosterEntry> into)
        {
            int count = r.Byte();
            for (int i = 0; i < count; i++)
            {
                int id = r.Byte();
                string name = r.Text();
                int spawn = r.Byte();
                int kills = r.UShort();
                int deaths = r.UShort();
                bool connected = r.Byte() != 0;

                RosterEntry e = new RosterEntry((byte)id, name, spawn);
                e.kills = kills;
                e.deaths = deaths;
                e.connected = connected;
                into.Add(e);
            }
        }

        public static byte[] State(PlayerStateData d)
        {
            NetWriter w = new NetWriter(NetMsg.State);
            w.Byte(d.id);
            w.Points(d.position);
            w.Float(d.yaw);
            w.Float(d.pitch);
            w.Byte((byte)d.flags);
            w.Float(d.health);
            return w.Done();
        }

        public static PlayerStateData ReadState(NetReader r)
        {
            PlayerStateData d;
            d.id = r.Byte();
            d.position = r.Points();
            d.yaw = r.Float();
            d.pitch = r.Float();
            d.flags = (PlayerFlags)r.Byte();
            d.health = r.Float();
            return d;
        }

        public static byte[] Shot(ShotData d)
        {
            NetWriter w = new NetWriter(NetMsg.Shot);
            w.Byte(d.shooter);
            w.Points(d.from);
            w.Points(d.to);
            w.Byte(d.hit);
            w.Float(d.damage);
            return w.Done();
        }

        public static ShotData ReadShot(NetReader r)
        {
            ShotData d;
            d.shooter = r.Byte();
            d.from = r.Points();
            d.to = r.Points();
            d.hit = r.Byte();
            d.damage = r.Float();
            return d;
        }

        public static byte[] Death(DeathData d)
        {
            NetWriter w = new NetWriter(NetMsg.Death);
            w.Byte(d.victim);
            w.Byte(d.killer);
            return w.Done();
        }

        public static DeathData ReadDeath(NetReader r)
        {
            DeathData d;
            d.victim = r.Byte();
            d.killer = r.Byte();
            return d;
        }

        public static byte[] Respawn(RespawnData d)
        {
            NetWriter w = new NetWriter(NetMsg.Respawn);
            w.Byte(d.id);
            w.Points(d.position);
            w.Float(d.yaw);
            return w.Done();
        }

        public static RespawnData ReadRespawn(NetReader r)
        {
            RespawnData d;
            d.id = r.Byte();
            d.position = r.Points();
            d.yaw = r.Float();
            return d;
        }

        public static byte[] Ping()
        {
            return new NetWriter(NetMsg.Ping).Done();
        }

        public static byte[] Pong()
        {
            return new NetWriter(NetMsg.Pong).Done();
        }

        // ---------------- 局域网广播 ----------------

        /// <summary>主机每秒广播一次，菜单据此列出可加入的对局。</summary>
        public static byte[] Beacon(string hostName, int players, int port)
        {
            string text = BeaconTag + ";" + NetConfig.ProtocolVersion + ";" + players + ";" + port + ";" + (hostName ?? "");
            return Encoding.UTF8.GetBytes(text);
        }

        public static bool TryReadBeacon(string text, out string hostName, out int players, out int port)
        {
            hostName = null;
            players = 0;
            port = 0;

            if (string.IsNullOrEmpty(text)) return false;

            string[] parts = text.Split(';');
            if (parts.Length < 5) return false;
            if (parts[0] != BeaconTag) return false;

            byte version;
            if (!byte.TryParse(parts[1], out version) || version != NetConfig.ProtocolVersion) return false;

            if (!int.TryParse(parts[2], out players)) return false;
            if (!int.TryParse(parts[3], out port)) return false;

            hostName = parts[4];
            return port > 0;
        }
    }
}

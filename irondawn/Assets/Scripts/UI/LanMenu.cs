using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using IronDawn.Multiplayer;
using UnityEngine;

namespace IronDawn
{
    /// <summary>
    /// 对局外的界面：建房、按 IP 加入、以及局域网内自动发现的对局列表。
    ///
    /// 局域网发现是"主机每秒广播一包，菜单在 BeaconPort 收"这种最土也最省事的做法：
    /// 不需要大厅服务器，也不需要玩家自己去查本机 IP——但 IP 仍然显示出来，
    /// 万一广播被防火墙拦了，手输依然能用。
    /// </summary>
    public sealed class LanMenu : MonoBehaviour
    {
        public ArenaRuntime runtime;
        public bool Visible;
        public string Notice;

        private sealed class Beacon
        {
            public string address;
            public string hostName;
            public int players;
            public int port;
            public float seenAt;
        }

        private readonly List<Beacon> _beacons = new List<Beacon>();
        private readonly List<string> _localAddresses = new List<string>();

        private string _playerName = "玩家";
        private string _joinAddress = "";
        private string _status = "";

        private LanSocket _discovery;
        private float _nextDiscoveryTry;

        private Font _font;
        private GUIStyle _title;
        private GUIStyle _subtitle;
        private GUIStyle _label;
        private GUIStyle _small;
        private GUIStyle _button;
        private GUIStyle _field;
        private bool _stylesReady;

        void Awake()
        {
            _font = CreateOsFont();
            Application.runInBackground = true;
        }

        void Update()
        {
            if (!Visible)
            {
                CloseDiscovery();
                return;
            }

            if (_localAddresses.Count == 0) _localAddresses.AddRange(CollectLocalAddresses());

            ListenForBeacons();
            PruneBeacons();
        }

        void OnDestroy()
        {
            CloseDiscovery();
        }

        // ---------------- 局域网发现 ----------------

        private void ListenForBeacons()
        {
            if (_discovery == null)
            {
                if (Time.unscaledTime < _nextDiscoveryTry) return;

                _nextDiscoveryTry = Time.unscaledTime + 2f;
                _discovery = new LanSocket();

                if (!_discovery.Open(NetConfig.BeaconPort))
                {
                    // 端口被占用（同机跑两个实例很常见）：不影响手输 IP 加入
                    _discovery = null;
                    return;
                }
            }

            for (int guard = 0; guard < 64; guard++)
            {
                byte[] data;
                IPEndPoint from;
                if (!_discovery.Receive(out data, out from)) break;

                string text = Encoding.UTF8.GetString(data);

                string hostName;
                int players;
                int port;
                if (!NetCodec.TryReadBeacon(text, out hostName, out players, out port)) continue;

                AddBeacon(from.Address.ToString(), hostName, players, port);
            }
        }

        private void AddBeacon(string address, string hostName, int players, int port)
        {
            for (int i = 0; i < _beacons.Count; i++)
            {
                Beacon b = _beacons[i];
                if (b.address != address || b.port != port) continue;

                b.hostName = hostName;
                b.players = players;
                b.seenAt = Time.unscaledTime;
                return;
            }

            Beacon fresh = new Beacon();
            fresh.address = address;
            fresh.hostName = hostName;
            fresh.players = players;
            fresh.port = port;
            fresh.seenAt = Time.unscaledTime;
            _beacons.Add(fresh);
        }

        private void PruneBeacons()
        {
            for (int i = _beacons.Count - 1; i >= 0; i--)
                if (Time.unscaledTime - _beacons[i].seenAt > 3f) _beacons.RemoveAt(i);
        }

        private void CloseDiscovery()
        {
            if (_discovery == null) return;

            _discovery.Close();
            _discovery = null;
        }

        private static List<string> CollectLocalAddresses()
        {
            List<string> result = new List<string>();

            try
            {
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();

                for (int i = 0; i < nics.Length; i++)
                {
                    NetworkInterface nic = nics[i];
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    UnicastIPAddressInformationCollection addrs = nic.GetIPProperties().UnicastAddresses;

                    foreach (UnicastIPAddressInformation info in addrs)
                    {
                        if (info.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                        string ip = info.Address.ToString();
                        if (ip.StartsWith("127.")) continue;
                        if (result.Contains(ip)) continue;

                        result.Add(ip);
                    }
                }
            }
            catch (Exception)
            {
                // 某些平台拿不到网卡信息，退回回环地址
            }

            if (result.Count == 0) result.Add("127.0.0.1");
            return result;
        }

        // ---------------- 界面 ----------------

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
            catch (Exception)
            {
            }

            return null;
        }

        private void EnsureStyles()
        {
            if (_stylesReady && _label != null) return;

            _title = new GUIStyle(GUI.skin.label);
            _title.fontSize = 40;
            _title.alignment = TextAnchor.MiddleCenter;
            _title.normal.textColor = new Color(0.92f, 0.94f, 0.98f);

            _subtitle = new GUIStyle(GUI.skin.label);
            _subtitle.fontSize = 15;
            _subtitle.alignment = TextAnchor.MiddleCenter;
            _subtitle.normal.textColor = new Color(0.55f, 0.62f, 0.72f);

            _label = new GUIStyle(GUI.skin.label);
            _label.fontSize = 15;
            _label.normal.textColor = new Color(0.86f, 0.88f, 0.92f);

            _small = new GUIStyle(_label);
            _small.fontSize = 13;
            _small.normal.textColor = new Color(0.62f, 0.67f, 0.75f);

            _button = new GUIStyle(GUI.skin.button);
            _button.fontSize = 16;
            _button.fixedHeight = 38f;

            _field = new GUIStyle(GUI.skin.textField);
            _field.fontSize = 16;
            _field.fixedHeight = 30f;

            if (_font != null)
            {
                _title.font = _font;
                _subtitle.font = _font;
                _label.font = _font;
                _small.font = _font;
                _button.font = _font;
                _field.font = _font;
            }

            _stylesReady = true;
        }

        private void OnGUI()
        {
            if (!Visible) return;

            EnsureStyles();

            float w = Mathf.Min(Screen.width - 60f, 520f);
            float h = Screen.height - 60f;
            float x = (Screen.width - w) * 0.5f;
            float y = 30f;

            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 1f);
            GUI.Box(new Rect(x - 18f, y - 12f, w + 36f, h), GUIContent.none);
            GUI.color = prev;

            GUILayout.BeginArea(new Rect(x, y, w, h));
            GUILayout.Space(18f);

            GUILayout.Label("IRON DAWN", _title);
            GUILayout.Label("局域网对战 · 封闭竞技场", _subtitle);
            GUILayout.Space(18f);

            // ---- 身份 ----
            GUILayout.Label("玩家名", _label);
            _playerName = GUILayout.TextField(_playerName ?? string.Empty, NetConfig.MaxNameLength, _field);
            GUILayout.Space(12f);

            // ---- 建房 ----
            if (GUILayout.Button("创建对局（本机当主机）", _button))
            {
                _status = "";
                runtime.HostGame(_playerName);
            }

            GUILayout.Label("端口 " + NetConfig.GamePort + " · 上限 " + NetConfig.MaxPlayers + " 人 · 本机地址："
                + string.Join(" / ", _localAddresses.ToArray()), _small);

            GUILayout.Space(16f);

            // ---- 局域网发现 ----
            GUILayout.Label("局域网内的对局", _label);

            if (_beacons.Count == 0)
            {
                GUILayout.Label("正在搜索……（也可以直接在下面填 IP）", _small);
            }
            else
            {
                for (int i = 0; i < _beacons.Count; i++)
                {
                    Beacon b = _beacons[i];
                    string text = b.address + " · " + b.hostName + " · " + b.players + "/" + NetConfig.MaxPlayers;

                    if (GUILayout.Button(text, _button))
                    {
                        _status = "";
                        runtime.JoinGame(_playerName, b.address);
                        return;
                    }
                }
            }

            GUILayout.Space(16f);

            // ---- 手输加入 ----
            GUILayout.Label("主机 IP", _label);
            _joinAddress = GUILayout.TextField(_joinAddress ?? string.Empty, 64, _field);
            GUILayout.Space(6f);

            if (GUILayout.Button("加入对局", _button))
            {
                string address = (_joinAddress ?? string.Empty).Trim();
                if (address.Length == 0)
                {
                    _status = "请先填写主机 IP";
                }
                else
                {
                    _status = "";
                    runtime.JoinGame(_playerName, address);
                }
            }

            GUILayout.Space(16f);
            GUILayout.Label("WASD 移动 · 空格 跳 · C 蹲 · Q/E 探头 · 右键 瞄准 · 左键 射击 · R 换弹 · ESC 释放鼠标", _small);

            GUILayout.FlexibleSpace();

            if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status, _label);
            if (!string.IsNullOrEmpty(Notice)) GUILayout.Label(Notice, _small);

            GUILayout.Space(14f);
            GUILayout.EndArea();
        }
    }
}

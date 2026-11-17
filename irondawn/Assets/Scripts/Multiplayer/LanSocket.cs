using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace IronDawn.Multiplayer
{
    /// <summary>
    /// UDP 套接字薄封装。局域网对战不需要可靠传输框架，
    /// 这里只负责"开了没、收一包、发一包"，不碰任何游戏逻辑。
    /// </summary>
    public sealed class LanSocket : IDisposable
    {
        private UdpClient _udp;

        public int Port { get; private set; }
        public bool IsOpen { get { return _udp != null; } }

        /// <summary>port 传 0 表示让系统分配临时端口（客户端用）。</summary>
        public bool Open(int port)
        {
            Close();

            try
            {
                _udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
                _udp.EnableBroadcast = true;

                IPEndPoint local = (IPEndPoint)_udp.Client.LocalEndPoint;
                Port = local != null ? local.Port : port;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[IronDawn] 打开 UDP 端口 " + port + " 失败: " + e.Message);
                Close();
                Port = 0;
                return false;
            }
        }

        public void Send(byte[] data, IPEndPoint to)
        {
            if (_udp == null || data == null || to == null) return;

            try
            {
                _udp.Send(data, data.Length, to);
            }
            catch (SocketException)
            {
                // 目标暂时不可达是局域网的常态，丢掉即可
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public bool Receive(out byte[] data, out IPEndPoint from)
        {
            data = null;
            from = null;

            if (_udp == null) return false;

            try
            {
                if (_udp.Available <= 0) return false;

                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] packet = _udp.Receive(ref remote);
                if (packet == null || packet.Length == 0) return false;

                data = packet;
                from = remote;
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public void Close()
        {
            if (_udp == null) return;

            try { _udp.Close(); }
            catch (Exception) { }

            _udp = null;
            Port = 0;
        }

        public void Dispose()
        {
            Close();
        }
    }
}

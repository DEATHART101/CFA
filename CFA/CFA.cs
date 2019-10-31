using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CFA
{
    public class CFAClient
    {
        #region Defines

        public enum SendControlTypes
        {
            None,
            Arrival,
            Ordered,
            Both
        };

        private const int COMMMON_BUFFER_LENGTH = 2000;
        private const int MESSAGE_QUEUE_LENGTH = 2000;
        private const int MESSAGE_RESEND_INTERVAL = 2;  // ms

        private class CFATunnel
        {
            private byte[] m_buffer;

            private int m_unACKedPackages;
            private int m_counter;
            private byte[][] m_messages;

            public CFATunnel()
            {
                m_buffer = new byte[COMMMON_BUFFER_LENGTH];
                ClearBuffer(m_buffer);

                m_unACKedPackages = 0;
                m_counter = 0;
                m_messages = new byte[MESSAGE_QUEUE_LENGTH][];
            }

            #region Interfaces

            public bool AllClear
            {
                get
                {
                    return m_unACKedPackages == 0;
                }
            }

            public void SendPackage(byte[] dgram, int bytes, UdpClient send_client, IPEndPoint remote_ep)
            {
                if (m_counter == MESSAGE_QUEUE_LENGTH - 1)
                {

                }
                else
                {
                    m_counter++;
                    int length = BuildCFAPackgage(dgram, bytes, SendControlTypes.Arrival, m_counter, m_buffer);

                    byte[] built_message = new byte[length];
                    m_buffer.CopyTo(built_message, 0);
                    m_messages[m_counter] = built_message;

                    send_client.Send(built_message, length, remote_ep);
                }

                m_unACKedPackages++;
            }

            public void ResendUnAckedPackages(UdpClient send_client, IPEndPoint remote_ep)
            {
                for (int i = 0; i < m_counter; i++)
                {
                    if (m_messages[i] != null)
                    {
                        send_client.Send(m_messages[i], m_messages[i].Length, remote_ep);
                    }
                }
            }

            public void ACKPackage(int package_id)
            {
                if (m_messages[package_id] != null)
                {
                    m_unACKedPackages--;
                }

                m_messages[package_id] = null;
            }

            #endregion
        }

        #endregion

        private UdpClient m_udpClient;
        private int m_udpPort;
        private byte[] m_buffer;

        private int m_listenPort;

        private Dictionary<IPEndPoint, CFATunnel> m_remoteTunnels;

        public CFAClient()
        {
            m_listenPort = -1;

            m_udpClient = new UdpClient();
            m_udpPort = ((IPEndPoint)(m_udpClient.Client.LocalEndPoint)).Port;
            m_buffer = new byte[COMMMON_BUFFER_LENGTH];
            ClearBuffer(m_buffer);

            m_lastTunnelWorkTime = DateTime.Now;

            m_remoteTunnels = new Dictionary<IPEndPoint, CFATunnel>();
        }

        public CFAClient(int port)
        {
            m_listenPort = port;

            m_udpClient = new UdpClient(m_listenPort);
            m_buffer = new byte[COMMMON_BUFFER_LENGTH];

            m_remoteTunnels = new Dictionary<IPEndPoint, CFATunnel>();
        }

        #region Interfaces

        public byte[] Receive(ref IPEndPoint remoteEP)
        {
            
        }

        public void Send(byte[] dgram, int bytes, IPEndPoint endPoint, SendControlTypes sendControlType)
        {
            switch (sendControlType)
            {
                case SendControlTypes.None:
                    {
                        m_udpClient.Send(dgram, bytes, endPoint);
                    }
                    break;
                case SendControlTypes.Ordered:
                    break;
                case SendControlTypes.Arrival:
                case SendControlTypes.Both:
                    {
                        lock (m_remoteTunnels)
                        {
                            if (m_remoteTunnels.ContainsKey(endPoint))
                            {
                                m_remoteTunnels[endPoint].SendPackage(dgram, bytes, m_udpClient, endPoint);
                            }
                            else
                            {
                                CreateNewTunnel(endPoint).SendPackage(dgram, bytes, m_udpClient, endPoint);
                            }
                        }
                    }

                    break;
                default:
                    break;
            }

            // Will automaticlly close when all packages are acked.
            StartTunnelWork();
        }

        #endregion

        #region Tunnel Work

        private void StartTunnelWork()
        {
            ThreadStart send_ts = new ThreadStart(TunnelSend);
            m_tunnelSendThread = new Thread(send_ts);
            m_tunnelSendThread.Start();

            ThreadStart recv_ts = new ThreadStart(TunnelReceive);
            m_tunnelRecvACKThread = new Thread(recv_ts);
            m_tunnelRecvACKThread.Start();
        }

        private Thread m_tunnelSendThread;
        private UdpClient m_tunnelSendUdpClient;
        private DateTime m_lastTunnelWorkTime;

        private void TunnelSend()
        {
            m_tunnelSendUdpClient = new UdpClient(m_udpPort);

            while (true)
            {
                var delta_time = DateTime.Now - m_lastTunnelWorkTime;
                if (delta_time.Milliseconds >= MESSAGE_RESEND_INTERVAL)
                {
                    m_lastTunnelWorkTime = DateTime.Now;

                    // Resend packages
                    lock (m_remoteTunnels)
                    {
                        // If all clear
                        foreach (var ep_tunnel in m_remoteTunnels)
                        {
                            if (!ep_tunnel.Value.AllClear)
                            {
                                goto NOT_CLEAR;
                            }
                        }
                        break;

                        NOT_CLEAR:
                        foreach (var ep_tunnel in m_remoteTunnels)
                        {
                            ep_tunnel.Value.ResendUnAckedPackages(m_tunnelSendUdpClient, ep_tunnel.Key);
                        }
                    }
                }
            }

            m_tunnelSendUdpClient.Close();
            m_tunnelSendUdpClient = null;
            try
            {
                m_tunnelReceiveUdpClient.Close();
            }
            finally
            {
                m_tunnelReceiveUdpClient = null;
                m_tunnelRecvACKThread.Abort();
                m_tunnelRecvACKThread = null;
            }
        }

        private Thread m_tunnelRecvACKThread;
        private UdpClient m_tunnelReceiveUdpClient;

        private void TunnelReceive()
        {
            m_tunnelReceiveUdpClient = new UdpClient(m_udpPort);

            while (true)
            {
                IPEndPoint remote_ep = new IPEndPoint(IPAddress.Any, 0);
                byte[] recv_data = m_tunnelReceiveUdpClient.Receive(ref remote_ep);

                ProccessACKPackage(remote_ep, recv_data);
            }
        }

        #endregion

        #region Helpers

        private CFATunnel CreateNewTunnel(IPEndPoint end_point)
        {
            CFATunnel tunnel = new CFATunnel();
            m_remoteTunnels[end_point] = tunnel;

            return tunnel;
        }

        private static void ClearBuffer(byte[] buffer)
        {
            int length = buffer.Length;
            for (int i = 0; i < length; i++)
            {
                buffer[i] = 0;
            }
        }

        private static void ByteWrite(byte[] source, int count, byte[] dest, int start)
        {
            int end = start + count;
            for (int i = start; i < end; i++)
            {
                dest[i] = source[i - start];
            }
        }

        private void ProccessCFAPackgage(IPEndPoint remote_ep, byte[] package)
        {
            if (package[0] == 67 &&
                package[1] == 70 &&
                package[2] == 65)
            {
                // Is a CFA package
            }
            else
            {
                // Is a normal udp package
                m_receivePackages.Enqueue(package);
            }
        }

        private void ProccessACKPackage(IPEndPoint remote_ep, byte[] package)
        {
            if (package[0] == 67 &&
                package[1] == 70 &&
                package[2] == 65)
            {
                // Is a CFA package
                SendControlTypes control_type = (SendControlTypes)package[3];
                int package_id = 0;
                package_id = (package[4] << 24);
                package_id = package_id | (package[5] << 16);
                package_id = package_id | (package[6] << 8);
                package_id = package_id | (package[7]);

                switch (control_type)
                {
                    case SendControlTypes.None:
                        break;
                    case SendControlTypes.Ordered:
                        break;
                    case SendControlTypes.Arrival:
                    case SendControlTypes.Both:
                        {
                            lock (m_remoteTunnels)
                            {
                                if (m_remoteTunnels.ContainsKey(remote_ep))
                                {
                                    m_remoteTunnels[remote_ep].ACKPackage(package_id);
                                }
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        private static int BuildCFAPackgage(byte[] dgram, int bytes, SendControlTypes sendControlType, int package_id, byte[] copy_to)
        {
            ClearBuffer(copy_to);

            copy_to[0] = 67;    // C
            copy_to[1] = 70;    // F
            copy_to[2] = 65;    // A
            copy_to[3] = (byte)sendControlType;
            copy_to[4] = (byte)((package_id >> 24) & 255) ;
            copy_to[5] = (byte)((package_id >> 16) & 255);
            copy_to[6] = (byte)((package_id >> 8) & 255);
            copy_to[7] = (byte)((package_id) & 255);
            ByteWrite(dgram, bytes, copy_to, 8);

            return bytes + 8;
        }

        #endregion
    }
}

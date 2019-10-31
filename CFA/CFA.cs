using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

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

        private class CFATunnel
        {
            #region Defines

            private const int MESSAGE_QUEUE_LENGTH = 2000;
            private const int MESSAGE_RESEND_INTERVAL = 2;  // ms

            #endregion

            private int m_
            private byte[][] m_messages;

            public CFATunnel()
            {
                m_messages = new byte[MESSAGE_QUEUE_LENGTH][];
            }
        }

        #endregion

        private UdpClient m_updClient;
        private byte[] m_buffer;


        public CFAClient()
        {
        }

        #region Interfaces

        public void Send(byte[] dgram, int bytes, IPEndPoint endPoint, SendControlTypes sendControlType)
        {
            

            switch (sendControlType)
            {
                case SendControlTypes.None:
                    break;
                case SendControlTypes.Arrival:
                    break;
                case SendControlTypes.Ordered:
                    break;
                case SendControlTypes.Both:
                    break;
                default:
                    break;
            }
        }

        #endregion

        #region Helpers

        private void ClearBuffer()
        {
            for (int i = 0; i < COMMMON_BUFFER_LENGTH; i++)
            {
                m_buffer[i] = 0;
            }
        }

        #endregion
    }
}

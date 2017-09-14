﻿using System;
using Hazel;

namespace SM64O
{
    public static class ConnectionExtensions
    {
        public static void SendBytes(this Connection conn, PacketType type, byte[] data,
            SendOption sendOption = SendOption.None,
            string origin = "unk")
        {
            byte[] buffer = new byte[data.Length + 1];
            buffer[0] = (byte)type;
            Array.Copy(data, 0, buffer, 1, data.Length);

            NetworkLogger.Singleton.Value.LogOutgoingPacket(buffer, origin);

            conn.SendBytes(buffer, sendOption);
        }
    }
}
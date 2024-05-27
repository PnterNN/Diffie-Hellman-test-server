using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SProjectServer.Util;

namespace SProjectServer.NET.IO
{
    public class PacketReader : BinaryReader
    {
        private NetworkStream _ns;
        public PacketReader(NetworkStream ns) : base(ns)
        {
            _ns = ns;
        }
        public string ReadMessage()
        {
            byte[] msgBuffer;
            var length = ReadInt32();
            msgBuffer = new byte[length];
            _ns.Read(msgBuffer, 0, length);

            var msg = Encoding.UTF8.GetString(msgBuffer);
            return msg;
        }

        public string ReadEncryptedMessage(byte[] masterKey)
        {
            byte[] msgBuffer;
            var length = ReadInt32();
            msgBuffer = new byte[length];
            _ns.Read(msgBuffer, 0, length);
            return AesUtil.DecryptStringFromBytes_Aes(msgBuffer, masterKey);
        }
        public byte[] ReadPublicKey()
        {
            int length = ReadInt32();
            byte[] publicKey = new byte[length];
            _ns.Read(publicKey, 0, length);

            return publicKey;
        }
    }
}

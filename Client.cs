using SProjectServer.NET.IO;
using SProjectServer.Util;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SProjectServer
{
    public class Client
    {
        public TcpClient ClientSocket { get; set; }
        PacketReader _packetReader;
        private RichTextBox console;

        private string username;

        private BigInteger p;
        private BigInteger g;
        private BigInteger serverPrivateKey;
        private BigInteger serverPublicKey;
        private BigInteger clientPublicKey;
        private BigInteger sharedKey;
        private byte[] masterKey;


        public static BigInteger ModPow(BigInteger baseValue, BigInteger exponent, BigInteger modulus)
        {
            return BigInteger.ModPow(baseValue, exponent, modulus);
        }

        public Client(TcpClient client, RichTextBox console)
        {
            this.console = console;
            ClientSocket = client;
            _packetReader = new PacketReader(ClientSocket.GetStream());
            process();

            Random random = new Random();
            serverPrivateKey = random.Next(100, 10000);

            RandomPrimeGenerator rpg = new RandomPrimeGenerator();

            p = rpg.GenerateRandomPrime();
            g = rpg.GenerateRandomPrime();

            serverPublicKey = ModPow(p, serverPrivateKey, g);

            console.Invoke(new Action(() =>
            {
                console.Text += "p: " + p + "\n";
                console.Text += "g: " + g + "\n";
                console.Text += "Server private key: " + serverPrivateKey + "\n";
                console.Text += "Server public key: " + serverPublicKey + "\n";
            }));

            PacketBuilder testPacket = new PacketBuilder();
            testPacket.WriteOpCode(0);
            testPacket.WriteMessage(p.ToString());
            testPacket.WriteMessage(g.ToString());
            testPacket.WriteMessage(serverPublicKey.ToString());
            this.ClientSocket.Client.Send(testPacket.GetPacketBytes());
        }

        private void process()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        var opcode = _packetReader.ReadByte();
                        switch (opcode)
                        {
                            case 0:
                                clientPublicKey = BigInteger.Parse(_packetReader.ReadMessage());
                                sharedKey = ModPow(clientPublicKey, serverPrivateKey, g);
                                byte[] doubleBytes = sharedKey.ToByteArray();
                                using (SHA256 sha256 = SHA256.Create())
                                {
                                    masterKey = sha256.ComputeHash(doubleBytes);
                                }

                                console?.Invoke(new Action(() =>
                                {
                                    console.Text += "Client public key: " + clientPublicKey + "\n";
                                    console.Text += "sharedKey: " + sharedKey + "\n";
                                    console.Text += "Ana anahtar: " + BitConverter.ToString(masterKey).Replace("-", "") + "\n";
                                }));
                                break;
                            case 1:
                                username = DecryptStringFromBytes_Aes(Convert.FromBase64String(_packetReader.ReadMessage()), masterKey);
                                if (Form1._users.Any(u => u.username == username))
                                {
                                    PacketBuilder pb = new PacketBuilder();
                                    pb.WriteOpCode(1);
                                    pb.WriteMessage(Convert.ToBase64String(EncryptStringToBytes_Aes("false", masterKey)));
                                    this.ClientSocket.Client.Send(pb.GetPacketBytes());
                                    return;
                                }
                                else
                                {
                                    PacketBuilder pb = new PacketBuilder();
                                    pb.WriteOpCode(1);
                                    pb.WriteMessage(Convert.ToBase64String(EncryptStringToBytes_Aes("true", masterKey)));
                                    this.ClientSocket.Client.Send(pb.GetPacketBytes());
                                    Form1._users.Add(this);
                                }
                                Form1._users.ForEach(u =>
                                {
                                    Form1._users.ForEach(u2 =>
                                    {
                                        if (u2.username != u.username)
                                        {
                                            PacketBuilder pb = new PacketBuilder();
                                            pb.WriteOpCode(2);
                                            pb.WriteMessage(Convert.ToBase64String(EncryptStringToBytes_Aes(u2.username, u.masterKey)));
                                            u.ClientSocket.Client.Send(pb.GetPacketBytes());
                                        }
                                    });
                                });
                                console?.Invoke(new Action(() =>
                                {
                                    console.Text += username + " connected\n";
                                }));
                                break;
                            case 4:
                                string sendingUser = DecryptStringFromBytes_Aes(Convert.FromBase64String(_packetReader.ReadMessage()), masterKey);
                                string message = DecryptStringFromBytes_Aes(Convert.FromBase64String(_packetReader.ReadMessage()), masterKey);

                                if (sendingUser == "Everyone")
                                {
                                    foreach (var user in Form1._users)
                                    {
                                        if (user.username != username)
                                        {
                                            PacketBuilder pb = new PacketBuilder();
                                            pb.WriteOpCode(4);
                                            pb.WriteMessage(Convert.ToBase64String(EncryptStringToBytes_Aes(message, user.masterKey)));
                                            user.ClientSocket.Client.Send(pb.GetPacketBytes());
                                        }
                                    }
                                }
                                else
                                {
                                    foreach (var user in Form1._users)
                                    {
                                        if (user.username == sendingUser)
                                        {
                                            PacketBuilder pb = new PacketBuilder();
                                            pb.WriteOpCode(4);
                                            pb.WriteMessage(Convert.ToBase64String(EncryptStringToBytes_Aes(message, user.masterKey)));
                                            user.ClientSocket.Client.Send(pb.GetPacketBytes());
                                        }
                                    }
                                }
                                console?.Invoke(new Action(() =>
                                {
                                    console.Text += username + ": " + message + "\n";
                                }));
                                break;
                            case 5:
                                string sendingUser2= _packetReader.ReadMessage();
                                string message2 = _packetReader.ReadMessage();

                                if (sendingUser2 == "Everyone")
                                {
                                    foreach (var user in Form1._users)
                                    {
                                        if (user.username != username)
                                        {
                                            PacketBuilder pb = new PacketBuilder();
                                            pb.WriteOpCode(4);
                                            pb.WriteMessage(Convert.ToBase64String(EncryptStringToBytes_Aes(message2, user.masterKey)));
                                            user.ClientSocket.Client.Send(pb.GetPacketBytes());
                                        }
                                    }
                                }
                                else
                                {
                                    foreach (var user in Form1._users)
                                    {
                                        if (user.username == sendingUser2)
                                        {
                                            PacketBuilder pb = new PacketBuilder();
                                            pb.WriteOpCode(4);
                                            pb.WriteMessage(Convert.ToBase64String(EncryptStringToBytes_Aes(message2, user.masterKey)));
                                            user.ClientSocket.Client.Send(pb.GetPacketBytes());
                                        }
                                    }
                                }
                                console?.Invoke(new Action(() =>
                                {
                                    console.Text += username + ": " + message2 + "\n";
                                }));
                                break;

                        }
                    }
                    catch
                    {
                        Form1._users.ForEach(u =>
                        {
                            PacketBuilder pb = new PacketBuilder();
                            pb.WriteOpCode(3);
                            pb.WriteMessage(Convert.ToBase64String(EncryptStringToBytes_Aes(username, u.masterKey)));
                            u.ClientSocket.Client.Send(pb.GetPacketBytes());
                        });
                        console?.Invoke(new Action(() =>
                        {
                            console.Text += "Client disconnected\n";
                        }));
                        return;
                    }
                }
            });
        }

        public void sendServerMessage(string message)
        {
            PacketBuilder pb = new PacketBuilder();
            pb.WriteOpCode(4);
            pb.WriteMessage(Convert.ToBase64String(EncryptStringToBytes_Aes("SERVER: " + message, masterKey)));
            this.ClientSocket.Client.Send(pb.GetPacketBytes());
        }

        #region encryption and decryption
        public static byte[] EncryptStringToBytes_Aes(string plainText, byte[] key)
        {
            byte[] encrypted;

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key;
                aesAlg.Mode = CipherMode.CBC; // Cipher Block Chaining
                aesAlg.Padding = PaddingMode.PKCS7; // PKCS7 padding

                // IV (Initialization Vector) oluştur
                aesAlg.GenerateIV();

                // IV'yi şifrelenmiş metnin başına ekleyerek kaydet
                byte[] iv = aesAlg.IV;

                using (ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV))
                {
                    using (MemoryStream msEncrypt = new MemoryStream())
                    {
                        using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                            {
                                // Metni şifrele
                                swEncrypt.Write(plainText);
                            }
                            encrypted = msEncrypt.ToArray();
                        }
                    }
                }

                // IV'yi şifrelenmiş metnin başına ekleyerek kaydet
                byte[] result = new byte[iv.Length + encrypted.Length];
                Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
                Buffer.BlockCopy(encrypted, 0, result, iv.Length, encrypted.Length);
                return result;
            }
        }

        public static string DecryptStringFromBytes_Aes(byte[] cipherText, byte[] key)
        {
            // İlk 16 byte IV, geri kalanı şifrelenmiş metin
            byte[] iv = new byte[16];
            byte[] cipher = new byte[cipherText.Length - 16];
            Buffer.BlockCopy(cipherText, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(cipherText, iv.Length, cipher, 0, cipher.Length);

            string plaintext = null;

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key;
                aesAlg.IV = iv;
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;

                using (ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV))
                {
                    using (MemoryStream msDecrypt = new MemoryStream(cipher))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                            {
                                // Şifreli metni çöz
                                plaintext = srDecrypt.ReadToEnd();
                            }
                        }
                    }
                }
            }

            return plaintext;
        }
        #endregion
    }
}

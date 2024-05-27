using SProjectServer.database;
using SProjectServer.model;
using SProjectServer.NET.IO;
using SProjectServer.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SProjectServer
{
    public class Client
    {
        public TcpClient ClientSocket { get; set; }
        PacketReader _packetReader;
        private RichTextBox console;

        private string uid;
        private string username;
        private string email;
        private string password;
        private DatabaseHandler db;

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

        public Client(TcpClient client, RichTextBox console, DatabaseHandler db)
        {
            this.db = db;
            this.console = console;
            ClientSocket = client;
            _packetReader = new PacketReader(ClientSocket.GetStream());
            process();

            Random random = new Random();
            serverPrivateKey = random.Next(100000, 999999);

            RandomPrimeGenerator rpg = new RandomPrimeGenerator();

            p = rpg.GenerateRandomPrime();
            g = rpg.GenerateRandomPrime();

            serverPublicKey = ModPow(p, serverPrivateKey, g);

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
                                    console.Text += "P: " + p + "\n";
                                    console.Text += "G: " + g + "\n";
                                    console.Text += "Server public key: " + serverPublicKey + "\n";
                                    console.Text += "Client public key: " + clientPublicKey + "\n";
                                    console.Text += "Server private key: " + serverPrivateKey + "\n";
                                    console.Text += "Sharedkey: "+ sharedKey+" \n";
                                }));
                                break;
                            case 1:
                                email = _packetReader.ReadEncryptedMessage(masterKey);
                                password = _packetReader.ReadEncryptedMessage(masterKey);
                                if(!Regex.IsMatch(email, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
                                {
                                    PacketBuilder loginPacket = new PacketBuilder();
                                    loginPacket.WriteOpCode(1);
                                    loginPacket.WriteEncryptedMessage("false", masterKey);
                                    loginPacket.WriteEncryptedMessage("email format wrong!", masterKey);
                                    this.ClientSocket.Client.Send(loginPacket.GetPacketBytes());
                                    console.Invoke(new Action(() =>
                                    {
                                        console.Text += email + " Login failed\n";
                                    }));
                                    break;
                                }
                                if (db.CheckLoginUser(email, password))
                                {
                                    uid = db.GetUID(email);
                                    username = db.GetName(uid);
                                    PacketBuilder loginPacket = new PacketBuilder();
                                    loginPacket.WriteOpCode(1);
                                    loginPacket.WriteEncryptedMessage("true", masterKey);
                                    loginPacket.WriteEncryptedMessage(uid, masterKey);
                                    loginPacket.WriteEncryptedMessage(username, masterKey);
                                    this.ClientSocket.Client.Send(loginPacket.GetPacketBytes());

                                    List<MessageModel> messages = db.getMessages(uid);
                                    if (messages != null && messages.Any())
                                    {
                                        foreach (MessageModel oldMessage in messages)
                                        {
                                            if (oldMessage.ReceiverID == "Everyone")
                                            {
                                                PacketBuilder pb = new PacketBuilder();
                                                pb.WriteOpCode(7);
                                                pb.WriteEncryptedMessage(db.GetName(oldMessage.SenderID), masterKey);
                                                pb.WriteEncryptedMessage("Everyone", masterKey);
                                                pb.WriteEncryptedMessage(oldMessage.MessageText, masterKey);
                                                this.ClientSocket.Client.Send(pb.GetPacketBytes());
                                            }
                                            else
                                            {
                                                PacketBuilder pb = new PacketBuilder();
                                                pb.WriteOpCode(7);
                                                pb.WriteEncryptedMessage(db.GetName(oldMessage.SenderID), masterKey);
                                                pb.WriteEncryptedMessage(db.GetName(oldMessage.ReceiverID), masterKey);
                                                pb.WriteEncryptedMessage(oldMessage.MessageText, masterKey);
                                                this.ClientSocket.Client.Send(pb.GetPacketBytes());
                                            }

                                        }
                                    }


                                    Form1._users.ForEach(u =>
                                    {
                                        Form1._users.ForEach(u2 =>
                                        {
                                            if (u2.username != u.username)
                                            {
                                                if (u2.username != null)
                                                {
                                                    PacketBuilder pb = new PacketBuilder();
                                                    pb.WriteOpCode(3);
                                                    pb.WriteEncryptedMessage(u2.username, u.masterKey);
                                                    pb.WriteEncryptedMessage(u2.uid, u.masterKey);
                                                    u.ClientSocket.Client.Send(pb.GetPacketBytes());
                                                }
                                            }
                                        });
                                    });

                                    console?.Invoke(new Action(() =>
                                    {
                                        console.Text += username + " connected\n";
                                    }));
                                }
                                else
                                {
                                    PacketBuilder loginPacket = new PacketBuilder();
                                    loginPacket.WriteOpCode(1);
                                    loginPacket.WriteEncryptedMessage("false", masterKey);
                                    loginPacket.WriteEncryptedMessage("email or password wrong!", masterKey);
                                    this.ClientSocket.Client.Send(loginPacket.GetPacketBytes());
                                    console?.Invoke(new Action(() =>
                                    {
                                        console.Text += email +" Login failed\n";
                                    }));
                                }
                                break;
                            case 2:
                                username = _packetReader.ReadEncryptedMessage(masterKey);
                                email = _packetReader.ReadEncryptedMessage(masterKey);
                                password = _packetReader.ReadEncryptedMessage(masterKey);
                                if(!Regex.IsMatch(username, @"^[a-zA-Z0-9_]{3,16}$"))
                                {
                                    PacketBuilder registerPacket = new PacketBuilder();
                                    registerPacket.WriteOpCode(2);
                                    registerPacket.WriteEncryptedMessage("false", masterKey);
                                    registerPacket.WriteEncryptedMessage("username format wrong!", masterKey);
                                    this.ClientSocket.Client.Send(registerPacket.GetPacketBytes());
                                    console?.Invoke(new Action(() =>
                                    {
                                        console.Text += username + " Register failed\n";
                                    }));
                                    break;
                                }
                                if(!Regex.IsMatch(email, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
                                {
                                    PacketBuilder registerPacket = new PacketBuilder();
                                    registerPacket.WriteOpCode(2);
                                    registerPacket.WriteEncryptedMessage("false", masterKey);
                                    registerPacket.WriteEncryptedMessage("email format wrong!", masterKey);
                                    this.ClientSocket.Client.Send(registerPacket.GetPacketBytes());
                                    console?.Invoke(new Action(() =>
                                    {
                                        console.Text += username + " Register failed\n";
                                    }));
                                    break;
                                }
                                if (!db.CheckRegisterUser(username, email))
                                {
                                        
                                    uid = Guid.NewGuid().ToString();
                                    db.InsertUser(username, uid, email, password);
                                    PacketBuilder registerPacket = new PacketBuilder();
                                    registerPacket.WriteOpCode(2);
                                    registerPacket.WriteEncryptedMessage("true", masterKey);
                                    registerPacket.WriteEncryptedMessage(uid, masterKey);
                                    this.ClientSocket.Client.Send(registerPacket.GetPacketBytes());
                                    Form1._users.ForEach(u =>
                                    {
                                        Form1._users.ForEach(u2 =>
                                        {
                                            if (u2.username != u.username)
                                            {
                                                if(u2.username != null)
                                                {
                                                    PacketBuilder pb = new PacketBuilder();
                                                    pb.WriteOpCode(3);
                                                    pb.WriteEncryptedMessage(u2.username, u.masterKey);
                                                    pb.WriteEncryptedMessage(u2.uid, u.masterKey);
                                                    u.ClientSocket.Client.Send(pb.GetPacketBytes());
                                                }
                                            }
                                        });
                                    });


                                    List<MessageModel> messages = db.getMessages(uid);
                                    if (messages != null && messages.Any())
                                    {
                                        foreach (MessageModel oldMessage in messages)
                                        {
                                            if (oldMessage.ReceiverID == "Everyone")
                                            {
                                                PacketBuilder pb = new PacketBuilder();
                                                pb.WriteOpCode(7);
                                                pb.WriteEncryptedMessage(db.GetName(oldMessage.SenderID), masterKey);
                                                pb.WriteEncryptedMessage("Everyone", masterKey);
                                                pb.WriteEncryptedMessage(oldMessage.MessageText, masterKey);
                                                this.ClientSocket.Client.Send(pb.GetPacketBytes());
                                            }
                                            else
                                            {
                                                PacketBuilder pb = new PacketBuilder();
                                                pb.WriteOpCode(7);
                                                pb.WriteEncryptedMessage(db.GetName(oldMessage.SenderID), masterKey);
                                                pb.WriteEncryptedMessage(db.GetName(oldMessage.ReceiverID), masterKey);
                                                pb.WriteEncryptedMessage(oldMessage.MessageText, masterKey);
                                                this.ClientSocket.Client.Send(pb.GetPacketBytes());
                                            }

                                        }
                                    }

                                    console?.Invoke(new Action(() =>
                                    {
                                        console.Text += username + " registered\n";
                                    }));
                                }
                                else
                                {
                                    PacketBuilder registerPacket = new PacketBuilder();
                                    registerPacket.WriteOpCode(2);
                                    registerPacket.WriteEncryptedMessage("false", masterKey);
                                    registerPacket.WriteEncryptedMessage("username or email already exists!", masterKey);
                                    this.ClientSocket.Client.Send(registerPacket.GetPacketBytes());
                                    console?.Invoke(new Action(() =>
                                    {
                                        console.Text += username + " Register failed\n";
                                    }));
                                }
                                break;
                            case 5:
                                string encryptedMessageSendingUser = _packetReader.ReadEncryptedMessage(masterKey);
                                string encryptedMessage = _packetReader.ReadEncryptedMessage(masterKey);
                                string encryptedMessageUID = Guid.NewGuid().ToString();
                                if (encryptedMessageSendingUser == "Everyone")
                                {
                                    db.InsertMessage(encryptedMessageUID, uid, "Everyone", encryptedMessage);
                                    foreach (var user in Form1._users)
                                    {
                                        if (user.username != username)
                                        {
                                            if(user.username != null)
                                            {
                                                PacketBuilder pb = new PacketBuilder();
                                                pb.WriteOpCode(5);
                                                pb.WriteEncryptedMessage(this.username, user.masterKey);
                                                pb.WriteEncryptedMessage(encryptedMessage, user.masterKey);
                                                user.ClientSocket.Client.Send(pb.GetPacketBytes());
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    foreach (var user in Form1._users)
                                    {
                                        if (user.username == encryptedMessageSendingUser)
                                        {
                                            db.InsertMessage(encryptedMessageUID, uid, user.uid, encryptedMessage);
                                            PacketBuilder pb = new PacketBuilder();
                                            pb.WriteOpCode(5);
                                            pb.WriteEncryptedMessage(this.username, user.masterKey);
                                            pb.WriteEncryptedMessage(encryptedMessage, user.masterKey);
                                            user.ClientSocket.Client.Send(pb.GetPacketBytes());
                                        }
                                    }
                                }
                                console?.Invoke(new Action(() =>
                                {
                                    console.Text += this.username + "->" + encryptedMessageSendingUser + ": " + encryptedMessage + "\n";
                                }));
                                break;
                            case 6:
                                string messageSendingUser = _packetReader.ReadMessage();
                                string message = _packetReader.ReadMessage();
                                string messageUID = Guid.NewGuid().ToString();
                                if (messageSendingUser == "Everyone")
                                {
                                    db.InsertMessage(messageUID, uid, "Everyone", message);
                                    foreach (var user in Form1._users)
                                    {
                                        if (user.username != username)
                                        {
                                            if (user.username != null)
                                            {
                                                PacketBuilder pb = new PacketBuilder();
                                                pb.WriteOpCode(6);
                                                pb.WriteMessage(this.username);
                                                pb.WriteMessage(message);
                                                user.ClientSocket.Client.Send(pb.GetPacketBytes());
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    foreach (var user in Form1._users)
                                    {
                                        if (user.username == messageSendingUser)
                                        {
                                            db.InsertMessage(messageUID, db.GetUID(this.email), user.uid, message);
                                            PacketBuilder pb = new PacketBuilder();
                                            pb.WriteOpCode(6);
                                            pb.WriteMessage(this.username);
                                            pb.WriteMessage(message);
                                            user.ClientSocket.Client.Send(pb.GetPacketBytes());
                                        }
                                    }
                                }
                                console?.Invoke(new Action(() =>
                                {
                                    console.Text += this.username + "->" + messageSendingUser + ": " + message + "\n";
                                }));
                                break;
                            default:
                                console?.Invoke(new Action(() =>
                                {
                                    console.Text += "Unknown opcode received\n";
                                }));
                                break;
                        }
                    }
                    catch
                    {
                        console?.Invoke(new Action(() =>
                        {
                            console.Text += username + " disconnected\n";
                        }));
                        try
                        {
                            Form1._users.ForEach(u =>
                            {
                                PacketBuilder pb = new PacketBuilder();
                                pb.WriteOpCode(4);
                                pb.WriteEncryptedMessage(username, u.masterKey);
                                pb.WriteEncryptedMessage(uid, u.masterKey);
                                u.ClientSocket.Client.Send(pb.GetPacketBytes());
                            });
                        }
                        catch
                        {

                        }
                        Form1._users.Remove(this);
                        ClientSocket.Close();
                        return;
                    }
                }
            });
        }

        public void sendServerMessage(string message)
        {
            PacketBuilder pb = new PacketBuilder();
            pb.WriteOpCode(5);
            pb.WriteEncryptedMessage("SERVER: " + message, masterKey);
            this.ClientSocket.Client.Send(pb.GetPacketBytes());
        }
    }
}

using SProjectServer.NET.IO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace SProjectServer
{
    public partial class Form1 : Form
    {

        static TcpListener _listener;
        public static List<Client> _users;
        public Form1()
        {
            InitializeComponent();
            serverStartButton.Enabled = false;
            sendButton.Enabled = false;
        }

        private void sendButton_Click(object sender, EventArgs e)
        {
            if (messageBox.Text.Length > 0)
            {
                _users.ForEach(u =>
                {
                    u.sendServerMessage(messageBox.Text);
                });
                console.Invoke(new Action(() =>
                {
                    console.Text += "SERVER: " + messageBox.Text + "\n";
                }));
            }
            messageBox.Text = "";
        }
        private void serverStartButton_Click(object sender, EventArgs e)
        {
            serverStartButton.Enabled = false;
            sendButton.Enabled = true;
            portBox.Enabled = false;
            _users = new List<Client>();
            _listener = new TcpListener(IPAddress.Any, int.Parse(portBox.Text));
            _listener.Start();
            console.Invoke(new Action(() =>
            {
                console.Text += "Server started on port " + portBox.Text + "\n";
            }));
            Task.Run(() =>
            {
                while (true)
                {

                    TcpClient client = _listener.AcceptTcpClient();
                    Client c = new Client(client, console);
                }
            });
            
        }

        private void portBox_TextChanged(object sender, EventArgs e)
        {
            if (portBox.Text.Length > 0)
            {
                serverStartButton.Enabled = true;
            }
            else
            {
                serverStartButton.Enabled = false;
            }
        }

        private void messageBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void console_TextChanged(object sender, EventArgs e)
        {

        }
    }
}

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using SProjectServer.model;
using System.Windows.Forms;
using System.Security.Cryptography;

namespace SProjectServer.database
{
    public class DatabaseHandler
    {
        private readonly string connectionString;
        private RichTextBox console;
        public DatabaseHandler(RichTextBox console)
        {
            this.console = console;
            connectionString = "Server=127.0.0.1,3306;Database=naberchatappv2;Uid=JavaProject;Pwd=JavaProject_ICU123;";
        }

        protected MySqlConnection GetConnection()
        {
            return new MySqlConnection(connectionString);
        }

        public void createDatabase()
        {
            try
            {
                using (MySqlConnection conn = GetConnection())
                {
                    conn.Open();

                    string query = @"CREATE TABLE IF NOT EXISTS `messages` (
                                        `MessageID` varchar(50) NOT NULL,
                                        `SenderID` varchar(50) NOT NULL,
                                        `ReceiverID` varchar(50) NOT NULL,
                                        `Message` text DEFAULT NULL,
                                        `Time` timestamp NOT NULL DEFAULT current_timestamp()
                                    );

                                    CREATE TABLE IF NOT EXISTS `users` (
                                        `UID` varchar(50) NOT NULL,
                                        `Username` varchar(50) NOT NULL,
                                        `Email` varchar(100) NOT NULL,
                                        `Password` text NOT NULL,
                                        PRIMARY KEY (`UID`)
                                    );";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch
            {
                console.Invoke(new Action(() =>
                {
                    console.Text += "Tablo oluşturulurken hata oluştu: " + "\n";
                }));
            }
        }


        public void InsertMessage(string uid, string senderID, string receiverID, string message)
        {
            try
            {
                using (MySqlConnection conn = GetConnection())
                {
                    conn.Open();

                    string query = @"INSERT INTO Messages (MessageID, SenderID, ReceiverID, Message)
                                 VALUES (@UID, @SenderID, @ReceiverID, @Message)"
                    ;

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UID", uid);
                        cmd.Parameters.AddWithValue("@SenderID", senderID);
                        cmd.Parameters.AddWithValue("@ReceiverID", receiverID);
                        cmd.Parameters.AddWithValue("@Message", message);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                console.Invoke(new Action(() =>
                {
                    console.Text += "Mesaj eklenirken hata oluştu: " + ex.Message + "\n";
                }));
            }
        }

        public List<MessageModel> getMessages(string uid)
        {
            List<MessageModel> messages = new List<MessageModel>();
            try
            {
                using (MySqlConnection conn = GetConnection())
                {
                    conn.Open();

                    string query = @"SELECT MessageID, SenderID, ReceiverID, Message, Time 
                                    FROM Messages 
                                    WHERE (SenderID = @UID) OR (ReceiverID = @UID) OR (ReceiverID = 'Everyone')";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UID", uid);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                MessageModel msg = new MessageModel
                                {
                                    MessageID = reader.GetString("MessageID"),
                                    SenderID = reader.GetString("SenderID"),
                                    ReceiverID = reader.GetString("ReceiverID"),
                                    MessageText = reader.GetString("Message"),
                                    Time = reader.GetDateTime("Time")
                                };
                                messages.Add(msg);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                console.Invoke(new Action(() =>
                {
                    console.Text += "Mesajlar çekilirken hata oluştu: " + ex.Message + "\n";
                }));
            }
            return messages;
        }
        public string GetMail(string uid)
        {
            try
            {
                using (MySqlConnection conn = GetConnection())
                {
                    conn.Open();

                    string query = "SELECT Email FROM users WHERE UID=@UID";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@UID", MySqlDbType.VarChar).Value = uid;

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                reader.Read();
                                if (!reader.IsDBNull(0))
                                {
                                    string email = reader.GetString(0);
                                    return email;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                console.Invoke(new Action(() =>
                {
                    console.Text += "Mail çekilirken hata oluştu: " + ex.Message + "\n";
                }));
            }
            return null;
        }

        public string GetName(string uid)
        {
            try
            {
                using (MySqlConnection conn = GetConnection())
                {
                    conn.Open();

                    string query = "SELECT Username FROM users WHERE UID=@UID";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@UID", MySqlDbType.VarChar).Value = uid;

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                reader.Read();
                                if (!reader.IsDBNull(0))
                                {
                                    string name = reader.GetString(0);
                                    return name;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                console.Invoke(new Action(() =>
                {
                    console.Text += "İsim çekilirken hata oluştu: " + ex.Message + "\n";
                }));
            }

            return null;
        }

        public string GetUID(string email)
        {
            try
            {
                using (MySqlConnection conn = GetConnection())
                {
                    conn.Open();

                    string query = "SELECT UID FROM users WHERE Email=@Email";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@Email", MySqlDbType.VarChar).Value = email;

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return reader.GetString(0);
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                console.Invoke(new Action(() =>
                {
                    console.Text += "UID çekilirken hata oluştu: " + ex.Message + "\n";
                }));
                return null;
            }
        }


        public bool CheckLoginUser(string email, string password)
        {
            try
            {
                using (MySqlConnection conn = GetConnection())
                {
                    conn.Open();

                    string query = "SELECT * FROM users WHERE Email=@email AND Password=@password";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@email", MySqlDbType.VarChar).Value = email;
                        cmd.Parameters.Add("@password", MySqlDbType.VarChar).Value = password;

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                console.Invoke(new Action(() =>
                {
                    console.Text += "Giriş kontrolü yapılırken hata oluştu: " + ex.Message + "\n";
                }));
                return false;
            }
        }

        public bool CheckRegisterUser(string username, string email)
        {
            try
            {
                using (MySqlConnection conn = GetConnection())
                {
                    conn.Open();

                    string query = "SELECT * FROM users WHERE Email=@email OR Username=@username";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@email", MySqlDbType.VarChar).Value = email;
                        cmd.Parameters.Add("@username", MySqlDbType.VarChar).Value = username;

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                console.Invoke(new Action(() =>
                {
                    console.Text += "Kayıt kontrolü yapılırken hata oluştu: " + ex.Message + "\n";
                }));
                return false;
            }
        }

        public void InsertUser(string username, string uid, string email, string password)
        {
            try
            {
                using (MySqlConnection conn = GetConnection())
                {
                    conn.Open();

                    string query = "INSERT INTO users (Username, UID, Email, Password) VALUES (@username, @uid, @email, @password)";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@username", MySqlDbType.VarChar).Value = username;
                        cmd.Parameters.Add("@uid", MySqlDbType.VarChar).Value = uid;
                        cmd.Parameters.Add("@email", MySqlDbType.VarChar).Value = email;
                        cmd.Parameters.Add("@password", MySqlDbType.VarChar).Value = password;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                console.Invoke(new Action(() =>
                {
                    console.Text += "Kullanıcı eklenirken hata oluştu: " + ex.Message + "\n";
                }));
            }
        }
    }
}

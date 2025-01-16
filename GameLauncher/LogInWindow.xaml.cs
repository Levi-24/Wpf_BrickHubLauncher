﻿using System.Windows;
using System.IO;
using System.Text;
using MySql.Data.MySqlClient;
using Konscious.Security.Cryptography;
using System.Net.Mail;
namespace GameLauncher
{
    public partial class LoginWindow : Window
    {
        private const string DatabaseConnectionString = "server=localhost;uid=root;pwd=;database=launcher_test";
        private const string SettingsFile = "user.settings";

        public LoginWindow()
        {
            InitializeComponent();
            LoadUserSettings();
        }

        #region General
        private void LoadUserSettings()
        {
            if (File.Exists(SettingsFile))
            {
                try
                {
                    using StreamReader reader = new StreamReader(SettingsFile);
                    string fullSettings = reader.ReadToEnd();
                    string[] pieces = fullSettings.Split(';');

                    string savedUsername = pieces[0];
                    string savedHash = pieces[1];
                    string savedSalt = pieces[2];

                    (string storedHash, string storedSalt) = GetStoredPasswordHashAndSalt(DatabaseConnectionString, savedUsername);
                    if (storedHash == savedHash && storedSalt == savedSalt)
                    {
                        MainWindow mainWindow = new MainWindow(savedUsername);
                        mainWindow.Show();
                        this.Close();
                    }
                    chkRemember.IsChecked = true;
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        private void ToRegister_Click(object sender, RoutedEventArgs e)
        {
            regBtn.IsEnabled = true;
            logBtn.IsEnabled = false;
            chkRemember.Visibility = Visibility.Hidden;
            emailTxb.Visibility = Visibility.Visible;
            emailTxt.Visibility = Visibility.Visible;
        }
        #endregion

        #region LogIn
        private void Login_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text;
            string enteredPassword = txtPassword.Password;

            (string storedHash, string storedSalt) = GetStoredPasswordHashAndSalt(DatabaseConnectionString, username);

            bool isPasswordValid = VerifyPassword(enteredPassword, storedHash, storedSalt);

            if (isPasswordValid)
            {
                if (chkRemember.IsChecked == true)
                {
                    using StreamWriter writer = new StreamWriter(SettingsFile);
                    writer.Write(username + ";");
                    writer.Write(storedHash + ";");
                    writer.Write(storedSalt + ";");
                }
                else if (File.Exists(SettingsFile))
                {
                    File.Delete(SettingsFile);
                }

                MainWindow mainWindow = new MainWindow(username);
                mainWindow.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show("Hiba a bejelentkezés során!", "Hiba!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        static (string storedHash, string storedSalt) GetStoredPasswordHashAndSalt(string connectionString, string username)
        {
            string storedHash = string.Empty;
            string storedSalt = string.Empty;

            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();

                string query = "SELECT password_hash, salt FROM users WHERE username = @username";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@username", username);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            storedHash = reader["password_hash"].ToString();
                            storedSalt = reader["salt"].ToString();
                        }
                    }
                }
            }

            return (storedHash, storedSalt);
        }

        static bool VerifyPassword(string enteredPassword, string storedHash, string storedSalt)
        {
            try
            {
                byte[] salt = Convert.FromBase64String(storedSalt);

                // Hash the entered password using the same salt and Argon2id
                using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(enteredPassword)))
                {
                    argon2.Iterations = 4;
                    argon2.MemorySize = 65536;
                    argon2.DegreeOfParallelism = 8;
                    argon2.Salt = salt;

                    // Generate the hash for the entered password
                    byte[] enteredHashBytes = argon2.GetBytes(32);

                    // Compare the generated hash with the stored hash
                    string enteredHashBase64 = Convert.ToBase64String(enteredHashBytes);
                    return enteredHashBase64 == storedHash;
                }
            }
            catch (Exception)
            {
                return false;
            }

        }
        #endregion

        #region Register
        private void Register_Click(object sender, RoutedEventArgs e)
        {
            regBtn.IsEnabled = false;
            logBtn.IsEnabled = true;
            chkRemember.Visibility = Visibility.Visible;
            emailTxb.Visibility = Visibility.Hidden;
            emailTxt.Visibility = Visibility.Hidden;

            string password = txtPassword.Password.ToString();

            if (!string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(txtUsername.ToString()) && !string.IsNullOrEmpty(emailTxb.ToString()))
            {
                if (EmailValidator(emailTxb.ToString()))
                {
                    (string hashedPassword, string salt) = HashPassword(password);
                    RegisterUserInDatabase(DatabaseConnectionString, txtUsername.Text, emailTxb.Text, hashedPassword, salt);
                }
                else
                {
                    MessageBox.Show("Az email cím formátuma nem megfelelő!", "Hiba!", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("A felhasználónév és a jelszó nem lehet üres!", "Hiba!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public bool EmailValidator(string emailaddress)
        {
            try
            {
                MailAddress m = new MailAddress(emailaddress);

                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        static (string hashedPassword, string salt) HashPassword(string password)
        {
            using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password)))
            {
                // Set Argon2 parameters (tune these values based on your requirements)
                argon2.Iterations = 4; // Number of iterations
                argon2.MemorySize = 65536; // Memory cost (64 MB)
                argon2.DegreeOfParallelism = 8; // Degree of parallelism (how many CPU cores)
                argon2.Salt = GenerateRandomSalt(); // Salt generation

                // Generate the hash
                byte[] hashBytes = argon2.GetBytes(32); // 32-byte hash

                // Convert hash and salt to Base64 strings
                string hashedPassword = Convert.ToBase64String(hashBytes);
                string salt = Convert.ToBase64String(argon2.Salt);

                return (hashedPassword, salt);
            }
        }

        static byte[] GenerateRandomSalt()
        {
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                byte[] salt = new byte[16]; // 16-byte salt
                rng.GetBytes(salt);
                return salt;
            }
        }

        static void RegisterUserInDatabase(string connectionString, string username, string email, string passwordHash, string salt)
        {
            List<string> usernames = new List<string>();

            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();

                string query = "SELECT username FROM users WHERE username = @username";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@username", username);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            usernames.Add(reader["username"].ToString());
                        }
                    }
                }
            }

            if (usernames.Contains(username))
            {
                MessageBox.Show("A felhasználónév már foglalt!", "Hiba!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "INSERT INTO users (username, email, password_hash, salt) VALUES (@username, @email, @password_hash, @salt)";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.Parameters.AddWithValue("@email", email);
                        cmd.Parameters.AddWithValue("@password_hash", passwordHash);
                        cmd.Parameters.AddWithValue("@salt", salt);

                        cmd.ExecuteNonQuery(); // Execute the insert
                    }
                }
            }
        }
        #endregion
    }
}

using System.Windows;
using System.IO;
using System.Text;
using MySql.Data.MySqlClient;
using Konscious.Security.Cryptography;
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

        //Already registered check
        //LogIn checks
        //Register Checks
        //Better Register

        private void LoadUserSettings()
        {
            if (File.Exists(SettingsFile))
            {
                string savedUsername = File.ReadAllText(SettingsFile);
                txtUsername.Text = savedUsername;
                chkRemember.IsChecked = true;
            }
        }

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
                    File.WriteAllText(SettingsFile, username);
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
            string storedHash = null;
            string storedSalt = null;

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
        #endregion

        #region Register
        private void Register_Click(object sender, RoutedEventArgs e)
        {
            string password = txtPassword.Password.ToString();
            (string hashedPassword, string salt) = HashPassword(password);
            RegisterUserInDatabase(DatabaseConnectionString, txtUsername.Text, hashedPassword, salt);
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

        static void RegisterUserInDatabase(string connectionString, string username, string passwordHash, string salt)
        {
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();

                string query = "INSERT INTO users (username, email, password_hash, salt) VALUES (@username, @email, @password_hash, @salt)";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@username", username);
                    cmd.Parameters.AddWithValue("@email", "fasztorta@gmail.com");
                    cmd.Parameters.AddWithValue("@password_hash", passwordHash);
                    cmd.Parameters.AddWithValue("@salt", salt);

                    cmd.ExecuteNonQuery(); // Execute the insert
                }
            }
        }
        #endregion
    }
}

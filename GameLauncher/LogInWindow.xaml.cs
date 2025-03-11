using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using MySql.Data.MySqlClient;
using System.Net.Mail;
using System.Windows;
using System.Text;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace GameLauncher
{
    public partial class LoginWindow : Window
    {

        private const string DBConnectionString = AppSettings.DatabaseConnectionString;
        private readonly string RememberMeTokenFile = AppSettings.RememberMeToken;
        private bool showPassword = false;
        private bool isUpdating = false;
        private int currentId;

        public LoginWindow()
        {
            InitializeComponent();
            favIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/brickhubLogo.png"));
            checkIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/closed.png"));
            LoadUserSettings();
        }

        #region General
        private static int GetCurrentId(string email)
        {
            using (MySqlConnection conn = new(DBConnectionString))
            {
                conn.Open();

                string query = "SELECT id FROM users WHERE email = @email";
                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@email", email);
                using MySqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return (int)reader["id"];
                }
            }
            throw new InvalidOperationException("No user found with the specified email.");
        }

        private void LoadUserSettings()
        {
            if (File.Exists(RememberMeTokenFile))
            {
                try
                {
                    var temp = File.ReadAllText(RememberMeTokenFile).Split('@');
                    currentId = int.Parse(temp[0]);
                    string token = temp[1];
                    bool isExpired;
                    bool isDevice;

                    using MySqlConnection conn = new(DBConnectionString);
                    conn.Open();

                    string query = "SELECT device, token, expiry_date FROM tokens WHERE user_id = @user_id";
                    using MySqlCommand cmd = new(query, conn);
                    cmd.Parameters.AddWithValue("@user_id", currentId);
                    using MySqlDataReader reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        isExpired = DateTime.TryParse(reader["expiry_date"].ToString(), out DateTime expiryDate) && expiryDate < DateTime.Now;
                        isDevice = bool.TryParse(reader["device"].ToString(), out bool device);

                        if (isDevice && !isExpired && reader["token"].ToString() == token )
                        {
                            MainWindow mainWindow = new(currentId);
                            mainWindow.Show();
                            Close();
                        }
                    }
                }
                catch (Exception)
                {
                    MessageBox.Show("Error during the loading of the user settings!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                    throw;
                }
            }
        }

        private void ToRegister_Click(object sender, RoutedEventArgs e)
        {
            RegisterButton.Visibility = RegisterButton.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            LogInButton.Visibility = LogInButton.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            showPassword = false;
            txtPassword.Password = string.Empty;

            if (RegisterButton.IsVisible)
            {
                Header.Text = "Register";
                ToRegisterButton.Content = "To Log In";
                txtPasswordAgain.Visibility = Visibility.Visible;
                lblPasswordAgain.Visibility = Visibility.Visible;
                RememberMeCheckbox.Visibility = Visibility.Hidden;
                lblName.Visibility = Visibility.Visible;
                txtName.Visibility = Visibility.Visible;
            }
            else
            {
                Header.Text = "Log In";
                ToRegisterButton.Content = "To Register";
                txtPasswordAgain.Visibility = Visibility.Hidden;
                lblPasswordAgain.Visibility = Visibility.Hidden;
                RememberMeCheckbox.Visibility = Visibility.Visible;
                lblName.Visibility = Visibility.Hidden;
                txtName.Visibility = Visibility.Hidden;
            }

        }

        private void TxtPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!isUpdating)
            {
                isUpdating = true;
                txtShowPassword.Text = txtPassword.Password;
                isUpdating = false;
            }
        }

        private void TxtShowPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!isUpdating)
            {
                isUpdating = true;
                txtPassword.Password = txtShowPassword.Text;
                isUpdating = false;
            }
        }

        private void ShowPasswordIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            showPassword = !showPassword;
            txtShowPassword.Visibility = txtShowPassword.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            txtPassword.Visibility = txtPassword.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

            if (txtShowPassword.Visibility == Visibility.Visible)
                txtShowPassword.Text = txtPassword.Password;

            if (showPassword)
                checkIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/open.png"));
            else
                checkIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/closed.png"));
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
        #endregion

        #region LogIn
        private void Login_Click(object sender, RoutedEventArgs e)
        {
            string email = txtEmail.Text;
            string enteredPassword = txtPassword.Password;

            try
            {
                currentId = GetCurrentId(email);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Login Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtEmail.Clear();
                txtPassword.Clear();
                txtEmail.Focus();
                return;
            }

            (string storedHash, string storedSalt) = GetStoredPasswordHashAndSalt(email);
            bool isPasswordValid = VerifyPassword(enteredPassword, storedHash, storedSalt);

            if (isPasswordValid)
            {
                if (RememberMeCheckbox.IsChecked == true)
                {
                   RememberMeToken();
                }

                MainWindow mainWindow = new(currentId);
                mainWindow.Show();
                Close();
            }
            else
            {
                MessageBox.Show("Invalid password!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static (string storedHash, string storedSalt) GetStoredPasswordHashAndSalt(string email)
        {
            string storedHash = string.Empty;
            string storedSalt = string.Empty;

            MySqlConnection conn = new(DBConnectionString);
            conn.Open();

            string query = "SELECT password_hash, salt FROM users WHERE email = @email";
            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@email", email);

            using MySqlDataReader reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                storedHash = reader["password_hash"].ToString();
                storedSalt = reader["salt"].ToString();
            }

            return (storedHash, storedSalt);
        }

        static bool VerifyPassword(string enteredPassword, string storedHash, string storedSalt)
        {
            try
            {
                byte[] salt = Convert.FromBase64String(storedSalt);

                // Hash the entered password using the same salt and Argon2id
                using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(enteredPassword));
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
            catch (Exception)
            {
                return false;
            }
        }

        private void RememberMeToken()
        {
            string token = Guid.NewGuid().ToString();
            DateTime expiryDate = DateTime.Now.AddDays(7);

            using MySqlConnection conn = new(DBConnectionString);
            conn.Open();

            string query = "INSERT INTO tokens (device, user_id, token, expiry_date) VALUES (@device, @user_id, @token, @expiry_date)";
            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@device", 1);
            cmd.Parameters.AddWithValue("@user_id", currentId);
            cmd.Parameters.AddWithValue("@token", token);
            cmd.Parameters.AddWithValue("@expiry_date", expiryDate);

            cmd.ExecuteNonQuery();

            File.WriteAllText(RememberMeTokenFile, currentId + "@" + token);
        }
        #endregion

        #region Register
        private void Register_Click(object sender, RoutedEventArgs e)
        {
            string name = txtName.Text;
            string email = txtEmail.Text;
            string password = txtPassword.Password.ToString();
            string passwordAgain = txtPasswordAgain.Password.ToString();

            if (!string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(email))
            {
                if (password == passwordAgain)
                {
                    if (EmailValidator(email))
                    {
                        (string hashedPassword, string salt) = HashPassword(password);
                        RegisterUserInDatabase(DBConnectionString, name, email, hashedPassword, salt);

                        ToRegister_Click(sender, e);
                        txtPassword.Password = string.Empty;
                        txtPasswordAgain.Password = string.Empty;
                        txtName.Text = string.Empty;
                    }
                    else
                    {
                        MessageBox.Show("The E-mail is not in a correct format!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("The passwords do not match!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("The name, password and E-mail fields can not be empty!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static bool EmailValidator(string email)
        {
            try
            {
                MailAddress m = new(email);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        static (string hashedPassword, string salt) HashPassword(string password)
        {
            using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password));
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

        static byte[] GenerateRandomSalt()
        {
            byte[] salt = new byte[16];
            RandomNumberGenerator.Fill(salt);
            return salt;
        }

        static void RegisterUserInDatabase(string connectionString, string name, string email, string passwordHash, string salt)
        {
            List<string> emails = [];

            using (MySqlConnection conn = new(connectionString))
            {
                conn.Open();

                string query = "SELECT email FROM users WHERE email = @email";
                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@email", email);
                using MySqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    emails.Add(reader["email"].ToString());
                }
            }

            if (emails.Contains(email))
            {
                MessageBox.Show("This E-mail is already taken!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                using MySqlConnection conn = new(connectionString);
                conn.Open();

                string query = "INSERT INTO users (name, email, password_hash, salt) VALUES (@name, @email, @password_hash, @salt)";
                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@email", email);
                cmd.Parameters.AddWithValue("@password_hash", passwordHash);
                cmd.Parameters.AddWithValue("@salt", salt);

                cmd.ExecuteNonQuery();
            }
        }
        #endregion
    }
}
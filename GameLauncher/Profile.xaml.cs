using System.Data;
using System.IO;
using System.Windows;
using MySql.Data.MySqlClient;

namespace GameLauncher
{
    public partial class Profile : Window
    {
        private const string ConnectionString = "Server=localhost;Database=game_launcher;Uid=root;Pwd=;";
        private readonly int userId;
        private const string SettingsFile = "user.settings";

        public Profile()
        {
            InitializeComponent();
            userId = GetUserId();
            LoadData();
        }

        public async void LoadData()
        {
            try
            {
                var query = @"SELECT * FROM users;";
                using var conn = new MySqlConnection(ConnectionString);
                await conn.OpenAsync();

                using var cmd = new MySqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var mySqlReader = reader as MySqlDataReader;
                    if (mySqlReader != null)
                    {
                        string name = reader.GetString("name");
                        string userName = reader.GetString("username");
                        string email = reader.GetString("email");
                        DateTime registerDate = reader.GetDateTime("created_at");

                        lblName.Content = name;
                        lblUsername.Content = userName;
                        lblEmail.Content = email;
                        lblRegisterDate.Content = registerDate;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database error: {ex.Message}");
            }

        }

        private int GetUserId()
        {
            using (MySqlConnection connection = new MySqlConnection(ConnectionString))
            {
                connection.Open();
                using StreamReader reader = new StreamReader(SettingsFile);
                string fullSettings = reader.ReadToEnd();
                string[] pieces = fullSettings.Split(';');

                string savedUsername = pieces[0];
                string query = $"SELECT id FROM users WHERE username = '{savedUsername}';";

                using (MySqlCommand cmd = new MySqlCommand(query, connection))
                {
                    return (int)cmd.ExecuteScalar();
                }
            }
            //else
            //{
            //    MessageBox.Show("User not found. Please log in again.");
            //    LoginWindow loginWindow = new LoginWindow();
            //    loginWindow.Show();
            //    Close();
            //    return 0;
            //}
        }
    }
}

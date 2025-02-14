using System.Data;
using System.Windows;
using MySql.Data.MySqlClient;

namespace GameLauncher
{
    public partial class Profile : Window
    {
        private const string ConnectionString = "Server=localhost;Database=game_launcher;Uid=root;Pwd=;";

        public Profile(int userId)
        {
            InitializeComponent();
            LoadData(userId);
            LoadCombinedPlayTime(userId);
        }

        public async void LoadData(int userId)
        {
            try
            {
                var query = @$"SELECT * FROM users WHERE id LIKE '{userId}';";
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

        public async void LoadCombinedPlayTime(int userId)
        {
            try
            {
                var query = @$"SELECT SUM(playtime_minutes) AS combined_minutes FROM playtime WHERE user_id LIKE '{userId}';";
                using var conn = new MySqlConnection(ConnectionString);
                await conn.OpenAsync();

                using var cmd = new MySqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var mySqlReader = reader as MySqlDataReader;
                    if (mySqlReader != null)
                    {
                        int combinedMinutes = reader.GetInt32("combined_minutes");
                        lblCombinedPlaytime.Content = $"{combinedMinutes / 60} hours {combinedMinutes % 60} minutes";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database error: {ex.Message}");
            }
        }
    }
}

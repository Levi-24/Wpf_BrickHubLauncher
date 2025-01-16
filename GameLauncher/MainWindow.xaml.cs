using MySql.Data.MySqlClient;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GameLauncher
{
    public partial class MainWindow : Window
    {
        private string username;
        private ObservableCollection<Game> Games { get; set; } = new ObservableCollection<Game>();
        private string ImageDirectory = System.IO.Path.Combine(Environment.CurrentDirectory, "DownloadedImages");

        public MainWindow(string username)
        {
            InitializeComponent();
            LoadGames();
            GamesList.ItemsSource = Games;
            TestName.Text = username;
        }

        private async void LoadGames()
        {
            string connectionString = "Server=localhost;Database=launcher_test;Uid=root;Pwd=;";
            string query = "SELECT * FROM games";

            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var game = new Game
                            {
                                Id = reader.GetInt32("id"),
                                Name = reader.GetString("name"),
                                Description = reader.GetString("description"),
                                ImageUrl = reader["image_path"] != DBNull.Value ? reader.GetString("image_path") : null,
                                ReleaseDate = reader.GetDateTime("release_date"),
                            };

                            game.LocalImagePath = await DownloadImageAsync(game.ImageUrl);

                            if (string.IsNullOrEmpty(game.LocalImagePath))
                            {
                                game.LocalImagePath = await DownloadImageAsync("https://as1.ftcdn.net/v2/jpg/04/34/72/82/1000_F_434728286_OWQQvAFoXZLdGHlObozsolNeuSxhpr84.jpg");
                            }

                            Games.Add(game);
                        }
                    }
                }
            }
        }

        private void GamesList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (GamesList.SelectedItem is Game selectedGame)
            {
                GameDescription.Text = selectedGame.Description;

                if (!string.IsNullOrEmpty(selectedGame.LocalImagePath) && File.Exists(selectedGame.LocalImagePath))
                {
                    GameImage.Source = new BitmapImage(new Uri(selectedGame.LocalImagePath));
                }
            }
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private async Task<string> DownloadImageAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            try
            {
                if (!Directory.Exists(ImageDirectory))
                {
                    Directory.CreateDirectory(ImageDirectory);
                }

                string fileName = System.IO.Path.GetFileName(new Uri(url).LocalPath);
                string localFilePath = System.IO.Path.Combine(ImageDirectory, fileName);

                if (File.Exists(localFilePath))
                {
                    return localFilePath;
                }

                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(localFilePath, imageBytes);

                    return localFilePath;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public class Game
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string ImageUrl { get; set; }
            public string LocalImagePath { get; set; }
            public DateTime ReleaseDate { get; set; }
        }

        private void LogOut_Click(object sender, RoutedEventArgs e)
        {
            const string SettingsFile = "user.settings";
            File.Delete(SettingsFile);

            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
    }
}
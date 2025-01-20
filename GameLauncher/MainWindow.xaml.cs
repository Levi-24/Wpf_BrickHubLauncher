using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using MySql.Data.MySqlClient;
using System.Net.Http;
using System.Windows;
using System.IO;

namespace GameLauncher
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<Game> Games = new ObservableCollection<Game>();
        private string ImageDirectory = Path.Combine(Environment.CurrentDirectory, "DownloadedImages");
        const string SettingsFile = "user.settings";

        public MainWindow()
        {
            InitializeComponent();
            LoadGames();
            DownloadImageAsync("https://png.pngtree.com/png-vector/20190820/ourmid/pngtree-no-image-vector-illustration-isolated-png-image_1694547.jpg");
            GamesList.ItemsSource = Games;
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
                            int id = reader.GetInt32("id");
                            string name = reader.GetString("name");
                            string description = reader.GetString("description");
                            string imageUrl = reader["image_path"] != DBNull.Value ? reader.GetString("image_path") : null;
                            DateTime releaseDate = reader.GetDateTime("release_date");
                            string localImagePath = await DownloadImageAsync(imageUrl);

                            Games.Add(new Game(id, name, description, imageUrl, localImagePath, releaseDate));
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

                if (File.Exists(selectedGame.LocalImagePath))
                {
                    GameImage.Source = new BitmapImage(new Uri(selectedGame.LocalImagePath));
                }
            }
        }

        private async Task<string> DownloadImageAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
                return Path.Combine(ImageDirectory, "pngtree-no-image-vector-illustration-isolated-png-image_1694547.jpg");

            try
            {
                if (!Directory.Exists(ImageDirectory))
                {
                    Directory.CreateDirectory(ImageDirectory);
                }

                string fileName = Path.GetFileName(new Uri(url).LocalPath);
                string localFilePath = Path.Combine(ImageDirectory, fileName);

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
                return Path.Combine(ImageDirectory, "pngtree-no-image-vector-illustration-isolated-png-image_1694547.jpg");
            }
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Azt hitted xddd :3 ");
        }

        private void LogOut_Click(object sender, RoutedEventArgs e)
        {
            File.Delete(SettingsFile);
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            string fileUrl = "https://getsamplefiles.com/download/zip/sample-3.zip";
            string tempPath = Path.Combine(Path.GetTempPath(), "sample.zip");

            string targetDirectory = @"../../../downloads";
            string targetPath = Path.Combine(targetDirectory, "sample.zip");

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            var progress = new Progress<double>(value =>
            {
                ProgressBar.Value = value;
            });

            try
            {
                await DownloadFileWithProgressAsync(fileUrl, tempPath, progress);

                if (File.Exists(tempPath))
                {
                    File.Move(tempPath, targetPath);
                    MessageBox.Show($"File successfully downloaded and moved to: {targetPath}");
                }
                else
                {
                    MessageBox.Show("Downloaded file not found in the temporary path.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }












        private async Task DownloadFileWithProgressAsync(string fileUrl, string destinationPath, IProgress<double> progress)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    using (var response = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                        var canReportProgress = totalBytes != -1 && progress != null;

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            var buffer = new byte[8192];
                            long totalRead = 0;
                            int bytesRead;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);

                                totalRead += bytesRead;

                                if (canReportProgress)
                                {
                                    double percentage = (double)totalRead / totalBytes * 100;
                                    progress.Report(percentage);
                                }
                            }
                        }
                    }

                    MessageBox.Show($"Download completed! File saved to: {destinationPath}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}");
                }
            }
        }
    }
}
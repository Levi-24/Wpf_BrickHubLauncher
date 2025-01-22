using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using MySql.Data.MySqlClient;

namespace GameLauncher
{
    public partial class MainWindow : Window
    {
        //I removed nulbale types from the properties
        //mention it in the documentation
        private ObservableCollection<Game> Games = new ObservableCollection<Game>();
        private string ImageDirectory = Path.Combine(Environment.CurrentDirectory, "DownloadedImages");
        private string GameDirectory = Path.Combine(Environment.CurrentDirectory, "DownloadedGames");
        private const string SettingsFile = "user.settings";
        private const string connectionString = "Server=localhost;Database=launcher_test;Uid=root;Pwd=;";

        public MainWindow()
        {
            InitializeComponent();
            LoadGames();
            GamesList.ItemsSource = Games;
        }

        private async void LoadGames()
        {
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
                            string imageUrl = reader["image_path"] != DBNull.Value ? reader.GetString("image_path") : "https://i.postimg.cc/mDvhPW7C/NoImage.jpg";
                            string downloadLink = reader["download_link"] != DBNull.Value ? reader.GetString("download_link") : null;
                            string localImagePath = await DownloadImageAsync(imageUrl);
                            DateTime releaseDate = reader.GetDateTime("release_date");

                            Games.Add(new Game(id, name, description, imageUrl, downloadLink, localImagePath, releaseDate));
                        }
                    }
                }
            }
        }

        private void GamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GamesList.SelectedItem is Game selectedGame)
            {
                //User Interface
                DownloadButton.IsEnabled = !string.IsNullOrEmpty(selectedGame.DownloadLink);
                DownloadButton.Visibility = Visibility.Visible;
                LaunchButton.Visibility = Visibility.Visible;
                ProgressBar.Visibility = Visibility.Visible;
                ProgressBar.Value = 0;
                GameDescription.Text = selectedGame.Description;
                GameImage.Source = new BitmapImage(new Uri(selectedGame.LocalImagePath));
            }
        }

        private async Task<string> DownloadImageAsync(string url)
        {
            try
            {
                if (!Directory.Exists(ImageDirectory))
                    Directory.CreateDirectory(ImageDirectory);

                string fileName = Path.GetFileName(new Uri(url).LocalPath);
                string localFilePath = Path.Combine(ImageDirectory, fileName);

                if (File.Exists(localFilePath))
                    return localFilePath;

                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(localFilePath, imageBytes);

                    return localFilePath;
                }
            }
            catch (Exception)
            {
                return Path.Combine(ImageDirectory, "NoImage.jpg");
            }
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            //if (Games[GamesList.SelectedIndex] != null && !string.IsNullOrEmpty(Games[GamesList.SelectedIndex].InstallPath))
            //{
            //    string executablePath = Path.Combine(Games[GamesList.SelectedIndex].InstallPath, /*Here comes the exe name (game.exe)*/);

            //    if (File.Exists(executablePath))
            //        System.Diagnostics.Process.Start(executablePath);
            //    else
            //        MessageBox.Show("Game exe not found!");
            //}
        }

        private void LogOutButton_Click(object sender, RoutedEventArgs e)
        {
            File.Delete(SettingsFile);
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            string fileUrl = Games[GamesList.SelectedIndex].DownloadLink;
            string tempPath = Path.Combine(Path.GetTempPath(), "sample.zip");
            string targetPath = Path.Combine(GameDirectory, Games[GamesList.SelectedIndex].Name + ".zip");
            string installPath = Path.Combine(GameDirectory, Games[GamesList.SelectedIndex].Name);

            //EZ A FILE DIRECTORY SELECT
            //string targetDirectory = "";

            //using (var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog())
            //{
            //    dialog.IsFolderPicker = true;
            //    if (dialog.ShowDialog() == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
            //    {
            //        targetDirectory = dialog.FileName;
            //    }
            //}

            if (!Directory.Exists(GameDirectory))
                Directory.CreateDirectory(GameDirectory);

            var progress = new Progress<double>(value => { ProgressBar.Value = value; });

            try
            {
                await DownloadFileWithProgressAsync(fileUrl, tempPath, progress);

                if (File.Exists(tempPath))
                {
                    File.Move(tempPath, targetPath);

                    ExtractZip(targetPath, installPath);

                    string originalFolderPath = installPath;
                    string parentDirectory = Directory.GetParent(originalFolderPath).FullName;

                    string[] subFolders = Directory.GetDirectories(originalFolderPath);

                    string subFolderPath = subFolders[0];

                    string newSubFolderPath = Path.Combine(parentDirectory, Path.GetFileName(subFolderPath));
                    Directory.Move(subFolderPath, newSubFolderPath);

                    Directory.Delete(originalFolderPath, true);
                    MessageBox.Show($"Download completed!");
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

        //Ezt meg kell nézni
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
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}");
                }
            }
        }

        public void ExtractZip(string zipFilePath, string installDirectory)
        {
            if (!Directory.Exists(installDirectory))
                Directory.CreateDirectory(installDirectory);

            ZipFile.ExtractToDirectory(zipFilePath, installDirectory);

            File.Delete(zipFilePath);
        }
    }
}
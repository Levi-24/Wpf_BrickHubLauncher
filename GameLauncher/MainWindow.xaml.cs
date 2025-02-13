using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MySql.Data.MySqlClient;

namespace GameLauncher
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<Game> Games = new();
        private List<GameInstallationInfo> Executables = new();
        private readonly string ImageDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DownloadedImages");
        private readonly string GameDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DownloadedGames");
        private static string InstallationFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installedGames.json");
        private const string SettingsFile = "user.settings";
        private const string ConnectionString = "Server=localhost;Database=game_launcher;Uid=root;Pwd=;";
        private readonly int userId;
        private DateTime gameStartTime;
        private int totalPlaytimeMinutes;

        public MainWindow()
        {
            InitializeComponent();
            DownloadImageAsync("https://i.postimg.cc/mDvhPW7C/NoImage.jpg");
            LoadGamesAsync();
            userId = GetUserId();
            Executables = LoadGameExecutables();
            GamesList.ItemsSource = Games;
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

        private void LogOutButton_Click(object sender, RoutedEventArgs e)
        {
            File.Delete(SettingsFile);
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            Close();
        }

        private async void LoadGamesAsync()
        {
            try
            {
                var query = @"SELECT g.*, 
                              dev.name AS developer_name, 
                              pub.name AS publisher_name
                            FROM games g
                              JOIN members dev ON g.developer_id = dev.id
                              JOIN members pub ON g.publisher_id = pub.id";
                using var conn = new MySqlConnection(ConnectionString);
                await conn.OpenAsync();

                using var cmd = new MySqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var mySqlReader = reader as MySqlDataReader;
                    if (mySqlReader != null)
                    {
                        var game = await ParseGameAsync(mySqlReader);
                        Games.Add(game);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading games: {ex.Message}");
            }
        }

        private async Task<Game> ParseGameAsync(MySqlDataReader reader)
        {
            int id = reader.GetInt32("id");
            string name = reader.GetString("name");
            string exeName = reader.GetString("exe_name");
            string description = reader.GetString("description");
            string imageUrl = reader["image_path"] != DBNull.Value ? reader.GetString("image_path") : "https://i.postimg.cc/mDvhPW7C/NoImage.jpg";
            string downloadLink = reader["download_link"] != DBNull.Value ? reader.GetString("download_link") : null;
            string localImagePath = await DownloadImageAsync(imageUrl);
            DateTime releaseDate = reader.GetDateTime("release_date");
            string developerName = reader.GetString("developer_name");
            string publisherName = reader.GetString("publisher_name");

            return new Game(id, name, exeName, description, imageUrl, downloadLink, localImagePath, releaseDate, developerName, publisherName);
        }

        private void GamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GamesList.SelectedItem is Game selectedGame)
            {
                DownloadButton.IsEnabled = !string.IsNullOrEmpty(selectedGame.DownloadLink);
                lblReleaseDate.Content = selectedGame.ReleaseDate.ToString("yyyy MMMM dd.");
                lblDev.Content = selectedGame.DeveloperName;
                lblPublisher.Content = selectedGame.PublisherName;
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
                EnsureDirectoryExists(ImageDirectory);

                string fileName = Path.GetFileName(new Uri(url).LocalPath);
                string localFilePath = Path.Combine(ImageDirectory, fileName);

                if (File.Exists(localFilePath))
                    return localFilePath;

                using HttpClient client = new();
                byte[] imageBytes = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(localFilePath, imageBytes);

                return localFilePath;
            }
            catch
            {
                return Path.Combine(ImageDirectory, "NoImage.jpg");
            }
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (GamesList.SelectedItem is Game selectedGame)
            {
                Executables = LoadGameExecutables();
                var gameInfo = Executables.FirstOrDefault(g => g.GameId == selectedGame.Id);

                if (gameInfo != null && File.Exists(gameInfo.ExecutablePath))
                {
                    try
                    {
                        Process gameProcess = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = gameInfo.ExecutablePath,
                                UseShellExecute = true
                            },
                            EnableRaisingEvents = true
                        };

                        gameStartTime = DateTime.Now;

                        gameProcess.Exited += (s, args) => OnGameExit(selectedGame.Name, selectedGame.Id);
                        gameProcess.Start();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error launching the game: {ex.Message}");
                    }
                }
                else
                {
                    MessageBox.Show("Executable not found for the selected game.");
                }
            }
        }

        private void OnGameExit(string gameName, int gameID)
        {
            try
            {
                TimeSpan playDuration = DateTime.Now - gameStartTime;
                int minutesPlayed = (int)playDuration.TotalMinutes;

                totalPlaytimeMinutes += minutesPlayed;

                DateTime lastPlayed = DateTime.Now;

                SavePlaytimeToDatabase(userId, gameID, minutesPlayed, lastPlayed);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error tracking playtime: {ex.Message}");
            }
        }

        private void SavePlaytimeToDatabase(int userId, int gameId, int minutesPlayed, DateTime lastPlayed)
        {

            try
            {
                using (MySqlConnection connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();

                    string query = @"
                        INSERT INTO playtime (user_id, game_id, playtime_minutes, last_played)
                        VALUES (@UserId, @GameId, @MinutesPlayed, @LastPlayed)
                        ON DUPLICATE KEY UPDATE 
                        playtime_minutes = playtime_minutes + VALUES(playtime_minutes),
                        last_played = VALUES(last_played);";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@GameId", gameId);
                        cmd.Parameters.AddWithValue("@MinutesPlayed", minutesPlayed);
                        cmd.Parameters.AddWithValue("@LastPlayed", lastPlayed);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database error: {ex.Message}");
            }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (GamesList.SelectedItem is not Game selectedGame) return;

            if (Executables.Where(x => x.GameId == selectedGame.Id).Count() != 0)
            {
                MessageBox.Show("This Game is already installed!");
                return;
            }

            EnsureDirectoryExists(GameDirectory);
            string targetPath = await SelectDownloadDirectoryAsync(selectedGame);
            if (targetPath == null) return;

            string installPath = targetPath.Remove(targetPath.Length - 4);

            try
            {
                await DownloadAndInstallGameAsync(selectedGame, targetPath, installPath);
                selectedGame.InstallPath = installPath;

                string executablePath = Path.Combine(installPath, selectedGame.ExeName);
                var gameInfo = new GameInstallationInfo
                {
                    GameId = selectedGame.Id,
                    ExeName = selectedGame.ExeName,
                    ExecutablePath = executablePath
                };

                var gameInstallations = LoadGameExecutables();
                gameInstallations.Add(gameInfo);
                SaveGameExecutables(gameInstallations);

                MessageBox.Show("Download completed!");
                Executables = LoadGameExecutables();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }

        private async Task<string> SelectDownloadDirectoryAsync(Game selectedGame)
        {
            using var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog
            {
                IsFolderPicker = true,
                InitialDirectory = GameDirectory
            };

            if (dialog.ShowDialog() == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
                return Path.Combine(dialog.FileName, $"{selectedGame.Name}.zip");

            return null;
        }

        private async Task DownloadAndInstallGameAsync(Game game, string targetPath, string installPath)
        {
            if (Directory.Exists(installPath))
            {
                MessageBox.Show("The file is already downloaded!");
                return;
            }

            var progress = new Progress<double>(value => ProgressBar.Value = value);
            await DownloadFileWithProgressAsync(game.DownloadLink, targetPath, progress);
            ExtractZip(targetPath, installPath);
        }

        private async Task DownloadFileWithProgressAsync(string fileUrl, string destinationPath, IProgress<double> progress)
        {
            using HttpClient client = new();
            using var response = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1L;
            bool canReportProgress = totalBytes != -1 && progress != null;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            byte[] buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalRead += bytesRead;

                if (canReportProgress)
                {
                    double percentage = (double)totalRead / totalBytes * 100;
                    progress.Report(percentage);
                }
            }
        }

        public void ExtractZip(string zipFilePath, string installDirectory)
        {
            EnsureDirectoryExists(installDirectory);
            ZipFile.ExtractToDirectory(zipFilePath, installDirectory);
            File.Delete(zipFilePath);
        }

        private static void EnsureDirectoryExists(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
        }

        private void SaveGameExecutables(List<GameInstallationInfo> games)
        {
            try
            {
                var json = JsonSerializer.Serialize(games);
                File.WriteAllText(InstallationFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving game data: {ex.Message}");
            }
        }

        private List<GameInstallationInfo> LoadGameExecutables()
        {
            try
            {
                if (File.Exists(InstallationFilePath))
                {
                    var json = File.ReadAllText(InstallationFilePath);
                    return JsonSerializer.Deserialize<List<GameInstallationInfo>>(json) ?? new List<GameInstallationInfo>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading game data: {ex.Message}");
            }
            return new List<GameInstallationInfo>();
        }

        private void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to uninstall this game?", "Confirm Uninstall", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                if (GamesList.SelectedItem is not Game selectedGame) return;

                var gameInstallations = LoadGameExecutables();

                var gameInfo = gameInstallations.FirstOrDefault(g => g.GameId == selectedGame.Id);

                if (gameInfo != null)
                {
                    try
                    {
                        gameInfo.ExecutablePath = gameInfo.ExecutablePath.Remove(gameInfo.ExecutablePath.Length - gameInfo.ExeName.Length);

                        if (Directory.Exists(gameInfo.ExecutablePath))
                        {
                            Directory.Delete(gameInfo.ExecutablePath, true);
                        }

                        gameInstallations.Remove(gameInfo);

                        SaveGameExecutables(gameInstallations);

                        MessageBox.Show("Game uninstalled successfully!");

                        Executables = LoadGameExecutables();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred while uninstalling the game: {ex.Message}");
                    }
                }
                else
                {
                    MessageBox.Show("The game is not found in the installed list.");
                }
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            Profile profileWindow = new Profile();
            profileWindow.Show();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (GamesList.SelectedItem is Game selectedGame)
            {
                Review review = new Review(selectedGame.Id);
                review.Show();
            }
            else
            {
                MessageBox.Show("No game selected!");
            }
        }
    }
}
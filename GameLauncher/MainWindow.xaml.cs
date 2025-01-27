using System.Collections.ObjectModel;
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
        private readonly List<GameInstallationInfo> Executables = new();
        private readonly string ImageDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DownloadedImages");
        private readonly string GameDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DownloadedGames");
        private static string InstallationFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installedGames.json");
        private const string SettingsFile = "user.settings";
        private const string ConnectionString = "Server=localhost;Database=launcher_test;Uid=root;Pwd=;";

        public MainWindow()
        {
            InitializeComponent();
            LoadGamesAsync();
            Executables = LoadGameExecutables();
            GamesList.ItemsSource = Games;
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
                var query = "SELECT * FROM games";
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
            string description = reader.GetString("description");
            string imageUrl = reader["image_path"] != DBNull.Value ? reader.GetString("image_path") : "https://i.postimg.cc/mDvhPW7C/NoImage.jpg";
            string downloadLink = reader["download_link"] != DBNull.Value ? reader.GetString("download_link") : null;
            string localImagePath = await DownloadImageAsync(imageUrl);
            DateTime releaseDate = reader.GetDateTime("release_date");

            return new Game(id, name, description, imageUrl, downloadLink, localImagePath, releaseDate);
        }

        private void GamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GamesList.SelectedItem is Game selectedGame)
            {
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

        //School version
        //private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        //{
        //    if (!Directory.Exists(GameDirectory))
        //        Directory.CreateDirectory(GameDirectory);

        //    Game selectedGame = Games[GamesList.SelectedIndex];
        //    string fileUrl = selectedGame.DownloadLink;
        //    string tempPath = Path.Combine(Path.GetTempPath(), selectedGame.Name + ".zip");
        //    string targetPath = Path.Combine(GameDirectory, selectedGame.Name + ".zip");

        //    //Directory select - add option to choose default or select
        //    using (var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog())
        //    {
        //        dialog.IsFolderPicker = true;
        //        dialog.InitialDirectory = GameDirectory;
        //        if (dialog.ShowDialog() == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
        //        {
        //            targetPath = Path.Combine(dialog.FileName, selectedGame.Name + ".zip");
        //        }
        //        else return;
        //    }

        //    string installPath = targetPath.Remove(targetPath.Length - 4);
        //    var progress = new Progress<double>(value => { ProgressBar.Value = value; });

        //    try
        //    {
        //        if (!Directory.Exists(tempPath))
        //        {
        //            await DownloadFileWithProgressAsync(fileUrl, tempPath, progress);
        //            File.Move(tempPath, targetPath);
        //            ExtractZip(targetPath, installPath);

        //            selectedGame.InstallPath = installPath;

        //            MessageBox.Show($"Download completed!");
        //        }
        //        else
        //        {
        //            MessageBox.Show("The file is already downloaded!");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"An error occurred: {ex.Message}");
        //    }
        //}

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (GamesList.SelectedItem is Game selectedGame)
            {
                List<GameInstallationInfo> gameInstallations = LoadGameExecutables();

                var gameInfo = gameInstallations.FirstOrDefault(g => g.GameId == selectedGame.Id);

                if (gameInfo != null && File.Exists(gameInfo.ExecutablePath))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = gameInfo.ExecutablePath,
                            UseShellExecute = true
                        });
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

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (GamesList.SelectedItem is not Game selectedGame) return;
            //Ez szar
            //Ez a szar nem fut le ami itt van alattamˇˇˇˇˇ
            //Ha már telepítve van de nyomok egy downloadot a filet nem tölti le viszont hozzáad egy exe pathet amit nem töröl majd uninstallnál így felhalmozódik
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

                string executablePath = Path.Combine(installPath, selectedGame.Name + ".exe");
                var gameInfo = new GameInstallationInfo
                {
                    GameId = selectedGame.Id,
                    ExecutablePath = executablePath
                };

                var gameInstallations = LoadGameExecutables();
                gameInstallations.Add(gameInfo);
                SaveGameExecutables(gameInstallations);

                MessageBox.Show("Download completed!");
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
                        gameInfo.ExecutablePath = gameInfo.ExecutablePath.Remove(gameInfo.ExecutablePath.Length - (Games[gameInfo.GameId].Name.Length + 5));

                        if (Directory.Exists(gameInfo.ExecutablePath))
                        {
                            Directory.Delete(gameInfo.ExecutablePath, true);
                        }

                        gameInstallations.Remove(gameInfo);

                        SaveGameExecutables(gameInstallations);

                        MessageBox.Show("Game uninstalled successfully!");
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
    }
}
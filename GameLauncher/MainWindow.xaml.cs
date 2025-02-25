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
using Google.Protobuf.WellKnownTypes;
using MySql.Data.MySqlClient;
using static System.Net.Mime.MediaTypeNames;

namespace GameLauncher
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<Game> Games = new();
        private ObservableCollection<ReviewClass> Reviews = new();
        private List<Game> Executables = new();
        private readonly string ImageDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DownloadedImages");
        private readonly string GameDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DownloadedGames");
        private readonly string InstallationFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installedGames.json");
        private const string SettingsFile = "user.settings";
        private const string ConnectionString = "Server=localhost;Database=game_launcher;Uid=root;Pwd=;";
        private readonly int userId;
        private DateTime gameStartTime;
        private int totalPlaytimeMinutes;
        private Game _selectedGame;
        private Game SelectedGame
        {
            get => _selectedGame;
            set
            {
                if (_selectedGame != value)
                {
                    _selectedGame = value;
                    UpdateUIForSelectedGame();
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DownloadImageAsync("https://i.postimg.cc/mDvhPW7C/NoImage.jpg");
            LoadGamesAsync();
            userId = GetUserId();
            Executables = LoadGameExecutables();
            GamesList.ItemsSource = Games;
        }

        private void GamesListItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Game selectedGame)
            {
                SelectedGame = selectedGame;
            }
        }

        private int GetUserId()
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    using StreamReader reader = new StreamReader(SettingsFile);
                    string fullSettings = reader.ReadToEnd();
                    string[] pieces = fullSettings.Split(';');

                    string savedName = pieces[0];
                    string query = $"SELECT id FROM users WHERE name = '{savedName}';";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        return (int)cmd.ExecuteScalar();
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("User not found. Please log in again.");
                LoginWindow loginWindow = new LoginWindow();
                loginWindow.Show();
                Close();
                return 0;
                throw;
            }
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
                        int playTime = LoadPlaytime(mySqlReader.GetInt32("id"));
                        double rating = CalculateRating(mySqlReader.GetInt32("id"));
                        var game = await ParseGameAsync(mySqlReader, playTime, rating);
                        Games.Add(game);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading games: {ex.Message}");
            }
        }

        private int LoadPlaytime(int gameId)
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();

                    string query = @$"SELECT playtime.playtime_minutes FROM playtime 
                                    INNER JOIN users ON playtime.user_id = users.id 
                                    WHERE users.id = {userId} AND game_id = {gameId};";
                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        var result = cmd.ExecuteScalar();
                        return result != null ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Playtime ERROR!");;
                throw;
            }
        }

        private double CalculateRating(int gameId)
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();

                    string query = @$"SELECT AVG(rating) FROM reviews WHERE game_id = {gameId};";
                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        var result = cmd.ExecuteScalar();

                        if (result == null || result == DBNull.Value)
                        {
                            return 0.0;
                        }

                        return Convert.ToDouble(result);
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Rating ERROR!"); ;
                throw;
            }
        }

        private async Task<Game> ParseGameAsync(MySqlDataReader reader, int playTime, double rating)
        {
            int id = reader.GetInt32("id");
            string name = reader.GetString("name");
            string exeName = reader.GetString("exe_name") + ".exe";
            string description = reader.GetString("description");
            string imageUrl = reader["image_path"] != DBNull.Value ? reader.GetString("image_path") : "https://i.postimg.cc/mDvhPW7C/NoImage.jpg";
            string downloadLink = reader["download_link"] != DBNull.Value ? reader.GetString("download_link") : null;
            string localImagePath = await DownloadImageAsync(imageUrl);
            DateTime releaseDate = reader.GetDateTime("release_date");
            string developerName = reader.GetString("developer_name");
            string publisherName = reader.GetString("publisher_name");

            return new Game(id, name, exeName, description, imageUrl, downloadLink, localImagePath, releaseDate, developerName, publisherName, playTime, rating);
        }

        private void UpdateUIForSelectedGame()
        {
            if (SelectedGame != null)
            {
                spGameData.Visibility = Visibility.Visible;
                DownloadButton.IsEnabled = !string.IsNullOrEmpty(SelectedGame.DownloadLink);
                tbReleaseDate.Text = SelectedGame.ReleaseDate.ToString("yyyy MMMM dd.");
                tbDeveloper.Text = SelectedGame.DeveloperName;
                tbPublisher.Text = SelectedGame.PublisherName;
                DownloadButton.Visibility = Visibility.Visible;
                LaunchButton.Visibility = Visibility.Visible;
                ProgressBar.Visibility = Visibility.Visible;
                ProgressBar.Value = 0;
                GameDescription.Text = SelectedGame.Description;
                tbRating.Text = $"{SelectedGame.Rating}/10";
                tbPlaytime.Text = $"Playtime: {SelectedGame.PlayTime} minutes";
                GameImage.Source = new BitmapImage(new Uri(SelectedGame.LocalImagePath));
                lblGameName.Content = SelectedGame.Name;
                var loadedReviews = loadReviews(SelectedGame.Id);
                Reviews.Clear();
                foreach (var review in loadedReviews)
                {
                    Reviews.Add(review);
                }
                lbxReviews.ItemsSource = Reviews;
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
            if (SelectedGame != null)
            {
                Executables = LoadGameExecutables();
                var gameInfo = Executables.FirstOrDefault(g => g.Id == SelectedGame.Id);

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

                        gameProcess.Exited += (s, args) => OnGameExit(SelectedGame.Name, SelectedGame.Id);
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
            if (SelectedGame == null) return;

            if (Executables.Where(x => x.Id == SelectedGame.Id).Count() != 0)
            {
                MessageBox.Show("This Game is already installed!");
                return;
            }

            EnsureDirectoryExists(GameDirectory);
            string targetPath = await SelectDownloadDirectoryAsync(SelectedGame);
            if (targetPath == null) return;

            string installPath = targetPath.Remove(targetPath.Length - 4);

            try
            {
                await DownloadAndInstallGameAsync(SelectedGame, targetPath, installPath);
                SelectedGame.InstallPath = installPath;

                string executablePath = Path.Combine(installPath, SelectedGame.ExeName);
                var gameInfo = new Game(SelectedGame.Id, SelectedGame.ExeName, executablePath);

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

        private void SaveGameExecutables(List<Game> games)
        {
            try
            {
                var simplifiedGames = games.Select(game => new
                {
                    game.Id,
                    game.ExeName,
                    game.ExecutablePath
                }).ToList();

                var json = JsonSerializer.Serialize(simplifiedGames);
                File.WriteAllText(InstallationFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving game data: {ex.Message}");
            }
        }

        private List<Game> LoadGameExecutables()
        {
            try
            {
                if (File.Exists(InstallationFilePath))
                {
                    var json = File.ReadAllText(InstallationFilePath);
                    return JsonSerializer.Deserialize<List<Game>>(json) ?? new List<Game>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading game data: {ex.Message}");
            }
            return new List<Game>();
        }

        private void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to uninstall this game?", "Confirm Uninstall", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                if (SelectedGame == null) return;

                var gameInstallations = LoadGameExecutables();

                var gameInfo = gameInstallations.FirstOrDefault(g => g.Id == SelectedGame.Id);

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

        private async void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            Profile profileWindow = new Profile(userId);
            profileWindow.Show();
        }

        private void ReadWriteReviewButton_Click(object sender, RoutedEventArgs e)
        {
            lbxReviews.Visibility = lbxReviews.Visibility.Equals(Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
            txbContent.Visibility = txbContent.Visibility.Equals(Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
            txbTitle.Visibility = txbTitle.Visibility.Equals(Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
            sldrRating.Visibility = sldrRating.Visibility.Equals(Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
            lblRating.Visibility = lblRating.Visibility.Equals(Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
            btnSubmit.Visibility = btnSubmit.Visibility.Equals(Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
            lblRatingText.Visibility = lblRatingText.Visibility.Equals(Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
            btnChange.Content = btnChange.Content.Equals("Add Review") ? "Show Reviews" : "Add Review";
        }

        private void btnSubmit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();

                    string query = @"
                        INSERT INTO reviews (game_id, user_id, rating, review_title, review_text)
                        VALUES (@GameId, @UserId, @Rating, @Title, @Text)";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@GameId", SelectedGame.Id);
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@Rating", sldrRating.Value);
                        cmd.Parameters.AddWithValue("@Title", txbTitle.Text);
                        cmd.Parameters.AddWithValue("@Text", txbContent.Text);

                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Review submitted successfully");

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database error: {ex.Message}");
            }
        }

        private void sldrRating_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lblRating != null && sldrRating != null)
            {
                lblRating.Content = sldrRating.Value;
            }
        }

        private ObservableCollection<ReviewClass> loadReviews(int currentGameId)
        {
            try
            {
                ObservableCollection<ReviewClass> reviews = new();

                using (MySqlConnection connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    string query = $"SELECT * FROM reviews WHERE game_id = '{currentGameId}';";


                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int reviewUserId = reader.GetInt32("user_id");
                                string reviewName = getName(reviewUserId);

                                reviews.Add(new ReviewClass(
                                    reviewName,
                                    reader.GetInt32("rating"),
                                    reader.GetString("review_title"),
                                    reader.GetString("review_text")
                                ));
                            }
                        }
                    }
                }
                return reviews;
            }
            catch (Exception)
            {
                MessageBox.Show("Error while connecting to the database");
                lblGameName.Content = $"Game ID : {currentGameId}";
                throw;
            }
        }

        private string getName(int reviewUserId)
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    string query = $"SELECT name FROM users WHERE id = '{reviewUserId}';";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        return cmd.ExecuteScalar().ToString();
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Error while connecting to the database");
                return "Unknown";
                throw;
            }
        }

        private void ChangeLibraryVisibility(object sender, RoutedEventArgs e)
        {
            ChangeReviewVisibility(sender, e);
            LibraryGrid.Visibility = LibraryGrid.Visibility.Equals(Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
        }

        private void ChangeReviewVisibility(object sender, RoutedEventArgs e)
        {
            txbContent.Visibility = txbContent.Visibility.Equals(Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
            sldrRating.Visibility = sldrRating.Visibility.Equals(Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
            lblRating.Visibility = lblRating.Visibility.Equals(Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
            txbTitle.Visibility = txbTitle.Visibility.Equals(Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
            lblRatingText.Visibility = lblRatingText.Visibility.Equals(Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
            btnSubmit.Visibility = btnSubmit.Visibility.Equals(Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
            lbxReviews.Visibility = lbxReviews.Visibility.Equals(Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
            btnChange.Visibility = btnChange.Visibility.Equals(Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
            lblGameName.Visibility = lblGameName.Visibility.Equals(Visibility.Visible) ? Visibility.Hidden : Visibility.Visible;
        }

        private void ReviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedGame != null)
            {
                    Reviews.Clear();
                    var currentGameId = SelectedGame.Id;
                    var currentUserId = userId;
                ChangeLibraryVisibility(sender, e);

                InitializeComponent();

                    this.DataContext = this;

                    try
                    {
                        using (MySqlConnection connection = new MySqlConnection(ConnectionString))
                        {
                            connection.Open();
                            string query = $"SELECT name FROM games WHERE id = '{currentGameId}';";

                            using (MySqlCommand cmd = new MySqlCommand(query, connection))
                            {
                                lblGameName.Content = cmd.ExecuteScalar().ToString();
                            }
                        }
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Error while connecting to the database");
                        lblGameName.Content = $"Game ID : {currentGameId}";
                        throw;
                    }

                    var loadedReviews = loadReviews(currentGameId);
                    foreach (var review in loadedReviews)
                    {
                        Reviews.Add(review);
                    }

                    lbxReviews.ItemsSource = Reviews;
            }
            else
            {
                MessageBox.Show("No game selected!");
            }
        }
    }
}
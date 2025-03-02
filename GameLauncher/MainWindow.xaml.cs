using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MySql.Data.MySqlClient;

namespace GameLauncher
{
    public partial class MainWindow : Window
    {
        //Collections
        private ObservableCollection<Game> Games = new();
        private ObservableCollection<Review> Reviews = new();
        private List<Game> Executables = new();
        //Directory / File Paths
        private readonly string ImageDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DownloadedImages");
        private readonly string GameDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DownloadedGames");
        private readonly string InstalledGamesFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installedGames.json");
        private const string SettingsFile = "user.settings";
        //DB Connection
        private const string ConnectionString = "Server=localhost;Database=game_launcher;Uid=root;Pwd=;";
        //Variables
        private readonly int userId;
        private DateTime gameStartTime;
        private int totalPlaytimeMinutes;
        //Selected Objects
        private Button _selectedButton;
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
            LoadGamesAsync();
            userId = GetUserId();
            Executables = LoadGameExecutables();
            GamesList.ItemsSource = Games;
        }

        #region Start
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
                        double rating = LoadAverageRating(mySqlReader.GetInt32("id"));
                        var game = await ParseGameAsync(mySqlReader, playTime, rating);
                        Games.Add(game);
                    }
                }

                if (Games.Count > 0)
                {
                    SelectedGame = Games[0];
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
                MessageBox.Show("Playtime ERROR!"); ;
                throw;
            }
        }

        private double LoadAverageRating(int gameId)
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
        #endregion

        #region Download & Install
        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            EnsureDirectoryExists(GameDirectory);
            string zipPath = SelectDownloadDirectoryAsync(SelectedGame);
            if (zipPath == null) return;

            string installPath = zipPath.Remove(zipPath.Length - 4);

            try
            {
                await DownloadAndInstallGameAsync(SelectedGame, zipPath, installPath);
                SelectedGame.InstallPath = installPath;

                string executablePath = Path.Combine(installPath, SelectedGame.ExeName);
                var gameInstallationInfo = new Game(SelectedGame.Id, SelectedGame.ExeName, executablePath);

                var gameInstallations = LoadGameExecutables();
                gameInstallations.Add(gameInstallationInfo);
                SaveGameExecutables(gameInstallations);

                MessageBox.Show("Download completed!");
                DownloadButton.IsEnabled = false;
                Executables = LoadGameExecutables();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }

        private static void EnsureDirectoryExists(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
        }

        private string SelectDownloadDirectoryAsync(Game selectedGame)
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Select Folder",
                InitialDirectory = GameDirectory
            };

            if (folderDialog.ShowDialog() == true)
            {
                var folderName = folderDialog.FolderName;
                return Path.Combine(folderDialog.FolderName, $"{selectedGame.Name}.zip");
            }

            return null;
        }

        private async Task DownloadAndInstallGameAsync(Game game, string zipPath, string installPath)
        {
            var progress = new Progress<double>(value => ProgressBar.Value = value);
            await DownloadFileWithProgressAsync(game.DownloadLink, zipPath, progress);
            ExtractZip(zipPath, installPath);
        }
        //Fogalmam sincs hogy működikˇˇˇˇˇˇˇˇˇˇ
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

        public void ExtractZip(string zipPath, string installPath)
        {
            EnsureDirectoryExists(installPath);
            ZipFile.ExtractToDirectory(zipPath, installPath);
            File.Delete(zipPath);
        }
        #endregion

        #region EXE Handling
        private List<Game> LoadGameExecutables()
        {
            try
            {
                if (File.Exists(InstalledGamesFilePath))
                {
                    var json = File.ReadAllText(InstalledGamesFilePath);
                    return JsonSerializer.Deserialize<List<Game>>(json) ?? new List<Game>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading game data: {ex.Message}");
            }
            return new List<Game>();
        }

        private void SaveGameExecutables(List<Game> games)
        {
            try
            {
                var simplifiedGameInfo = games.Select(game => new
                {
                    game.Id,
                    game.ExeName,
                    game.ExecutablePath
                }).ToList();

                var json = JsonSerializer.Serialize(simplifiedGameInfo);
                File.WriteAllText(InstalledGamesFilePath, json);
            }
            catch (JsonException jsonEx)
            {
                MessageBox.Show($"Error serializing game data: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving game data: {ex.Message}");
            }
        }
        #endregion

        #region External Window
        private void LogOutButton_Click(object sender, RoutedEventArgs e)
        {
            File.Delete(SettingsFile);
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            Close();
        }

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            Profile profileWindow = new Profile(userId);
            profileWindow.Show();
        }
        #endregion

        private void UpdateUIForSelectedGame()
        {
            if (SelectedGame != null)
            {
                DownloadButton.IsEnabled = !string.IsNullOrEmpty(SelectedGame.DownloadLink);
                if (Executables.Where(x => x.Id == SelectedGame.Id).Count() > 0)
                {
                    DownloadButton.IsEnabled = false;
                }
                GameInfo.Visibility = Visibility.Visible;
                tbReleaseDate.Text = SelectedGame.ReleaseDate.ToString("yyyy MMMM dd.");
                tbGameName.Text = SelectedGame.Name;
                tbDeveloper.Text = SelectedGame.DeveloperName;
                tbPublisher.Text = SelectedGame.PublisherName;
                DownloadButton.Visibility = Visibility.Visible;
                LaunchButton.Visibility = Visibility.Visible;
                ProgressBar.Visibility = Visibility.Visible;
                ProgressBar.Value = 0;
                GameDescription.Text = SelectedGame.Description;
                tbRating.Text = $"{SelectedGame.Rating}/10";
                tbPlaytime.Text = $"{SelectedGame.PlayTime} minutes";
                GameImage.Source = new BitmapImage(new Uri(SelectedGame.LocalImagePath));
                lblGameName.Text = SelectedGame.Name;
                var loadedReviews = LoadReviews(SelectedGame.Id);
                Reviews.Clear();
                foreach (var review in loadedReviews)
                {
                    Reviews.Add(review);
                }
                lbxReviews.ItemsSource = Reviews;
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
                        DownloadButton.IsEnabled = true;

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

        #region Review
        private ObservableCollection<Review> LoadReviews(int gameId)
        {
            try
            {
                ObservableCollection<Review> reviews = new();

                using (MySqlConnection connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    string query = $"SELECT * FROM reviews WHERE game_id = '{gameId}';";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                reviews.Add(new Review(
                                    GetUsername(reader.GetInt32("user_id")),
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
                throw;
            }
        }

        private string GetUsername(int UserId)
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    string query = $"SELECT name FROM users WHERE id = '{UserId}';";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        return cmd.ExecuteScalar().ToString();
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Error while connecting to the database");
                throw;
            }
        }

        private void DisplayReviews(object sender, RoutedEventArgs e)
        {
            Reviews.Clear();
            ChangeReviewVisibility(sender, e);

            try
            {
                lblGameName.Text = SelectedGame.Name;
                Reviews = LoadReviews(SelectedGame.Id);
                lbxReviews.ItemsSource = Reviews;
            }
            catch (Exception)
            {
                MessageBox.Show("Error while loading reviews");
                throw;
            }
        }

        private void SubmitReview(object sender, RoutedEventArgs e)
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
                DisplayReviews(sender, e);
                lbxReviews.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database error: {ex.Message}");
            }

            txbContent.Text = "Content";
            txbTitle.Text = "Title";
            sldrRating.Value = 1;
            lblRating.Text = "1";
        }

        private void SwitchReviewMode(object sender, RoutedEventArgs e)
        {
            lbxReviews.Visibility = lbxReviews.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
            btnChange.Content = btnChange.Content.Equals("Add Review") ? "Show Reviews" : "Add Review";
        }
        #endregion

        #region UI
        private void ChangeLibraryVisibility(object sender, RoutedEventArgs e)
        {
            LibraryGrid.Visibility = Visibility.Visible;
            ReviewGrid.Visibility = Visibility.Hidden;
        }

        private void ChangeReviewVisibility(object sender, RoutedEventArgs e)
        {
            ReviewGrid.Visibility = Visibility.Visible;
            LibraryGrid.Visibility = Visibility.Hidden;
        }

        private void RatingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lblRating != null && sldrRating != null)
            {
                lblRating.Text = Convert.ToString(sldrRating.Value);
            }
        }

        private void ChangeSelectedGameButton(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Game selectedGame)
            {
                SelectedGame = selectedGame;
            }
            if (_selectedButton != null)
            {
                _selectedButton.ClearValue(BackgroundProperty);
                _selectedButton.ClearValue(ForegroundProperty);
                _selectedButton.ClearValue(BorderThicknessProperty);
                _selectedButton.ClearValue(FontWeightProperty);
            }

            Button clickedButton = sender as Button;
            if (clickedButton != null)
            {
                clickedButton.Background = new SolidColorBrush(Colors.LightSlateGray);
                clickedButton.Foreground = new SolidColorBrush(Colors.Black);
                clickedButton.BorderThickness = new Thickness(3);
                clickedButton.FontWeight = FontWeights.Bold;
                _selectedButton = clickedButton;
            }
        }
        #endregion
    }
}
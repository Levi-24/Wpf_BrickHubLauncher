using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MySql.Data.MySqlClient;

namespace GameLauncher
{
    public partial class MainWindow : Window
    {
        //Collections
        private readonly ObservableCollection<Game> Games = [];
        private ObservableCollection<Review> Reviews = [];
        private List<Game> Executables = [];
        //Directory / File Paths
        private readonly string ImageDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DownloadedImages");
        private readonly string GameDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DownloadedGames");
        private readonly string InstalledGamesFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installedGames.json");
        private const string RememberMeTokenFile = AppSettings.RememberMeToken;
        private const string DBConnectionString = AppSettings.DatabaseConnectionString;
        //Variables
        private readonly int userId;
        private DateTime gameStartTime;
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

        public MainWindow(string email)
        {
            InitializeComponent();
            _ = InitializeAsync();
            logoImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/brickhubLogo.png"));
            userId = GetUserId(email);
        }

        private async Task InitializeAsync()
        {
            await LoadGamesAsync();
            GamesList.ItemsSource = Games;
            await LoadingScreenAsync();
            Executables = LoadGameExecutables();
        }

        #region Start
        private int GetUserId(string email)
        {
            try
            {
                using MySqlConnection connection = new(DBConnectionString);
                connection.Open();
                string query = $"SELECT id FROM users WHERE email = '{email}';";

                using MySqlCommand cmd = new(query, connection);
                return (int)cmd.ExecuteScalar();
            }
            catch (Exception)
            {
                MessageBox.Show("User not found. Please log in again.");
                LoginWindow loginWindow = new();
                loginWindow.Show();
                Close();
                return 0;
                throw;
            }
        }

        private async Task LoadGamesAsync()
        {
            try
            {
                var query = @"SELECT g.*, 
                dev.name AS developer_name, 
                pub.name AS publisher_name
                FROM games g
                JOIN members dev ON g.developer_id = dev.id
                JOIN members pub ON g.publisher_id = pub.id";
                using var conn = new MySqlConnection(DBConnectionString);
                await conn.OpenAsync();

                using var cmd = new MySqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (reader is MySqlDataReader mySqlReader)
                    {
                        int playTime = LoadPlaytime(mySqlReader.GetInt32("id"));
                        double rating = LoadAverageRating(mySqlReader.GetInt32("id"));
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
                using MySqlConnection connection = new(DBConnectionString);
                connection.Open();

                string query = @$"SELECT playtime.playtime_minutes FROM playtime 
                                    INNER JOIN users ON playtime.user_id = users.id 
                                    WHERE users.id = {userId} AND game_id = {gameId};";
                using MySqlCommand cmd = new(query, connection);
                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : 0;
            }
            catch (Exception)
            {
                MessageBox.Show("Playtime ERROR!"); ;
                throw;
            }
        }

        private static double LoadAverageRating(int gameId)
        {
            try
            {
                using MySqlConnection connection = new(DBConnectionString);
                connection.Open();

                string query = @$"SELECT AVG(rating) FROM reviews WHERE game_id = {gameId};";
                using MySqlCommand cmd = new(query, connection);
                var result = cmd.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                {
                    return 0.0;
                }

                return Convert.ToDouble(result);
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
            string description = RemoveHTMLTags(reader.GetString("description"));
            string imageUrl = reader["image_path"] != DBNull.Value ? reader.GetString("image_path") : "https://i.postimg.cc/mDvhPW7C/NoImage.jpg";
            string downloadLink = reader["download_link"] != DBNull.Value ? reader.GetString("download_link") : null;
            string localImagePath = await DownloadImageAsync(imageUrl);
            DateTime releaseDate = reader.GetDateTime("release_date");
            string developerName = reader.GetString("developer_name");
            string publisherName = reader.GetString("publisher_name");

            return new Game(id, name, exeName, description, imageUrl, downloadLink, localImagePath, releaseDate, developerName, publisherName, playTime, rating);
        }

        public static string RemoveHTMLTags(string text)
        {
            return Regex.Replace(text, "<.*?>", string.Empty);
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

        #region Download & Install & Uninstall
        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            GamesList.IsHitTestVisible = false;

            EnsureDirectoryExists(GameDirectory);
            string zipPath = SelectDownloadDirectoryAsync(SelectedGame);
            if (zipPath == null) return;

            string installPath = zipPath[..^4];

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
                LaunchButton.IsEnabled = true;
                UninstallButton.IsEnabled = true;
                DownloadButton.Content = "Installed";
                ProgressBar.Value = 0;
                Executables = LoadGameExecutables();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
            GamesList.IsHitTestVisible = true;
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
        private static async Task DownloadFileWithProgressAsync(string fileUrl, string destinationPath, IProgress<double> progress)
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

        public static void ExtractZip(string zipPath, string installPath)
        {
            EnsureDirectoryExists(installPath);
            ZipFile.ExtractToDirectory(zipPath, installPath);
            File.Delete(zipPath);
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
                        gameInfo.ExecutablePath = gameInfo.ExecutablePath[..^gameInfo.ExeName.Length];

                        if (Directory.Exists(gameInfo.ExecutablePath))
                        {
                            Directory.Delete(gameInfo.ExecutablePath, true);
                        }

                        gameInstallations.Remove(gameInfo);

                        SaveGameExecutables(gameInstallations);

                        MessageBox.Show("Game uninstalled successfully!");
                        DownloadButton.IsEnabled = true;
                        LaunchButton.IsEnabled = false;
                        UninstallButton.IsEnabled = false;
                        DownloadButton.Content = "Download";

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

        #endregion

        #region EXE Handling
        private List<Game> LoadGameExecutables()
        {
            try
            {
                if (File.Exists(InstalledGamesFilePath))
                {
                    var json = File.ReadAllText(InstalledGamesFilePath);
                    return JsonSerializer.Deserialize<List<Game>>(json) ?? [];
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading game data: {ex.Message}");
            }
            return [];
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

        #region Logout
        private void LogOutButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(RememberMeTokenFile))
            {
                var pieces = File.ReadAllText(RememberMeTokenFile).Split(';');
                string token = pieces[0];

                using MySqlConnection connection = new(DBConnectionString);
                connection.Open();
                string query = $"DELETE FROM tokens WHERE device = '1' AND token = '{token}';";

                using MySqlCommand cmd = new(query, connection);
                cmd.ExecuteScalar();

                File.Delete(RememberMeTokenFile);
            }

            LoginWindow loginWindow = new();
            loginWindow.Show();
            Close();
        }
        #endregion

        #region Launch & Playtime
        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedGame != null)
            {
                Executables = LoadGameExecutables();
                var gameInfo = Executables.FirstOrDefault(g => g.Id == SelectedGame.Id);
                LaunchButton.IsEnabled = false;
                UninstallButton.IsEnabled = false;

                if (gameInfo != null && File.Exists(gameInfo.ExecutablePath))
                {
                    try
                    {
                        Process gameProcess = new()
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = gameInfo.ExecutablePath,
                                UseShellExecute = true
                            },
                            EnableRaisingEvents = true
                        };

                        gameStartTime = DateTime.Now;

                        gameProcess.Exited += (s, args) => OnGameExit(SelectedGame.Id);
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

        private void OnGameExit(int gameID)
        {
            try
            {
                TimeSpan playDuration = DateTime.Now - gameStartTime;
                int minutesPlayed = (int)playDuration.TotalMinutes;

                DateTime lastPlayed = DateTime.Now;

                SavePlaytimeToDatabase(userId, gameID, minutesPlayed, lastPlayed);
                int playTime = LoadPlaytime(gameID);
                Games.First(g => g.Id == SelectedGame.Id).PlayTime = playTime;

                Dispatcher.Invoke(() =>
                {
                    LaunchButton.IsEnabled = true;
                    UninstallButton.IsEnabled = true;
                    tbPlaytime.Text = $"{SelectedGame.PlayTime} minutes";
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error tracking playtime: {ex.Message}");
            }
        }

        private static void SavePlaytimeToDatabase(int userId, int gameId, int minutesPlayed, DateTime lastPlayed)
        {
            try
            {
                using MySqlConnection connection = new(DBConnectionString);
                connection.Open();

                string query = @"
                INSERT INTO playtime (user_id, game_id, playtime_minutes, last_played)
                VALUES (@UserId, @GameId, @MinutesPlayed, @LastPlayed)
                ON DUPLICATE KEY UPDATE 
                playtime_minutes = playtime_minutes + @MinutesPlayed,
                last_played = @LastPlayed;";

                using MySqlCommand cmd = new(query, connection);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@GameId", gameId);
                cmd.Parameters.AddWithValue("@MinutesPlayed", minutesPlayed);
                cmd.Parameters.AddWithValue("@LastPlayed", lastPlayed);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database error: {ex.Message}");
            }
        }

        #endregion

        #region Review
        private ObservableCollection<Review> LoadReviews(int gameId)
        {
            Reviews.Clear();
            try
            {
                ObservableCollection<Review> reviews = [];

                using (MySqlConnection connection = new(DBConnectionString))
                {
                    connection.Open();
                    string query = $"SELECT * FROM reviews WHERE game_id = '{gameId}';";

                    using MySqlCommand cmd = new(query, connection);
                    using MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        reviews.Add(new Review(
                            reader.GetInt32("user_id"),
                            GetUsername(reader.GetInt32("user_id")),
                            reader.GetInt32("rating"),
                            reader.GetString("review_title"),
                            reader.GetString("review_text"),
                            reader.GetInt32("user_id") == userId
                        ));
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

        private static string GetUsername(int UserId)
        {
            try
            {
                using MySqlConnection connection = new(DBConnectionString);
                connection.Open();
                string query = $"SELECT name FROM users WHERE id = '{UserId}';";

                using MySqlCommand cmd = new(query, connection);
                return cmd.ExecuteScalar().ToString();
            }
            catch (Exception)
            {
                MessageBox.Show("Error while connecting to the database");
                throw;
            }
        }

        private void DisplayReviews(object sender, RoutedEventArgs e)
        {
            if (SelectedGame == null)
            {
                MessageBox.Show("Please select a game first.");
                return;
            }

            Reviews.Clear();
            ChangeReviewVisibility(sender, e);

            try
            {
                lblGameName.Text = SelectedGame.Name + " reviews:";
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
                using (MySqlConnection connection = new(DBConnectionString))
                {
                    connection.Open();
                    string query = @"
                        INSERT INTO reviews (game_id, user_id, rating, review_title, review_text)
                        VALUES (@GameId, @UserId, @Rating, @Title, @Text)";

                    using MySqlCommand cmd = new(query, connection);
                    cmd.Parameters.AddWithValue("@GameId", SelectedGame.Id);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@Rating", sldrRating.Value);
                    cmd.Parameters.AddWithValue("@Title", txbTitle.Text);
                    cmd.Parameters.AddWithValue("@Text", txbContent.Text);
                    cmd.ExecuteNonQuery();
                }

                double rating = LoadAverageRating(SelectedGame.Id);
                Games.First(g => g.Id == SelectedGame.Id).Rating = rating;

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
            if (SelectedGame == null)
            {
                MessageBox.Show("Please select a game first.");
                return;
            }

            LibraryGrid.Visibility = Visibility.Visible;
            ReviewGrid.Visibility = Visibility.Hidden;
            UpdateUIForSelectedGame();
        }

        private void UpdateUIForSelectedGame()
        {
            if (SelectedGame != null)
            {
                welcomeGrid.Visibility = Visibility.Collapsed;
                if (ReviewGrid.Visibility != Visibility.Visible)
                {
                    LibraryGrid.Visibility = Visibility.Visible;
                }
                //Disable download button if there is no download link
                if (SelectedGame.IsDownloadLinkValid)
                {
                    DownloadButton.IsEnabled = true;
                    UninstallButton.IsEnabled = false;
                    LaunchButton.IsEnabled = false;
                    DownloadButton.Content = "Download";
                }
                else
                {
                    DownloadButton.IsEnabled = false;
                    DownloadButton.Content = "Download Link Not Valid";
                }
                //Disable download button if the game is already installed
                if (Executables.Where(x => x.Id == SelectedGame.Id).Any())
                {
                    DownloadButton.IsEnabled = false;
                    UninstallButton.IsEnabled = true;
                    LaunchButton.IsEnabled = true;
                    DownloadButton.Content = "Installed";
                }
                //Set selectedGame value for UI elements
                lblGameName.Text = SelectedGame.Name + " reviews:";
                tbReleaseDate.Text = SelectedGame.ReleaseDate.ToString("yyyy MMMM dd.");
                tbGameName.Text = SelectedGame.Name;
                tbDeveloper.Text = SelectedGame.DeveloperName;
                tbPublisher.Text = SelectedGame.PublisherName;
                tbGameDescription.Text = SelectedGame.Description;
                tbRating.Text = $"{SelectedGame.Rating}/10";
                tbPlaytime.Text = $"{SelectedGame.PlayTime} minutes";
                GameImage.Source = new BitmapImage(new Uri(SelectedGame.LocalImagePath));
                //Load reviews
                Reviews = LoadReviews(SelectedGame.Id);
                lbxReviews.ItemsSource = Reviews;
            }
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

            if (sender is Button clickedButton)
            {
                clickedButton.Background = new SolidColorBrush(Colors.LightSlateGray);
                clickedButton.Foreground = new SolidColorBrush(Colors.Black);
                clickedButton.BorderThickness = new Thickness(3);
                clickedButton.FontWeight = FontWeights.Bold;
                _selectedButton = clickedButton;
            }
        }
        #endregion

        private async Task LoadingScreenAsync()
        {
            SplashScreen splash = new()
            {
                Owner = this, // Set the owner to the main window
                WindowStartupLocation = WindowStartupLocation.CenterOwner // Center it over the main window
            };
            splash.Show();
            // Perform link validation asynchronously
            await ValidateDownloadLinksAsync();

            // Close the splash screen on the UI thread
            splash.Close();
        }

        private async Task ValidateDownloadLinksAsync()
        {
            foreach (var game in Games)
            {
                game.IsDownloadLinkValid = await IsLinkValidAsync(game.DownloadLink);
            }
        }

        private static async Task<bool> IsLinkValidAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false; // Treat empty or null URLs as invalid
            }

            using HttpClient client = new();
            try
            {
                // Make a HEAD request to check if the URL is valid
                using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error validating link {url}: {ex.Message}");
                return false;
            }
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
    }
}
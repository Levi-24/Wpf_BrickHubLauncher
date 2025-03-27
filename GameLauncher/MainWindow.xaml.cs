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
        private readonly ObservableCollection<Game> Games = [];
        private ObservableCollection<Review> Reviews = [];
        private List<Game> Executables = [];
        private readonly string ImageDirectory = AppSettings.ImageDirectory;
        private readonly string GameDirectory = AppSettings.GameDirectory;
        private readonly string InstalledGamesFilePath = AppSettings.InstalledGamesFilePath;
        private readonly string RememberMeTokenFile = AppSettings.RememberMeToken;
        private const string DBConnectionString = AppSettings.DatabaseConnectionString;
        private readonly int currentId;
        private DateTime gameStartTime;
        private Button _selectedButton;
        private bool isMaximized;
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

        public MainWindow(int loginId)
        {
            InitializeComponent();
            GetLatestArticle();
            _ = InitializeAsync();
            currentId = loginId;
        }

        #region Start
        private async Task InitializeAsync()
        {
            await LoadGamesAsync();
            GamesList.ItemsSource = Games.OrderBy(x => x.Name);
            await LoadingScreenAsync();
            Cursor = Cursors.Arrow;
            Executables = LoadGameExecutables();
            WindowState = WindowState.Normal;
        }

        private async Task LoadGamesAsync()
        {
            try
            {
                var query = @"SELECT g.*, dev.name AS developer_name, pub.name AS publisher_name
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
                MessageBox.Show($"Error while loading games: {ex.Message}", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int LoadPlaytime(int gameId)
        {
            try
            {
                using MySqlConnection conn = new(DBConnectionString);
                conn.Open();

                string query = @$"SELECT playtime.playtime_minutes FROM playtime 
                                    INNER JOIN users ON playtime.user_id = users.id 
                                    WHERE users.id = {currentId} AND game_id = {gameId};";
                using MySqlCommand cmd = new(query, conn);
                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while loading playtime: {ex.Message}", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private static double LoadAverageRating(int gameId)
        {
            try
            {
                using MySqlConnection conn = new(DBConnectionString);
                conn.Open();

                string query = @$"SELECT AVG(rating) FROM reviews WHERE game_id = {gameId};";
                using MySqlCommand cmd = new(query, conn);
                var result = cmd.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                    return 0.0;

                return Convert.ToDouble(result);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while loading ratings: {ex.Message}", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private async Task<Game> ParseGameAsync(MySqlDataReader reader, int playTime, double rating)
        {
            int id = reader.GetInt32("id");
            string name = reader.GetString("name");
            string exeName = reader.GetString("exe_name") + ".exe";
            string description = RemoveHTMLTags(reader.GetString("description"));
            string imageUrl = reader["image_path"] != DBNull.Value ? reader.GetString("image_path") : "https://i.postimg.cc/L6pL1Zkr/NoImage.png";
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
                return Path.Combine(Directory.GetParent(AppContext.BaseDirectory).Parent.Parent.Parent.FullName, "Resources/NoImage.png");
            }
        }

        private async void GetLatestArticle()
        {
            var query = @"SELECT a.title, a.author, a.image, a.content, a.created_at, game.name AS game_name
                          FROM articles a
                          JOIN games game ON a.game_id = game.id 
                          ORDER BY a.created_at DESC LIMIT 1;";
            using var conn = new MySqlConnection(DBConnectionString);
            await conn.OpenAsync();

            using var cmd = new MySqlCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                if (reader is MySqlDataReader mySqlReader)
                {
                    welcomeArticleImage.Source = new BitmapImage(new Uri(await DownloadImageAsync(reader["image"] != DBNull.Value ? reader.GetString("image") : "https://i.postimg.cc/L6pL1Zkr/NoImage.png")));
                    tbWelcomeTitle.Text = reader.GetString("title");
                    tbWelcomeAuthor.Text = "Author: " + reader.GetString("author");
                    tbWelcomeContent.Text = RemoveHTMLTags(reader.GetString("content"));
                    tbWelcomeGameName.Text = reader.GetString("game_name");
                    tbWelcomeDate.Text = reader.GetDateTime("created_at").ToString("yyyy. MM. dd.");
                }
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

                MessageBox.Show("Download completed!", "Success!", MessageBoxButton.OK, MessageBoxImage.Information);
                DownloadButton.IsEnabled = false;
                LaunchButton.IsEnabled = true;
                UninstallButton.IsEnabled = true;
                DownloadButton.Content = "Installed";
                ProgressBar.Value = 0;
                Executables = LoadGameExecutables();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occured: {ex.Message}", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
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

            GamesList.IsHitTestVisible = true;
            return null;
        }

        private async Task DownloadAndInstallGameAsync(Game game, string zipPath, string installPath)
        {
            var progress = new Progress<double>(value => ProgressBar.Value = value);
            await DownloadFileWithProgressAsync(game.DownloadLink, zipPath, progress);
            ExtractZip(zipPath, installPath);
        }

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

                        MessageBox.Show("Game uninstalled successfully!", "Success!", MessageBoxButton.OK, MessageBoxImage.Information);
                        DownloadButton.IsEnabled = true;
                        LaunchButton.IsEnabled = false;
                        UninstallButton.IsEnabled = false;
                        DownloadButton.Content = "Download";

                        Executables = LoadGameExecutables();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred while uninstalling the game: {ex.Message}", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("The game is not found in the installed list.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Error while loading game executables: {ex.Message}", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Error while serializing game data: {jsonEx.Message}", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while saving game executables: {ex.Message}", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Logout
        private void LogOutButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to log out?", "Log out", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                if (File.Exists(RememberMeTokenFile))
                {
                    string token = File.ReadAllText(RememberMeTokenFile);

                    using MySqlConnection conn = new(DBConnectionString);
                    conn.Open();

                    string query = $"DELETE FROM tokens WHERE device = 1 AND token = '{token}';";

                    using MySqlCommand cmd = new(query, conn);
                    cmd.ExecuteScalar();

                    File.Delete(RememberMeTokenFile);
                }

                LoginWindow loginWindow = new();
                loginWindow.Show();
                Close();
            }
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
                        MessageBox.Show($"Error launching the game: {ex.Message}", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                        LaunchButton.IsEnabled = true;
                        UninstallButton.IsEnabled = true;
                    }
                }
                else
                {
                    MessageBox.Show("Executable not found for the selected game.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                    LaunchButton.IsEnabled = true;
                    UninstallButton.IsEnabled = true;
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

                SavePlaytimeToDatabase(currentId, gameID, minutesPlayed, lastPlayed);
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
                MessageBox.Show($"Error tracking playtime: {ex.Message}", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Database error: {ex.Message}", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
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

                using (MySqlConnection conn = new(DBConnectionString))
                {
                    conn.Open();
                    string query = $"SELECT * FROM reviews WHERE game_id = '{gameId}';";

                    using MySqlCommand cmd = new(query, conn);
                    using MySqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        reviews.Add(new Review(
                            reader.GetInt32("user_id"),
                            GetUsername(reader.GetInt32("user_id")),
                            reader.GetInt32("rating"),
                            reader.GetString("review_title"),
                            reader.GetString("review_text"),
                            reader.GetInt32("user_id") == currentId
                        ));
                    }
                }
                return reviews;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while connecting to the database: {ex.Message}", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private static string GetUsername(int UserId)
        {
            try
            {
                using MySqlConnection conn = new(DBConnectionString);
                conn.Open();
                string query = $"SELECT name FROM users WHERE id = '{UserId}';";

                using MySqlCommand cmd = new(query, conn);
                return cmd.ExecuteScalar().ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while connecting to the database: {ex.Message}", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void DisplayReviews(object sender, RoutedEventArgs e)
        {
            if (SelectedGame == null)
            {
                MessageBox.Show("Please select a game first.", "Select a game!", MessageBoxButton.OK, MessageBoxImage.Information);
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
            catch (Exception ex)
            {
                MessageBox.Show($"Error while loading reviews: {ex.Message}", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void SubmitReview(object sender, RoutedEventArgs e)
        {
            if(txbTitle.Text != "" && txbContent.Text != "")
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
                        cmd.Parameters.AddWithValue("@UserId", currentId);
                        cmd.Parameters.AddWithValue("@Rating", sldrRating.Value);
                        cmd.Parameters.AddWithValue("@Title", txbTitle.Text);
                        cmd.Parameters.AddWithValue("@Text", txbContent.Text);
                        cmd.ExecuteNonQuery();
                    }

                    double rating = LoadAverageRating(SelectedGame.Id);
                    Games.First(g => g.Id == SelectedGame.Id).Rating = rating;

                    MessageBox.Show("Review submitted successfully", "Success!", MessageBoxButton.OK, MessageBoxImage.Information);
                    DisplayReviews(sender, e);
                    lbxReviews.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Database error: {ex.Message}", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                btnChange.Content = "Add Review";
                txbContent.Text = "Content";
                txbTitle.Text = "Title";
                sldrRating.Value = 1;
                lblRating.Text = "1";
            }
            else
            {
                MessageBox.Show("Please fill in all fields.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SwitchReviewMode(object sender, RoutedEventArgs e)
        {
            lbxReviews.Visibility = lbxReviews.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
            btnSubmit.Visibility = btnSubmit.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
            btnChange.Content = btnChange.Content.Equals("Write A Review") ? "Show Reviews" : "Write A Review";
        }
        #endregion

        #region UI
        private void ChangeLibraryVisibility(object sender, RoutedEventArgs e)
        {
            if (SelectedGame == null)
            {
                MessageBox.Show("Please select a game first.", "Select a game!", MessageBoxButton.OK, MessageBoxImage.Information);
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
                WelcomeGrid.Visibility = Visibility.Collapsed;
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
                    UninstallButton.IsEnabled = false;
                    LaunchButton.IsEnabled = false;
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
                tbReleaseDate.Text = SelectedGame.ReleaseDate.ToString("yyyy. MMMM. dd.");
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

        #region LoadingScreen & Validation
        private async Task LoadingScreenAsync()
        {
            SplashScreen splash = new()
            {
                Owner = this,
            };
            splash.Show();
            await ValidateDownloadLinksAsync();
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
                return false;
            }

            using HttpClient client = new();
            try
            {
                using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error validating link {url}: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region TitleBar
        public void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        public void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (isMaximized)
            {
                DraggableBorder.IsHitTestVisible = true;
                WindowState = WindowState.Normal;
                Width = 1000;
                Height = 630;
            }
            else
            {
                DraggableBorder.IsHitTestVisible = false;
                WindowState = WindowState.Normal;
                Left = SystemParameters.WorkArea.Left;
                Top = SystemParameters.WorkArea.Top;
                Width = SystemParameters.WorkArea.Width;
                Height = SystemParameters.WorkArea.Height;
            }

            isMaximized = !isMaximized;
        }

        public void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        #endregion
    }
}
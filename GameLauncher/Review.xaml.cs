using MySql.Data.MySqlClient;
using System.Collections.ObjectModel;
using System.Windows;

namespace GameLauncher
{
    public partial class Review : Window
    {
        ObservableCollection<ReviewClass> Reviews = new();
        private const string ConnectionString = "Server=localhost;Database=game_launcher;Uid=root;Pwd=;";
        private int currentGameId;
        private int currentUserId;

        public Review(int gameID, int userId)
        {
            currentGameId = gameID;
            currentUserId = userId;
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
                                string reviewUserName = getUserName(reviewUserId);

                                reviews.Add(new ReviewClass(
                                    reviewUserName,
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

        private string getUserName(int reviewUserId)
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(ConnectionString))
                {
                    connection.Open();
                    string query = $"SELECT username FROM users WHERE id = '{reviewUserId}';";

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

        private void sldrRating_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lblRating != null && sldrRating != null)
            {
                lblRating.Content = sldrRating.Value;
            }
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
                        cmd.Parameters.AddWithValue("@GameId",currentGameId );
                        cmd.Parameters.AddWithValue("@UserId", currentUserId);
                        cmd.Parameters.AddWithValue("@Rating", sldrRating.Value);
                        cmd.Parameters.AddWithValue("@Title", txbTitle.Text);
                        cmd.Parameters.AddWithValue("@Text", txbContent.Text);

                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Review submitted successfully");
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database error: {ex.Message}");
            }
        }

        private void ButtonChange_Click(object sender, RoutedEventArgs e)
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
    }
}

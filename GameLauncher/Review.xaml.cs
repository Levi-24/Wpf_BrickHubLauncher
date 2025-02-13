using MySql.Data.MySqlClient;
using System.Windows;

namespace GameLauncher
{
    public partial class Review : Window
    {
        private const string ConnectionString = "Server=localhost;Database=game_launcher;Uid=root;Pwd=;";
        private int currentGameId;
        private int currentUserId;

        public Review(int gameID, int userId)
        {
            currentGameId = gameID;
            currentUserId = userId;
            InitializeComponent();
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
    }
}

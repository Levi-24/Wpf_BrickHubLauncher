namespace GameLauncher
{
    public class Review
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public int Rating { get; set; }
        public string ReviewTitle { get; set; }
        public string ReviewText { get; set; }
        public bool IsCurrentUser { get; set; }

        public Review(int userId, string userName, int rating, string reviewTitle, string reviewText, bool isCurrentUser)
        {
            UserId = userId;
            UserName = userName;
            Rating = rating;
            ReviewTitle = reviewTitle;
            ReviewText = reviewText;
            IsCurrentUser = isCurrentUser;
        }
    }
}

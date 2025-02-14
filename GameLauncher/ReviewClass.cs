namespace GameLauncher
{
    public class ReviewClass
    {
        public string UserName { get; set; }
        public int Rating { get; set; }
        public string ReviewTitle { get; set; }
        public string ReviewText { get; set; }

        public ReviewClass(string userName, int rating, string reviewTitle, string reviewText)
        {
            UserName = userName;
            Rating = rating;
            ReviewTitle = reviewTitle;
            ReviewText = reviewText;
        }
    }
}

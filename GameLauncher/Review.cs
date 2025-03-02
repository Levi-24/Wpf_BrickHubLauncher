namespace GameLauncher
{
    public class Review
    {
        public string Name { get; set; }
        public int Rating { get; set; }
        public string ReviewTitle { get; set; }
        public string ReviewText { get; set; }

        public Review(string name, int rating, string reviewTitle, string reviewText)
        {
            Name = name;
            Rating = rating;
            ReviewTitle = reviewTitle;
            ReviewText = reviewText;
        }
    }
}

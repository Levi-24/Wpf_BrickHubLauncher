namespace GameLauncher
{
    public class ReviewClass
    {
        public string Name { get; set; }
        public int Rating { get; set; }
        public string ReviewTitle { get; set; }
        public string ReviewText { get; set; }

        public ReviewClass(string name, int rating, string reviewTitle, string reviewText)
        {
            Name = name;
            Rating = rating;
            ReviewTitle = reviewTitle;
            ReviewText = reviewText;
        }
    }
}

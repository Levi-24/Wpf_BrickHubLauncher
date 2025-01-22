namespace GameLauncher
{
    internal class Game
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public string DownloadLink { get; set; }
        public string InstallPath { get; set; }
        public string LocalImagePath { get; set; }
        public DateTime ReleaseDate { get; set; }

        public Game(int id, string name, string description, string imageUrl, string downloadLink, string localImagePath, DateTime releaseDate)
        {
            Id = id;
            Name = name;
            Description = description;
            ImageUrl = imageUrl;
            DownloadLink = downloadLink;
            LocalImagePath = localImagePath;
            ReleaseDate = releaseDate;
        }
    }
}

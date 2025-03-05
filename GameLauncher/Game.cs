using System.Net.Http;
using System.Text.Json.Serialization;

namespace GameLauncher
{
    internal class Game
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ExeName { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public string DownloadLink { get; set; }
        public string InstallPath { get; set; }
        public string LocalImagePath { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string DeveloperName { get; set; }
        public string PublisherName { get; set; }
        public int PlayTime { get; set; }
        public double Rating { get; set; }
        public string ExecutablePath { get; set; }
        public bool IsDownloadLinkValid { get; set; }

        //For storing game data in collection
        public Game(int id, string name, string exeName, string description, string imageUrl, string downloadLink, string localImagePath, DateTime releaseDate, string developerName, string publisherName, int playTime, double rating)
        {
            Id = id;
            Name = name;
            ExeName = exeName;
            Description = description;
            ImageUrl = imageUrl;
            DownloadLink = downloadLink;
            LocalImagePath = localImagePath;
            ReleaseDate = releaseDate;
            DeveloperName = developerName;
            PublisherName = publisherName;
            PlayTime = playTime;
            Rating = rating;
            IsDownloadLinkValid = IsLinkValid(DownloadLink);
        }

        //For saving executable path in json file
        [JsonConstructor]
        public Game(int id, string exeName, string executablePath)
        {
            Id = id;
            ExeName = exeName;
            ExecutablePath = executablePath;
        }

        private static bool IsLinkValid(string url)
        {
            using HttpClient client = new();
            try
            {
                // Make a HEAD request to check if the URL is valid
                using var response = client.Send(new HttpRequestMessage(HttpMethod.Head, url));
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}

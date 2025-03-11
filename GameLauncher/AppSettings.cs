using System.IO;

namespace GameLauncher
{
    public static class AppSettings
    {
        public const string DatabaseConnectionString = "database=game_launcher;server=localhost;uid=root;";

        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BrickHub");

        public static readonly string RememberMeToken = Path.Combine(AppDataFolder, "rememberMeToken.txt");
        public static readonly string ImageDirectory = Path.Combine(AppDataFolder, "DownloadedImages");
        public static readonly string GameDirectory = Path.Combine(AppDataFolder, "DownloadedGames");
        public static readonly string InstalledGamesFilePath = Path.Combine(AppDataFolder, "installedGames.json");

        static AppSettings()
        {
            // Ensure directories exist
            Directory.CreateDirectory(AppDataFolder);
            Directory.CreateDirectory(ImageDirectory);
            Directory.CreateDirectory(GameDirectory);
        }
    }
}

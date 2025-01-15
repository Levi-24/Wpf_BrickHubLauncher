using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GameLauncher
{
    public partial class MainWindow : Window
    {
        private string CurrentUser;

        public MainWindow()
        {
            InitializeComponent();
            LoadGames();
        }

        // Constructor with parameters for custom initialization
        public MainWindow(string username) : this() // Call the parameterless constructor
        {
            CurrentUser = username;
        }

        private void LoadGames()
        {
            // Replace with database call to fetch game data
            lstGames.ItemsSource = new List<Game>
            {
                new Game { Name = "Game 1" },
                new Game { Name = "Game 2" },
                new Game { Name = "Game 3" },
            };
        }

        private void Launch_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Launching game...");
            // Add code to start the game executable
        }

        private void DownloadUpdate_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Downloading/Updating game...");
            // Add code to download or update game files
        }

        private void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Uninstalling game...");
            // Add code to uninstall game and update database
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
    }

    public class Game
    {
        public string Name { get; set; }
    }


}
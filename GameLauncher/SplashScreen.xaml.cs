using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace GameLauncher
{
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
            Cursor = Cursors.AppStarting;
            favIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/brickhubLogo.png"));
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}

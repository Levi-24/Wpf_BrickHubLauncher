using System.Windows;
using System.Windows.Input;

namespace GameLauncher
{
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
            Cursor = Cursors.AppStarting;
        }
    }
}

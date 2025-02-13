using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GameLauncher
{
    public partial class Review : Window
    {
        public Review(int gameID)
        {
            int currentGameId = gameID;
            InitializeComponent();
            lblGameName.Content = currentGameId;

        }

        private void sldrRating_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lblRating != null && sldrRating != null)
            {
                lblRating.Content = sldrRating.Value;
            }
        }
    }
}

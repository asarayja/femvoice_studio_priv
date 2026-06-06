using System.Windows;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// Window for displaying detailed training session information for a specific day.
    /// </summary>
    public partial class DayDetailsWindow : Window
    {
        public DayDetailsWindow()
        {
            InitializeComponent();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

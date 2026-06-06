using System.Windows;
using System.Windows.Controls;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// Code-behind for ExerciseSummaryView
    /// </summary>
    public partial class ExerciseSummaryView : UserControl
    {
        public ExerciseSummaryView()
        {
            InitializeComponent();
        }
        
        private void OnContinueClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is ExerciseSummaryViewModel vm)
            {
                vm.ContinueCommand.Execute(null);
            }
        }
        
        private void OnFinishClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is ExerciseSummaryViewModel vm)
            {
                vm.FinishCommand.Execute(null);
            }
        }
    }
}

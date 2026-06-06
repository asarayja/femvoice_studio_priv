using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FemVoiceStudio.ViewModels;

namespace FemVoiceStudio.Views
{
    /// <summary>
    /// Calendar window for viewing training history.
    /// </summary>
    public partial class CalendarWindow : Window
    {
        private readonly CalendarViewModel _viewModel;
        
        public CalendarWindow()
        {
            InitializeComponent();
            
            _viewModel = new CalendarViewModel();
            DataContext = _viewModel;
            
            // Bind data
            CalendarGrid.ItemsSource = _viewModel.Days;
            MonthYearText.Text = _viewModel.MonthYearText;
            
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CalendarViewModel.MonthYearText))
                {
                    MonthYearText.Text = _viewModel.MonthYearText;
                }
            };
        }
        
        private void OnPreviousMonth(object sender, RoutedEventArgs e)
        {
            _viewModel.PreviousMonth();
            CalendarGrid.ItemsSource = null;
            CalendarGrid.ItemsSource = _viewModel.Days;
        }
        
        private void OnNextMonth(object sender, RoutedEventArgs e)
        {
            _viewModel.NextMonth();
            CalendarGrid.ItemsSource = null;
            CalendarGrid.ItemsSource = _viewModel.Days;
        }
        
        private void OnGoToToday(object sender, RoutedEventArgs e)
        {
            _viewModel.GoToToday();
            CalendarGrid.ItemsSource = null;
            CalendarGrid.ItemsSource = _viewModel.Days;
        }
        
        private void OnDayClicked(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is CalendarDay day)
            {
                _viewModel.OnDayClicked(day);
            }
        }
    }
}

// [v0.1: MilestonesWindow] Code-behind for MilestonesWindow
using System.Windows;
using IdleWork.App.ViewModels;

namespace IdleWork.App.Views
{
    public partial class MilestonesWindow : Window
    {
        public MilestonesWindow()
        {
            InitializeComponent();
            DataContext = new MilestonesViewModel();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

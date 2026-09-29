// [v0.2: RulesManagerView] Code-behind for RulesManagerView with automatic single-row selection edit syncing
using System.Windows.Controls;
using IdleWork.App.ViewModels;

namespace IdleWork.App.Views
{
    public partial class RulesManagerView : UserControl
    {
        public RulesManagerView()
        {
            InitializeComponent();
        }

        private void RulesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is RulesManagerViewModel vm && sender is ListView lv)
            {
                vm.OnRuleSelectionChanged(lv.SelectedItems);
            }
        }
    }
}

// [v0.2: TimelineView] Code-behind for TimelineView with modal dialog launchers
using System.Windows;
using System.Windows.Controls;
using IdleWork.App.ViewModels;

namespace IdleWork.App.Views
{
    public partial class TimelineView : UserControl
    {
        public TimelineView()
        {
            InitializeComponent();
            DataContextChanged += TimelineView_DataContextChanged;
        }

        private void TimelineView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is TimelineViewModel oldVm)
            {
                oldVm.RequestCreateNew -= Vm_RequestCreateNew;
            }
            if (e.NewValue is TimelineViewModel newVm)
            {
                newVm.RequestCreateNew += Vm_RequestCreateNew;
            }
        }

        private async void Vm_RequestCreateNew(object? sender, bool isRule)
        {
            if (DataContext is not TimelineViewModel vm) return;

            var window = Window.GetWindow(this);

            if (isRule)
            {
                var ruleDialog = new NewRuleDialog(vm.SelectedActivity, vm.AvailableProjects)
                {
                    Owner = window
                };

                if (ruleDialog.ShowDialog() == true && ruleDialog.CreatedRule != null)
                {
                    await vm.OnNewRuleCreatedAsync(ruleDialog.CreatedRule);
                }
                else
                {
                    vm.CancelCreation();
                }
            }
            else
            {
                var projectDialog = new NewProjectDialog(null, vm.CachedRules)
                {
                    Owner = window
                };

                if (projectDialog.ShowDialog() == true && projectDialog.CreatedProject != null)
                {
                    await vm.OnNewProjectCreatedAsync(projectDialog.CreatedProject, projectDialog.CorrelatedRules);
                }
                else
                {
                    vm.CancelCreation();
                }
            }
        }
    }
}

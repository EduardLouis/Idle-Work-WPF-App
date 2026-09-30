// [v0.003: ProjectsView] Code-behind for ProjectsView UserControl with NewRuleDialog integration
using System.Windows;
using System.Windows.Controls;
using IdleWork.App.ViewModels;

namespace IdleWork.App.Views
{
    public partial class ProjectsView : UserControl
    {
        public ProjectsView()
        {
            InitializeComponent();
            DataContextChanged += ProjectsView_DataContextChanged;
        }

        private void ProjectsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ProjectsViewModel oldVm)
            {
                oldVm.RequestCreateNewRule -= Vm_RequestCreateNewRule;
            }
            if (e.NewValue is ProjectsViewModel newVm)
            {
                newVm.RequestCreateNewRule += Vm_RequestCreateNewRule;
            }
        }

        private void Vm_RequestCreateNewRule(object? sender, System.EventArgs e)
        {
            if (DataContext is not ProjectsViewModel vm) return;

            var window = Window.GetWindow(this);
            var dlg = new NewRuleDialog(null, vm.Projects, vm.Categories, vm.Tags, vm.ProjectName)
            {
                Owner = window
            };

            if (dlg.ShowDialog() == true && dlg.CreatedRule != null)
            {
                // [v0.003: NewProjectOnTheFly] Sync newly created project to project view collection
                if (dlg.NewlyCreatedProject != null && !vm.Projects.Any(p => p.Name == dlg.NewlyCreatedProject.Name))
                {
                    vm.Projects.Add(dlg.NewlyCreatedProject);
                }
                vm.OnNewRuleCreated(dlg.CreatedRule);
            }
        }
    }
}

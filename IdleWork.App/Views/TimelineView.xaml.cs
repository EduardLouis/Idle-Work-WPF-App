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
                oldVm.RequestPreviewScreenshot -= Vm_RequestPreviewScreenshot;
            }
            if (e.NewValue is TimelineViewModel newVm)
            {
                newVm.RequestCreateNew += Vm_RequestCreateNew;
                newVm.RequestPreviewScreenshot += Vm_RequestPreviewScreenshot;
            }
        }

        private void Vm_RequestPreviewScreenshot(object? sender, Core.Models.ActivityTimeSpan act)
        {
            if (act == null || string.IsNullOrEmpty(act.ScreenshotPath) || !System.IO.File.Exists(act.ScreenshotPath))
                return;

            var window = Window.GetWindow(this);
            var dialog = new ScreenshotPreviewDialog(
                act.ScreenshotPath,
                act.DisplayAppDescription,
                act.WindowTitle,
                act.StartTime)
            {
                Owner = window
            };
            dialog.ShowDialog();
        }

        private async void Vm_RequestCreateNew(object? sender, bool isRule)
        {
            if (DataContext is not TimelineViewModel vm) return;

            var window = Window.GetWindow(this);

            if (isRule)
            {
                var ruleDialog = new NewRuleDialog(vm.SelectedActivity, vm.AvailableProjects, vm.CachedCategories, vm.CachedTags)
                {
                    Owner = window
                };

                if (ruleDialog.ShowDialog() == true && ruleDialog.CreatedRule != null)
                {
                    // [v0.003: NewProjectOnTheFly] Sync newly created project to timeline cache
                    if (ruleDialog.NewlyCreatedProject != null && !vm.AvailableProjects.Any(p => p.Name == ruleDialog.NewlyCreatedProject.Name))
                    {
                        vm.AvailableProjects.Add(ruleDialog.NewlyCreatedProject);
                    }
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

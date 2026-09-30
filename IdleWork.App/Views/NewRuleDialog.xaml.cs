// [v0.003: NewRuleDialog] Code-behind for NewRuleDialog with on-the-fly project creation
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using IdleWork.App.Core.Models;
using IdleWork.App.Core.Services;

namespace IdleWork.App.Views
{
    public partial class NewRuleDialog : Window
    {
        private const string CreateNewProjectPlaceholder = "+ Add New Project...";

        public AutoTagRule? CreatedRule { get; private set; }
        public Project? NewlyCreatedProject { get; private set; }

        private readonly ObservableCollection<string> _projectNames = new ObservableCollection<string>();
        private string? _previousSelectedProject;

        public NewRuleDialog(
            ActivityTimeSpan? contextActivity,
            IEnumerable<Project> availableProjects,
            IEnumerable<WorkCategory>? availableCategories = null,
            IEnumerable<WorkTag>? availableTags = null,
            string? preselectedProjectName = null)
        {
            InitializeComponent();

            foreach (var p in availableProjects)
            {
                if (!string.IsNullOrWhiteSpace(p.Name) && !_projectNames.Contains(p.Name))
                {
                    _projectNames.Add(p.Name);
                }
            }

            if (!string.IsNullOrWhiteSpace(preselectedProjectName) && !_projectNames.Contains(preselectedProjectName))
            {
                _projectNames.Insert(0, preselectedProjectName);
            }

            // Append the quick-create placeholder at the bottom of the list
            _projectNames.Add(CreateNewProjectPlaceholder);
            ProjectsComboBox.ItemsSource = _projectNames;

            var categoryNames = availableCategories != null && availableCategories.Any()
                ? availableCategories.Select(c => c.Name).ToList()
                : new List<string> { "BIM", "Development", "Design", "Admin", "Meeting", "Research", "QA/QC" };
            CategoryComboBox.ItemsSource = categoryNames;

            var tagNames = availableTags != null && availableTags.Any()
                ? availableTags.Select(t => t.Name).ToList()
                : new List<string> { "Revit", "AutoCAD", "3D", "2D", "Teams", "Email", "Code", "Billable", "Internal", "Documentation" };
            TagsComboBox.ItemsSource = tagNames;

            if (contextActivity != null)
            {
                ProcessTextBox.Text = contextActivity.ProcessName ?? "";
                TitlePatternTextBox.Text = !string.IsNullOrEmpty(contextActivity.DocumentName)
                    ? contextActivity.DocumentName
                    : contextActivity.WindowTitle ?? "";

                RuleNameTextBox.Text = !string.IsNullOrEmpty(contextActivity.DocumentName)
                    ? $"{contextActivity.ProcessName} - {contextActivity.DocumentName}"
                    : $"{contextActivity.ProcessName} Rule";

                string? targetPrj = !string.IsNullOrEmpty(preselectedProjectName)
                    ? preselectedProjectName
                    : contextActivity.ProjectName;

                if (!string.IsNullOrEmpty(targetPrj) && _projectNames.Contains(targetPrj))
                {
                    ProjectsComboBox.SelectedItem = targetPrj;
                    _previousSelectedProject = targetPrj;
                }
                else if (_projectNames.Count > 1)
                {
                    ProjectsComboBox.SelectedIndex = 0;
                    _previousSelectedProject = _projectNames[0];
                }

                if (!string.IsNullOrEmpty(contextActivity.Category))
                {
                    CategoryComboBox.Text = contextActivity.Category;
                }
                else if (categoryNames.Count > 0)
                {
                    CategoryComboBox.SelectedIndex = 0;
                }

                if (!string.IsNullOrEmpty(contextActivity.Tags))
                {
                    TagsComboBox.Text = contextActivity.Tags;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(preselectedProjectName) && _projectNames.Contains(preselectedProjectName))
                {
                    ProjectsComboBox.SelectedItem = preselectedProjectName;
                    _previousSelectedProject = preselectedProjectName;
                    RuleNameTextBox.Text = $"{preselectedProjectName} Rule";
                }
                else if (_projectNames.Count > 1)
                {
                    ProjectsComboBox.SelectedIndex = 0;
                    _previousSelectedProject = _projectNames[0];
                }

                if (categoryNames.Count > 0)
                    CategoryComboBox.SelectedIndex = 0;
            }

            Loaded += (s, e) =>
            {
                RuleNameTextBox.Focus();
                if (!string.IsNullOrEmpty(RuleNameTextBox.Text))
                {
                    RuleNameTextBox.SelectAll();
                }
            };
        }

        private async void NewProject_Click(object sender, RoutedEventArgs e)
        {
            await PromptCreateNewProjectAsync();
        }

        private async void ProjectsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = ProjectsComboBox.SelectedItem as string;
            if (selected == CreateNewProjectPlaceholder)
            {
                await PromptCreateNewProjectAsync();
            }
            else if (!string.IsNullOrEmpty(selected))
            {
                _previousSelectedProject = selected;
            }
        }

        private async Task PromptCreateNewProjectAsync()
        {
            var dlg = new NewProjectDialog(null, null)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true && dlg.CreatedProject != null)
            {
                var newProj = dlg.CreatedProject;
                NewlyCreatedProject = newProj;

                // Persist new project to SQLite database
                await DatabaseService.Instance.SaveProjectAsync(newProj).ConfigureAwait(true);

                // Persist any correlated rules defined inside project dialog
                if (dlg.CorrelatedRules.Count > 0)
                {
                    foreach (var rule in dlg.CorrelatedRules)
                    {
                        rule.TargetProject = newProj.Name;
                        await DatabaseService.Instance.SaveRuleAsync(rule).ConfigureAwait(true);
                    }
                }

                // Insert into dropdown list before the placeholder
                int insertIdx = _projectNames.IndexOf(CreateNewProjectPlaceholder);
                if (insertIdx < 0) insertIdx = _projectNames.Count;

                if (!_projectNames.Contains(newProj.Name))
                {
                    _projectNames.Insert(insertIdx, newProj.Name);
                }

                _previousSelectedProject = newProj.Name;
                ProjectsComboBox.SelectedItem = newProj.Name;
            }
            else
            {
                // Revert selection if user canceled out of quick create
                if (ProjectsComboBox.SelectedItem as string == CreateNewProjectPlaceholder)
                {
                    ProjectsComboBox.SelectedItem = _previousSelectedProject 
                        ?? _projectNames.FirstOrDefault(p => p != CreateNewProjectPlaceholder);
                }
            }
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            string name = RuleNameTextBox.Text?.Trim() ?? "";
            string process = ProcessTextBox.Text?.Trim() ?? "";
            string targetProject = (ProjectsComboBox.SelectedItem as string ?? ProjectsComboBox.Text)?.Trim() ?? "";
            string targetCategory = (CategoryComboBox.SelectedItem as string ?? CategoryComboBox.Text)?.Trim() ?? "";
            string targetTags = (TagsComboBox.SelectedItem as string ?? TagsComboBox.Text)?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please enter a rule name.", "Rule Name Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                RuleNameTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(targetProject) || targetProject == CreateNewProjectPlaceholder)
            {
                MessageBox.Show("Please select or create a valid target project.", "Target Project Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                ProjectsComboBox.Focus();
                return;
            }

            CreatedRule = new AutoTagRule
            {
                RuleName = name,
                ProcessFilter = string.IsNullOrWhiteSpace(process) ? null : process,
                TitlePattern = string.IsNullOrWhiteSpace(TitlePatternTextBox.Text) ? null : TitlePatternTextBox.Text.Trim(),
                TargetProject = targetProject,
                TargetCategory = string.IsNullOrWhiteSpace(targetCategory) ? null : targetCategory,
                TargetTags = string.IsNullOrWhiteSpace(targetTags) ? null : targetTags,
                IsEnabled = true,
                Priority = 50
            };

            DialogResult = true;
            Close();
        }
    }
}

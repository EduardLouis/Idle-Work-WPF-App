// [v0.2: NewRuleDialog] Code-behind for NewRuleDialog
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using IdleWork.App.Core.Models;

namespace IdleWork.App.Views
{
    public partial class NewRuleDialog : Window
    {
        public AutoTagRule? CreatedRule { get; private set; }

        public NewRuleDialog(ActivityTimeSpan? contextActivity, IEnumerable<Project> availableProjects, IEnumerable<WorkCategory>? availableCategories = null, IEnumerable<WorkTag>? availableTags = null)
        {
            InitializeComponent();

            var projectNames = availableProjects.Select(p => p.Name).ToList();
            ProjectsComboBox.ItemsSource = projectNames;

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

                if (!string.IsNullOrEmpty(contextActivity.ProjectName) && projectNames.Contains(contextActivity.ProjectName))
                {
                    ProjectsComboBox.SelectedItem = contextActivity.ProjectName;
                }
                else if (projectNames.Count > 0)
                {
                    ProjectsComboBox.SelectedIndex = 0;
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
                if (projectNames.Count > 0)
                    ProjectsComboBox.SelectedIndex = 0;
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

            if (string.IsNullOrWhiteSpace(targetProject))
            {
                MessageBox.Show("Please select or enter a target project.", "Target Project Required", MessageBoxButton.OK, MessageBoxImage.Warning);
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

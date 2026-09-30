// [v0.2: NewProjectDialog] Code-behind for NewProjectDialog with rule correlation support
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using IdleWork.App.Core.Models;

namespace IdleWork.App.Views
{
    public partial class NewProjectDialog : Window
    {
        public Project? CreatedProject { get; private set; }
        public List<AutoTagRule> CorrelatedRules { get; } = new List<AutoTagRule>();
        public ObservableCollection<SelectableRule> Rules { get; } = new ObservableCollection<SelectableRule>();

        public NewProjectDialog(string? initialName = null, IEnumerable<AutoTagRule>? existingRules = null)
        {
            InitializeComponent();

            if (existingRules != null)
            {
                foreach (var r in existingRules)
                {
                    var item = new SelectableRule(r, isSelected: false);
                    item.SelectionChanged += (s, e) => UpdateRulesSummary();
                    Rules.Add(item);
                }
            }
            RulesItemsControl.ItemsSource = Rules;
            UpdateRulesSummary();

            if (!string.IsNullOrWhiteSpace(initialName))
            {
                NameTextBox.Text = initialName.Trim();
            }

            Loaded += (s, e) =>
            {
                NameTextBox.Focus();
                if (!string.IsNullOrEmpty(NameTextBox.Text))
                {
                    NameTextBox.SelectAll();
                }
            };
        }

        private void UpdateRulesSummary()
        {
            var selected = Rules.Where(r => r.IsSelected).ToList();
            if (selected.Count == 0)
                RulesSummaryTextBlock.Text = "No rules linked (Click to select)";
            else if (selected.Count == 1)
                RulesSummaryTextBlock.Text = $"1 rule linked: {selected[0].RuleName}";
            else if (selected.Count <= 2)
                RulesSummaryTextBlock.Text = $"{selected.Count} rules linked: {string.Join(", ", selected.Select(r => r.RuleName))}";
            else
                RulesSummaryTextBlock.Text = $"{selected.Count} rules linked: {selected[0].RuleName}, {selected[1].RuleName}...";
        }

        private void SelectAllRules_Click(object sender, RoutedEventArgs e)
        {
            foreach (var r in Rules) r.IsSelected = true;
            UpdateRulesSummary();
        }

        private void ClearAllRules_Click(object sender, RoutedEventArgs e)
        {
            foreach (var r in Rules) r.IsSelected = false;
            UpdateRulesSummary();
        }

        private async void AddNewRule_Click(object sender, RoutedEventArgs e)
        {
            var db = IdleWork.App.Core.Services.DatabaseService.Instance;
            var projects = await db.GetProjectsAsync();
            var categories = await db.GetCategoriesAsync();
            var tags = await db.GetTagsAsync();

            string prjName = NameTextBox.Text?.Trim() ?? "New Project";
            var dlg = new NewRuleDialog(null, projects, categories, tags, prjName)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true && dlg.CreatedRule != null)
            {
                var item = new SelectableRule(dlg.CreatedRule, isSelected: true, isNew: true);
                item.SelectionChanged += (s, ev) => UpdateRulesSummary();
                Rules.Add(item);
                UpdateRulesSummary();
            }
        }

        private void ColorChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                ColorTextBox.Text = hex;
            }
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            string name = NameTextBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please enter a project name.", "Project Name Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameTextBox.Focus();
                return;
            }

            CreatedProject = new Project
            {
                Name = name,
                Code = CodeTextBox.Text?.Trim() ?? "",
                ColorHex = string.IsNullOrWhiteSpace(ColorTextBox.Text) ? "#3B82F6" : ColorTextBox.Text.Trim(),
                Description = DescTextBox.Text?.Trim() ?? "",
                IsActive = true
            };

            CorrelatedRules.Clear();
            foreach (var ruleItem in Rules)
            {
                if (ruleItem.IsSelected)
                {
                    ruleItem.Rule.TargetProject = name;
                    CorrelatedRules.Add(ruleItem.Rule);
                }
            }

            DialogResult = true;
            Close();
        }
    }
}

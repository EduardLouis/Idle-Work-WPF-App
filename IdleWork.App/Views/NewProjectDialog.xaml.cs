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
                    Rules.Add(new SelectableRule(r, isSelected: false));
                }
            }
            RulesItemsControl.ItemsSource = Rules;

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

        private void ColorChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                ColorTextBox.Text = hex;
            }
        }

        private void AddQuickRule_Click(object sender, RoutedEventArgs e)
        {
            string proc = QuickRuleProcessTextBox.Text?.Trim() ?? "";
            string title = QuickRuleTitleTextBox.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(proc) && string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Please enter an application process or title keyword for the rule.", "Rule Info Needed", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string prj = !string.IsNullOrWhiteSpace(NameTextBox.Text) ? NameTextBox.Text.Trim() : "Project";
            string ruleName = !string.IsNullOrWhiteSpace(proc) ? $"{prj} - {proc}" : $"{prj} - {title}";

            var newRule = new AutoTagRule
            {
                RuleName = ruleName,
                ProcessFilter = string.IsNullOrWhiteSpace(proc) ? null : proc,
                TitlePattern = string.IsNullOrWhiteSpace(title) ? null : title,
                TargetProject = prj,
                IsEnabled = true,
                Priority = 50
            };

            Rules.Add(new SelectableRule(newRule, isSelected: true, isNew: true));
            QuickRuleProcessTextBox.Text = "";
            QuickRuleTitleTextBox.Text = "";
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

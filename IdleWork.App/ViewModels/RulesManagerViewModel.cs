// [v0.2: RulesManagerVM] Smart classification rules & Software Priority ranking manager
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using IdleWork.App.Core.Models;
using IdleWork.App.Core.Services;

namespace IdleWork.App.ViewModels
{
    public class RulesManagerViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly RuleClassifierService _classifierService;
        private readonly ActivityAggregator? _aggregator;

        private AutoTagRule? _selectedRule;
        private int _editingRuleId = 0;
        private string _newRuleName = "";
        private string _newProcessFilter = "";
        private string _newTitlePattern = "";
        private string _newTargetProject = "";
        private string _newTargetCategory = "";
        private string _newTargetTags = "";
        private string _statusMessage = "";

        // Software Priority fields
        private SoftwarePriority? _selectedPriority;
        private string _newPrioProcess = "";
        private string _newPrioDisplayName = "";

        public ObservableCollection<AutoTagRule> Rules { get; } = new ObservableCollection<AutoTagRule>();
        public ObservableCollection<SoftwarePriority> SoftwarePriorities { get; } = new ObservableCollection<SoftwarePriority>();
        public ObservableCollection<Project> AvailableProjects { get; } = new ObservableCollection<Project>();

        public AutoTagRule? SelectedRule
        {
            get => _selectedRule;
            set
            {
                if (SetProperty(ref _selectedRule, value))
                {
                    if (value != null)
                    {
                        // [v0.2: SmartRulesAutoEdit] Selecting a rule automatically enters Update mode
                        _editingRuleId = value.Id;
                        LoadRuleIntoForm(value);
                        OnPropertyChanged(nameof(SaveButtonText));
                        StatusMessage = $"Selected rule: {value.RuleName}";
                    }
                    else
                    {
                        _editingRuleId = 0;
                        OnPropertyChanged(nameof(SaveButtonText));
                    }
                }
            }
        }

        public SoftwarePriority? SelectedPriority
        {
            get => _selectedPriority;
            set => SetProperty(ref _selectedPriority, value);
        }

        public string NewRuleName
        {
            get => _newRuleName;
            set => SetProperty(ref _newRuleName, value);
        }

        public string NewProcessFilter
        {
            get => _newProcessFilter;
            set => SetProperty(ref _newProcessFilter, value);
        }

        public string NewTitlePattern
        {
            get => _newTitlePattern;
            set => SetProperty(ref _newTitlePattern, value);
        }

        public string NewTargetProject
        {
            get => _newTargetProject;
            set => SetProperty(ref _newTargetProject, value);
        }

        public string NewTargetCategory
        {
            get => _newTargetCategory;
            set => SetProperty(ref _newTargetCategory, value);
        }

        public string NewTargetTags
        {
            get => _newTargetTags;
            set => SetProperty(ref _newTargetTags, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // [v0.2: SmartRulesAutoEdit] Button text transitions to Update Rule when a single row is selected
        public string SaveButtonText => _editingRuleId == 0 ? "+ Add Persistent Rule" : "💾 Update Rule";

        public string NewPrioProcess
        {
            get => _newPrioProcess;
            set => SetProperty(ref _newPrioProcess, value);
        }

        public string NewPrioDisplayName
        {
            get => _newPrioDisplayName;
            set => SetProperty(ref _newPrioDisplayName, value);
        }

        // [v0.2: SmartRulesMultiSelect] Commands for Rules with multi-selection support
        public ICommand SaveRuleCommand { get; }
        public ICommand EditRuleCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand DuplicateRuleCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand DeleteRuleCommand { get; }
        public ICommand ToggleEnableCommand { get; }
        public ICommand EnableSelectedCommand { get; }
        public ICommand DisableSelectedCommand { get; }
        public ICommand RunRetroactiveBatchCommand { get; }
        public ICommand RefreshCommand { get; }

        // Commands for Software Priorities
        public ICommand AddPriorityCommand { get; }
        public ICommand DeletePriorityCommand { get; }
        public ICommand MovePrioUpCommand { get; }
        public ICommand MovePrioDownCommand { get; }

        private bool _isBatchUpdating = false;

        public RulesManagerViewModel(
            DatabaseService databaseService,
            RuleClassifierService classifierService,
            ActivityAggregator? aggregator = null)
        {
            _databaseService = databaseService;
            _classifierService = classifierService;
            _aggregator = aggregator;

            // [v0.2: SmartRulesMultiSelect] Commands accept IList parameter from ListView.SelectedItems
            SaveRuleCommand = new RelayCommand(async () => await SaveRuleAsync());
            EditRuleCommand = new RelayCommand((param) => EditSelectedRule(param as IList));
            CancelEditCommand = new RelayCommand(ClearForm);
            DuplicateRuleCommand = new RelayCommand(async (param) => await DuplicateSelectedRulesAsync(param as IList));
            MoveUpCommand = new RelayCommand(async (param) => await MoveRulesUpAsync(param as IList));
            MoveDownCommand = new RelayCommand(async (param) => await MoveRulesDownAsync(param as IList));
            DeleteRuleCommand = new RelayCommand(async (param) => await DeleteSelectedRulesAsync(param as IList));
            ToggleEnableCommand = new RelayCommand(async (param) => await ToggleSelectedRulesEnabledAsync(param as IList));
            EnableSelectedCommand = new RelayCommand(async (param) => await SetSelectedRulesEnabledAsync(true, param as IList));
            DisableSelectedCommand = new RelayCommand(async (param) => await SetSelectedRulesEnabledAsync(false, param as IList));
            RunRetroactiveBatchCommand = new RelayCommand(async () => await RunRetroactiveBatchAsync());
            RefreshCommand = new RelayCommand(RefreshAll);

            AddPriorityCommand = new RelayCommand(async () => await AddPriorityAsync());
            DeletePriorityCommand = new RelayCommand(async (param) => await DeletePriorityAsync(param as IList));
            MovePrioUpCommand = new RelayCommand(async (param) => await MovePrioUpAsync(param as IList));
            MovePrioDownCommand = new RelayCommand(async (param) => await MovePrioDownAsync(param as IList));

            RefreshAll();
        }

        public void RefreshAll()
        {
            LoadRulesAsync();
            LoadPrioritiesAsync();
            LoadProjectsAsync();
        }

        private void SubscribeRule(AutoTagRule rule)
        {
            rule.PropertyChanged += OnRulePropertyChanged;
        }

        private void UnsubscribeRule(AutoTagRule rule)
        {
            rule.PropertyChanged -= OnRulePropertyChanged;
        }

        private async void OnRulePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // [v0.2: SmartRulesToggle] Direct row CheckBox toggle syncs with DB and reloads classifier
            if (e.PropertyName == nameof(AutoTagRule.IsEnabled) && sender is AutoTagRule rule && !_isBatchUpdating)
            {
                await _databaseService.SaveRuleAsync(rule);
                await _classifierService.RefreshRulesAsync();
                StatusMessage = $"Rule '{rule.RuleName}' is now {(rule.IsEnabled ? "Enabled" : "Disabled")}.";
            }
        }

        public async void LoadRulesAsync()
        {
            foreach (var r in Rules)
                UnsubscribeRule(r);

            var rules = await _databaseService.GetRulesAsync();
            Rules.Clear();
            foreach (var r in rules)
            {
                SubscribeRule(r);
                Rules.Add(r);
            }
        }

        public async void LoadPrioritiesAsync()
        {
            var prios = await _databaseService.GetSoftwarePrioritiesAsync();
            SoftwarePriorities.Clear();
            foreach (var p in prios)
                SoftwarePriorities.Add(p);
            RenumberPrioritiesInCollection();
        }

        public async void LoadProjectsAsync()
        {
            var projects = await _databaseService.GetProjectsAsync();
            AvailableProjects.Clear();
            foreach (var p in projects)
                AvailableProjects.Add(p);

            if (string.IsNullOrEmpty(NewTargetProject) && AvailableProjects.Count > 0)
                NewTargetProject = AvailableProjects[0].Name;
        }

        private void LoadRuleIntoForm(AutoTagRule rule)
        {
            NewRuleName = rule.RuleName ?? "";
            NewProcessFilter = rule.ProcessFilter ?? "";
            NewTitlePattern = rule.TitlePattern ?? "";
            NewTargetProject = rule.TargetProject ?? "";
            NewTargetCategory = rule.TargetCategory ?? "";
            NewTargetTags = rule.TargetTags ?? "";
        }

        private List<AutoTagRule> GetSelectedRules(IList? selectedItems)
        {
            if (selectedItems != null && selectedItems.Count > 0)
            {
                return selectedItems.Cast<AutoTagRule>().ToList();
            }
            return SelectedRule != null ? new List<AutoTagRule> { SelectedRule } : new List<AutoTagRule>();
        }

        private void EditSelectedRule(IList? selectedItems = null)
        {
            var selected = GetSelectedRules(selectedItems);
            var target = selected.FirstOrDefault() ?? SelectedRule;
            if (target == null)
            {
                StatusMessage = "Please select a rule from the list to edit.";
                return;
            }

            _editingRuleId = target.Id;
            LoadRuleIntoForm(target);
            OnPropertyChanged(nameof(SaveButtonText));
            StatusMessage = selected.Count > 1 
                ? $"Editing rule: {target.RuleName} (1 of {selected.Count} selected)"
                : $"Editing rule: {target.RuleName}";
        }

        // [v0.2: SmartRulesAutoEdit] Switch to update mode if exactly 1 row is selected; batch mode if multiple rows
        public void OnRuleSelectionChanged(IList? selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0)
            {
                _editingRuleId = 0;
                OnPropertyChanged(nameof(SaveButtonText));
            }
            else if (selectedItems.Count == 1)
            {
                var rule = selectedItems[0] as AutoTagRule;
                if (rule != null)
                {
                    _editingRuleId = rule.Id;
                    _selectedRule = rule;
                    OnPropertyChanged(nameof(SelectedRule));
                    LoadRuleIntoForm(rule);
                    OnPropertyChanged(nameof(SaveButtonText));
                    StatusMessage = $"Selected rule: {rule.RuleName}";
                }
            }
            else
            {
                // Multi-selection: batch mode, not single edit mode
                _editingRuleId = 0;
                OnPropertyChanged(nameof(SaveButtonText));
                StatusMessage = $"{selectedItems.Count} rules selected for batch actions.";
            }
        }

        private void ClearForm()
        {
            _editingRuleId = 0;
            _selectedRule = null;
            OnPropertyChanged(nameof(SelectedRule));
            NewRuleName = "";
            NewProcessFilter = "";
            NewTitlePattern = "";
            NewTargetCategory = "";
            NewTargetTags = "";
            OnPropertyChanged(nameof(SaveButtonText));
            StatusMessage = "";
        }

        private async Task SaveRuleAsync()
        {
            if (string.IsNullOrWhiteSpace(NewRuleName))
            {
                StatusMessage = "Rule name cannot be empty.";
                return;
            }

            if (string.IsNullOrWhiteSpace(NewProcessFilter) && string.IsNullOrWhiteSpace(NewTitlePattern))
            {
                StatusMessage = "Provide either an application process or a window title pattern.";
                return;
            }

            if (_editingRuleId != 0)
            {
                // Update existing rule
                var existing = Rules.FirstOrDefault(r => r.Id == _editingRuleId);
                if (existing != null)
                {
                    existing.RuleName = NewRuleName;
                    existing.ProcessFilter = NewProcessFilter;
                    existing.TitlePattern = NewTitlePattern;
                    existing.TargetProject = NewTargetProject;
                    existing.TargetCategory = NewTargetCategory;
                    existing.TargetTags = NewTargetTags;

                    await _databaseService.SaveRuleAsync(existing);
                    await _classifierService.RefreshRulesAsync();
                    StatusMessage = $"Updated rule: {existing.RuleName}";
                }
                ClearForm();
            }
            else
            {
                // Create new rule
                var newRule = new AutoTagRule
                {
                    RuleName = NewRuleName,
                    ProcessFilter = NewProcessFilter,
                    TitlePattern = NewTitlePattern,
                    TargetProject = string.IsNullOrWhiteSpace(NewTargetProject) ? "General Work" : NewTargetProject,
                    TargetCategory = NewTargetCategory,
                    TargetTags = NewTargetTags,
                    Priority = (Rules.Count + 1) * 10,
                    IsEnabled = true
                };

                await _databaseService.SaveRuleAsync(newRule);
                await _classifierService.RefreshRulesAsync();
                SubscribeRule(newRule);
                Rules.Add(newRule);
                StatusMessage = $"Created rule: {newRule.RuleName}";
                ClearForm();
            }
        }

        // [v0.2: SmartRulesMultiSelect] Duplicate all selected rules
        public async Task DuplicateSelectedRulesAsync(IList? selectedItems = null)
        {
            var selected = GetSelectedRules(selectedItems);
            if (selected.Count == 0)
            {
                StatusMessage = "Select at least one rule to duplicate.";
                return;
            }

            int count = 0;
            foreach (var rule in selected)
            {
                var duplicate = new AutoTagRule
                {
                    RuleName = $"{rule.RuleName} (Copy)",
                    ProcessFilter = rule.ProcessFilter,
                    TitlePattern = rule.TitlePattern,
                    TargetProject = rule.TargetProject,
                    TargetCategory = rule.TargetCategory,
                    TargetTags = rule.TargetTags,
                    Priority = rule.Priority + 1,
                    IsEnabled = rule.IsEnabled,
                    IsRegex = rule.IsRegex
                };

                await _databaseService.SaveRuleAsync(duplicate);
                SubscribeRule(duplicate);
                Rules.Add(duplicate);
                count++;
            }

            await _classifierService.RefreshRulesAsync();
            StatusMessage = $"Duplicated {count} rule(s).";
        }

        // [v0.2: SmartRulesMultiSelect] Move selected rules up in priority
        public async Task MoveRulesUpAsync(IList? selectedItems = null)
        {
            var selected = GetSelectedRules(selectedItems);
            if (selected.Count == 0) return;

            var indices = selected.Select(r => Rules.IndexOf(r))
                                  .Where(i => i >= 0)
                                  .OrderBy(i => i)
                                  .ToList();

            if (indices.Count == 0 || indices.First() == 0) return;

            foreach (int idx in indices)
            {
                Rules.Move(idx, idx - 1);
            }

            await _databaseService.SaveRulesOrderAsync(Rules.ToList());
            await _classifierService.RefreshRulesAsync();
            StatusMessage = $"Moved {selected.Count} rule(s) up.";
        }

        // [v0.2: SmartRulesMultiSelect] Move selected rules down in priority
        public async Task MoveRulesDownAsync(IList? selectedItems = null)
        {
            var selected = GetSelectedRules(selectedItems);
            if (selected.Count == 0) return;

            var indices = selected.Select(r => Rules.IndexOf(r))
                                  .Where(i => i >= 0)
                                  .OrderByDescending(i => i)
                                  .ToList();

            if (indices.Count == 0 || indices.First() >= Rules.Count - 1) return;

            foreach (int idx in indices)
            {
                Rules.Move(idx, idx + 1);
            }

            await _databaseService.SaveRulesOrderAsync(Rules.ToList());
            await _classifierService.RefreshRulesAsync();
            StatusMessage = $"Moved {selected.Count} rule(s) down.";
        }

        // [v0.2: SmartRulesMultiSelect] Delete all selected rules
        public async Task DeleteSelectedRulesAsync(IList? selectedItems = null)
        {
            var selected = GetSelectedRules(selectedItems);
            if (selected.Count == 0)
            {
                StatusMessage = "Select at least one rule to delete.";
                return;
            }

            _isBatchUpdating = true;
            try
            {
                int count = selected.Count;
                foreach (var rule in selected)
                {
                    UnsubscribeRule(rule);
                    await _databaseService.DeleteRuleAsync(rule.Id);
                    Rules.Remove(rule);
                }

                await _classifierService.RefreshRulesAsync();
                ClearForm();
                StatusMessage = $"Deleted {count} rule(s).";
            }
            finally
            {
                _isBatchUpdating = false;
            }
        }

        // [v0.2: SmartRulesToggle] Toggle enabled state for all selected rules
        public async Task ToggleSelectedRulesEnabledAsync(IList? selectedItems = null)
        {
            var selected = GetSelectedRules(selectedItems);
            if (selected.Count == 0)
            {
                StatusMessage = "Select at least one rule to toggle.";
                return;
            }

            _isBatchUpdating = true;
            try
            {
                // If all selected rules are enabled, disable them all; otherwise enable them all
                bool targetState = !selected.All(r => r.IsEnabled);
                foreach (var rule in selected)
                {
                    rule.IsEnabled = targetState;
                    await _databaseService.SaveRuleAsync(rule);
                }

                await _classifierService.RefreshRulesAsync();
                StatusMessage = $"{(targetState ? "Enabled" : "Disabled")} {selected.Count} rule(s).";
            }
            finally
            {
                _isBatchUpdating = false;
            }
        }

        public async Task SetSelectedRulesEnabledAsync(bool enabled, IList? selectedItems = null)
        {
            var selected = GetSelectedRules(selectedItems);
            if (selected.Count == 0) return;

            _isBatchUpdating = true;
            try
            {
                foreach (var rule in selected)
                {
                    rule.IsEnabled = enabled;
                    await _databaseService.SaveRuleAsync(rule);
                }
                await _classifierService.RefreshRulesAsync();
                StatusMessage = $"Set {selected.Count} rule(s) enabled = {enabled}.";
            }
            finally
            {
                _isBatchUpdating = false;
            }
        }

        private async Task RunRetroactiveBatchAsync()
        {
            StatusMessage = "Running retroactive batch classification...";
            int totalUpdated = 0;
            var rules = await _databaseService.GetRulesAsync();
            foreach (var rule in rules.Where(r => r.IsEnabled))
            {
                totalUpdated += await _databaseService.ApplyRuleRetroactivelyAsync(rule, DateTime.Today.AddDays(-14));
            }
            StatusMessage = $"Retroactive batch complete. Updated {totalUpdated} past activities.";
        }

        // Software Priority Methods
        private void RenumberPrioritiesInCollection()
        {
            // [v0.2: PrioritySync] Explicitly renumber UI collection and fire property change notifications
            for (int i = 0; i < SoftwarePriorities.Count; i++)
            {
                SoftwarePriorities[i].Priority = i;
            }
        }

        private List<SoftwarePriority> GetSelectedPriorities(IList? selectedItems)
        {
            if (selectedItems != null && selectedItems.Count > 0)
            {
                return selectedItems.Cast<SoftwarePriority>().ToList();
            }
            return SelectedPriority != null ? new List<SoftwarePriority> { SelectedPriority } : new List<SoftwarePriority>();
        }

        private async Task AddPriorityAsync()
        {
            if (string.IsNullOrWhiteSpace(NewPrioProcess))
            {
                StatusMessage = "Application process name is required (e.g. Revit, acad).";
                return;
            }

            var prio = new SoftwarePriority
            {
                ProcessFilter = NewPrioProcess.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(NewPrioDisplayName) ? NewPrioProcess.Trim() : NewPrioDisplayName.Trim(),
                Priority = SoftwarePriorities.Count,
                IsEnabled = true
            };

            await _databaseService.SaveSoftwarePriorityAsync(prio);
            SoftwarePriorities.Add(prio);
            RenumberPrioritiesInCollection();
            NewPrioProcess = "";
            NewPrioDisplayName = "";
            if (_aggregator != null) await _aggregator.RefreshPrioritiesAsync();
            StatusMessage = $"Added priority software: {prio.DisplayName} (Prio {prio.Priority})";
        }

        public async Task DeletePriorityAsync(IList? selectedItems = null)
        {
            var selected = GetSelectedPriorities(selectedItems);
            if (selected.Count == 0) return;

            int count = selected.Count;
            foreach (var p in selected)
            {
                await _databaseService.DeleteSoftwarePriorityAsync(p.Id);
                SoftwarePriorities.Remove(p);
            }

            RenumberPrioritiesInCollection();
            await _databaseService.SaveSoftwarePrioritiesOrderAsync(SoftwarePriorities.ToList());
            if (_aggregator != null) await _aggregator.RefreshPrioritiesAsync();
            StatusMessage = $"Removed {count} priority software item(s).";
        }

        public async Task MovePrioUpAsync(IList? selectedItems = null)
        {
            var selected = GetSelectedPriorities(selectedItems);
            if (selected.Count == 0) return;

            var indices = selected.Select(p => SoftwarePriorities.IndexOf(p))
                                  .Where(i => i >= 0)
                                  .OrderBy(i => i)
                                  .ToList();

            if (indices.Count == 0 || indices.First() == 0) return;

            foreach (int idx in indices)
            {
                SoftwarePriorities.Move(idx, idx - 1);
            }

            RenumberPrioritiesInCollection();
            await _databaseService.SaveSoftwarePrioritiesOrderAsync(SoftwarePriorities.ToList());
            if (_aggregator != null) await _aggregator.RefreshPrioritiesAsync();
            StatusMessage = $"Promoted {selected.Count} priority item(s).";
        }

        public async Task MovePrioDownAsync(IList? selectedItems = null)
        {
            var selected = GetSelectedPriorities(selectedItems);
            if (selected.Count == 0) return;

            var indices = selected.Select(p => SoftwarePriorities.IndexOf(p))
                                  .Where(i => i >= 0)
                                  .OrderByDescending(i => i)
                                  .ToList();

            if (indices.Count == 0 || indices.First() >= SoftwarePriorities.Count - 1) return;

            foreach (int idx in indices)
            {
                SoftwarePriorities.Move(idx, idx + 1);
            }

            RenumberPrioritiesInCollection();
            await _databaseService.SaveSoftwarePrioritiesOrderAsync(SoftwarePriorities.ToList());
            if (_aggregator != null) await _aggregator.RefreshPrioritiesAsync();
            StatusMessage = $"Demoted {selected.Count} priority item(s).";
        }
    }
}

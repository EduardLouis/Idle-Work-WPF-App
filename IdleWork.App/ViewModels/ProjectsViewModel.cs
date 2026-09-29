// [v0.2: ProjectsVM] User-defined projects and client accounts management ViewModel
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using IdleWork.App.Core.Models;
using IdleWork.App.Core.Services;

namespace IdleWork.App.ViewModels
{
    public class ProjectsViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        private Project? _selectedProject;
        private int _editingProjectId = 0;
        private string _projectName = "";
        private string _projectCode = "";
        private string _projectColorHex = "#3B82F6";
        private string _projectDescription = "";
        private string _statusMessage = "";

        // [v0.2: RuleCorrelation] Correlated classification rules
        private string _newRuleProcessFilter = "";
        private string _newRuleTitlePattern = "";

        public ObservableCollection<Project> Projects { get; } = new ObservableCollection<Project>();
        public ObservableCollection<SelectableRule> AvailableRules { get; } = new ObservableCollection<SelectableRule>();

        public Project? SelectedProject
        {
            get => _selectedProject;
            set
            {
                if (SetProperty(ref _selectedProject, value))
                {
                    if (value != null)
                    {
                        // [v0.2: ProjectEdit] Selecting an item automatically sets Update mode
                        _editingProjectId = value.Id;
                        LoadProjectIntoForm(value);
                        OnPropertyChanged(nameof(SaveButtonText));
                        SyncRuleSelections(value.Name);
                        StatusMessage = $"Selected project: {value.Name}";
                    }
                    else
                    {
                        _editingProjectId = 0;
                        OnPropertyChanged(nameof(SaveButtonText));
                    }
                }
            }
        }

        public string ProjectName
        {
            get => _projectName;
            set => SetProperty(ref _projectName, value);
        }

        public string ProjectCode
        {
            get => _projectCode;
            set => SetProperty(ref _projectCode, value);
        }

        public string ProjectColorHex
        {
            get => _projectColorHex;
            set => SetProperty(ref _projectColorHex, value);
        }

        public string ProjectDescription
        {
            get => _projectDescription;
            set => SetProperty(ref _projectDescription, value);
        }

        public string NewRuleProcessFilter
        {
            get => _newRuleProcessFilter;
            set => SetProperty(ref _newRuleProcessFilter, value);
        }

        public string NewRuleTitlePattern
        {
            get => _newRuleTitlePattern;
            set => SetProperty(ref _newRuleTitlePattern, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string SaveButtonText => _editingProjectId == 0 ? "+ Create Project" : "💾 Update Project";

        // Preset Color Swatches
        public ObservableCollection<string> ColorPresets { get; } = new ObservableCollection<string>
        {
            "#3B82F6", // Blue
            "#10B981", // Emerald
            "#8B5CF6", // Purple
            "#F59E0B", // Amber
            "#06B6D4", // Cyan
            "#EC4899", // Pink
            "#EF4444", // Red
            "#64748B"  // Slate
        };

        public ICommand SaveProjectCommand { get; }
        public ICommand DeleteProjectCommand { get; }
        public ICommand ClearFormCommand { get; }
        public ICommand SelectColorCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand AddQuickRuleCommand { get; }

        public ProjectsViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;

            SaveProjectCommand = new RelayCommand(async () => await SaveProjectAsync());
            DeleteProjectCommand = new RelayCommand(async () => await DeleteProjectAsync());
            ClearFormCommand = new RelayCommand(ClearForm);
            SelectColorCommand = new RelayCommand(param => { if (param is string color && !string.IsNullOrEmpty(color)) ProjectColorHex = color; });
            RefreshCommand = new RelayCommand(async () => await InitializeAsync());
            AddQuickRuleCommand = new RelayCommand(AddQuickRule);

            _ = InitializeAsync();
        }

        private readonly SemaphoreSlim _loadLock = new SemaphoreSlim(1, 1);

        public async Task InitializeAsync()
        {
            await LoadProjectsAsync();
            await LoadRulesAsync();
        }

        public async Task LoadProjectsAsync()
        {
            await _loadLock.WaitAsync();
            try
            {
                var list = await _databaseService.GetProjectsAsync();
                Projects.Clear();
                foreach (var p in list)
                    Projects.Add(p);
            }
            finally
            {
                _loadLock.Release();
            }
        }

        public async Task LoadRulesAsync()
        {
            await _loadLock.WaitAsync();
            try
            {
                var rules = await _databaseService.GetRulesAsync();
                var items = new List<SelectableRule>();
                foreach (var r in rules)
                {
                    bool isLinked = !string.IsNullOrEmpty(ProjectName) && string.Equals(r.TargetProject, ProjectName, StringComparison.OrdinalIgnoreCase);
                    items.Add(new SelectableRule(r, isSelected: isLinked));
                }

                AvailableRules.Clear();
                foreach (var item in items)
                {
                    AvailableRules.Add(item);
                }
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private void SyncRuleSelections(string projectName)
        {
            foreach (var ruleItem in AvailableRules)
            {
                ruleItem.IsSelected = !string.IsNullOrEmpty(projectName) &&
                    string.Equals(ruleItem.Rule.TargetProject, projectName, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void LoadProjectIntoForm(Project project)
        {
            ProjectName = project.Name;
            ProjectCode = project.Code;
            ProjectColorHex = string.IsNullOrEmpty(project.ColorHex) ? "#3B82F6" : project.ColorHex;
            ProjectDescription = project.Description;
        }

        private void AddQuickRule()
        {
            string proc = NewRuleProcessFilter.Trim();
            string pattern = NewRuleTitlePattern.Trim();

            if (string.IsNullOrWhiteSpace(proc) && string.IsNullOrWhiteSpace(pattern))
            {
                StatusMessage = "Please enter an application process (e.g. Revit) or title pattern.";
                return;
            }

            string targetPrj = !string.IsNullOrWhiteSpace(ProjectName) ? ProjectName.Trim() : "Pending Project";
            string ruleName = !string.IsNullOrWhiteSpace(proc)
                ? $"{targetPrj} - {proc}"
                : $"{targetPrj} - {pattern}";

            var newRule = new AutoTagRule
            {
                RuleName = ruleName,
                ProcessFilter = string.IsNullOrWhiteSpace(proc) ? null : proc,
                TitlePattern = string.IsNullOrWhiteSpace(pattern) ? null : pattern,
                TargetProject = targetPrj,
                IsEnabled = true,
                Priority = 50
            };

            AvailableRules.Add(new SelectableRule(newRule, isSelected: true, isNew: true));
            NewRuleProcessFilter = "";
            NewRuleTitlePattern = "";
            StatusMessage = $"Added new rule '{newRule.RuleName}' to be linked on save.";
        }

        private void ClearForm()
        {
            _editingProjectId = 0;
            _selectedProject = null;
            OnPropertyChanged(nameof(SelectedProject));

            ProjectName = "";
            ProjectCode = "";
            ProjectColorHex = "#3B82F6";
            ProjectDescription = "";
            NewRuleProcessFilter = "";
            NewRuleTitlePattern = "";

            foreach (var r in AvailableRules)
            {
                r.IsSelected = false;
            }

            OnPropertyChanged(nameof(SaveButtonText));
            StatusMessage = "Form cleared for new project creation.";
        }

        public async Task SaveProjectAsync()
        {
            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                StatusMessage = "Project name cannot be empty.";
                return;
            }

            string savedProjectName = ProjectName.Trim();
            string savedProjectCode = string.IsNullOrWhiteSpace(ProjectCode)
                ? savedProjectName.Substring(0, Math.Min(3, savedProjectName.Length)).ToUpperInvariant()
                : ProjectCode.Trim().ToUpperInvariant();

            if (_editingProjectId != 0)
            {
                var existing = Projects.FirstOrDefault(p => p.Id == _editingProjectId);
                if (existing != null)
                {
                    existing.Name = savedProjectName;
                    existing.Code = savedProjectCode;
                    existing.ColorHex = ProjectColorHex;
                    existing.Description = ProjectDescription;

                    await _databaseService.SaveProjectAsync(existing);
                    StatusMessage = $"Updated project: {existing.Name}";
                }
            }
            else
            {
                var newProject = new Project
                {
                    Name = savedProjectName,
                    Code = savedProjectCode,
                    ColorHex = ProjectColorHex,
                    Description = ProjectDescription,
                    IsActive = true
                };

                await _databaseService.SaveProjectAsync(newProject);
                Projects.Add(newProject);
                StatusMessage = $"Created project: {newProject.Name}";
            }

            // [v0.2: RuleCorrelation] Correlate selected and newly defined rules with this project
            foreach (var ruleItem in AvailableRules.ToList())
            {
                if (ruleItem.IsSelected)
                {
                    ruleItem.Rule.TargetProject = savedProjectName;
                    await _databaseService.SaveRuleAsync(ruleItem.Rule);
                }
                else if (!ruleItem.IsSelected && string.Equals(ruleItem.Rule.TargetProject, savedProjectName, StringComparison.OrdinalIgnoreCase))
                {
                    // Unlink
                    ruleItem.Rule.TargetProject = "";
                    await _databaseService.SaveRuleAsync(ruleItem.Rule);
                }
            }

            // Refresh rules from database to ensure up-to-date state
            await LoadRulesAsync();
            ClearForm();
        }

        private async Task DeleteProjectAsync()
        {
            if (SelectedProject == null) return;
            var p = SelectedProject;
            await _databaseService.DeleteProjectAsync(p.Id);
            Projects.Remove(p);
            ClearForm();
            StatusMessage = $"Deleted project: {p.Name}";
        }
    }
}

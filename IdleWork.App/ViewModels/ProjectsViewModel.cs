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

        public event EventHandler? RequestCreateNewRule;

        public string SelectedRulesSummaryText
        {
            get
            {
                var selected = AvailableRules.Where(r => r.IsSelected).ToList();
                if (selected.Count == 0)
                    return "No rules linked (Click to select)";
                if (selected.Count == 1)
                    return $"1 rule linked: {selected[0].RuleName}";
                if (selected.Count <= 2)
                    return $"{selected.Count} rules linked: {string.Join(", ", selected.Select(r => r.RuleName))}";
                return $"{selected.Count} rules linked: {selected[0].RuleName}, {selected[1].RuleName}...";
            }
        }

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

        // [v0.2: Categories] Categories management
        private WorkCategory? _selectedCategory;
        private int _editingCategoryId = 0;
        private string _categoryName = "";
        private string _categoryDescription = "";
        private string _categoryColorHex = "#3B82F6";

        public ObservableCollection<WorkCategory> Categories { get; } = new ObservableCollection<WorkCategory>();

        public WorkCategory? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    if (value != null)
                    {
                        _editingCategoryId = value.Id;
                        CategoryName = value.Name;
                        CategoryDescription = value.Description ?? "";
                        CategoryColorHex = value.ColorHex;
                        OnPropertyChanged(nameof(SaveCategoryButtonText));
                        StatusMessage = $"Selected category: {value.Name}";
                    }
                    else
                    {
                        _editingCategoryId = 0;
                        OnPropertyChanged(nameof(SaveCategoryButtonText));
                    }
                }
            }
        }

        public string CategoryName
        {
            get => _categoryName;
            set => SetProperty(ref _categoryName, value);
        }

        public string CategoryDescription
        {
            get => _categoryDescription;
            set => SetProperty(ref _categoryDescription, value);
        }

        public string CategoryColorHex
        {
            get => _categoryColorHex;
            set => SetProperty(ref _categoryColorHex, value);
        }

        public string SaveCategoryButtonText => _editingCategoryId == 0 ? "+ Add Category" : "💾 Update Category";

        // [v0.2: Tags] Tags management
        private WorkTag? _selectedTag;
        private int _editingTagId = 0;
        private string _tagName = "";
        private string _tagDescription = "";

        public ObservableCollection<WorkTag> Tags { get; } = new ObservableCollection<WorkTag>();

        public WorkTag? SelectedTag
        {
            get => _selectedTag;
            set
            {
                if (SetProperty(ref _selectedTag, value))
                {
                    if (value != null)
                    {
                        _editingTagId = value.Id;
                        TagName = value.Name;
                        TagDescription = value.Description ?? "";
                        OnPropertyChanged(nameof(SaveTagButtonText));
                        StatusMessage = $"Selected tag: {value.Name}";
                    }
                    else
                    {
                        _editingTagId = 0;
                        OnPropertyChanged(nameof(SaveTagButtonText));
                    }
                }
            }
        }

        public string TagName
        {
            get => _tagName;
            set => SetProperty(ref _tagName, value);
        }

        public string TagDescription
        {
            get => _tagDescription;
            set => SetProperty(ref _tagDescription, value);
        }

        public string SaveTagButtonText => _editingTagId == 0 ? "+ Add Tag" : "💾 Update Tag";

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
        public ICommand OpenNewRuleDialogCommand { get; }
        public ICommand SelectAllRulesCommand { get; }
        public ICommand ClearAllRulesCommand { get; }

        public ICommand SaveCategoryCommand { get; }
        public ICommand DeleteCategoryCommand { get; }
        public ICommand ClearCategoryFormCommand { get; }
        public ICommand SelectCategoryColorCommand { get; }

        public ICommand SaveTagCommand { get; }
        public ICommand DeleteTagCommand { get; }
        public ICommand ClearTagFormCommand { get; }

        public ProjectsViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;

            SaveProjectCommand = new RelayCommand(async () => await SaveProjectAsync());
            DeleteProjectCommand = new RelayCommand(async () => await DeleteProjectAsync());
            ClearFormCommand = new RelayCommand(ClearForm);
            SelectColorCommand = new RelayCommand(param => { if (param is string color && !string.IsNullOrEmpty(color)) ProjectColorHex = color; });
            RefreshCommand = new RelayCommand(async () => await InitializeAsync());
            AddQuickRuleCommand = new RelayCommand(AddQuickRule);
            OpenNewRuleDialogCommand = new RelayCommand(() => RequestCreateNewRule?.Invoke(this, EventArgs.Empty));
            SelectAllRulesCommand = new RelayCommand(() =>
            {
                foreach (var r in AvailableRules) r.IsSelected = true;
                OnPropertyChanged(nameof(SelectedRulesSummaryText));
            });
            ClearAllRulesCommand = new RelayCommand(() =>
            {
                foreach (var r in AvailableRules) r.IsSelected = false;
                OnPropertyChanged(nameof(SelectedRulesSummaryText));
            });

            SaveCategoryCommand = new RelayCommand(async () => await SaveCategoryAsync());
            DeleteCategoryCommand = new RelayCommand(async () => await DeleteCategoryAsync());
            ClearCategoryFormCommand = new RelayCommand(ClearCategoryForm);
            SelectCategoryColorCommand = new RelayCommand(param => { if (param is string color && !string.IsNullOrEmpty(color)) CategoryColorHex = color; });

            SaveTagCommand = new RelayCommand(async () => await SaveTagAsync());
            DeleteTagCommand = new RelayCommand(async () => await DeleteTagAsync());
            ClearTagFormCommand = new RelayCommand(ClearTagForm);

            _ = InitializeAsync();
        }

        private readonly SemaphoreSlim _loadLock = new SemaphoreSlim(1, 1);

        public async Task InitializeAsync()
        {
            await LoadProjectsAsync();
            await LoadRulesAsync();
            await LoadCategoriesAsync();
            await LoadTagsAsync();
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
                    item.SelectionChanged += (s, e) => OnPropertyChanged(nameof(SelectedRulesSummaryText));
                    AvailableRules.Add(item);
                }
                OnPropertyChanged(nameof(SelectedRulesSummaryText));
            }
            finally
            {
                _loadLock.Release();
            }
        }

        public void OnNewRuleCreated(AutoTagRule newRule)
        {
            var item = new SelectableRule(newRule, isSelected: true, isNew: true);
            item.SelectionChanged += (s, e) => OnPropertyChanged(nameof(SelectedRulesSummaryText));
            AvailableRules.Add(item);
            OnPropertyChanged(nameof(SelectedRulesSummaryText));
            StatusMessage = $"Created and linked rule '{newRule.RuleName}'.";
        }

        private void SyncRuleSelections(string projectName)
        {
            foreach (var ruleItem in AvailableRules)
            {
                ruleItem.IsSelected = !string.IsNullOrEmpty(projectName) &&
                    string.Equals(ruleItem.Rule.TargetProject, projectName, StringComparison.OrdinalIgnoreCase);
            }
            OnPropertyChanged(nameof(SelectedRulesSummaryText));
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
            OnPropertyChanged(nameof(SelectedRulesSummaryText));

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

        // [v0.2: Categories] Category CRUD methods
        public async Task LoadCategoriesAsync()
        {
            await _loadLock.WaitAsync();
            try
            {
                var list = await _databaseService.GetCategoriesAsync();
                Categories.Clear();
                foreach (var c in list)
                    Categories.Add(c);
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private async Task SaveCategoryAsync()
        {
            string name = CategoryName.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                StatusMessage = "Please enter a category name.";
                return;
            }

            if (_editingCategoryId != 0)
            {
                var existing = Categories.FirstOrDefault(c => c.Id == _editingCategoryId);
                if (existing != null)
                {
                    existing.Name = name;
                    existing.Description = CategoryDescription;
                    existing.ColorHex = CategoryColorHex;
                    await _databaseService.SaveCategoryAsync(existing);
                    StatusMessage = $"Updated category: {existing.Name}";
                }
            }
            else
            {
                var newCat = new WorkCategory
                {
                    Name = name,
                    Description = CategoryDescription,
                    ColorHex = CategoryColorHex
                };
                await _databaseService.SaveCategoryAsync(newCat);
                Categories.Add(newCat);
                StatusMessage = $"Created category: {newCat.Name}";
            }

            ClearCategoryForm();
        }

        private async Task DeleteCategoryAsync()
        {
            if (SelectedCategory == null) return;
            var cat = SelectedCategory;
            await _databaseService.DeleteCategoryAsync(cat.Id);
            Categories.Remove(cat);
            ClearCategoryForm();
            StatusMessage = $"Deleted category: {cat.Name}";
        }

        public void ClearCategoryForm()
        {
            _editingCategoryId = 0;
            _selectedCategory = null;
            OnPropertyChanged(nameof(SelectedCategory));
            CategoryName = "";
            CategoryDescription = "";
            CategoryColorHex = "#3B82F6";
            OnPropertyChanged(nameof(SaveCategoryButtonText));
        }

        // [v0.2: Tags] Tag CRUD methods
        public async Task LoadTagsAsync()
        {
            await _loadLock.WaitAsync();
            try
            {
                var list = await _databaseService.GetTagsAsync();
                Tags.Clear();
                foreach (var t in list)
                    Tags.Add(t);
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private async Task SaveTagAsync()
        {
            string name = TagName.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                StatusMessage = "Please enter a tag name.";
                return;
            }

            if (_editingTagId != 0)
            {
                var existing = Tags.FirstOrDefault(t => t.Id == _editingTagId);
                if (existing != null)
                {
                    existing.Name = name;
                    existing.Description = TagDescription;
                    await _databaseService.SaveTagAsync(existing);
                    StatusMessage = $"Updated tag: {existing.Name}";
                }
            }
            else
            {
                var newTag = new WorkTag
                {
                    Name = name,
                    Description = TagDescription
                };
                await _databaseService.SaveTagAsync(newTag);
                Tags.Add(newTag);
                StatusMessage = $"Created tag: {newTag.Name}";
            }

            ClearTagForm();
        }

        private async Task DeleteTagAsync()
        {
            if (SelectedTag == null) return;
            var tag = SelectedTag;
            await _databaseService.DeleteTagAsync(tag.Id);
            Tags.Remove(tag);
            ClearTagForm();
            StatusMessage = $"Deleted tag: {tag.Name}";
        }

        public void ClearTagForm()
        {
            _editingTagId = 0;
            _selectedTag = null;
            OnPropertyChanged(nameof(SelectedTag));
            TagName = "";
            TagDescription = "";
            OnPropertyChanged(nameof(SaveTagButtonText));
        }
    }
}

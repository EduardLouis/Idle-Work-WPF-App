// [v0.1: RuleClassifierService] Persistent smart classification engine and 1-click rule learning
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IdleWork.App.Core.Models;

namespace IdleWork.App.Core.Services
{
    public class RuleClassifierService
    {
        private readonly DatabaseService _databaseService;
        private List<AutoTagRule> _cachedRules = new List<AutoTagRule>();
        private readonly object _lock = new object();

        public RuleClassifierService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            // [v0.1: RuleClassifier] Asynchronously populate rules cache without blocking constructor / UI thread
            _ = RefreshRulesAsync();
        }

        public async Task RefreshRulesAsync()
        {
            try
            {
                var rules = await _databaseService.GetRulesAsync().ConfigureAwait(false);
                lock (_lock)
                {
                    _cachedRules = rules.Where(r => r.IsEnabled).OrderBy(r => r.Priority).ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RuleClassifierService] RefreshRulesAsync error: {ex.Message}");
            }
        }

        public void ClassifyActivity(ActivityTimeSpan activity)
        {
            if (activity.IsManualEdit)
                return; // Respect manual overrides

            List<AutoTagRule> rules;
            lock (_lock)
            {
                rules = _cachedRules;
            }

            foreach (var rule in rules)
            {
                if (Matches(activity, rule))
                {
                    activity.ProjectName = rule.TargetProject;
                    if (!string.IsNullOrWhiteSpace(rule.TargetCategory))
                        activity.Category = rule.TargetCategory;
                    if (!string.IsNullOrWhiteSpace(rule.TargetTags))
                        activity.Tags = rule.TargetTags;
                    break; // Highest priority match wins
                }
            }
        }

        public bool Matches(ActivityTimeSpan activity, AutoTagRule rule)
        {
            if (!rule.IsEnabled)
                return false;

            bool appMatch = true;
            if (!string.IsNullOrWhiteSpace(rule.ProcessFilter))
            {
                appMatch = activity.ProcessName.Contains(rule.ProcessFilter, StringComparison.OrdinalIgnoreCase);
            }

            bool titleMatch = true;
            if (!string.IsNullOrWhiteSpace(rule.TitlePattern))
            {
                if (rule.IsRegex)
                {
                    try
                    {
                        titleMatch = Regex.IsMatch(activity.WindowTitle, rule.TitlePattern, RegexOptions.IgnoreCase);
                    }
                    catch
                    {
                        titleMatch = false;
                    }
                }
                else
                {
                    titleMatch = activity.WindowTitle.Contains(rule.TitlePattern, StringComparison.OrdinalIgnoreCase);
                }
            }

            return appMatch && titleMatch;
        }

        // 1-Click "Assign & Remember" Rule Learning
        public async Task<int> AssignAndRememberRuleAsync(
            ActivityTimeSpan activity,
            string projectName,
            string? category = null,
            string? tags = null,
            bool matchProcess = true,
            bool matchDocumentTitle = false,
            bool applyRetroactively = true)
        {
            string? processFilter = matchProcess ? activity.ProcessName : null;
            string? titleFilter = matchDocumentTitle && !string.IsNullOrWhiteSpace(activity.DocumentName)
                ? activity.DocumentName
                : null;

            string ruleName = !string.IsNullOrEmpty(processFilter) && !string.IsNullOrEmpty(titleFilter)
                ? $"{processFilter} - {titleFilter} -> {projectName}"
                : !string.IsNullOrEmpty(processFilter)
                    ? $"{processFilter} -> {projectName}"
                    : $"{titleFilter} -> {projectName}";

            var newRule = new AutoTagRule
            {
                RuleName = ruleName,
                ProcessFilter = processFilter,
                TitlePattern = titleFilter,
                TargetProject = projectName,
                TargetCategory = category ?? activity.Category,
                TargetTags = tags ?? activity.Tags,
                Priority = 25,
                IsEnabled = true
            };

            await _databaseService.SaveRuleAsync(newRule).ConfigureAwait(false);
            await RefreshRulesAsync().ConfigureAwait(false);

            int updatedCount = 0;
            if (applyRetroactively)
            {
                updatedCount = await _databaseService.ApplyRuleRetroactivelyAsync(newRule).ConfigureAwait(false);
            }

            return updatedCount;
        }
    }
}

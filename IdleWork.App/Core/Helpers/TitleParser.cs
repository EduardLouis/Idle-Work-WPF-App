// [v0.1: TitleParser] Heuristic document and project name extractor from window titles
using System;
using System.Text.RegularExpressions;

namespace IdleWork.App.Core.Helpers
{
    public static class TitleParser
    {
        public static string ExtractDocumentName(string processName, string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return string.Empty;

            string pLower = processName.ToLowerInvariant();

            // Autodesk Revit: "Autodesk Revit 2025 - [Project_TowerA.rvt - Floor Plan: Level 1]"
            if (pLower.Contains("revit"))
            {
                var match = Regex.Match(windowTitle, @"\[(.*?)(?:\s*-\s*[^\]]+)?\]");
                if (match.Success)
                    return match.Groups[1].Value.Trim();
            }

            // AutoCAD: "Autodesk AutoCAD 2024 - [Drawing1.dwg]"
            if (pLower.Contains("acad"))
            {
                var match = Regex.Match(windowTitle, @"\[(.*?)\]");
                if (match.Success)
                    return match.Groups[1].Value.Trim();
            }

            // Visual Studio / VS Code: "idle_work - Visual Studio Code" or "IdleWork.sln - Microsoft Visual Studio"
            if (pLower.Contains("code") || pLower.Contains("devenv"))
            {
                var parts = windowTitle.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                    return parts[0].Trim();
            }

            // Microsoft Office: "Budget2026.xlsx - Excel", "DesignSpec.docx - Word"
            if (pLower.Contains("excel") || pLower.Contains("winword") || pLower.Contains("powerpnt"))
            {
                var parts = windowTitle.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                    return parts[0].Trim();
            }

            // Browser tabs: "Issue #42 · EduardLouis/Idle-Work - Google Chrome"
            if (pLower.Contains("chrome") || pLower.Contains("msedge") || pLower.Contains("firefox"))
            {
                int lastDash = windowTitle.LastIndexOf(" - ", StringComparison.Ordinal);
                if (lastDash > 0)
                    return windowTitle.Substring(0, lastDash).Trim();
            }

            // General fallback: return title trimmed up to first 60 chars
            return windowTitle.Length > 60 ? windowTitle.Substring(0, 57) + "..." : windowTitle;
        }
    }
}

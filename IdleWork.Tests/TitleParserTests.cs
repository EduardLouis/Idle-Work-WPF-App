// [v0.1: Test] Unit tests for TitleParser document extraction and AppVersionHelper
using IdleWork.App.Core.Helpers;
using Xunit;

namespace IdleWork.Tests
{
    public class TitleParserTests
    {
        [Fact]
        public void ExtractDocumentName_RevitTitle_ExtractsRvtFileName()
        {
            string title = "Autodesk Revit 2025 - [Hospital_TowerA.rvt - Floor Plan: Level 1]";
            string doc = TitleParser.ExtractDocumentName("Revit", title);

            Assert.Equal("Hospital_TowerA.rvt", doc);
        }

        [Fact]
        public void ExtractDocumentName_AutoCADTitle_ExtractsDwgName()
        {
            string title = "Autodesk AutoCAD 2024 - [Drawing_Section_01.dwg]";
            string doc = TitleParser.ExtractDocumentName("acad", title);

            Assert.Equal("Drawing_Section_01.dwg", doc);
        }

        [Fact]
        public void ExtractDocumentName_VSCodeTitle_ExtractsFolderOrProject()
        {
            string title = "IdleWork - Visual Studio Code";
            string doc = TitleParser.ExtractDocumentName("Code", title);

            Assert.Equal("IdleWork", doc);
        }

        [Fact]
        public void ExtractDocumentName_ExcelTitle_ExtractsWorkbookName()
        {
            string title = "Project_Budget_2026.xlsx - Excel";
            string doc = TitleParser.ExtractDocumentName("excel", title);

            Assert.Equal("Project_Budget_2026.xlsx", doc);
        }
    }

    public class AppVersionHelperTests
    {
        [Fact]
        public void AppVersionHelper_ExposesMajorMinorAndBuildTimestamp()
        {
            // [v0.2: Versioning] Assert dynamic version matches Directory.Build.props v0.2
            Assert.Equal("0.2", AppVersionHelper.Version);
            Assert.False(string.IsNullOrWhiteSpace(AppVersionHelper.BuildTimestamp));
            Assert.Contains("v0.2", AppVersionHelper.AppTitle);
        }
    }
}

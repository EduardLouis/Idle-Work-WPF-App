// [v0.2: Versioning] Unified version and dynamic build timestamp resolver supporting Major (0-999) & Minor (0-999)
using System;
using System.Reflection;

namespace IdleWork.App.Core.Helpers
{
    public static class AppVersionHelper
    {
        public static string Version { get; }
        public static int Major { get; }
        public static int Minor { get; }
        public static string BuildTimestamp { get; }
        public static string InformationalVersion { get; }
        public static string AppTitle { get; }

        static AppVersionHelper()
        {
            var assembly = typeof(AppVersionHelper).Assembly;
            var infoVerAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            InformationalVersion = infoVerAttr?.InformationalVersion ?? "0.2-20260929.0000";

            var parts = InformationalVersion.Split('-');
            Version = parts.Length > 0 ? parts[0] : "0.2";
            BuildTimestamp = parts.Length > 1 ? parts[1] : DateTime.Now.ToString("yyyyMMdd.HHmm");

            // Extract Major (0-999) and Minor (0-999) components
            var segments = Version.Split('.');
            Major = segments.Length > 0 && int.TryParse(segments[0], out int maj) ? Math.Clamp(maj, 0, 999) : 0;
            Minor = segments.Length > 1 && int.TryParse(segments[1], out int min) ? Math.Clamp(min, 0, 999) : 2;

            AppTitle = $"Idle-Work v{Version} (Build {BuildTimestamp})";
        }
    }
}

// [v0.1: PluginSDK] Extensible C# Plugin interface for 3rd-party and custom data enrichment
using System.Threading.Tasks;
using IdleWork.App.Core.Models;

namespace IdleWork.App.Plugins
{
    public interface IIdleWorkPlugin
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        string Version { get; }
        bool IsEnabled { get; set; }

        Task InitializeAsync();
        Task EnrichActivityAsync(ActivityTimeSpan activity);
        Task OnTimeSpanCompletedAsync(ActivityTimeSpan activity);
        Task ShutdownAsync();
    }
}

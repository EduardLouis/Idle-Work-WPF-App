// [v0.1: PluginManager] Plugin registry and lifecycle manager
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdleWork.App.Core.Models;

namespace IdleWork.App.Plugins
{
    public class PluginManager
    {
        private readonly List<IIdleWorkPlugin> _plugins = new List<IIdleWorkPlugin>();
        public IReadOnlyList<IIdleWorkPlugin> Plugins => _plugins.AsReadOnly();

        public PluginManager()
        {
            // Register built-in native C# plugins
            RegisterPlugin(new TeamsIntegrationPlugin());
            RegisterPlugin(new OutlookCalendarPlugin());
            RegisterPlugin(new PowerAutomatePlugin());
        }

        public void RegisterPlugin(IIdleWorkPlugin plugin)
        {
            _plugins.Add(plugin);
            plugin.InitializeAsync();
        }

        public async Task EnrichActivityAsync(ActivityTimeSpan activity)
        {
            foreach (var plugin in _plugins)
            {
                if (plugin.IsEnabled)
                {
                    try
                    {
                        await plugin.EnrichActivityAsync(activity);
                    }
                    catch
                    {
                        // Ignore individual plugin errors to prevent disrupting tracker
                    }
                }
            }
        }

        public async Task OnTimeSpanCompletedAsync(ActivityTimeSpan activity)
        {
            foreach (var plugin in _plugins)
            {
                if (plugin.IsEnabled)
                {
                    try
                    {
                        await plugin.OnTimeSpanCompletedAsync(activity);
                    }
                    catch
                    {
                        // Ignore individual plugin errors
                    }
                }
            }
        }

        public async Task ShutdownAsync()
        {
            foreach (var plugin in _plugins)
            {
                try
                {
                    await plugin.ShutdownAsync();
                }
                catch
                {
                }
            }
        }
    }
}

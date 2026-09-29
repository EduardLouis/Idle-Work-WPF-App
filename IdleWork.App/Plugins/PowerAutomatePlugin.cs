// [v0.1: PowerAutomatePlugin] PowerAutomate flow webhook and local export trigger plugin
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using IdleWork.App.Core.Models;

namespace IdleWork.App.Plugins
{
    public class PowerAutomatePlugin : IIdleWorkPlugin
    {
        public string Id => "microsoft.powerautomate";
        public string Name => "PowerAutomate Connector";
        public string Description => "Dispatches completed activities or timesheets via HTTP webhooks or file drops to PowerAutomate flows.";
        public string Version => "0.1";
        public bool IsEnabled { get; set; } = false; // Disabled by default until user configures webhook

        public string? WebhookUrl { get; set; }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task EnrichActivityAsync(ActivityTimeSpan activity)
        {
            return Task.CompletedTask;
        }

        public async Task OnTimeSpanCompletedAsync(ActivityTimeSpan activity)
        {
            if (!IsEnabled || string.IsNullOrWhiteSpace(WebhookUrl))
                return;

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var json = JsonSerializer.Serialize(activity);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await client.PostAsync(WebhookUrl, content);
            }
            catch
            {
                // Silently ignore network failures for plugin webhooks
            }
        }

        public Task ShutdownAsync()
        {
            return Task.CompletedTask;
        }
    }
}

// [v0.003: LoggingService] Structured local file logging in AppData + background Cloud Web API telemetry
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IdleWork.App.Core.Models;

namespace IdleWork.App.Core.Services
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Activity
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public LogLevel Level { get; set; } = LogLevel.Info;
        public string Tag { get; set; } = "General";
        public string Message { get; set; } = "";
        public string? Exception { get; set; }
        public Dictionary<string, object>? Properties { get; set; }
    }

    public class TelemetryBatchPayload
    {
        public string MachineName { get; set; } = Environment.MachineName;
        public string UserName { get; set; } = Environment.UserName;
        public string AppVersion { get; set; } = "v0.003";
        public string OsVersion { get; set; } = Environment.OSVersion.ToString();
        public DateTime BatchTimestampUtc { get; set; } = DateTime.UtcNow;
        public List<LogEntry> Entries { get; set; } = new List<LogEntry>();
    }

    public class LoggingService : IDisposable
    {
        private static LoggingService? _instance;
        public static LoggingService Instance => _instance ??= new LoggingService();

        public string LogDirectory { get; }
        public string CloudEndpointUrl { get; set; } = "";
        public bool EnableCloudTelemetry { get; set; } = true;

        private readonly ConcurrentQueue<LogEntry> _fileQueue = new ConcurrentQueue<LogEntry>();
        private readonly ConcurrentQueue<LogEntry> _cloudQueue = new ConcurrentQueue<LogEntry>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly HttpClient _httpClient;
        private readonly Task _backgroundProcessor;
        private readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

        public LoggingService(string? customLogDir = null)
        {
            LogDirectory = customLogDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IdleWork",
                "Logs");

            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }
            }
            catch { }

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(8)
            };

            _backgroundProcessor = Task.Run(ProcessQueuesLoopAsync);
        }

        public void LogDebug(string tag, string message)
        {
            Enqueue(LogLevel.Debug, tag, message);
        }

        public void LogDebug(string message)
        {
            Enqueue(LogLevel.Debug, "General", message);
        }

        public void LogInfo(string tag, string message)
        {
            Enqueue(LogLevel.Info, tag, message);
        }

        public void LogWarn(string tag, string message, Exception? ex = null)
        {
            Enqueue(LogLevel.Warning, tag, message, ex);
        }

        public void LogError(string tag, string message, Exception? ex = null)
        {
            Enqueue(LogLevel.Error, tag, message, ex);
        }

        public void LogActivity(ActivityTimeSpan activity)
        {
            var props = new Dictionary<string, object>
            {
                { "processName", activity.ProcessName ?? "" },
                { "windowTitle", activity.WindowTitle ?? "" },
                { "documentName", activity.DocumentName ?? "" },
                { "projectName", activity.ProjectName ?? "" },
                { "category", activity.Category ?? "" },
                { "durationSeconds", activity.DurationSeconds },
                { "state", activity.State ?? "" }
            };

            Enqueue(LogLevel.Activity, "ActivityTracker", $"Activity Span: {activity.ProcessName} ({activity.DurationSeconds:F0}s)", null, props);
        }

        public void LogEvent(string eventName, Dictionary<string, object>? properties = null)
        {
            Enqueue(LogLevel.Info, "Event", eventName, null, properties);
        }

        private void Enqueue(LogLevel level, string tag, string message, Exception? ex = null, Dictionary<string, object>? props = null)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Tag = tag,
                Message = message,
                Exception = ex?.ToString(),
                Properties = props
            };

            _fileQueue.Enqueue(entry);

            // Cloud queue holds non-debug events, capped to avoid excessive memory if offline
            if (_cloudQueue.Count < 1000)
            {
                _cloudQueue.Enqueue(entry);
            }

            System.Diagnostics.Debug.WriteLine($"[IdleWork.{level}] [{tag}] {message}");
        }

        private async Task ProcessQueuesLoopAsync()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await WritePendingFileLogsAsync().ConfigureAwait(false);
                    await DispatchPendingCloudLogsAsync().ConfigureAwait(false);
                    await Task.Delay(2500, _cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LoggingService] Loop error: {ex.Message}");
                }
            }

            // Final drain upon exit
            try
            {
                await WritePendingFileLogsAsync().ConfigureAwait(false);
                await DispatchPendingCloudLogsAsync().ConfigureAwait(false);
            }
            catch { }
        }

        private async Task WritePendingFileLogsAsync()
        {
            if (_fileQueue.IsEmpty) return;

            var batch = new List<LogEntry>();
            while (_fileQueue.TryDequeue(out var entry) && batch.Count < 200)
            {
                batch.Add(entry);
            }

            if (batch.Count == 0) return;

            string filePath = Path.Combine(LogDirectory, $"idlework_{DateTime.Now:yyyyMMdd}.log");

            var sb = new StringBuilder();
            foreach (var item in batch)
            {
                sb.Append($"[{item.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}] [{item.Level.ToString().ToUpperInvariant()}] [{item.Tag}] {item.Message}");
                if (!string.IsNullOrEmpty(item.Exception))
                {
                    sb.AppendLine();
                    sb.Append($"    Exception: {item.Exception}");
                }
                if (item.Properties != null && item.Properties.Count > 0)
                {
                    try
                    {
                        sb.AppendLine();
                        sb.Append($"    Props: {JsonSerializer.Serialize(item.Properties)}");
                    }
                    catch { }
                }
                sb.AppendLine();
            }

            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!Directory.Exists(LogDirectory)) Directory.CreateDirectory(LogDirectory);
                await File.AppendAllTextAsync(filePath, sb.ToString()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoggingService] File append failed: {ex.Message}");
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private async Task DispatchPendingCloudLogsAsync()
        {
            // Only send if CloudEndpointUrl is populated and cloud telemetry is enabled
            if (string.IsNullOrWhiteSpace(CloudEndpointUrl) || !EnableCloudTelemetry || _cloudQueue.IsEmpty)
            {
                return;
            }

            var batch = new List<LogEntry>();
            while (_cloudQueue.TryDequeue(out var item) && batch.Count < 50)
            {
                batch.Add(item);
            }

            if (batch.Count == 0) return;

            var payload = new TelemetryBatchPayload
            {
                Entries = batch
            };

            try
            {
                string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync(CloudEndpointUrl, content, _cts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[LoggingService] Cloud dispatch status: {response.StatusCode}");
                    // Re-enqueue items if server returned temporary error, up to cap
                    if ((int)response.StatusCode >= 500 && _cloudQueue.Count < 500)
                    {
                        foreach (var itm in batch) _cloudQueue.Enqueue(itm);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoggingService] Cloud dispatch error (endpoint unreachable): {ex.Message}");
                // If offline or network error, re-enqueue batch if under cap
                if (_cloudQueue.Count < 500)
                {
                    foreach (var itm in batch) _cloudQueue.Enqueue(itm);
                }
            }
        }

        public async Task FlushAsync()
        {
            await WritePendingFileLogsAsync().ConfigureAwait(false);
            await DispatchPendingCloudLogsAsync().ConfigureAwait(false);
        }

        public void Dispose()
        {
            try
            {
                _cts.Cancel();
                _backgroundProcessor.Wait(1000);
            }
            catch { }
            finally
            {
                _cts.Dispose();
                _httpClient.Dispose();
                _fileLock.Dispose();
            }
        }
    }
}

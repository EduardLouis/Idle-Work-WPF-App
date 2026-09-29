// [v0.1: IdleDetectionService] 3-Input inactivity sensing (Keyboard, Mouse, Mic Peak) with Meeting Mode
using System;
using System.Timers;
using IdleWork.App.Core.Native;

namespace IdleWork.App.Core.Services
{
    public class IdleDetectionService : IDisposable
    {
        private readonly AudioLevelService _audioService;
        private readonly System.Timers.Timer _checkTimer;
        private DateTime _lastVoiceDetectedTime = DateTime.MinValue;

        public int IdleTimeoutSeconds { get; set; } = 180; // 3 minutes
        public string CurrentState { get; private set; } = "Active"; // Active, Idle, Meeting
        public TimeSpan IdleDuration { get; private set; } = TimeSpan.Zero;

        public event EventHandler<string>? StateChanged;

        public IdleDetectionService(AudioLevelService audioService)
        {
            _audioService = audioService;
            _audioService.PeakLevelChanged += AudioService_PeakLevelChanged;

            _checkTimer = new System.Timers.Timer(500); // Check twice per second
            _checkTimer.Elapsed += CheckTimer_Elapsed;
            _checkTimer.Start();
        }

        private void AudioService_PeakLevelChanged(object? sender, float peak)
        {
            if (peak >= _audioService.SensitivityThreshold)
            {
                _lastVoiceDetectedTime = DateTime.Now;
            }
        }

        private void CheckTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            var lii = new User32.LASTINPUTINFO();
            lii.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(lii);

            uint idleTicks = 0;
            if (User32.GetLastInputInfo(ref lii))
            {
                uint currentTick = (uint)Environment.TickCount;
                idleTicks = currentTick >= lii.dwTime ? currentTick - lii.dwTime : 0;
            }

            IdleDuration = TimeSpan.FromMilliseconds(idleTicks);
            double idleSec = IdleDuration.TotalSeconds;

            string newState;
            if (idleSec < IdleTimeoutSeconds)
            {
                newState = "Active";
            }
            else
            {
                // Check if voice was detected within the last 20 seconds
                bool recentVoice = (DateTime.Now - _lastVoiceDetectedTime).TotalSeconds < 20;
                if (recentVoice)
                {
                    newState = "Meeting";
                }
                else
                {
                    newState = "Idle";
                }
            }

            if (newState != CurrentState)
            {
                CurrentState = newState;
                StateChanged?.Invoke(this, CurrentState);
            }
        }

        // [v0.2: OfflineTracking] Gets the exact timestamp of the user's last keyboard/mouse input
        public DateTime GetLastInputTime()
        {
            var lii = new User32.LASTINPUTINFO();
            lii.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(lii);

            if (User32.GetLastInputInfo(ref lii))
            {
                uint currentTick = (uint)Environment.TickCount;
                uint idleTicks = currentTick >= lii.dwTime ? currentTick - lii.dwTime : 0;
                return DateTime.Now.AddMilliseconds(-idleTicks);
            }
            return DateTime.Now;
        }

        public void Dispose()
        {
            _checkTimer.Stop();
            _checkTimer.Dispose();
            _audioService.PeakLevelChanged -= AudioService_PeakLevelChanged;
        }
    }
}

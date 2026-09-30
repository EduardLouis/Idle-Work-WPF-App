// [v0.1: AudioLevelService] Passive microphone peak audio level meter with dedicated single-thread COM apartment
using System;
using System.Threading;
using NAudio.CoreAudioApi;

namespace IdleWork.App.Core.Services
{
    public class AudioLevelService : IDisposable
    {
        private readonly Thread _workerThread;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isDisposed;

        public float CurrentPeakLevel { get; private set; }
        public float SensitivityThreshold { get; set; } = 0.05f; // 5% peak audio
        public bool IsSpeaking => CurrentPeakLevel >= SensitivityThreshold;

        public event EventHandler<float>? PeakLevelChanged;

        public AudioLevelService()
        {
            _workerThread = new Thread(AudioSamplingLoop)
            {
                IsBackground = true,
                Name = "IdleWork_AudioMeter"
            };
            _workerThread.SetApartmentState(ApartmentState.MTA);
            _workerThread.Start();
        }

        private void AudioSamplingLoop()
        {
            MMDeviceEnumerator? enumerator = null;
            MMDevice? captureDevice = null;
            DateTime lastDeviceCheck = DateTime.MinValue;

            while (!_cts.Token.IsCancellationRequested)
            {
                float peak = 0.0f;
                try
                {
                    // Refresh / acquire device periodically if missing
                    if (captureDevice == null && (DateTime.Now - lastDeviceCheck).TotalSeconds > 3.0)
                    {
                        lastDeviceCheck = DateTime.Now;
                        enumerator ??= new MMDeviceEnumerator();
                        if (enumerator.HasDefaultAudioEndpoint(DataFlow.Capture, Role.Communications))
                        {
                            captureDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                        }
                    }

                    if (captureDevice != null)
                    {
                        peak = captureDevice.AudioMeterInformation.MasterPeakValue;
                    }
                }
                catch (Exception)
                {
                    // If device disconnected or COM state invalid, reset device handle and retry later
                    try
                    {
                        captureDevice?.Dispose();
                    }
                    catch { }
                    captureDevice = null;
                    peak = 0.0f;
                }

                CurrentPeakLevel = peak;
                PeakLevelChanged?.Invoke(this, peak);

                try
                {
                    Thread.Sleep(150);
                }
                catch (ThreadInterruptedException)
                {
                    break;
                }
            }

            try
            {
                captureDevice?.Dispose();
                enumerator?.Dispose();
            }
            catch { }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _cts.Cancel();
            _workerThread.Interrupt();
        }
    }
}

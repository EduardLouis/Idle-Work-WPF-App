// [v0.1: AudioLevelService] Passive microphone peak audio level meter with hardened COM exception handling
using System;
using System.Diagnostics;
using System.Threading;
using NAudio.CoreAudioApi;

namespace IdleWork.App.Core.Services
{
    public class AudioLevelService : IDisposable
    {
        private readonly Thread _workerThread;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isDisposed;
        private bool _isAudioMeterSupported = true;

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
            // [v0.1: CoreAudio] STA apartment state for Windows COM multimedia devices
            _workerThread.SetApartmentState(ApartmentState.STA);
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
                    // Refresh / acquire device periodically if missing or retry after backoff
                    double retryInterval = _isAudioMeterSupported ? 5.0 : 30.0;
                    if (captureDevice == null && (DateTime.Now - lastDeviceCheck).TotalSeconds > retryInterval)
                    {
                        lastDeviceCheck = DateTime.Now;
                        try
                        {
                            enumerator ??= new MMDeviceEnumerator();
                            if (enumerator.HasDefaultAudioEndpoint(DataFlow.Capture, Role.Communications))
                            {
                                captureDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                            }
                            else if (enumerator.HasDefaultAudioEndpoint(DataFlow.Capture, Role.Console))
                            {
                                captureDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[AudioLevelService] Endpoint acquisition notice: {ex.Message}");
                            captureDevice = null;
                        }
                    }

                    if (captureDevice != null && _isAudioMeterSupported)
                    {
                        try
                        {
                            peak = captureDevice.AudioMeterInformation.MasterPeakValue;
                        }
                        catch (InvalidCastException icEx)
                        {
                            // Some audio devices / virtual endpoints do not support IAudioMeterInformation COM interface
                            Debug.WriteLine($"[AudioLevelService] Audio meter COM interface not supported: {icEx.Message}");
                            _isAudioMeterSupported = false;
                            captureDevice?.Dispose();
                            captureDevice = null;
                            peak = 0.0f;
                        }
                        catch (Exception devEx)
                        {
                            Debug.WriteLine($"[AudioLevelService] Audio meter read error: {devEx.Message}");
                            captureDevice?.Dispose();
                            captureDevice = null;
                            peak = 0.0f;
                        }
                    }
                }
                catch (Exception loopEx)
                {
                    Debug.WriteLine($"[AudioLevelService] Loop exception: {loopEx.Message}");
                    try
                    {
                        captureDevice?.Dispose();
                    }
                    catch { }
                    captureDevice = null;
                    peak = 0.0f;
                }

                CurrentPeakLevel = peak;
                try
                {
                    PeakLevelChanged?.Invoke(this, peak);
                }
                catch
                {
                    // Subscriber error should not crash meter thread
                }

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
            try
            {
                _workerThread.Interrupt();
            }
            catch { }
        }
    }
}

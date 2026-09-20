using InkCanvasPlus.Helpers;
using System;
using System.Diagnostics;
using System.Timers;

namespace InkCanvasPlus.Services
{
    /// <summary>
    /// Periodic auto-kill of classroom companion processes (PPTService / Seewo assistant /
    /// EasiNote float ball). Behavior and default on/off flags stay with Settings.Automation;
    /// this type only owns the timer and the taskkill / float-ball steps.
    /// </summary>
    public sealed class ProcessWatchdog : IDisposable
    {
        public const double DefaultIntervalMs = 1000;

        private readonly Func<bool> _isAutoKillPptService;
        private readonly Func<bool> _isAutoKillEasiNote;
        private readonly Func<string, int> _countProcessesByName;
        private readonly Action<string> _runTaskKill;
        private readonly Action _killEasiNoteFloatBall;
        private readonly Timer _timer;
        private bool _disposed;

        public ProcessWatchdog(
            Func<bool> isAutoKillPptService,
            Func<bool> isAutoKillEasiNote,
            Func<string, int> countProcessesByName = null,
            Action<string> runTaskKill = null,
            Action killEasiNoteFloatBall = null,
            double intervalMs = DefaultIntervalMs)
        {
            _isAutoKillPptService = isAutoKillPptService ?? (() => false);
            _isAutoKillEasiNote = isAutoKillEasiNote ?? (() => false);
            _countProcessesByName = countProcessesByName ?? DefaultCountProcessesByName;
            _runTaskKill = runTaskKill ?? DefaultRunTaskKill;
            _killEasiNoteFloatBall = killEasiNoteFloatBall ?? AutoKillHelper.KillEasiNoteFloatBall;

            _timer = new Timer { Interval = intervalMs, AutoReset = true };
            _timer.Elapsed += TimerOnElapsed;
        }

        public bool IsRunning => !_disposed && _timer.Enabled;

        public void Start()
        {
            if (_disposed) return;
            _timer.Start();
        }

        public void Stop()
        {
            if (_disposed) return;
            _timer.Stop();
        }

        /// <summary>
        /// One poll. Public so tests can inject process list / taskkill fakes without waiting on the timer.
        /// </summary>
        public void Tick()
        {
            if (_disposed) return;
            try
            {
                // 希沃相关： easinote swenserver RemoteProcess EasiNote.MediaHttpService smartnote.cloud EasiUpdate smartnote EasiUpdate3 EasiUpdate3Protect SeewoP2P CefSharp.BrowserSubprocess SeewoUploadService
                string arg = "/F";
                if (_isAutoKillPptService())
                {
                    if (_countProcessesByName("PPTService") > 0)
                    {
                        arg += " /IM PPTService.exe";
                    }
                    if (_countProcessesByName("SeewoIwbAssistant") > 0)
                    {
                        arg += " /IM SeewoIwbAssistant.exe" +
                            " /IM Sia.Guard.exe";
                    }
                }
                if (arg != "/F")
                {
                    _runTaskKill(arg);
                }
                if (_isAutoKillEasiNote())
                {
                    if (_countProcessesByName("EasiNote") > 0)
                    {
                        _killEasiNoteFloatBall();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.NewLog(ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Elapsed -= TimerOnElapsed;
            _timer.Stop();
            _timer.Dispose();
        }

        private void TimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            Tick();
        }

        private static int DefaultCountProcessesByName(string name)
        {
            return Process.GetProcessesByName(name).Length;
        }

        private static void DefaultRunTaskKill(string arg)
        {
            var p = new Process();
            p.StartInfo = new ProcessStartInfo("taskkill", arg);
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.Start();
        }
    }
}

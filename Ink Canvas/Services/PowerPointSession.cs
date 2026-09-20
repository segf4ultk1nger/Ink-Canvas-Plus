using InkCanvasPlus.Helpers;
using Microsoft.Office.Interop.PowerPoint;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace InkCanvasPlus.Services
{
    public sealed class PowerPointSlideShowBeganEventArgs : EventArgs
    {
        public PowerPointSlideShowBeganEventArgs(string presentationName, int slideCount, int currentShowPosition, double slideWidth, double slideHeight)
        {
            PresentationName = presentationName;
            SlideCount = slideCount;
            CurrentShowPosition = currentShowPosition;
            SlideWidth = slideWidth;
            SlideHeight = slideHeight;
        }

        public string PresentationName { get; }
        public int SlideCount { get; }
        public int CurrentShowPosition { get; }
        public double SlideWidth { get; }
        public double SlideHeight { get; }
    }

    public sealed class PowerPointSlideChangedEventArgs : EventArgs
    {
        public PowerPointSlideChangedEventArgs(string presentationName, int slideCount, int currentShowPosition)
        {
            PresentationName = presentationName;
            SlideCount = slideCount;
            CurrentShowPosition = currentShowPosition;
        }

        public string PresentationName { get; }
        public int SlideCount { get; }
        public int CurrentShowPosition { get; }
    }

    public sealed class PowerPointSlideShowEndedEventArgs : EventArgs
    {
        public PowerPointSlideShowEndedEventArgs(string presentationName, int slideCount)
        {
            PresentationName = presentationName;
            SlideCount = slideCount;
        }

        public string PresentationName { get; }
        public int SlideCount { get; }
    }

    /// <summary>
    /// Owns the PowerPoint Application RCW, COM event subscriptions, and attach lifetime.
    /// Timer and BtnCheckPPT both call TryAttach only — neither subscribes COM events itself.
    /// </summary>
    public sealed class PowerPointSession : IDisposable
    {
        public const string RotProgId = "PowerPoint.Application";

        private readonly DispatcherGate _gate;
        private readonly Func<bool> _hasWpsProcess;
        private readonly Func<bool> _isWpsSupported;
        private readonly Func<object> _getActiveObject;

        private Microsoft.Office.Interop.PowerPoint.Application _pptApplication;
        private Presentation _presentation;
        private Slides _slides;
        private bool _eventsSubscribed;
        private bool _isAttached;
        private bool _disposed;

        public PowerPointSession()
            : this(null, null, null, null)
        {
        }

        /// <summary>
        /// Test seams: inject process / ROT hooks so Office is not required.
        /// Production uses the parameterless constructor.
        /// </summary>
        public PowerPointSession(
            DispatcherGate dispatcher,
            Func<bool> hasWpsProcess,
            Func<bool> isWpsSupported,
            Func<object> getActiveObject)
        {
            _gate = dispatcher ?? new DispatcherGate();
            _hasWpsProcess = hasWpsProcess ?? DefaultHasWpsProcess;
            _isWpsSupported = isWpsSupported ?? DefaultIsWpsSupported;
            _getActiveObject = getActiveObject ?? DefaultGetActiveObject;
        }

        public event EventHandler<PowerPointSlideShowBeganEventArgs> SlideShowBegan;
        public event EventHandler<PowerPointSlideChangedEventArgs> SlideChanged;
        public event EventHandler<PowerPointSlideShowEndedEventArgs> SlideShowEnded;
        public event EventHandler Detached;

        public bool IsAttached => _isAttached;
        public int SlideCount { get; private set; }
        public int CurrentSlideIndex { get; private set; }
        public string PresentationName { get; private set; }
        public bool IsSlideShowRunning { get; private set; }
        public bool HasHiddenSlides { get; private set; }
        public int SubscribeCount { get; private set; }

        /// <summary>
        /// Same rule as today's timer: a running <c>wpp</c> process plus
        /// <see cref="PowerPointSettings.IsSupportWPS"/> off means do not attach.
        /// BtnCheckPPT should not call this — it always tries TryAttach.
        /// </summary>
        public bool ShouldSkipBecauseWps()
        {
            try
            {
                return _hasWpsProcess() && !_isWpsSupported();
            }
            catch (Exception ex)
            {
                LogHelper.NewLog(ex);
                return false;
            }
        }

        /// <summary>
        /// Attach to a running PowerPoint via ROT. Idempotent: already attached returns true
        /// without a second event subscription. Failure returns false so the timer can poll.
        /// All COM work runs on the UI thread.
        /// </summary>
        public bool TryAttach()
        {
            if (_disposed) return false;

            var result = false;
            _gate.OnUi(() => result = TryAttachOnUi());
            return result;
        }

        public void Detach()
        {
            if (_disposed) return;
            _gate.OnUi(() => DetachOnUi(raiseDetached: true));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _gate.OnUi(() =>
            {
                DetachOnUi(raiseDetached: false);
                _disposed = true;
            });
            if (_gate.HasDispatcher == false)
            {
                _disposed = true;
            }
        }

        public PowerPointSlideShowBeganEventArgs CaptureRunningSlideShow()
        {
            PowerPointSlideShowBeganEventArgs snapshot = null;
            _gate.OnUi(() =>
            {
                if (!_isAttached || _pptApplication == null) return;
                try
                {
                    if (_pptApplication.SlideShowWindows.Count < 1) return;
                    snapshot = SnapshotBegan(_pptApplication.SlideShowWindows[1]);
                    IsSlideShowRunning = true;
                }
                catch (Exception ex)
                {
                    LogHelper.NewLog(ex);
                }
            });
            return snapshot;
        }

        public void GotoSlide(int page)
        {
            _gate.OnUi(() =>
            {
                if (_pptApplication == null || _presentation == null) return;
                try
                {
                    if (_pptApplication.SlideShowWindows.Count >= 1)
                    {
                        _presentation.SlideShowWindow.View.GotoSlide(page);
                    }
                    else
                    {
                        _presentation.Windows[1].View.GotoSlide(page);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.NewLog(ex);
                }
            });
        }

        public void PreviousSlide()
        {
            _gate.OnUi(() =>
            {
                if (_pptApplication == null) return;
                var window = _pptApplication.SlideShowWindows[1];
                window.Activate();
                window.View.Previous();
            });
        }

        public void NextSlide()
        {
            _gate.OnUi(() =>
            {
                if (_pptApplication == null) return;
                var window = _pptApplication.SlideShowWindows[1];
                window.Activate();
                window.View.Next();
            });
        }

        public void RunSlideShow()
        {
            _gate.OnUi(() =>
            {
                if (_presentation == null) return;
                _presentation.SlideShowSettings.Run();
            });
        }

        public void ExitSlideShow()
        {
            _gate.OnUi(() =>
            {
                if (_pptApplication == null) return;
                _pptApplication.SlideShowWindows[1].View.Exit();
            });
        }

        public void ShowSlideNavigation()
        {
            _gate.OnUi(() =>
            {
                if (_pptApplication == null) return;
                _pptApplication.SlideShowWindows[1].SlideNavigation.Visible = true;
            });
        }

        public void UnhideHiddenSlides()
        {
            _gate.OnUi(() =>
            {
                if (_slides == null) return;
                foreach (Slide slide in _slides)
                {
                    if (slide.SlideShowTransition.Hidden == Microsoft.Office.Core.MsoTriState.msoTrue)
                    {
                        slide.SlideShowTransition.Hidden = Microsoft.Office.Core.MsoTriState.msoFalse;
                    }
                }
                HasHiddenSlides = false;
            });
        }

        public int GetCurrentShowPosition()
        {
            var position = CurrentSlideIndex;
            _gate.OnUi(() =>
            {
                if (_pptApplication == null) return;
                try
                {
                    if (_pptApplication.SlideShowWindows.Count >= 1)
                    {
                        position = _pptApplication.SlideShowWindows[1].View.CurrentShowPosition;
                        CurrentSlideIndex = position;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.NewLog(ex);
                }
            });
            return position;
        }

        public string GetScreenshotLabel()
        {
            var name = PresentationName;
            var position = GetCurrentShowPosition();
            if (string.IsNullOrEmpty(name)) name = "Presentation";
            return name + "/" + position;
        }

        private bool TryAttachOnUi()
        {
            if (_isAttached) return true;

            object raw;
            try
            {
                raw = _getActiveObject();
            }
            catch (COMException)
            {
                // ROT miss is the expected 1s poll path. Trace at most; do not Error.
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("PowerPointSession.TryAttach", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
                return false;
            }

            if (raw == null) return false;

            var application = raw as Microsoft.Office.Interop.PowerPoint.Application;
            if (application == null)
            {
                _isAttached = true;
                SubscribeCount++;
                return true;
            }

            try
            {
                _pptApplication = application;
                _presentation = application.ActivePresentation;
                _slides = _presentation.Slides;
                SlideCount = _slides.Count;
                PresentationName = _presentation.Name;
                CurrentSlideIndex = TryReadCurrentSlideNumber();
                HasHiddenSlides = ScanHiddenSlides();
                IsSlideShowRunning = application.SlideShowWindows.Count >= 1;

                SubscribeComEvents();
                _isAttached = true;
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.NewLog(ex);
                CleanupComFields(release: true);
                return false;
            }
        }

        private void SubscribeComEvents()
        {
            if (_eventsSubscribed || _pptApplication == null) return;
            _pptApplication.PresentationClose += OnComPresentationClose;
            _pptApplication.SlideShowBegin += OnComSlideShowBegin;
            _pptApplication.SlideShowNextSlide += OnComSlideShowNextSlide;
            _pptApplication.SlideShowEnd += OnComSlideShowEnd;
            _eventsSubscribed = true;
            SubscribeCount++;
        }

        private void UnsubscribeComEvents()
        {
            if (!_eventsSubscribed || _pptApplication == null)
            {
                _eventsSubscribed = false;
                return;
            }

            try { _pptApplication.PresentationClose -= OnComPresentationClose; } catch (Exception ex) { LogHelper.NewLog(ex); }
            try { _pptApplication.SlideShowBegin -= OnComSlideShowBegin; } catch (Exception ex) { LogHelper.NewLog(ex); }
            try { _pptApplication.SlideShowNextSlide -= OnComSlideShowNextSlide; } catch (Exception ex) { LogHelper.NewLog(ex); }
            try { _pptApplication.SlideShowEnd -= OnComSlideShowEnd; } catch (Exception ex) { LogHelper.NewLog(ex); }
            _eventsSubscribed = false;
        }

        private void OnComSlideShowBegin(SlideShowWindow window)
        {
            _gate.OnUi(() =>
            {
                if (!_isAttached) return;
                try
                {
                    var snapshot = SnapshotBegan(window);
                    IsSlideShowRunning = true;
                    SlideCount = snapshot.SlideCount;
                    PresentationName = snapshot.PresentationName;
                    CurrentSlideIndex = snapshot.CurrentShowPosition;
                    SlideShowBegan?.Invoke(this, snapshot);
                }
                catch (Exception ex)
                {
                    LogHelper.NewLog(ex);
                }
            });
        }

        private void OnComSlideShowNextSlide(SlideShowWindow window)
        {
            _gate.OnUi(() =>
            {
                if (!_isAttached) return;
                try
                {
                    int position = window.View.CurrentShowPosition;
                    int count = window.Presentation.Slides.Count;
                    string name = window.Presentation.Name;
                    CurrentSlideIndex = position;
                    SlideCount = count;
                    PresentationName = name;
                    SlideChanged?.Invoke(this, new PowerPointSlideChangedEventArgs(name, count, position));
                }
                catch (Exception ex)
                {
                    LogHelper.NewLog(ex);
                }
            });
        }

        private void OnComSlideShowEnd(Presentation presentation)
        {
            _gate.OnUi(() =>
            {
                if (!_isAttached) return;
                try
                {
                    var snapshot = SnapshotEnded(presentation);
                    IsSlideShowRunning = false;
                    SlideShowEnded?.Invoke(this, snapshot);
                }
                catch (Exception ex)
                {
                    LogHelper.NewLog(ex);
                }
            });
        }

        private void OnComPresentationClose(Presentation presentation)
        {
            _gate.OnUi(() =>
            {
                var wasRunning = IsSlideShowRunning;
                var ended = SnapshotEnded(presentation);
                DetachOnUi(raiseDetached: true);
                if (wasRunning)
                {
                    SlideShowEnded?.Invoke(this, ended);
                }
            });
        }

        private void DetachOnUi(bool raiseDetached)
        {
            UnsubscribeComEvents();
            CleanupComFields(release: true);
            _isAttached = false;
            IsSlideShowRunning = false;
            HasHiddenSlides = false;
            if (raiseDetached)
            {
                try
                {
                    Detached?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    LogHelper.NewLog(ex);
                }
            }
        }

        private void CleanupComFields(bool release)
        {
            var slides = _slides;
            var presentation = _presentation;
            var application = _pptApplication;
            _slides = null;
            _presentation = null;
            _pptApplication = null;
            SlideCount = 0;
            CurrentSlideIndex = 0;
            PresentationName = null;

            if (!release) return;

            // Best-effort. Full COM RAII (FinalReleaseComObject / GC.Collect) is left unused:
            // releasing the Application RCW while Office events are in flight is a known crash source.
            TryReleaseCom(slides);
            TryReleaseCom(presentation);
            TryReleaseCom(application);
        }

        private int TryReadCurrentSlideNumber()
        {
            try
            {
                return _pptApplication.ActiveWindow.Selection.SlideRange.SlideNumber;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("TryReadCurrentSlideNumber: " + ex.Message, LogHelper.LogType.Trace);
                try
                {
                    return _pptApplication.SlideShowWindows[1].View.Slide.SlideNumber;
                }
                catch (Exception ex2)
                {
                    LogHelper.WriteLogToFile("TryReadCurrentSlideNumber.SlideShow: " + ex2.Message, LogHelper.LogType.Trace);
                    return 0;
                }
            }
        }

        private bool ScanHiddenSlides()
        {
            if (_slides == null) return false;
            foreach (Slide slide in _slides)
            {
                if (slide.SlideShowTransition.Hidden == Microsoft.Office.Core.MsoTriState.msoTrue)
                {
                    return true;
                }
            }
            return false;
        }

        private static PowerPointSlideShowBeganEventArgs SnapshotBegan(SlideShowWindow window)
        {
            var presentation = window.Presentation;
            return new PowerPointSlideShowBeganEventArgs(
                presentation.Name,
                presentation.Slides.Count,
                window.View.CurrentShowPosition,
                presentation.PageSetup.SlideWidth,
                presentation.PageSetup.SlideHeight);
        }

        private static PowerPointSlideShowEndedEventArgs SnapshotEnded(Presentation presentation)
        {
            if (presentation == null)
            {
                return new PowerPointSlideShowEndedEventArgs(null, 0);
            }

            try
            {
                return new PowerPointSlideShowEndedEventArgs(presentation.Name, presentation.Slides.Count);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("SnapshotEnded: " + ex.Message, LogHelper.LogType.Trace);
                return new PowerPointSlideShowEndedEventArgs(null, 0);
            }
        }

        private static void TryReleaseCom(object com)
        {
            if (com == null) return;
            try
            {
                if (Marshal.IsComObject(com))
                {
                    Marshal.ReleaseComObject(com);
                }
            }
            catch (Exception ex)
            {
                LogHelper.NewLog(ex);
            }
        }

        private static bool DefaultHasWpsProcess()
        {
            try
            {
                return Process.GetProcessesByName("wpp").Length > 0;
            }
            catch (Exception ex)
            {
                LogHelper.NewLog(ex);
                return false;
            }
        }

        private static bool DefaultIsWpsSupported()
        {
            try
            {
                return MainWindow.Settings.PowerPointSettings.IsSupportWPS;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("DefaultIsWpsSupported: " + ex.Message, LogHelper.LogType.Trace);
                return true;
            }
        }

        private static object DefaultGetActiveObject()
        {
            return Marshal.GetActiveObject(RotProgId);
        }
    }
}

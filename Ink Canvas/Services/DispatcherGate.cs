using System;
using System.Windows;
using System.Windows.Threading;

namespace InkCanvasPlus.Services
{
    /// <summary>
    /// Marshals work onto the WPF UI thread. Background → UI hops (PowerPointSession,
    /// screenshot tasks, delayed float-bar / notification UI) should go through here
    /// instead of raw <see cref="Application.Current"/>.Dispatcher.Invoke.
    /// </summary>
    public sealed class DispatcherGate
    {
        private readonly Dispatcher _dispatcher;

        public DispatcherGate(Dispatcher dispatcher = null)
        {
            _dispatcher = dispatcher ?? Application.Current?.Dispatcher;
        }

        public bool HasDispatcher => _dispatcher != null;

        public void OnUi(Action action)
        {
            if (action == null) return;
            var dispatcher = _dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;

            dispatcher.Invoke(action);
        }

        public void OnUiAsync(Action action)
        {
            if (action == null) return;
            var dispatcher = _dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;

            dispatcher.BeginInvoke(action);
        }

        /// <summary>
        /// One-shot <see cref="DispatcherTimer"/> on the UI dispatcher. Replaces
        /// <c>new Thread { Sleep; Dispatcher.Invoke }</c> delay hops.
        /// </summary>
        public DispatcherTimer RunOnce(TimeSpan delay, Action action)
        {
            if (action == null) return null;
            var dispatcher = _dispatcher;
            if (dispatcher == null) return null;
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return null;

            var timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher)
            {
                Interval = delay
            };
            EventHandler handler = null;
            handler = (sender, e) =>
            {
                timer.Stop();
                timer.Tick -= handler;
                if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;
                action();
            };
            timer.Tick += handler;
            timer.Start();
            return timer;
        }
    }
}

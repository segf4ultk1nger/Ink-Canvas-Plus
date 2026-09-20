using InkCanvasPlus.History;
using System;
using System.Collections.Generic;
using System.Windows.Ink;

namespace InkCanvasPlus.Domain
{
    public class InkDocument
    {
        public const int MaxPages = 99;

        private readonly List<InkPage> _pages = new List<InkPage>();

        public InkDocument(int pageCount = 1, bool persistHistory = true)
        {
            PersistHistory = persistHistory;
            Reset(pageCount);
        }

        /// <summary>
        /// Whiteboard pages replay TimeMachine history. PPT pages store strokes only (no undo stack across slides).
        /// </summary>
        public bool PersistHistory { get; set; }

        public IList<InkPage> Pages => _pages;

        public int CurrentIndex { get; private set; }

        public InkPage CurrentPage => _pages[CurrentIndex];

        public int PageCount => _pages.Count;

        public void Reset(int pageCount)
        {
            if (pageCount < 1) pageCount = 1;
            _pages.Clear();
            for (int i = 0; i < pageCount; i++)
            {
                _pages.Add(new InkPage());
            }
            CurrentIndex = 0;
        }

        public void SaveCurrent(StrokeCollection canvas, InkHistoryController history)
        {
            if (PersistHistory)
            {
                CurrentPage.SnapshotHistory(history);
            }
            else
            {
                CurrentPage.CaptureStrokes(canvas);
            }
        }

        public void RestoreCurrent(StrokeCollection canvas, InkHistoryController history)
        {
            if (PersistHistory)
            {
                CurrentPage.RestoreHistory(history);
            }
            else
            {
                history?.TimeMachine.ClearStrokeHistory();
                CurrentPage.ApplyStrokes(canvas);
            }
        }

        /// <summary>
        /// Save current → optional <paramref name="afterSave"/> (screenshot stays in the caller) →
        /// Clear(CodeInput) → swap page → Import + Apply (or ISF strokes when <see cref="PersistHistory"/> is false).
        /// </summary>
        public void GoTo(
            int index,
            StrokeCollection canvas,
            InkHistoryController history,
            bool saveCurrent = true,
            Action afterSave = null)
        {
            if (index < 0 || index >= _pages.Count) return;
            if (saveCurrent)
            {
                SaveCurrent(canvas, history);
            }
            afterSave?.Invoke();
            if (history != null)
            {
                history.BeginSuppress(CommitReason.CodeInput);
                canvas.Clear();
                history.EndSuppress();
            }
            else
            {
                canvas.Clear();
            }
            CurrentIndex = index;
            RestoreCurrent(canvas, history);
        }

        public InkPage AddAfterCurrent()
        {
            if (_pages.Count >= MaxPages) return null;
            var page = new InkPage();
            int insertAt = CurrentIndex + 1;
            _pages.Insert(insertAt, page);
            CurrentIndex = insertAt;
            return page;
        }

        public bool RemoveCurrent()
        {
            if (_pages.Count <= 1) return false;
            int index = CurrentIndex;
            _pages.RemoveAt(index);
            if (index >= _pages.Count)
            {
                CurrentIndex = _pages.Count - 1;
            }
            return true;
        }
    }
}

using InkCanvasPlus.Helpers;
using InkCanvasPlus.History;
using System;
using System.IO;
using System.Windows.Ink;

namespace InkCanvasPlus.Domain
{
    public class InkPage
    {
        public StrokeCollection Strokes { get; set; }

        public TimeMachineHistory[] History { get; set; }

        public void SnapshotHistory(InkHistoryController history)
        {
            if (history == null) throw new ArgumentNullException(nameof(history));
            History = history.TimeMachine.ExportTimeMachineHistory();
            history.TimeMachine.ClearStrokeHistory();
        }

        public void RestoreHistory(InkHistoryController history)
        {
            if (history == null) throw new ArgumentNullException(nameof(history));
            var items = History ?? Array.Empty<TimeMachineHistory>();
            history.TimeMachine.ImportTimeMachineHistory(items);
            foreach (var item in items)
            {
                history.Apply(item);
            }
        }

        public void CaptureStrokes(StrokeCollection canvas)
        {
            Strokes = canvas == null ? new StrokeCollection() : canvas.Clone();
        }

        public void ApplyStrokes(StrokeCollection canvas)
        {
            if (canvas == null || Strokes == null || Strokes.Count == 0) return;
            canvas.Add(Strokes.Clone());
        }

        public byte[] ToIsfBytes()
        {
            if (Strokes == null || Strokes.Count == 0) return Array.Empty<byte>();
            using (var ms = new MemoryStream())
            {
                Strokes.Save(ms);
                return ms.ToArray();
            }
        }

        public void LoadIsf(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                Strokes = new StrokeCollection();
                return;
            }
            using (var ms = new MemoryStream(data))
            {
                Strokes = new StrokeCollection(ms);
            }
        }

        public void LoadIsf(Stream stream)
        {
            if (stream == null || stream.Length == 0)
            {
                Strokes = new StrokeCollection();
                return;
            }
            if (stream.CanSeek) stream.Position = 0;
            Strokes = new StrokeCollection(stream);
        }
    }
}

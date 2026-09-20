using InkCanvasPlus.Domain;
using InkCanvasPlus.History;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Ink;
using System.Windows.Input;

namespace InkCanvasPlus.Tests
{
    [TestClass]
    public class InkDocumentTests
    {
        private static Stroke CreateStroke()
        {
            var points = new StylusPointCollection
            {
                new StylusPoint(0, 0),
                new StylusPoint(10, 10)
            };
            return new Stroke(points);
        }

        private static StrokeCollection CreateCanvas(out InkHistoryController controller)
        {
            var canvas = new StrokeCollection();
            controller = new InkHistoryController();
            controller.Attach(canvas);
            return canvas;
        }

        [TestMethod]
        public void AddPages_GoTo_RestoresPreviouslySavedStrokes()
        {
            var canvas = CreateCanvas(out var history);
            var document = new InkDocument();

            canvas.Add(CreateStroke());
            document.SaveCurrent(canvas, history);
            history.BeginSuppress(CommitReason.CodeInput);
            canvas.Clear();
            history.EndSuppress();

            Assert.IsNotNull(document.AddAfterCurrent());
            Assert.AreEqual(2, document.PageCount);
            Assert.AreEqual(1, document.CurrentIndex);
            Assert.AreEqual(0, canvas.Count);

            document.GoTo(0, canvas, history);
            Assert.AreEqual(0, document.CurrentIndex);
            Assert.AreEqual(1, canvas.Count);
        }

        [TestMethod]
        public void RemoveCurrentPage_LeavesNeighborAndDropsRemovedPage()
        {
            var canvas = CreateCanvas(out var history);
            var document = new InkDocument();

            canvas.Add(CreateStroke());
            document.GoTo(0, canvas, history);
            Assert.AreEqual(1, canvas.Count);

            document.SaveCurrent(canvas, history);
            history.BeginSuppress(CommitReason.CodeInput);
            canvas.Clear();
            history.EndSuppress();
            Assert.IsNotNull(document.AddAfterCurrent());

            canvas.Add(CreateStroke());
            document.SaveCurrent(canvas, history);
            history.BeginSuppress(CommitReason.CodeInput);
            canvas.Clear();
            history.EndSuppress();
            Assert.IsNotNull(document.AddAfterCurrent());
            Assert.AreEqual(3, document.PageCount);
            Assert.AreEqual(2, document.CurrentIndex);

            document.GoTo(1, canvas, history);
            Assert.AreEqual(1, canvas.Count);

            history.BeginSuppress(CommitReason.CodeInput);
            canvas.Clear();
            history.EndSuppress();
            Assert.IsTrue(document.RemoveCurrent());
            Assert.AreEqual(2, document.PageCount);
            Assert.AreEqual(1, document.CurrentIndex);

            document.RestoreCurrent(canvas, history);
            Assert.AreEqual(0, canvas.Count);

            document.GoTo(0, canvas, history);
            Assert.AreEqual(1, canvas.Count);
        }

        [TestMethod]
        public void DesktopPage_Restore_DoesNotDependOnWhiteboardPageHistory()
        {
            var canvas = CreateCanvas(out var history);
            var desktop = new InkPage();
            var board = new InkDocument();

            canvas.Add(CreateStroke());
            desktop.SnapshotHistory(history);
            Assert.IsNull(board.CurrentPage.History);

            history.BeginSuppress(CommitReason.CodeInput);
            canvas.Clear();
            history.EndSuppress();
            Assert.AreEqual(0, canvas.Count);

            desktop.RestoreHistory(history);
            Assert.AreEqual(1, canvas.Count);
        }

        [TestMethod]
        public void AddAfterCurrent_StopsAt99Pages()
        {
            var document = new InkDocument();
            Assert.AreEqual(1, document.PageCount);

            for (int i = 0; i < InkDocument.MaxPages - 1; i++)
            {
                Assert.IsNotNull(document.AddAfterCurrent());
            }

            Assert.AreEqual(InkDocument.MaxPages, document.PageCount);
            Assert.IsNull(document.AddAfterCurrent());
            Assert.AreEqual(InkDocument.MaxPages, document.PageCount);
        }

        [TestMethod]
        public void PptLike_SwitchPages_RestoresStrokesWithoutHistoryStack()
        {
            var canvas = CreateCanvas(out var history);
            var document = new InkDocument(3, persistHistory: false);

            canvas.Add(CreateStroke());
            document.GoTo(1, canvas, history);
            Assert.AreEqual(1, document.CurrentIndex);
            Assert.AreEqual(0, canvas.Count);
            Assert.IsNull(document.Pages[0].History);
            Assert.AreEqual(0, history.TimeMachine.ExportTimeMachineHistory().Length);

            canvas.Add(CreateStroke());
            document.GoTo(0, canvas, history);
            Assert.AreEqual(0, document.CurrentIndex);
            Assert.AreEqual(1, canvas.Count);
            Assert.IsNull(document.Pages[1].History);

            document.GoTo(1, canvas, history);
            Assert.AreEqual(1, canvas.Count);
        }
    }
}

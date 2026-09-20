using InkCanvasPlus.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Ink;
using System.Windows.Input;

namespace InkCanvasPlus.Tests
{
    [TestClass]
    public class TimeMachineTests
    {
        private static StrokeCollection CreateStroke()
        {
            var points = new StylusPointCollection
            {
                new StylusPoint(0, 0),
                new StylusPoint(10, 10)
            };
            return new StrokeCollection { new Stroke(points) };
        }

        [TestMethod]
        public void CommitUserInput_MakesUndoAvailable()
        {
            var timeMachine = new TimeMachine();
            bool undoAvailable = false;
            timeMachine.OnUndoStateChanged += status => undoAvailable = status;

            timeMachine.CommitStrokeUserInputHistory(CreateStroke());

            Assert.IsTrue(undoAvailable);
            Assert.IsNotNull(timeMachine.Undo());
        }

        [TestMethod]
        public void UndoThenRedo_RestoresSameHistoryItem()
        {
            var timeMachine = new TimeMachine();
            var stroke = CreateStroke();
            timeMachine.CommitStrokeUserInputHistory(stroke);

            var undone = timeMachine.Undo();
            Assert.IsNotNull(undone);
            Assert.IsTrue(undone.StrokeHasBeenCleared);

            var redone = timeMachine.Redo();
            Assert.IsNotNull(redone);
            Assert.AreSame(undone, redone);
            Assert.IsFalse(redone.StrokeHasBeenCleared);
        }

        [TestMethod]
        public void CommitAfterUndo_TruncatesRedoBranch()
        {
            var timeMachine = new TimeMachine();
            var first = CreateStroke();
            var second = CreateStroke();
            var third = CreateStroke();

            timeMachine.CommitStrokeUserInputHistory(first);
            timeMachine.CommitStrokeUserInputHistory(second);
            timeMachine.Undo();
            timeMachine.CommitStrokeUserInputHistory(third);

            var exported = timeMachine.ExportTimeMachineHistory();
            Assert.AreEqual(2, exported.Length);
            Assert.AreSame(first, exported[0].CurrentStroke);
            Assert.AreSame(third, exported[1].CurrentStroke);

            Assert.IsNull(timeMachine.Redo());
        }

        [TestMethod]
        public void EmptyUndo_ReturnsNullAndDoesNotThrow()
        {
            var timeMachine = new TimeMachine();
            bool undoAvailable = true;
            timeMachine.OnUndoStateChanged += status => undoAvailable = status;

            TimeMachineHistory result = timeMachine.Undo();

            Assert.IsNull(result);
            Assert.IsFalse(undoAvailable);
            Assert.IsNull(timeMachine.Undo());
        }

        [TestMethod]
        public void EmptyRedo_ReturnsNullAndDoesNotThrow()
        {
            var timeMachine = new TimeMachine();
            Assert.IsNull(timeMachine.Redo());

            timeMachine.CommitStrokeUserInputHistory(CreateStroke());
            Assert.IsNull(timeMachine.Redo());
        }

        [TestMethod]
        public void ExportImport_RoundtripsIndexAtHistoryTip()
        {
            var timeMachine = new TimeMachine();
            var first = CreateStroke();
            var second = CreateStroke();
            timeMachine.CommitStrokeUserInputHistory(first);
            timeMachine.CommitStrokeUserInputHistory(second);

            var exported = timeMachine.ExportTimeMachineHistory();
            Assert.AreEqual(2, exported.Length);

            var imported = new TimeMachine();
            bool undoAvailable = false;
            bool redoAvailable = true;
            imported.OnUndoStateChanged += status => undoAvailable = status;
            imported.OnRedoStateChanged += status => redoAvailable = status;

            imported.ImportTimeMachineHistory(exported);

            Assert.IsTrue(undoAvailable);
            Assert.IsFalse(redoAvailable);
            Assert.IsNull(imported.Redo());

            var firstUndo = imported.Undo();
            Assert.IsNotNull(firstUndo);
            Assert.AreSame(second, firstUndo.CurrentStroke);

            var secondUndo = imported.Undo();
            Assert.IsNotNull(secondUndo);
            Assert.AreSame(first, secondUndo.CurrentStroke);

            Assert.IsNull(imported.Undo());

            var redone = imported.Redo();
            Assert.IsNotNull(redone);
            Assert.AreSame(first, redone.CurrentStroke);
        }
    }
}

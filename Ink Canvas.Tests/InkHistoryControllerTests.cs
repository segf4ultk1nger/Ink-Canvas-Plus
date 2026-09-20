using InkCanvasPlus.Helpers;
using InkCanvasPlus.History;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Windows.Ink;
using System.Windows.Input;

namespace InkCanvasPlus.Tests
{
    [TestClass]
    public class InkHistoryControllerTests
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

        [TestMethod]
        public void Attach_AddStroke_CommitsUserInput()
        {
            var strokes = new StrokeCollection();
            var controller = new InkHistoryController();
            controller.Attach(strokes);

            strokes.Add(CreateStroke());

            var history = controller.TimeMachine.ExportTimeMachineHistory();
            Assert.AreEqual(1, history.Length);
            Assert.AreEqual(TimeMachineHistoryType.UserInput, history[0].CommitType);
            Assert.AreEqual(1, history[0].CurrentStroke.Count);
            Assert.IsFalse(history[0].StrokeHasBeenCleared);
        }

        [TestMethod]
        public void Apply_UndoThenRedo_RestoresStrokeCount()
        {
            var strokes = new StrokeCollection();
            var controller = new InkHistoryController();
            controller.Attach(strokes);
            strokes.Add(CreateStroke());
            Assert.AreEqual(1, strokes.Count);

            var undone = controller.TimeMachine.Undo();
            Assert.IsNotNull(undone);
            controller.Apply(undone);
            Assert.AreEqual(0, strokes.Count);

            var redone = controller.TimeMachine.Redo();
            Assert.IsNotNull(redone);
            controller.Apply(redone);
            Assert.AreEqual(1, strokes.Count);
        }

        [TestMethod]
        public void BeginSuppressCodeInput_Clear_DoesNotCommit()
        {
            var strokes = new StrokeCollection();
            var controller = new InkHistoryController();
            controller.Attach(strokes);
            strokes.Add(CreateStroke());

            controller.BeginSuppress(CommitReason.CodeInput);
            strokes.Clear();
            controller.EndSuppress();

            Assert.AreEqual(0, strokes.Count);
            var history = controller.TimeMachine.ExportTimeMachineHistory();
            Assert.AreEqual(1, history.Length);
            Assert.AreEqual(TimeMachineHistoryType.UserInput, history[0].CommitType);
        }

        [TestMethod]
        public void NotifyPointerUp_AfterPointEraseBuffers_CommitsEraseHistory()
        {
            var strokes = new StrokeCollection();
            var controller = new InkHistoryController();
            var isEraseByPoint = false;
            controller.IsEraseByPoint = () => isEraseByPoint;
            controller.Attach(strokes);

            var original = CreateStroke();
            strokes.Add(original);
            Assert.AreEqual(1, controller.TimeMachine.ExportTimeMachineHistory().Length);

            isEraseByPoint = true;
            var fragment = CreateStroke();
            strokes.Remove(original);
            strokes.Add(fragment);
            Assert.AreEqual(1, controller.TimeMachine.ExportTimeMachineHistory().Length);
            Assert.AreEqual(1, strokes.Count);

            controller.NotifyPointerUp();

            var history = controller.TimeMachine.ExportTimeMachineHistory();
            Assert.AreEqual(2, history.Length);
            Assert.AreEqual(TimeMachineHistoryType.Clear, history[1].CommitType);
            Assert.IsTrue(history[1].StrokeHasBeenCleared);
            Assert.IsTrue(history[1].CurrentStroke.Contains(original));
            Assert.IsTrue(history[1].ReplacedStroke.Contains(fragment));
        }

        [TestMethod]
        public void Apply_StrokeNotInCollection_DoesNotThrow()
        {
            var strokes = new StrokeCollection();
            var controller = new InkHistoryController();
            controller.Attach(strokes);

            var missing = CreateStroke();
            var userInput = new TimeMachineHistory(
                new StrokeCollection { missing },
                TimeMachineHistoryType.UserInput,
                true);
            controller.Apply(userInput);
            Assert.AreEqual(0, strokes.Count);

            var stylusPoints = new Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>>
            {
                { missing, Tuple.Create(missing.StylusPoints.Clone(), missing.StylusPoints.Clone()) }
            };
            var manipulation = new TimeMachineHistory(stylusPoints, TimeMachineHistoryType.Manipulation);
            controller.Apply(manipulation);
            Assert.AreEqual(0, strokes.Count);
        }
    }
}

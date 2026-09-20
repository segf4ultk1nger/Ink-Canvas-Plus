using InkCanvasPlus.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Controls;
using System.Windows.Ink;

namespace InkCanvasPlus.Tests
{
    [TestClass]
    public class InkToolApplierTests
    {
        private static InkToolApplier Create(out FakeInkCanvasToolTarget target)
        {
            target = new FakeInkCanvasToolTarget();
            return new InkToolApplier(target);
        }

        [TestMethod]
        public void ApplyTool_Pen_ThenPointEraser_ThenPen_RestoresPen()
        {
            var applier = Create(out var target);
            var eraser = new EllipseStylusShape(50, 50);

            applier.ApplyTool(InkTool.Pen);
            Assert.AreEqual(InkTool.Pen, applier.CurrentTool);
            Assert.AreEqual(InkCanvasEditingMode.Ink, target.EditingMode);
            Assert.IsFalse(target.ForceEraser);
            Assert.AreEqual(0, target.DrawingShapeMode);

            applier.ApplyTool(InkTool.PointEraser, eraserShape: eraser);
            Assert.AreEqual(InkTool.PointEraser, applier.CurrentTool);
            Assert.AreEqual(InkCanvasEditingMode.EraseByPoint, target.EditingMode);
            Assert.IsTrue(target.ForceEraser);
            Assert.AreSame(eraser, target.EraserShape);

            applier.ApplyTool(InkTool.Pen);
            Assert.AreEqual(InkTool.Pen, applier.CurrentTool);
            Assert.AreEqual(InkCanvasEditingMode.Ink, target.EditingMode);
            Assert.IsFalse(target.ForceEraser);
            Assert.IsFalse(target.IsMarkerMode);
            Assert.AreEqual(0, target.DrawingShapeMode);
            Assert.IsTrue(target.IsManipulationEnabled);
        }

        [TestMethod]
        public void ApplyTool_Lasso_SetsSelect()
        {
            var applier = Create(out var target);

            applier.ApplyTool(InkTool.Pen);
            applier.ApplyTool(InkTool.Lasso);

            Assert.AreEqual(InkTool.Lasso, applier.CurrentTool);
            Assert.AreEqual(InkCanvasEditingMode.Select, target.EditingMode);
            Assert.IsTrue(target.ForceEraser);
            Assert.IsFalse(target.IsManipulationEnabled);
            Assert.AreEqual(0, target.DrawingShapeMode);
        }

        [TestMethod]
        public void ApplyTool_Marker_SetsInkAndMarkerFlag()
        {
            var applier = Create(out var target);

            applier.ApplyTool(InkTool.PointEraser, eraserShape: new EllipseStylusShape(5, 5));
            applier.ApplyTool(InkTool.Marker);

            Assert.AreEqual(InkTool.Marker, applier.CurrentTool);
            Assert.AreEqual(InkCanvasEditingMode.Ink, target.EditingMode);
            Assert.IsTrue(target.IsMarkerMode);
            Assert.IsFalse(target.ForceEraser);
        }

        [TestMethod]
        public void ApplyTool_Shape_SetsNoneAndShapeMode()
        {
            var applier = Create(out var target);

            applier.ApplyTool(InkTool.Pen);
            applier.ApplyTool(InkTool.Shape, shapeMode: 3);

            Assert.AreEqual(InkTool.Shape, applier.CurrentTool);
            Assert.AreEqual(InkCanvasEditingMode.None, target.EditingMode);
            Assert.AreEqual(3, target.DrawingShapeMode);
            Assert.IsTrue(target.ForceEraser);
            Assert.IsTrue(target.IsManipulationEnabled);
        }

        [TestMethod]
        public void ApplyTool_MultiTouch_SetsNoneWithoutClearingShapeMode()
        {
            var applier = Create(out var target);
            target.DrawingShapeMode = 4;
            target.ForceEraser = false;

            applier.ApplyTool(InkTool.MultiTouch);

            Assert.AreEqual(InkTool.MultiTouch, applier.CurrentTool);
            Assert.AreEqual(InkCanvasEditingMode.None, target.EditingMode);
            Assert.AreEqual(4, target.DrawingShapeMode);
            Assert.IsFalse(target.ForceEraser);
        }

        [TestMethod]
        public void ApplyTool_Eraser_NullShape_LeavesExistingEraserShape()
        {
            var applier = Create(out var target);
            var existing = new EllipseStylusShape(9, 9);
            target.EraserShape = existing;

            applier.ApplyTool(InkTool.StrokeEraser);

            Assert.AreEqual(InkCanvasEditingMode.EraseByStroke, target.EditingMode);
            Assert.AreSame(existing, target.EraserShape);
        }

        private sealed class FakeInkCanvasToolTarget : IInkCanvasToolTarget
        {
            public InkCanvasEditingMode EditingMode { get; set; } = InkCanvasEditingMode.Ink;
            public StylusShape EraserShape { get; set; }
            public int DrawingShapeMode { get; set; }
            public bool ForceEraser { get; set; }
            public bool IsManipulationEnabled { get; set; } = true;
            public bool IsMarkerMode { get; set; }
        }
    }
}

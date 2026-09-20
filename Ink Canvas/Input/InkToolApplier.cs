using System;
using System.Windows.Controls;
using System.Windows.Ink;

namespace InkCanvasPlus.Input
{
    /// <summary>
    /// Single entry for user-facing tool switches. Still sets InkCanvas EditingMode,
    /// EraserShape, and drawingShapeMode internally — this is not a Processor pipeline.
    /// </summary>
    public sealed class InkToolApplier
    {
        private readonly IInkCanvasToolTarget _target;

        public InkToolApplier(IInkCanvasToolTarget target)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public InkTool CurrentTool { get; private set; } = InkTool.Pen;

        /// <summary>
        /// Apply a user-facing tool. Does not invent eraser sizes: pass <paramref name="eraserShape"/>
        /// from the caller (BtnErase still computes 50×coefficient / 5,5). Null leaves EraserShape unchanged.
        /// </summary>
        public void ApplyTool(InkTool tool, int shapeMode = 0, StylusShape eraserShape = null)
        {
            switch (tool)
            {
                case InkTool.Pen:
                    _target.IsMarkerMode = false;
                    _target.DrawingShapeMode = 0;
                    _target.ForceEraser = false;
                    _target.IsManipulationEnabled = true;
                    _target.EditingMode = InkCanvasEditingMode.Ink;
                    break;
                case InkTool.Marker:
                    _target.IsMarkerMode = true;
                    _target.DrawingShapeMode = 0;
                    _target.ForceEraser = false;
                    _target.IsManipulationEnabled = true;
                    _target.EditingMode = InkCanvasEditingMode.Ink;
                    break;
                case InkTool.PointEraser:
                    _target.DrawingShapeMode = 0;
                    _target.ForceEraser = true;
                    if (eraserShape != null) _target.EraserShape = eraserShape;
                    _target.EditingMode = InkCanvasEditingMode.EraseByPoint;
                    break;
                case InkTool.StrokeEraser:
                    _target.DrawingShapeMode = 0;
                    _target.ForceEraser = true;
                    if (eraserShape != null) _target.EraserShape = eraserShape;
                    _target.EditingMode = InkCanvasEditingMode.EraseByStroke;
                    break;
                case InkTool.Lasso:
                    _target.DrawingShapeMode = 0;
                    _target.ForceEraser = true;
                    _target.IsManipulationEnabled = false;
                    _target.EditingMode = InkCanvasEditingMode.Select;
                    break;
                case InkTool.Shape:
                    _target.ForceEraser = true;
                    _target.DrawingShapeMode = shapeMode;
                    _target.IsManipulationEnabled = true;
                    _target.EditingMode = InkCanvasEditingMode.None;
                    break;
                case InkTool.MultiTouch:
                    _target.EditingMode = InkCanvasEditingMode.None;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tool), tool, null);
            }

            CurrentTool = tool;
        }
    }
}

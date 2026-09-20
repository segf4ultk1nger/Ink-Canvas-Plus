using InkCanvasPlus.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;

namespace InkCanvasPlus
{
    public partial class MainWindow : Window
    {
        private InkToolApplier _inkTools;

        /// <summary>
        /// User-facing tool switch. Hotkeys, floating-bar buttons, eraser, lasso, pen/marker, and
        /// shape-tool buttons must call this. Rubber-band drawing and touch heuristics must not.
        /// </summary>
        private void ApplyTool(InkTool tool, int shapeMode = 0, StylusShape eraserShape = null)
        {
            _inkTools.ApplyTool(tool, shapeMode, eraserShape);
        }

        /// <summary>
        /// Restore ink (pen or marker) without dropping the marker flag. Used when a color click
        /// or BtnPen should return to drawing, matching previous ColorSwitchCheck / BtnPen behavior.
        /// </summary>
        private void ApplyInkOrMarkerTool()
        {
            ApplyTool(isMarkerMode ? InkTool.Marker : InkTool.Pen);
        }

        /// <summary>
        /// User selected a geometry from the shape palette. Cancels single-finger-drag like the old
        /// BtnDraw* handlers. Long-press (Image_MouseDown) calls ApplyTool(Shape) without this.
        /// </summary>
        private void SelectShapeTool(int mode)
        {
            ApplyTool(InkTool.Shape, mode);
            CancelSingleFingerDragMode();
        }

        /// <summary>
        /// Temporarily disable InkCanvas inking so rubber-band shape preview can draw.
        /// Not a user tool switch — do not route through ApplyTool (that would re-enter Shape
        /// and can break preview).
        /// </summary>
        private void SuppressInkForShapePreview()
        {
            if (inkCanvas.EditingMode != InkCanvasEditingMode.None)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
            }
        }

        private sealed class MainWindowInkCanvasToolTarget : IInkCanvasToolTarget
        {
            private readonly MainWindow _window;

            public MainWindowInkCanvasToolTarget(MainWindow window)
            {
                _window = window;
            }

            public InkCanvasEditingMode EditingMode
            {
                get { return _window.inkCanvas.EditingMode; }
                set { _window.inkCanvas.EditingMode = value; }
            }

            public StylusShape EraserShape
            {
                get { return _window.inkCanvas.EraserShape; }
                set { _window.inkCanvas.EraserShape = value; }
            }

            public int DrawingShapeMode
            {
                get { return _window.drawingShapeMode; }
                set { _window.drawingShapeMode = value; }
            }

            public bool ForceEraser
            {
                get { return _window.forceEraser; }
                set { _window.forceEraser = value; }
            }

            public bool IsManipulationEnabled
            {
                get { return _window.inkCanvas.IsManipulationEnabled; }
                set { _window.inkCanvas.IsManipulationEnabled = value; }
            }

            public bool IsMarkerMode
            {
                get { return _window.isMarkerMode; }
                set { _window.isMarkerMode = value; }
            }
        }
    }
}

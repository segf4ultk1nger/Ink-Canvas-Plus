using System.Windows.Controls;
using System.Windows.Ink;

namespace InkCanvasPlus.Input
{
    /// <summary>
    /// User-facing ink tools. Transient InkCanvas modes (GestureOnly, rubber-band None)
    /// are not tools and must not go through <see cref="InkToolApplier.ApplyTool"/>.
    /// </summary>
    public enum InkTool
    {
        Pen,
        Marker,
        PointEraser,
        StrokeEraser,
        Lasso,
        Shape,
        MultiTouch
    }

    /// <summary>
    /// Testable surface for applying a tool without a WPF Window.
    /// MainWindow adapts <c>inkCanvas</c> plus shape/eraser flags.
    /// </summary>
    public interface IInkCanvasToolTarget
    {
        InkCanvasEditingMode EditingMode { get; set; }

        StylusShape EraserShape { get; set; }

        int DrawingShapeMode { get; set; }

        bool ForceEraser { get; set; }

        bool IsManipulationEnabled { get; set; }

        bool IsMarkerMode { get; set; }
    }
}

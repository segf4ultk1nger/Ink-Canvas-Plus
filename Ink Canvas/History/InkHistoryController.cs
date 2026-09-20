using InkCanvasPlus.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasPlus.History
{
    public enum CommitReason
    {
        UserInput,
        CodeInput,
        ShapeDrawing,
        ShapeRecognition,
        ClearingCanvas,
        Manipulation
    }

    /// <summary>
    /// Owns TimeMachine, stroke-collection listening, apply, and point-eraser buffers.
    /// Undo granularity and commit timing match the previous MainWindow implementation.
    /// </summary>
    public class InkHistoryController
    {
        private StrokeCollection _strokes;

        public InkHistoryController() : this(new TimeMachine())
        {
        }

        public InkHistoryController(TimeMachine timeMachine)
        {
            TimeMachine = timeMachine ?? throw new ArgumentNullException(nameof(timeMachine));
        }

        public TimeMachine TimeMachine { get; }

        public CommitReason CurrentCommitType { get; set; } = CommitReason.UserInput;

        /// <summary>
        /// When true, StrokesChanged buffers added/removed strokes until <see cref="NotifyPointerUp"/>.
        /// MainWindow should bind this to InkCanvas EraseByPoint.
        /// </summary>
        public Func<bool> IsEraseByPoint { get; set; }

        /// <summary>
        /// Number of strokes that must be present in the manipulation dictionary before auto-commit.
        /// Defaults to the attached collection count (selected-or-all is supplied by MainWindow).
        /// </summary>
        public Func<int> GetManipulationTargetCount { get; set; }

        /// <summary>
        /// Extra gate for auto-committing manipulation (no fingers down, not mouse-dragging).
        /// Null is treated as true.
        /// </summary>
        public Func<bool> CanAutoCommitManipulation { get; set; }

        public StrokeCollection ReplacedStroke { get; set; }
        public StrokeCollection AddedStroke { get; set; }

        public Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>> StrokeManipulationHistory { get; set; }

        public Dictionary<Stroke, StylusPointCollection> StrokeInitialHistory { get; } =
            new Dictionary<Stroke, StylusPointCollection>();

        public Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>> DrawingAttributesHistory { get; set; } =
            new Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>>();

        public Dictionary<Guid, List<Stroke>> DrawingAttributesHistoryFlag { get; } =
            new Dictionary<Guid, List<Stroke>>()
            {
                { DrawingAttributeIds.Color, new List<Stroke>() },
                { DrawingAttributeIds.DrawingFlags, new List<Stroke>() },
                { DrawingAttributeIds.IsHighlighter, new List<Stroke>() },
                { DrawingAttributeIds.StylusHeight, new List<Stroke>() },
                { DrawingAttributeIds.StylusTip, new List<Stroke>() },
                { DrawingAttributeIds.StylusTipTransform, new List<Stroke>() },
                { DrawingAttributeIds.StylusWidth, new List<Stroke>() }
            };

        public void BeginSuppress(CommitReason reason)
        {
            CurrentCommitType = reason;
        }

        public void EndSuppress()
        {
            CurrentCommitType = CommitReason.UserInput;
        }

        public void Attach(StrokeCollection strokes)
        {
            Detach();
            _strokes = strokes;
            if (_strokes == null) return;
            _strokes.StrokesChanged += StrokesOnStrokesChanged;
            foreach (var stroke in _strokes)
            {
                SubscribeStroke(stroke);
                StrokeInitialHistory[stroke] = stroke.StylusPoints.Clone();
            }
        }

        public void Detach()
        {
            if (_strokes == null) return;
            _strokes.StrokesChanged -= StrokesOnStrokesChanged;
            foreach (var stroke in _strokes)
            {
                UnsubscribeStroke(stroke);
            }
            _strokes = null;
        }

        public void Apply(TimeMachineHistory item)
        {
            if (item == null || _strokes == null) return;
            CurrentCommitType = CommitReason.CodeInput;
            try
            {
                ApplyCore(item);
            }
            catch (Exception ex)
            {
                LogHelper.NewLog(ex);
                LogHelper.WriteLogToFile("InkHistoryController.Apply failed: " + ex, LogHelper.LogType.Error);
            }
            finally
            {
                CurrentCommitType = CommitReason.UserInput;
            }
        }

        /// <summary>
        /// Commits point-eraser buffers gathered during StrokesChanged. Shape/manipulation/DA commits stay in MainWindow.
        /// </summary>
        public void NotifyPointerUp()
        {
            if (ReplacedStroke != null || AddedStroke != null)
            {
                TimeMachine.CommitStrokeEraseHistory(ReplacedStroke, AddedStroke);
                AddedStroke = null;
                ReplacedStroke = null;
            }
        }

        private void ApplyCore(TimeMachineHistory item)
        {
            if (item.CommitType == TimeMachineHistoryType.UserInput)
            {
                if (!item.StrokeHasBeenCleared)
                {
                    AddStrokes(item.CurrentStroke);
                }
                else
                {
                    RemoveStrokes(item.CurrentStroke, "UserInput");
                }
            }
            else if (item.CommitType == TimeMachineHistoryType.ShapeRecognition)
            {
                if (item.StrokeHasBeenCleared)
                {
                    RemoveStrokes(item.CurrentStroke, "ShapeRecognition.Current");
                    AddStrokes(item.ReplacedStroke);
                }
                else
                {
                    AddStrokes(item.CurrentStroke);
                    RemoveStrokes(item.ReplacedStroke, "ShapeRecognition.Replaced");
                }
            }
            else if (item.CommitType == TimeMachineHistoryType.Manipulation)
            {
                if (item.StylusPointDictionary == null) return;
                if (!item.StrokeHasBeenCleared)
                {
                    foreach (var currentStroke in item.StylusPointDictionary)
                    {
                        if (TryGetStrokeInCollection(currentStroke.Key, "Manipulation"))
                        {
                            currentStroke.Key.StylusPoints = currentStroke.Value.Item2;
                        }
                    }
                }
                else
                {
                    foreach (var currentStroke in item.StylusPointDictionary)
                    {
                        if (TryGetStrokeInCollection(currentStroke.Key, "Manipulation"))
                        {
                            currentStroke.Key.StylusPoints = currentStroke.Value.Item1;
                        }
                    }
                }
            }
            else if (item.CommitType == TimeMachineHistoryType.DrawingAttributes)
            {
                if (item.DrawingAttributes == null) return;
                if (!item.StrokeHasBeenCleared)
                {
                    foreach (var currentStroke in item.DrawingAttributes)
                    {
                        if (TryGetStrokeInCollection(currentStroke.Key, "DrawingAttributes"))
                        {
                            currentStroke.Key.DrawingAttributes = currentStroke.Value.Item2;
                        }
                    }
                }
                else
                {
                    foreach (var currentStroke in item.DrawingAttributes)
                    {
                        if (TryGetStrokeInCollection(currentStroke.Key, "DrawingAttributes"))
                        {
                            currentStroke.Key.DrawingAttributes = currentStroke.Value.Item1;
                        }
                    }
                }
            }
            else if (item.CommitType == TimeMachineHistoryType.Clear)
            {
                if (!item.StrokeHasBeenCleared)
                {
                    AddStrokes(item.CurrentStroke);
                    RemoveStrokes(item.ReplacedStroke, "Clear.Replaced");
                }
                else
                {
                    AddStrokes(item.ReplacedStroke);
                    RemoveStrokes(item.CurrentStroke, "Clear.Current");
                }
            }
        }

        private void AddStrokes(StrokeCollection source)
        {
            if (source == null) return;
            foreach (var stroke in source)
            {
                if (stroke == null) continue;
                if (!_strokes.Contains(stroke))
                    _strokes.Add(stroke);
            }
        }

        private void RemoveStrokes(StrokeCollection source, string context)
        {
            if (source == null) return;
            foreach (var stroke in source)
            {
                if (stroke == null) continue;
                if (_strokes.Contains(stroke))
                    _strokes.Remove(stroke);
                else
                    LogMissingStroke(context);
            }
        }

        private bool TryGetStrokeInCollection(Stroke stroke, string context)
        {
            if (stroke != null && _strokes.Contains(stroke)) return true;
            LogMissingStroke(context);
            return false;
        }

        private static void LogMissingStroke(string context)
        {
            var message = "InkHistoryController.Apply skipped a stroke that is not in the collection (" + context + ")";
            LogHelper.WriteLogToFile(message, LogHelper.LogType.Error);
            LogHelper.WriteLogToFile(message, LogHelper.LogType.Trace);
        }

        private void StrokesOnStrokesChanged(object sender, StrokeCollectionChangedEventArgs e)
        {
            foreach (var stroke in e?.Removed)
            {
                UnsubscribeStroke(stroke);
                StrokeInitialHistory.Remove(stroke);
            }
            foreach (var stroke in e?.Added)
            {
                SubscribeStroke(stroke);
                StrokeInitialHistory[stroke] = stroke.StylusPoints.Clone();
            }
            if (CurrentCommitType == CommitReason.CodeInput || CurrentCommitType == CommitReason.ShapeDrawing) return;
            if ((e.Added.Count != 0 || e.Removed.Count != 0) && IsPointEraserActive())
            {
                if (AddedStroke == null) AddedStroke = new StrokeCollection();
                if (ReplacedStroke == null) ReplacedStroke = new StrokeCollection();
                AddedStroke.Add(e.Added);
                ReplacedStroke.Add(e.Removed);
                return;
            }
            if (e.Added.Count != 0)
            {
                if (CurrentCommitType == CommitReason.ShapeRecognition)
                {
                    TimeMachine.CommitStrokeShapeHistory(ReplacedStroke, e.Added);
                    ReplacedStroke = null;
                    return;
                }
                else
                {
                    TimeMachine.CommitStrokeUserInputHistory(e.Added);
                    return;
                }
            }

            if (e.Removed.Count != 0)
            {
                if (CurrentCommitType == CommitReason.ShapeRecognition)
                {
                    ReplacedStroke = e.Removed;
                    return;
                }
                else if (!IsPointEraserActive() || CurrentCommitType == CommitReason.ClearingCanvas)
                {
                    TimeMachine.CommitStrokeEraseHistory(e.Removed);
                    return;
                }
            }
        }

        private bool IsPointEraserActive()
        {
            return IsEraseByPoint != null && IsEraseByPoint();
        }

        private void SubscribeStroke(Stroke stroke)
        {
            if (stroke == null) return;
            stroke.StylusPointsChanged += Stroke_StylusPointsChanged;
            stroke.StylusPointsReplaced += Stroke_StylusPointsReplaced;
            stroke.DrawingAttributesChanged += Stroke_DrawingAttributesChanged;
        }

        private void UnsubscribeStroke(Stroke stroke)
        {
            if (stroke == null) return;
            stroke.StylusPointsChanged -= Stroke_StylusPointsChanged;
            stroke.StylusPointsReplaced -= Stroke_StylusPointsReplaced;
            stroke.DrawingAttributesChanged -= Stroke_DrawingAttributesChanged;
        }

        private void Stroke_DrawingAttributesChanged(object sender, PropertyDataChangedEventArgs e)
        {
            var key = sender as Stroke;
            var currentValue = key.DrawingAttributes.Clone();
            DrawingAttributesHistory.TryGetValue(key, out var previousTuple);
            var previousValue = previousTuple?.Item1 ?? currentValue.Clone();
            var needUpdateValue = !DrawingAttributesHistoryFlag[e.PropertyGuid].Contains(key);
            if (needUpdateValue)
            {
                DrawingAttributesHistoryFlag[e.PropertyGuid].Add(key);
                Debug.Write(e.PreviousValue.ToString());
            }
            if (e.PropertyGuid == DrawingAttributeIds.Color && needUpdateValue)
            {
                previousValue.Color = (Color)e.PreviousValue;
            }
            if (e.PropertyGuid == DrawingAttributeIds.IsHighlighter && needUpdateValue)
            {
                previousValue.IsHighlighter = (bool)e.PreviousValue;
            }
            if (e.PropertyGuid == DrawingAttributeIds.StylusHeight && needUpdateValue)
            {
                previousValue.Height = (double)e.PreviousValue;
            }
            if (e.PropertyGuid == DrawingAttributeIds.StylusWidth && needUpdateValue)
            {
                previousValue.Width = (double)e.PreviousValue;
            }
            if (e.PropertyGuid == DrawingAttributeIds.StylusTip && needUpdateValue)
            {
                previousValue.StylusTip = (StylusTip)e.PreviousValue;
            }
            if (e.PropertyGuid == DrawingAttributeIds.StylusTipTransform && needUpdateValue)
            {
                previousValue.StylusTipTransform = (Matrix)e.PreviousValue;
            }
            if (e.PropertyGuid == DrawingAttributeIds.DrawingFlags && needUpdateValue)
            {
                previousValue.IgnorePressure = (bool)e.PreviousValue;
            }
            DrawingAttributesHistory[key] = new Tuple<DrawingAttributes, DrawingAttributes>(previousValue, currentValue);
        }

        private void Stroke_StylusPointsReplaced(object sender, StylusPointsReplacedEventArgs e)
        {
            StrokeInitialHistory[sender as Stroke] = e.NewStylusPoints.Clone();
        }

        private void Stroke_StylusPointsChanged(object sender, EventArgs e)
        {
            var count = GetManipulationTargetCount != null
                ? GetManipulationTargetCount()
                : (_strokes != null ? _strokes.Count : 0);
            if (StrokeManipulationHistory == null)
            {
                StrokeManipulationHistory = new Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>>();
            }
            StrokeManipulationHistory[sender as Stroke] =
                new Tuple<StylusPointCollection, StylusPointCollection>(StrokeInitialHistory[sender as Stroke], (sender as Stroke).StylusPoints.Clone());
            var canCommit = CanAutoCommitManipulation == null || CanAutoCommitManipulation();
            if ((StrokeManipulationHistory.Count == count || sender == null) && canCommit)
            {
                TimeMachine.CommitStrokeManipulationHistory(StrokeManipulationHistory);
                foreach (var item in StrokeManipulationHistory)
                {
                    StrokeInitialHistory[item.Key] = item.Value.Item2;
                }
                StrokeManipulationHistory = null;
            }
        }
    }
}

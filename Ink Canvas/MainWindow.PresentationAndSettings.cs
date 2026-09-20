using AutoUpdaterDotNET;
using InkCanvasPlus.Domain;
using InkCanvasPlus.Helpers;
using InkCanvasPlus.History;
using InkCanvasPlus.Input;
using InkCanvasPlus.Services;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Helpers;
using IWshRuntimeLibrary;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Input.StylusPlugIns;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Application = System.Windows.Application;
using File = System.IO.File;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;
using Point = System.Windows.Point;
using Timer = System.Timers.Timer;


namespace InkCanvasPlus
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Touch Events

        #region Multi-Touch

        bool isInMultiTouchMode = false;
        private void BorderMultiTouchMode_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isInMultiTouchMode)
            {
                inkCanvas.StylusDown -= MainWindow_StylusDown;
                inkCanvas.StylusMove -= MainWindow_StylusMove;
                inkCanvas.StylusUp -= MainWindow_StylusUp;
                inkCanvas.TouchDown -= MainWindow_TouchDown;
                inkCanvas.TouchDown += Main_Grid_TouchDown;
                ApplyInkOrMarkerTool();
                inkCanvas.Children.Clear();
                isInMultiTouchMode = false;
                SymbolIconMultiTouchMode.Symbol = iNKORE.UI.WPF.Modern.Controls.Symbol.People;
            }
            else
            {
                inkCanvas.StylusDown += MainWindow_StylusDown;
                inkCanvas.StylusMove += MainWindow_StylusMove;
                inkCanvas.StylusUp += MainWindow_StylusUp;
                inkCanvas.TouchDown -= Main_Grid_TouchDown;
                inkCanvas.TouchDown += MainWindow_TouchDown;
                ApplyTool(InkTool.MultiTouch);
                inkCanvas.Children.Clear();
                isInMultiTouchMode = true;
                SymbolIconMultiTouchMode.Symbol = iNKORE.UI.WPF.Modern.Controls.Symbol.Contact;
            }
        }

        private void MainWindow_TouchDown(object sender, TouchEventArgs e)
        {
            double boundWidth = GetTouchBoundWidth(e);
            if (boundWidth > BoundsWidth)
            {
                inkCanvas.EraserShape = new EllipseStylusShape(boundWidth, boundWidth);
                TouchDownPointsList[e.TouchDevice.Id] = InkCanvasEditingMode.EraseByPoint;
                // Transient palm-size erase while MultiTouch is on. Not a user tool switch:
                // ApplyTool(PointEraser) would set ForceEraser and clear shape mode.
                inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            }
            else
            {
                TouchDownPointsList[e.TouchDevice.Id] = InkCanvasEditingMode.None;
                // Custom stroke visuals draw while EditingMode is None. Not InkTool.MultiTouch.
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
            }
        }

        private void MainWindow_StylusDown(object sender, StylusDownEventArgs e)
        {
            TouchDownPointsList[e.StylusDevice.Id] = InkCanvasEditingMode.None;
        }

        private void MainWindow_StylusUp(object sender, StylusEventArgs e)
        {
            try
            {
                inkCanvas.Strokes.Add(GetStrokeVisual(e.StylusDevice.Id).Stroke);
                inkCanvas.Children.Remove(GetVisualCanvas(e.StylusDevice.Id));

                inkCanvas_StrokeCollected(inkCanvas, new InkCanvasStrokeCollectedEventArgs(GetStrokeVisual(e.StylusDevice.Id).Stroke));
            }
            catch (Exception ex)
            {
                Label.Content = ex.ToString();
                LogHelper.WriteLogToFile("MainWindow_StylusUp", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
            }
            try
            {
                StrokeVisualList.Remove(e.StylusDevice.Id);
                VisualCanvasList.Remove(e.StylusDevice.Id);
                TouchDownPointsList.Remove(e.StylusDevice.Id);
                if (StrokeVisualList.Count == 0 || VisualCanvasList.Count == 0 || TouchDownPointsList.Count == 0)
                {
                    inkCanvas.Children.Clear();
                    StrokeVisualList.Clear();
                    VisualCanvasList.Clear();
                    TouchDownPointsList.Clear();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("MainWindow_StylusUpCleanup", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
            }
        }

        private void MainWindow_StylusMove(object sender, StylusEventArgs e)
        {
            try
            {
                if (GetTouchDownPointsList(e.StylusDevice.Id) != InkCanvasEditingMode.None) return;
                try
                {
                    if (e.StylusDevice.StylusButtons[1].StylusButtonState == StylusButtonState.Down) return;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile("StylusBarrelButton: " + ex.Message, LogHelper.LogType.Trace);
                }
                var strokeVisual = GetStrokeVisual(e.StylusDevice.Id);
                var stylusPointCollection = e.GetStylusPoints(this);
                foreach (var stylusPoint in stylusPointCollection)
                {
                    strokeVisual.Add(new StylusPoint(stylusPoint.X, stylusPoint.Y, stylusPoint.PressureFactor));
                }

                strokeVisual.Redraw();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("MainWindow_StylusMove", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
            }
        }

        private StrokeVisual GetStrokeVisual(int id)
        {
            if (StrokeVisualList.TryGetValue(id, out var visual))
            {
                return visual;
            }

            var strokeVisual = new StrokeVisual(inkCanvas.DefaultDrawingAttributes.Clone());
            StrokeVisualList[id] = strokeVisual;
            var visualCanvas = new VisualCanvas(strokeVisual);
            VisualCanvasList[id] = visualCanvas;
            inkCanvas.Children.Add(visualCanvas);

            return strokeVisual;
        }

        private VisualCanvas GetVisualCanvas(int id)
        {
            if (VisualCanvasList.TryGetValue(id, out var visualCanvas))
            {
                return visualCanvas;
            }
            return null;
        }

        private InkCanvasEditingMode GetTouchDownPointsList(int id)
        {
            if (TouchDownPointsList.TryGetValue(id, out var inkCanvasEditingMode))
            {
                return inkCanvasEditingMode;
            }
            return inkCanvas.EditingMode;
        }

        private Dictionary<int, InkCanvasEditingMode> TouchDownPointsList { get; } = new Dictionary<int, InkCanvasEditingMode>();
        private Dictionary<int, StrokeVisual> StrokeVisualList { get; } = new Dictionary<int, StrokeVisual>();
        private Dictionary<int, VisualCanvas> VisualCanvasList { get; } = new Dictionary<int, VisualCanvas>();

        #endregion

        int lastTouchDownTime = 0, lastTouchUpTime = 0;

        Point iniP = new Point(0, 0);
        bool isLastTouchEraser = false;
        private bool forcePointEraser = true;
        private bool _lockSmith = false; //临时停用双指手势
        private bool lockSmithPPT = false;
        private bool lockSmithDesktop = false;

        private void ChangeLockSmithState(bool state)
        {
            _lockSmith = state;
            if (_lockSmith) LockSmithSymbol.Symbol = iNKORE.UI.WPF.Modern.Controls.Symbol.UnPin;
            else LockSmithSymbol.Symbol = iNKORE.UI.WPF.Modern.Controls.Symbol.Pin;

        }

        private double GetEraserSizeCoefficient()
        {
            double k = 1;
            switch (Settings.Canvas.EraserSize)
            {
                case -2:
                    k = 0.5;
                    break;
                case -1:
                    k = 0.8;
                    break;
                case 1:
                    k = 1.6;
                    break;
                case 2:
                    k = 2;
                    break;
                case 3:
                    k = 2.5;
                    break;
                case 4:
                    k = 3.6;
                    break;
            }
            return k;
        }

        private void Main_Grid_TouchDown(object sender, TouchEventArgs e)
        {
            BorderClearInDelete.Visibility = Visibility.Collapsed;
            if (ToggleSwitchDrawShapeBorderAutoHide.IsOn)
            {
                BorderDrawShape.Visibility = Visibility.Collapsed;
            }

            if (NeedUpdateIniP())
            {
                iniP = e.GetTouchPoint(inkCanvas).Position;
            }
            if (drawingShapeMode == 9 && isFirstTouchCuboid == false)
            {
                MouseTouchMove(iniP);
            }
            inkCanvas.Opacity = 1;
            double boundsWidth = GetTouchBoundWidth(e);
            var eraserMultiplier = 1d;
            if (!Settings.Advanced.EraserBindTouchMultiplier && Settings.Advanced.IsSpecialScreen) eraserMultiplier = 1 / Settings.Advanced.TouchMultiplier;
            if (boundsWidth > BoundsWidth)
            {
                isLastTouchEraser = true;
                if (drawingShapeMode == 0 && forceEraser) return;
                if (boundsWidth > BoundsWidth * 2.5)
                {
                    double size = boundsWidth * 3d * GetEraserSizeCoefficient() * eraserMultiplier;
                    inkCanvas.EraserShape = new EllipseStylusShape(size, size);
                    // Touch-size heuristic: palm → area erase. BoundsWidth / TouchMultiplier math is unchanged.
                    // Not ApplyTool: this is per-contact, not a user tool, and would set ForceEraser.
                    inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                }
                else
                {
                    if (StackPanelPPTControls.Visibility == Visibility.Visible && inkCanvas.Strokes.Count == 0 && Settings.PowerPointSettings.IsEnableFingerGestureSlideShowControl)
                    {
                        isLastTouchEraser = false;
                        // GestureOnly is not an InkTool; ApplyTool cannot express it.
                        inkCanvas.EditingMode = InkCanvasEditingMode.GestureOnly;
                        inkCanvas.Opacity = 0.1;
                    }
                    else
                    {
                        inkCanvas.EraserShape = new EllipseStylusShape(5, 5);
                        // Touch-size heuristic: finger-width → stroke erase. Not a user tool switch.
                        inkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                    }
                }
            }
            else
            {
                isLastTouchEraser = false;
                inkCanvas.EraserShape = forcePointEraser ? new EllipseStylusShape(50 * GetEraserSizeCoefficient(), 50 * GetEraserSizeCoefficient()) : new EllipseStylusShape(5, 5);
                if (forceEraser) return;
                // Restore ink for a normal-size contact without going through ApplyTool so
                // drawingShapeMode / ForceEraser / marker flag stay as the user left them.
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            }
        }

        public double GetTouchBoundWidth(TouchEventArgs e)
        {
            var args = e.GetTouchPoint(null).Bounds;
            double value;
            if (!Settings.Advanced.IsQuadIR) value = args.Width;
            else value = Math.Sqrt(args.Width * args.Height); //四边红外
            if (Settings.Advanced.IsSpecialScreen) value *= Settings.Advanced.TouchMultiplier;
            return value;
        }

        //记录触摸设备ID
        private List<int> dec = new List<int>();
        //中心点
        System.Windows.Point centerPoint;
        InkCanvasEditingMode lastInkCanvasEditingMode = InkCanvasEditingMode.Ink;
        bool isSingleFingerDragMode = false;

        //防止衣服误触造成的墨迹消失

        private void inkCanvas_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            dec.Add(e.TouchDevice.Id);
            //设备1个的时候，记录中心点
            if (dec.Count == 1)
            {
                TouchPoint touchPoint = e.GetTouchPoint(inkCanvas);
                centerPoint = touchPoint.Position;

                //记录第一根手指点击时的 StrokeCollection
                lastTouchDownStrokeCollection = inkCanvas.Strokes.Clone();
            }
            //设备两个及两个以上，将画笔功能关闭
            if (dec.Count > 1 || isSingleFingerDragMode || !Settings.Gesture.IsEnableTwoFingerGesture)
            {
                if (isInMultiTouchMode || !Settings.Gesture.IsEnableTwoFingerGesture) return;
                if (inkCanvas.EditingMode != InkCanvasEditingMode.None && inkCanvas.EditingMode != InkCanvasEditingMode.Select)
                {
                    lastInkCanvasEditingMode = inkCanvas.EditingMode;
                    // Two-finger gesture: pause inking. Restored on PreviewTouchUp. Not a tool switch.
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                }
            }
        }

        private void inkCanvas_PreviewTouchUp(object sender, TouchEventArgs e)
        {
            //手势完成后切回之前的状态
            if (dec.Count > 1)
            {
                if (inkCanvas.EditingMode == InkCanvasEditingMode.None)
                {
                    // Restore the mode saved in PreviewTouchDown. lastInkCanvasEditingMode may be
                    // GestureOnly or a heuristic erase, so this cannot go through ApplyTool.
                    inkCanvas.EditingMode = lastInkCanvasEditingMode;
                }
            }
            dec.Remove(e.TouchDevice.Id);
            inkCanvas.Opacity = 1;
        }
        private void inkCanvas_ManipulationStarting(object sender, ManipulationStartingEventArgs e)
        {
            e.Mode = ManipulationModes.All;
        }

        private void inkCanvas_ManipulationInertiaStarting(object sender, ManipulationInertiaStartingEventArgs e)
        {

        }

        private void Main_Grid_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            if (e.Manipulators.Count() == 0)
            {
                if (forceEraser) return;
                // Gesture finished: return to ink without ApplyTool so ForceEraser / shape mode stay put.
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            }
        }

        private void Main_Grid_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            if (isInMultiTouchMode || !Settings.Gesture.IsEnableTwoFingerGesture || _lockSmith) return;
            if (dec.Count >= 2 || isSingleFingerDragMode)
            {
                ManipulationDelta md = e.DeltaManipulation;
                Vector trans = md.Translation;  // 获得位移矢量
                double rotate = md.Rotation;  // 获得旋转角度
                Vector scale = md.Scale;  // 获得缩放倍数

                Matrix m = new Matrix();

                // Find center of element and then transform to get current location of center
                FrameworkElement fe = e.Source as FrameworkElement;
                Point center = new Point(fe.ActualWidth / 2, fe.ActualHeight / 2);
                center = m.Transform(center);  // 转换为矩阵缩放和旋转的中心点

                // Update matrix to reflect translation/rotation
                if (Settings.Gesture.IsEnableTwoFingerTranslate)
                    m.Translate(trans.X, trans.Y);  // 移动
                if (Settings.Gesture.IsEnableTwoFingerRotation)
                    m.RotateAt(rotate, center.X, center.Y);  // 旋转
                if (Settings.Gesture.IsEnableTwoFingerZoom)
                    m.ScaleAt(scale.X, scale.Y, center.X, center.Y);  // 缩放

                StrokeCollection strokes = inkCanvas.GetSelectedStrokes();
                if (strokes.Count != 0)
                {
                    foreach (Stroke stroke in strokes)
                    {
                        stroke.Transform(m, false);

                        foreach (Circle circle in circles)
                        {
                            if (stroke == circle.Stroke)
                            {
                                circle.R = GetDistance(circle.Stroke.StylusPoints[0].ToPoint(), circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].ToPoint()) / 2;
                                circle.Centroid = new Point((circle.Stroke.StylusPoints[0].X + circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].X) / 2,
                                                            (circle.Stroke.StylusPoints[0].Y + circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].Y) / 2);
                                break;
                            }
                        }

                        if (Settings.Gesture.IsEnableTwoFingerZoom)
                        {
                            try
                            {
                                stroke.DrawingAttributes.Width *= md.Scale.X;
                                stroke.DrawingAttributes.Height *= md.Scale.Y;
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile("StrokeScaleWidth: " + ex.Message, LogHelper.LogType.Trace);
                            }
                        }
                    }
                }
                else
                {
                    foreach (Stroke stroke in inkCanvas.Strokes)
                    {
                        stroke.Transform(m, false);

                        if (Settings.Gesture.IsEnableTwoFingerZoom)
                        {
                            try
                            {
                                stroke.DrawingAttributes.Width *= md.Scale.X;
                                stroke.DrawingAttributes.Height *= md.Scale.Y;
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile("StrokeScaleWidth: " + ex.Message, LogHelper.LogType.Trace);
                            }
                        }
                    }
                    foreach (Circle circle in circles)
                    {
                        circle.R = GetDistance(circle.Stroke.StylusPoints[0].ToPoint(), circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].ToPoint()) / 2;
                        circle.Centroid = new Point((circle.Stroke.StylusPoints[0].X + circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].X) / 2,
                                                    (circle.Stroke.StylusPoints[0].Y + circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].Y) / 2);
                    }
                }
            }
        }

        #endregion Touch Events

        #region PowerPoint

        private readonly PowerPointSession _pptSession = new PowerPointSession();
        int slidescount = 0;
        private void BtnCheckPPT_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_pptSession.TryAttach()) throw new Exception();
                slidescount = _pptSession.SlideCount;
                ResetPptDocument(slidescount);
                StackPanelPPTControls.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                StackPanelPPTControls.Visibility = Visibility.Collapsed;
                LogHelper.WriteLogToFile("BtnCheckPPT: " + ex.Message, LogHelper.LogType.Trace);
                MessageBox.Show("未找到幻灯片");
            }
        }
        internal void ToggleSwitchSupportWPS_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.PowerPointSettings.IsSupportWPS = BorderSettings.ToggleSwitchSupportWPS.IsOn;
            SaveSettingsToFile();
        }

        public static bool IsShowingRestoreHiddenSlidesWindow = false;

        public static bool IsNotifyPreviousPageWindowShown = false;

        private void TimerCheckPPT_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (IsShowingRestoreHiddenSlidesWindow) return;
            try
            {
                if (_pptSession.ShouldSkipBecauseWps()) return;

                if (!_pptSession.TryAttach())
                {
                    _ui.OnUi(() =>
                    {
                        BtnPPTSlideShow.Visibility = Visibility.Collapsed;
                    });
                    timerCheckPPT.Start();
                    return;
                }

                timerCheckPPT.Stop();
                slidescount = _pptSession.SlideCount;
                ResetPptDocument(slidescount);

                string presentationName = _pptSession.PresentationName;
                int attachedSlideCount = _pptSession.SlideCount;
                bool hasHiddenSlides = _pptSession.HasHiddenSlides;
                bool slideShowAlreadyRunning = _pptSession.IsSlideShowRunning;

                if (Settings.PowerPointSettings.IsNotifyPreviousPage)
                    _ui.OnUiAsync(() =>
                        {
                            string defaultFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) +
                                                       @"\Ink Canvas Strokes\Auto Saved\Presentations\";
                            string folderPath = defaultFolderPath + presentationName + "_" + attachedSlideCount;
                            if (File.Exists(folderPath + "/Position") & !IsNotifyPreviousPageWindowShown)
                            {
                                if (int.TryParse(File.ReadAllText(folderPath + "/Position"), out var page))
                                {
                                    IsNotifyPreviousPageWindowShown = true;
                                    if (page <= 0) return;
                                    new YesOrNoNotificationWindow($"上次播放到了第 {page} 页, 是否立即跳转", () =>
                                    {
                                        _pptSession.GotoSlide(page);
                                    }).ShowDialog();
                                }
                            }
                        });

                if (Settings.PowerPointSettings.IsNotifyHiddenPage)
                {
                    _ui.OnUiAsync(() =>
                    {
                        if (hasHiddenSlides && !IsShowingRestoreHiddenSlidesWindow)
                        {
                            IsShowingRestoreHiddenSlidesWindow = true;
                            new YesOrNoNotificationWindow("检测到此演示文稿包含隐藏的幻灯片，是否取消隐藏？",
                                () =>
                                {
                                    _pptSession.UnhideHiddenSlides();
                                }).ShowDialog();
                        }

                        BtnPPTSlideShow.Visibility = Visibility.Visible;
                    });
                }

                if (slideShowAlreadyRunning)
                {
                    var began = _pptSession.CaptureRunningSlideShow();
                    if (began != null)
                    {
                        PptSession_SlideShowBegan(_pptSession, began);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("TimerCheckPPT", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
                _ui.OnUi(() =>
                {
                    BtnPPTSlideShow.Visibility = Visibility.Collapsed;
                });
                timerCheckPPT.Start();
            }
        }

        private void PptSession_Detached(object sender, EventArgs e)
        {
            timerCheckPPT.Start();
        }

        bool isPresentationHaveBlackSpace = false;


        private string pptName = null;
        int currentShowPosition = -1;
        //bool isButtonBackgroundTransparent = true; //此变量仅用于保存用于幻灯片放映时的优化
        private void PptSession_SlideShowBegan(object sender, PowerPointSlideShowBeganEventArgs e)
        {
            LogHelper.WriteLogToFile("PowerPoint Application Slide Show Begin", LogHelper.LogType.Event);
            _ui.OnUi(() =>
            {
                IsPptShowActive = true;
                if (Surface == AppSurface.Whiteboard)
                {
                    // 退出画板模式
                    BtnSwitch_Click(null, null);
                }
                if (Surface != AppSurface.Whiteboard)
                {
                    Surface = AppSurface.PptShow;
                }

                //调整颜色
                double screenRatio = SystemParameters.PrimaryScreenWidth / SystemParameters.PrimaryScreenHeight;
                if (Math.Abs(screenRatio - 16.0 / 9) <= -0.01)
                {
                    if (e.SlideHeight != 0 && e.SlideWidth / e.SlideHeight < 1.65)
                    {
                        isPresentationHaveBlackSpace = true;
                        //isButtonBackgroundTransparent = BorderSettings.ToggleSwitchTransparentButtonBackground.IsOn;

                        if (BtnSwitchTheme.Content.ToString() == "深色")
                        {
                            //Light
                            BtnExit.Foreground = Brushes.White;
                            SymbolIconBtnColorBlackContent.Foreground = Brushes.White;
                            //ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                            //BtnExit.Background = new SolidColorBrush(StringToColor("#AACCCCCC"));
                        }
                        else
                        {
                            //Dark
                            //BtnExit.Background = new SolidColorBrush(StringToColor("#AA555555"));
                        }
                    }
                }
                else if (screenRatio == -256 / 135)
                {

                }

                slidescount = e.SlideCount;
                previousSlideID = 0;
                ResetPptDocument(slidescount);

                pptName = e.PresentationName;
                LogHelper.NewLog("Name: " + e.PresentationName);
                LogHelper.NewLog("Slides Count: " + slidescount.ToString());

                //检查是否有已有墨迹，并加载
                if (Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint)
                {
                    string defaultFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\Ink Canvas Strokes\Auto Saved\Presentations\";
                    if (Directory.Exists(defaultFolderPath + e.PresentationName + "_" + e.SlideCount))
                    {
                        LogHelper.WriteLogToFile("Found saved strokes", LogHelper.LogType.Trace);
                        FileInfo[] files = new DirectoryInfo(defaultFolderPath + e.PresentationName + "_" + e.SlideCount).GetFiles();
                        int count = 0;
                        foreach (FileInfo file in files)
                        {
                            if (file.Name != "Position")
                            {
                                int i = -1;
                                try
                                {
                                    i = int.Parse(System.IO.Path.GetFileNameWithoutExtension(file.Name));
                                    if (i >= 1 && i <= _pptDocument.PageCount)
                                    {
                                        _pptDocument.Pages[i - 1].LoadIsf(File.ReadAllBytes(file.FullName));
                                        count++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.WriteLogToFile(string.Format("Failed to load strokes on Slide {0}\n{1}", i, ex.ToString()), LogHelper.LogType.Error);
                                }
                            }
                        }
                        LogHelper.WriteLogToFile(string.Format("Loaded {0} saved strokes", count.ToString()));
                    }
                }

                pointDesktop = new Point(ViewboxFloatingBar.Margin.Left, ViewboxFloatingBar.Margin.Top);
                pointPPT = new Point(-1, -1);

                StackPanelPPTControls.Visibility = Visibility.Visible;
                BtnPPTSlideShow.Visibility = Visibility.Collapsed;
                BtnPPTSlideShowEnd.Visibility = Visibility.Visible;
                ViewBoxStackPanelMain.Margin = new Thickness(10, 10, 10, 10);
                lockSmithDesktop = _lockSmith;
                ChangeLockSmithState(!Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode);

                if (Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow && Main_Grid.Background == Brushes.Transparent)
                {
                    if (Surface == AppSurface.Whiteboard)
                    {
                        LeaveWhiteboardSurface();
                        GridBackgroundCover.Visibility = Visibility.Collapsed;

                        //SaveStrokes();
                        ClearStrokes(true);

                        if (BtnSwitchTheme.Content.ToString() == "浅色")
                        {
                            BtnSwitch.Content = "黑板";
                        }
                        else
                        {
                            BtnSwitch.Content = "白板";
                        }
                        StackPanelPPTButtons.Visibility = Visibility.Visible;
                    }
                    BtnHideInkCanvas_Click(BtnHideInkCanvas, null);
                }
                //if (GridBackgroundCover.Visibility == Visibility.Visible)
                //{
                //    SaveStrokes();
                //    currentMode = 0;
                //    GridBackgroundCover.Visibility = Visibility.Hidden;
                //}

                ClearStrokes(true);

                SetBorderFloatingBarMainControlsVisibility(true, false);
                BorderPenColorRed_MouseUp(BorderPenColorRed, null);

                if (Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow == false)
                {
                    BtnHideInkCanvas_Click(BtnHideInkCanvas, null);
                }

                isEnteredSlideShowEndEvent = false;
                PptNavigationTextBlock.Text = $"{e.CurrentShowPosition}/{e.SlideCount}";
                LogHelper.NewLog("PowerPoint Slide Show Loading process complete");

                _pptFloatBarMarginTimer?.Stop();
                _pptFloatBarMarginTimer = _ui.RunOnce(TimeSpan.FromMilliseconds(100), () =>
                {
                    ViewboxFloatingBar.Margin = new Thickness((SystemParameters.PrimaryScreenWidth - ViewboxFloatingBar.ActualWidth * FloatingBarScale) / 2, SystemParameters.PrimaryScreenHeight - 60 + ViewboxFloatingBar.ActualHeight * (1 - FloatingBarScale), -2000, -200);
                });
            });

            _ui.OnUi(() =>
            {
                UpdateWindowTitle();
            });
            //previousSlideID = Wn.View.CurrentShowPosition;
            ////检查是否有已有墨迹，并加载当前页
            //if (Settings.Automation.IsAutoSaveStrokesInPowerPoint)
            //{
            //    try
            //    {
            //        if (memoryStreams[Wn.View.CurrentShowPosition].Length > 0)
            //        {
            //            Application.Current.Dispatcher.Invoke(() =>
            //            {
            //                inkCanvas.Strokes = new System.Windows.Ink.StrokeCollection(memoryStreams[Wn.View.CurrentShowPosition]);
            //            });
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        LogHelper.WriteLogToFile(string.Format("Failed to load strokes for current slide (Slide {0})\n{1}", Wn.View.CurrentShowPosition, ex.ToString()), LogHelper.LogType.Error);
            //    }
            //}
        }

        bool isEnteredSlideShowEndEvent = false; //防止重复调用本函数导致墨迹保存失效
        private void PptSession_SlideShowEnded(object sender, PowerPointSlideShowEndedEventArgs e)
        {
            IsNotifyPreviousPageWindowShown = false;
            LogHelper.WriteLogToFile(string.Format("PowerPoint Slide Show End"), LogHelper.LogType.Event);
            if (isEnteredSlideShowEndEvent)
            {
                LogHelper.WriteLogToFile("Detected previous entrance, returning");
                return;
            }
            isEnteredSlideShowEndEvent = true;
            if (Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint)
            {
                string defaultFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\Ink Canvas Strokes\Auto Saved\Presentations\";
                string folderPath = defaultFolderPath + e.PresentationName + "_" + e.SlideCount;
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                try
                {
                    File.WriteAllText(folderPath + "/Position", previousSlideID.ToString());
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile("SavePptPosition", LogHelper.LogType.Error);
                    LogHelper.NewLog(ex);
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        if (currentShowPosition >= 1 && currentShowPosition <= _pptDocument.PageCount)
                        {
                            _pptDocument.Pages[currentShowPosition - 1].CaptureStrokes(inkCanvas.Strokes);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile("PptSlideShowEndCapture", LogHelper.LogType.Error);
                        LogHelper.NewLog(ex);
                    }
                });
                for (int i = 1; i <= e.SlideCount; i++)
                {
                    if (i > _pptDocument.PageCount) break;
                    var page = _pptDocument.Pages[i - 1];
                    if (page.Strokes != null)
                    {
                        try
                        {
                            byte[] srcBuf = page.ToIsfBytes();
                            if (srcBuf.Length > 8)
                            {
                                File.WriteAllBytes(folderPath + @"\" + i.ToString("0000") + ".icstk", srcBuf);
                                LogHelper.WriteLogToFile(string.Format("Saved strokes for Slide {0}, size={1}, byteLength={2}", i.ToString(), srcBuf.Length, srcBuf.Length));
                            }
                            else
                            {
                                File.Delete(folderPath + @"\" + i.ToString("0000") + ".icstk");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile(string.Format("Failed to save strokes for Slide {0}\n{1}", i, ex.ToString()), LogHelper.LogType.Error);
                            File.Delete(folderPath + @"\" + i.ToString("0000") + ".icstk");
                        }
                    }
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                isPresentationHaveBlackSpace = false;

                if (BtnSwitchTheme.Content.ToString() == "深色")
                {
                    //Light
                    BtnExit.Foreground = Brushes.Black;
                    SymbolIconBtnColorBlackContent.Foreground = Brushes.White;
                    //ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                }
                else
                {
                    //Dark
                }

                BtnPPTSlideShow.Visibility = Visibility.Visible;
                BtnPPTSlideShowEnd.Visibility = Visibility.Collapsed;
                StackPanelPPTControls.Visibility = Visibility.Collapsed;
                ViewBoxStackPanelMain.Margin = new Thickness(10, 10, 10, 55);
                ChangeLockSmithState(lockSmithDesktop);
                IsPptShowActive = false;
                if (Surface == AppSurface.PptShow)
                {
                    Surface = AppSurface.Desktop;
                }

                if (Surface == AppSurface.Whiteboard)
                {
                    LeaveWhiteboardSurface();
                    GridBackgroundCover.Visibility = Visibility.Collapsed;

                    //SaveStrokes();
                    ClearStrokes(true);
                    //RestoreStrokes(true);

                    if (BtnSwitchTheme.Content.ToString() == "浅色")
                    {
                        BtnSwitch.Content = "黑板";
                    }
                    else
                    {
                        BtnSwitch.Content = "白板";
                    }
                    StackPanelPPTButtons.Visibility = Visibility.Visible;
                }
                //if (GridBackgroundCover.Visibility == Visibility.Visible)
                //{
                //    SaveStrokes();
                //}


                ClearStrokes(true);

                if (Main_Grid.Background != Brushes.Transparent)
                {
                    BtnHideInkCanvas_Click(BtnHideInkCanvas, null);
                }

                if (pointDesktop != new Point(-1, -1))
                {
                    ViewboxFloatingBar.Margin = new Thickness(pointDesktop.X, pointDesktop.Y, -2000, -200);
                    if (Settings.Appearance.IsAutoCollapseFloatBar)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            SetBorderFloatingBarMainControlsVisibility(false);
                        }), DispatcherPriority.Loaded);
                    }

                }
            });

            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateWindowTitle();
            });
        }

        int previousSlideID = 0;
        readonly InkDocument _pptDocument = new InkDocument(1, persistHistory: false);

        private void ResetPptDocument(int slideCount)
        {
            _pptDocument.Reset(Math.Max(1, slideCount));
        }

        private void PptSession_SlideChanged(object sender, PowerPointSlideChangedEventArgs e)
        {
            LogHelper.WriteLogToFile(string.Format("PowerPoint Next Slide (Slide {0})", e.CurrentShowPosition), LogHelper.LogType.Event);
            if (e.CurrentShowPosition != previousSlideID)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    int index = e.CurrentShowPosition - 1;
                    bool saveCurrent = previousSlideID > 0;
                    try
                    {
                        _pptDocument.GoTo(index, inkCanvas.Strokes, _inkHistory, saveCurrent, () =>
                        {
                            if (inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber && Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint && !_isPptClickingBtnTurned)
                                SaveScreenShot(true, e.PresentationName + "/" + e.CurrentShowPosition);
                            _isPptClickingBtnTurned = false;
                        });
                        currentShowPosition = e.CurrentShowPosition;
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile("PptSlideChanged", LogHelper.LogType.Error);
                        LogHelper.NewLog(ex);
                    }

                    PptNavigationTextBlock.Text = $"{e.CurrentShowPosition}/{e.SlideCount}";
                });
                previousSlideID = e.CurrentShowPosition;

            }
        }

        private bool _isPptClickingBtnTurned = false;

        private void BtnPPTSlidesUp_Click(object sender, RoutedEventArgs e)
        {
            if (Surface == AppSurface.Whiteboard)
            {
                GridBackgroundCover.Visibility = Visibility.Collapsed;
                LeaveWhiteboardSurface();
            }

            _isPptClickingBtnTurned = true;
            if (inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber &&
                Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint)
                SaveScreenShot(true, _pptSession.GetScreenshotLabel());
            try
            {
                _pptSession.PreviousSlide();
            }
            catch (Exception ex)
            {
                LogHelper.NewLog(ex);
            }
        }

        private void BtnPPTSlidesDown_Click(object sender, RoutedEventArgs e)
        {
            if (Surface == AppSurface.Whiteboard)
            {
                GridBackgroundCover.Visibility = Visibility.Collapsed;
                LeaveWhiteboardSurface();
            }
            _isPptClickingBtnTurned = true;
            if (inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber &&
                Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint)
                SaveScreenShot(true, _pptSession.GetScreenshotLabel());
            try
            {
                _pptSession.NextSlide();
            }
            catch (Exception ex)
            {
                LogHelper.NewLog(ex);
            }
        }


        private async void PPTNavigationBtn_Click(object sender, MouseButtonEventArgs e)
        {
            Main_Grid.Background = new SolidColorBrush(StringToColor("#01FFFFFF"));
            BtnHideInkCanvas_Click(sender, e);
            try
            {
                _pptSession.ShowSlideNavigation();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("ShowSlideNavigation: " + ex.Message, LogHelper.LogType.Trace);
                ShowNotification("此功能不支持阅读模式，请在幻灯片放映模式下使用。");
            }
            // 控制居中
            if (IsPptShowActive)
            {
                if (ViewboxFloatingBar.Margin == new Thickness((SystemParameters.PrimaryScreenWidth - ViewboxFloatingBar.ActualWidth * FloatingBarScale) / 2, SystemParameters.PrimaryScreenHeight - 60 + ViewboxFloatingBar.ActualHeight * (1 - FloatingBarScale), -2000, -200))
                {
                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ViewboxFloatingBar.Margin = new Thickness((SystemParameters.PrimaryScreenWidth - ViewboxFloatingBar.ActualWidth * FloatingBarScale) / 2, SystemParameters.PrimaryScreenHeight - 60 + ViewboxFloatingBar.ActualHeight * (1 - FloatingBarScale), -2000, -200);
                    }), DispatcherPriority.Render);
                }
            }
        }

        private void BtnPPTSlideShow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _pptSession.RunSlideShow();
            }
            catch (Exception ex)
            {
                LogHelper.NewLog(ex);
            }
        }

        private void BtnPPTSlideShowEnd_Click(object sender, RoutedEventArgs e)
        {
            _ui.OnUi(() =>
            {
                try
                {
                    int pos = _pptSession.GetCurrentShowPosition();
                    if (pos >= 1 && pos <= _pptDocument.PageCount)
                    {
                        _pptDocument.Pages[pos - 1].CaptureStrokes(inkCanvas.Strokes);
                    }
                    timeMachine.ClearStrokeHistory();
                    IsNotifyPreviousPageWindowShown = false;
                }
                catch (Exception ex)
                {
                    LogHelper.NewLog(ex);
                }
            });
            try
            {
                _pptSession.ExitSlideShow();
            }
            catch (Exception ex)
            {
                LogHelper.NewLog(ex);
            }
        }

        #endregion

        #region Settings

        #region Behavior

        internal void ToggleSwitchRunAtStartup_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            if (BorderSettings.ToggleSwitchRunAtStartup.IsOn)
            {
                StartAutomaticallyCreate("InkCanvas");
            }
            else
            {
                StartAutomaticallyDel("InkCanvas");
            }
        }

        internal void ToggleSwitchSupportPowerPoint_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.PowerPointSettings.PowerPointSupport = BorderSettings.ToggleSwitchSupportPowerPoint.IsOn;
            SaveSettingsToFile();

            if (Settings.PowerPointSettings.PowerPointSupport)
            {
                timerCheckPPT.Start();
            }
            else
            {
                timerCheckPPT.Stop();
            }
        }

        internal void ToggleSwitchShowCanvasAtNewSlideShow_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow = BorderSettings.ToggleSwitchShowCanvasAtNewSlideShow.IsOn;
            SaveSettingsToFile();
        }

        #endregion

        #region Startup

        internal void ToggleSwitchAutoHideCanvas_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.Startup.IsAutoHideCanvas = BorderSettings.ToggleSwitchAutoHideCanvas.IsOn;
            SaveSettingsToFile();
        }

        internal void ToggleSwitchAutoEnterModeFinger_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.Startup.IsAutoEnterModeFinger = BorderSettings.ToggleSwitchAutoEnterModeFinger.IsOn;
            SaveSettingsToFile();
        }

        #endregion

        #region Appearance

        internal void ToggleSwitchAutoCollapseFloatBar_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Appearance.IsAutoCollapseFloatBar = BorderSettings.ToggleSwitchAutoCollapseFloatBar.IsOn;
            SaveSettingsToFile();
        }

        internal void ToggleSwitchFloatBarShowOnRight_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Appearance.IsFloatBarShowOnRight = BorderSettings.ToggleSwitchFloatBarShowOnRight.IsOn;
            SaveSettingsToFile();
            UpdateFloatBarExpansionDirection();
        }

        internal void ToggleSwitchRememberFloatBarPosition_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Appearance.IsRememberFloatBarPosition = BorderSettings.ToggleSwitchRememberFloatBarPosition.IsOn;
            SaveSettingsToFile();
            if (Settings.Appearance.IsRememberFloatBarPosition)
            {
                SaveFloatBarPositionToFile();
            }
        }

        private void UpdateFloatBarExpansionDirection()
        {
            if (!isLoaded) return;
            if (StackPanelFloatBarContainer == null) return;

            var isFloatBarShowOnRight = Settings.Appearance.IsFloatBarShowOnRight;

            // 通过子元素顺序控制展开方向，避免 FlowDirection 反转导致方向判断异常
            StackPanelFloatBarContainer.FlowDirection = FlowDirection.LeftToRight;
            BorderFloatingBarMainControls.FlowDirection = FlowDirection.LeftToRight;
            BorderFloatingBarMainControls.RenderTransformOrigin = isFloatBarShowOnRight ? new Point(1, 0.5) : new Point(0, 0.5);
            BorderFloatingBarMainControls.Margin = isFloatBarShowOnRight ? new Thickness(0, 0, 5, 0) : new Thickness(5, 0, 0, 0);

            StackPanelFloatBarContainer.Children.Remove(BorderFloatingBarEmoji);
            StackPanelFloatBarContainer.Children.Remove(BorderFloatingBarMainControls);

            if (isFloatBarShowOnRight)
            {
                StackPanelFloatBarContainer.Children.Add(BorderFloatingBarMainControls);
                StackPanelFloatBarContainer.Children.Add(BorderFloatingBarEmoji);
            }
            else
            {
                StackPanelFloatBarContainer.Children.Add(BorderFloatingBarEmoji);
                StackPanelFloatBarContainer.Children.Add(BorderFloatingBarMainControls);
            }

            // 重置位置
            if (Surface != AppSurface.Desktop) return;
            if (isFloatBarShowOnRight)
            {
                ViewboxFloatingBar.Margin = new Thickness(SystemParameters.WorkArea.Left + SystemParameters.WorkArea.Width - 80 - ViewboxFloatingBar.ActualWidth * FloatingBarScale, SystemParameters.WorkArea.Top + SystemParameters.WorkArea.Height - 80 + ViewboxFloatingBar.ActualHeight * (1 - FloatingBarScale), -2000, -200);
            }
            else
            {
                ViewboxFloatingBar.Margin = new Thickness(SystemParameters.WorkArea.Left + 80, SystemParameters.WorkArea.Top + SystemParameters.WorkArea.Height - 80 + ViewboxFloatingBar.ActualHeight * (1 - FloatingBarScale), -2000, -200);
            }
        }

        internal void ToggleSwitchShowButtonExit_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.Appearance.IsShowExitButton = BorderSettings.ToggleSwitchShowButtonExit.IsOn;
            SaveSettingsToFile();

            if (BorderSettings.ToggleSwitchShowButtonExit.IsOn)
            {
                BtnExit.Visibility = Visibility.Visible;
            }
            else
            {
                BtnExit.Visibility = Visibility.Collapsed;
            }
        }

        internal void ToggleSwitchShowButtonEraser_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.Appearance.IsShowEraserButton = BorderSettings.ToggleSwitchShowButtonEraser.IsOn;
            SaveSettingsToFile();

            if (BorderSettings.ToggleSwitchShowButtonEraser.IsOn)
            {
                BtnErase.Visibility = Visibility.Visible;
            }
            else
            {
                BtnErase.Visibility = Visibility.Collapsed;
            }
        }
        internal void ToggleSwitchShowButtonPPTNavigation_OnToggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.PowerPointSettings.IsShowPPTNavigation = BorderSettings.ToggleSwitchShowButtonPPTNavigation.IsOn;
            SaveSettingsToFile();

            ViewboxPPTSidesControl.Visibility =
                Settings.PowerPointSettings.IsShowPPTNavigation ? Visibility.Visible : Visibility.Collapsed;
            ViewboxPPTRightBottom.Visibility =
                Settings.PowerPointSettings.IsShowPPTNavigation ? Visibility.Visible : Visibility.Collapsed;
        }

        internal void ToggleSwitchShowVerticalPPTNavigation_OnToggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.PowerPointSettings.IsShowVerticalPPTNavigation = BorderSettings.ToggleSwitchShowVerticalPPTNavigation.IsOn;
            SaveSettingsToFile();

            ViewboxPPTLeftCenter.Visibility =
                Settings.PowerPointSettings.IsShowVerticalPPTNavigation ? Visibility.Visible : Visibility.Collapsed;
            ViewboxPPTRightCenter.Visibility =
                Settings.PowerPointSettings.IsShowVerticalPPTNavigation ? Visibility.Visible : Visibility.Collapsed;
        }

        internal void ComboBoxTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Appearance.Theme = BorderSettings.ComboBoxTheme.SelectedIndex;
            SystemEvents_UserPreferenceChanged(null, null);
            SaveSettingsToFile();
        }

        internal void BtnColorConfig_Click(object sender, RoutedEventArgs e)
        {
            new ColorConfigWindow { Owner = this }.Show();
            SetColors();
            ApplyMarkerMode();
        }


        internal void ToggleSwitchShowButtonHideControl_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.Appearance.IsShowHideControlButton = BorderSettings.ToggleSwitchShowButtonHideControl.IsOn;
            SaveSettingsToFile();

            if (BorderSettings.ToggleSwitchShowButtonHideControl.IsOn)
            {
                BtnHideControl.Visibility = Visibility.Visible;
            }
            else
            {
                BtnHideControl.Visibility = Visibility.Collapsed;
            }
        }

        internal void ToggleSwitchShowButtonLRSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.Appearance.IsShowLRSwitchButton = BorderSettings.ToggleSwitchShowButtonLRSwitch.IsOn;
            SaveSettingsToFile();

            if (BorderSettings.ToggleSwitchShowButtonLRSwitch.IsOn)
            {
                BtnSwitchSide.Visibility = Visibility.Visible;
            }
            else
            {
                BtnSwitchSide.Visibility = Visibility.Collapsed;
            }
        }

        internal void ToggleSwitchShowButtonModeFinger_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.Appearance.IsShowModeFingerToggleSwitch = BorderSettings.ToggleSwitchShowButtonModeFinger.IsOn;
            SaveSettingsToFile();

            if (BorderSettings.ToggleSwitchShowButtonModeFinger.IsOn)
            {
                StackPanelModeFinger.Visibility = Visibility.Visible;
            }
            else
            {
                StackPanelModeFinger.Visibility = Visibility.Collapsed;
            }
        }

        internal void ToggleSwitchTransparentButtonBackground_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.Appearance.IsTransparentButtonBackground = BorderSettings.ToggleSwitchTransparentButtonBackground.IsOn;
            if (Settings.Appearance.IsTransparentButtonBackground)
            {
                BtnExit.Background = new SolidColorBrush(StringToColor("#7F909090"));
            }
            else
            {
                if (BtnSwitchTheme.Content.ToString() == "深色")
                {
                    //Light
                    BtnExit.Background = new SolidColorBrush(StringToColor("#FFCCCCCC"));
                }
                else
                {
                    //Dark
                    BtnExit.Background = new SolidColorBrush(StringToColor("#FF555555"));
                }
            }

            SaveSettingsToFile();
        }

        internal void ToggleSwitchShowCursor_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.Canvas.IsShowCursor = BorderSettings.ToggleSwitchShowCursor.IsOn;
            inkCanvas_EditingModeChanged(inkCanvas, null);

            SaveSettingsToFile();
        }

        #endregion

        #region Canvas

        internal void ComboBoxPenStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Canvas.InkStyle = BorderSettings.ComboBoxPenStyle.SelectedIndex;
            SaveSettingsToFile();
        }

        internal void ComboBoxEraserSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Canvas.EraserSize = BorderSettings.ComboBoxEraserSize.SelectedIndex - 2;
            SaveSettingsToFile();
        }


        internal void ComboBoxEraserType_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Canvas.EraserType = BorderSettings.ComboBoxEraserType.SelectedIndex;
            SaveSettingsToFile();
        }

        internal void FloatingBarScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var slider = (Slider)sender;
            double val = slider.Value / 100.0;
            double clampedVal = val < 0.5 ? 0.5 : val > 1.0 ? 1.0 : val;
            if (slider.Value != clampedVal * 100)
            {
                slider.Value = clampedVal * 100;
                return;
            }
            if (!isLoaded) return;
            Settings.Appearance.ViewboxFloatingBarScaleTransformValue = clampedVal;
            SaveSettingsToFile();

            ViewboxFloatingBarScaleTransform.ScaleX = clampedVal;
            ViewboxFloatingBarScaleTransform.ScaleY = clampedVal;
        }

        internal void InkWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isLoaded) return;

            Settings.Canvas.InkWidth = ((Slider)sender).Value / 2;

            ApplyMarkerMode();
            SaveSettingsToFile();
        }

        internal void ComboBoxHyperbolaAsymptoteOption_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Canvas.HyperbolaAsymptoteOption = (OptionalOperation)BorderSettings.ComboBoxHyperbolaAsymptoteOption.SelectedIndex;
            SaveSettingsToFile();
        }

        #endregion

        #region Automation

        internal void ToggleSwitchAutoKillPptService_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsAutoKillPptService = BorderSettings.ToggleSwitchAutoKillPptService.IsOn;
            SaveSettingsToFile();
            SyncProcessWatchdog();
        }

        internal void ToggleSwitchAutoKillEasiNote_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsAutoKillEasiNote = BorderSettings.ToggleSwitchAutoKillEasiNote.IsOn;
            SaveSettingsToFile();
            SyncProcessWatchdog();
        }

        internal void ToggleSwitchSaveScreenshotsInDateFolders_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsSaveScreenshotsInDateFolders = BorderSettings.ToggleSwitchSaveScreenshotsInDateFolders.IsOn;
            SaveSettingsToFile();
        }

        internal void ToggleSwitchAutoSaveStrokesAtScreenshot_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsAutoSaveStrokesAtScreenshot = BorderSettings.ToggleSwitchAutoSaveStrokesAtScreenshot.IsOn;
            BorderSettings.ToggleSwitchAutoSaveStrokesAtClear.Header =
                BorderSettings.ToggleSwitchAutoSaveStrokesAtScreenshot.IsOn ? "清屏时自动截图并保存墨迹" : "清屏时自动截图";
            SaveSettingsToFile();
        }

        internal void ToggleSwitchAutoSaveStrokesAtClear_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsAutoSaveStrokesAtClear = BorderSettings.ToggleSwitchAutoSaveStrokesAtClear.IsOn;
            SaveSettingsToFile();
        }


        internal void ToggleSwitchExitingWritingMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.IsAutoClearWhenExitingWritingMode = BorderSettings.ToggleSwitchClearExitingWritingMode.IsOn;
            SaveSettingsToFile();
        }

        internal void ToggleSwitchHideStrokeWhenSelecting_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Canvas.HideStrokeWhenSelecting = BorderSettings.ToggleSwitchHideStrokeWhenSelecting.IsOn;
            SaveSettingsToFile();
        }

        internal void ToggleSwitchUsingWhiteboard_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Canvas.UsingWhiteboard = BorderSettings.ToggleSwitchUsingWhiteboard.IsOn;
            if (!Settings.Canvas.UsingWhiteboard)
            {
                BtnSwitchTheme.Content = "浅色";
            }
            else
            {
                BtnSwitchTheme.Content = "深色";
            }
            BtnSwitchTheme_Click(sender, e);
            SaveSettingsToFile();
        }

        internal void ToggleSwitchAutoSaveStrokesInPowerPoint_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint = BorderSettings.ToggleSwitchAutoSaveStrokesInPowerPoint.IsOn;
            SaveSettingsToFile();
        }

        internal void ToggleSwitchNotifyPreviousPage_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.PowerPointSettings.IsNotifyPreviousPage = BorderSettings.ToggleSwitchNotifyPreviousPage.IsOn;
            SaveSettingsToFile();
        }

        internal void ToggleSwitchNotifyHiddenPage_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.PowerPointSettings.IsNotifyHiddenPage = BorderSettings.ToggleSwitchNotifyHiddenPage.IsOn;
            SaveSettingsToFile();
        }

        internal void ToggleSwitchNoStrokeClearInPowerPoint_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.PowerPointSettings.IsNoClearStrokeOnSelectWhenInPowerPoint = BorderSettings.ToggleSwitchNoStrokeClearInPowerPoint.IsOn;
            SaveSettingsToFile();
        }


        internal void ToggleSwitchShowStrokeOnSelectInPowerPoint_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.PowerPointSettings.IsShowStrokeOnSelectInPowerPoint = BorderSettings.ToggleSwitchShowStrokeOnSelectInPowerPoint.IsOn;
            SaveSettingsToFile();
        }

        internal void SideControlMinimumAutomationSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Automation.MinimumAutomationStrokeNumber = (int)BorderSettings.SideControlMinimumAutomationSlider.Value;
            SaveSettingsToFile();
        }

        internal void ToggleSwitchAutoSaveScreenShotInPowerPoint_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint = BorderSettings.ToggleSwitchAutoSaveScreenShotInPowerPoint.IsOn;
            SaveSettingsToFile();
        }

        #endregion

        #region Gesture


        internal void ToggleSwitchEnableFingerGestureSlideShowControl_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.PowerPointSettings.IsEnableFingerGestureSlideShowControl = BorderSettings.ToggleSwitchEnableFingerGestureSlideShowControl.IsOn;

            SaveSettingsToFile();
        }

        internal void ToggleSwitchDisableLockSmithByDefault_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.Gesture.IsDisableLockSmithByDefault = BorderSettings.ToggleSwitchDisableLockSmithByDefault.IsOn;

            SaveSettingsToFile();
        }

        internal void ToggleSwitchEnableTwoFingerZoom_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.Gesture.IsEnableTwoFingerZoom = BorderSettings.ToggleSwitchEnableTwoFingerZoom.IsOn;

            SaveSettingsToFile();
        }

        internal void ToggleSwitchEnableTwoFingerTranslate_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.Gesture.IsEnableTwoFingerTranslate = BorderSettings.ToggleSwitchEnableTwoFingerTranslate.IsOn;

            SaveSettingsToFile();
        }

        internal void ToggleSwitchEnableTwoFingerRotation_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.Gesture.IsEnableTwoFingerRotation = BorderSettings.ToggleSwitchEnableTwoFingerRotation.IsOn;
            Settings.Gesture.IsEnableTwoFingerRotationOnSelection = BorderSettings.ToggleSwitchEnableTwoFingerRotationOnSelection.IsOn;

            SaveSettingsToFile();
        }

        internal void ToggleSwitchEnableTwoFingerGestureInPresentationMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode = BorderSettings.ToggleSwitchEnableTwoFingerGestureInPresentationMode.IsOn;

            SaveSettingsToFile();
        }

        #endregion

        #region Reset

        internal void BtnResetToDefault_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BorderSettings.FlyoutResetToDefault.Hide();
                isLoaded = false;
                File.Delete("settings.json");
                Settings = new Settings();
                LoadSettings(false);
                isLoaded = true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("BtnResetToDefault", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
            }
        }

        #endregion

        #region Ink To Shape

        internal void ToggleSwitchEnableInkToShape_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.InkToShape.IsInkToShapeEnabled = BorderSettings.ToggleSwitchEnableInkToShape.IsOn;
            SaveSettingsToFile();
        }

        internal void LineNormalizationThresholdSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.InkToShape.LineNormalizationThreshold = (double)BorderSettings.LineNormalizationThresholdSlider.Value;
            SaveSettingsToFile();
        }

        #endregion

        #region Advanced

        internal void ToggleSwitchIsSpecialScreen_OnToggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Advanced.IsSpecialScreen = BorderSettings.ToggleSwitchIsSpecialScreen.IsOn;
            BorderSettings.TouchMultiplierSlider.Visibility = BorderSettings.ToggleSwitchIsSpecialScreen.IsOn ? Visibility.Visible : Visibility.Collapsed;
            SaveSettingsToFile();
        }

        internal void TouchMultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isLoaded) return;
            Settings.Advanced.TouchMultiplier = e.NewValue;
            SaveSettingsToFile();
        }

        internal void ToggleSwitchEraserBindTouchMultiplier_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Advanced.EraserBindTouchMultiplier = BorderSettings.ToggleSwitchEraserBindTouchMultiplier.IsOn;
            SaveSettingsToFile();
        }

        internal void ToggleSwitchIsQuadIR_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Advanced.IsQuadIR = BorderSettings.ToggleSwitchIsQuadIR.IsOn;
            SaveSettingsToFile();
        }

        internal void ToggleSwitchIsLogEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Advanced.IsLogEnabled = BorderSettings.ToggleSwitchIsLogEnabled.IsOn;
            SaveSettingsToFile();
        }

        internal void ToggleSwitchDisableEdgeGesture_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            Settings.Advanced.DisableEdgeGesture = BorderSettings.ToggleSwitchDisableEdgeGesture.IsOn;
            EdgeGesturesUtils.DisableEdgeGestures(new WindowInteropHelper(this).Handle,
                Settings.Advanced.DisableEdgeGesture && Main_Grid.Background != Brushes.Transparent);
            SaveSettingsToFile();
        }

        #endregion

        /// <summary>
        /// Debounced persist via SettingsStore.ScheduleSave. Last toggle wins;
        /// Window_Closing / BtnExit / unhandled exception Flush write immediately.
        /// </summary>
        public static void SaveSettingsToFile()
        {
            AppSettingsStore.ScheduleSave();
        }

        private void SaveFloatBarPositionToFile()
        {
            try
            {
                File.WriteAllText(App.RootPath + positionFileName,
                    ViewboxFloatingBar.Margin.Left.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                    ViewboxFloatingBar.Margin.Top.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("SaveFloatBarPosition", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
            }
        }

        #endregion

    }
}

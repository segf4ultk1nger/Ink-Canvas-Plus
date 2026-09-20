using AutoUpdaterDotNET;
using InkCanvasPlus.Domain;
using InkCanvasPlus.Helpers;
using InkCanvasPlus.History;
using InkCanvasPlus.Input;
using InkCanvasPlus.Services;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Helpers;
using IWshRuntimeLibrary;
using Microsoft.Office.Interop.PowerPoint;
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
    public partial class MainWindow : Window
    {
        #region Ink Canvas Functions

        DrawingAttributes drawingAttributes;
        private void loadPenCanvas()
        {
            SetColors();
            try
            {
                //drawingAttributes = new DrawingAttributes();
                drawingAttributes = inkCanvas.DefaultDrawingAttributes;
                drawingAttributes.Color = ((SolidColorBrush)BtnColorRed.Background).Color;

                drawingAttributes.Height = 2.5;
                drawingAttributes.Width = 2.5;

                ApplyTool(InkTool.Pen);
                inkCanvas.Gesture += InkCanvas_Gesture;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("loadPenCanvas", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
            }
        }
        //ApplicationGesture lastApplicationGesture = ApplicationGesture.AllGestures;
        DateTime lastGestureTime = DateTime.Now;
        private void InkCanvas_Gesture(object sender, InkCanvasGestureEventArgs e)
        {
            ReadOnlyCollection<GestureRecognitionResult> gestures = e.GetGestureRecognitionResults();
            try
            {
                foreach (GestureRecognitionResult gest in gestures)
                {
                    //Trace.WriteLine(string.Format("Gesture: {0}, Confidence: {1}", gest.ApplicationGesture, gest.RecognitionConfidence));
                    if (StackPanelPPTControls.Visibility == Visibility.Visible)
                    {
                        if (gest.ApplicationGesture == ApplicationGesture.Left)
                        {
                            BtnPPTSlidesDown_Click(BtnPPTSlidesDown, null);
                        }
                        if (gest.ApplicationGesture == ApplicationGesture.Right)
                        {
                            BtnPPTSlidesUp_Click(BtnPPTSlidesUp, null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("InkCanvas_Gesture", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
            }
        }

        private void inkCanvas_EditingModeChanged(object sender, RoutedEventArgs e)
        {
            var inkCanvas1 = sender as InkCanvas;
            if (inkCanvas1 == null) return;
            if (Settings.Canvas.IsShowCursor)
            {
                if (inkCanvas1.EditingMode == InkCanvasEditingMode.Ink || drawingShapeMode != 0)
                {
                    inkCanvas1.ForceCursor = true;
                }
                else
                {
                    inkCanvas1.ForceCursor = false;
                }
            }
            else
            {
                inkCanvas1.ForceCursor = false;
            }
            if (inkCanvas1.EditingMode == InkCanvasEditingMode.Ink) forcePointEraser = !forcePointEraser;

            if (inkCanvas.EditingMode == InkCanvasEditingMode.Select)
            {
                SymbolIconSelect.Foreground = new SolidColorBrush(Color.FromRgb(0, 136, 255));
            }
            else
            {
                SymbolIconSelect.Foreground = new SolidColorBrush(FloatBarForegroundColor);
            }
        }

        #endregion Ink Canvas

        #region Hotkeys

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (StackPanelPPTControls.Visibility != Visibility.Visible || Surface == AppSurface.Whiteboard) return;
            if (e.Delta >= 120)
            {
                BtnPPTSlidesUp_Click(BtnPPTSlidesUp, null);
            }
            else if (e.Delta <= -120)
            {
                BtnPPTSlidesDown_Click(BtnPPTSlidesDown, null);
            }
        }

        private void Main_Grid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (StackPanelPPTControls.Visibility != Visibility.Visible || Surface == AppSurface.Whiteboard) return;

            if (e.Key == Key.Down || e.Key == Key.PageDown || e.Key == Key.Right || e.Key == Key.N || e.Key == Key.Space)
            {
                BtnPPTSlidesDown_Click(BtnPPTSlidesDown, null);
            }
            if (e.Key == Key.Up || e.Key == Key.PageUp || e.Key == Key.Left || e.Key == Key.P)
            {
                BtnPPTSlidesUp_Click(BtnPPTSlidesUp, null);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                KeyExit(null, null);
            }
        }

        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void Undo_HotKey(object sender, ExecutedRoutedEventArgs e)
        {
            if (BtnUndo.IsEnabled)
            {
                BtnUndo_Click(null, null);
            }
        }

        private void Redo_HotKey(object sender, ExecutedRoutedEventArgs e)
        {
            if (BtnRedo.IsEnabled)
            {
                BtnRedo_Click(null, null);
            }
        }

        private void KeyExit(object sender, ExecutedRoutedEventArgs e)
        {
            BtnPPTSlideShowEnd_Click(BtnPPTSlideShowEnd, null);
        }

        private void KeyChangeMode(object sender, ExecutedRoutedEventArgs e)
        {
            SymbolIconCursor_Click(null, null);
        }

        private void KeyChangeModeToMouse(object sender, ExecutedRoutedEventArgs e)
        {
            if (Main_Grid.Background != Brushes.Transparent)
            {
                SymbolIconCursor_Click(null, null);
            }
        }

        private void KeyChangeModeToPen(object sender, ExecutedRoutedEventArgs e)
        {
            if (Main_Grid.Background == Brushes.Transparent)
            {
                SymbolIconCursor_Click(null, null);
            }
            else if (ImageEraserMask.Visibility == Visibility.Visible || inkCanvas.EditingMode == InkCanvasEditingMode.Select)
            {
                SetColorByIndex(true);
            }
            else
            {
                if (Surface != AppSurface.Whiteboard)
                {
                    BorderPenColorRed_MouseUp(null, null);
                }
                else
                {
                    if (Settings.Canvas.UsingWhiteboard)
                    {
                        BorderPenColorBlack_MouseUp(null, null);
                    }
                    else
                    {
                        BorderPenColorWhite_MouseUp(null, null);
                    }
                }
            }
        }

        private void KeyChangeToEraser(object sender, ExecutedRoutedEventArgs e)
        {
            ImageEraser_MouseUp(null, null);
        }

        private void KeyChangeToLasso(object sender, ExecutedRoutedEventArgs e)
        {
            SymbolIconSelect_MouseUp(null, null);
        }

        private void KeyCapture(object sender, ExecutedRoutedEventArgs e)
        {
            BtnScreenshot_Click(sender, e);
        }

        private void KeyChangeToPen0(object sender, ExecutedRoutedEventArgs e)
        {
            BorderPenColorBlack_MouseUp(null, null);
        }

        private void KeyChangeToPen1(object sender, ExecutedRoutedEventArgs e)
        {
            BorderPenColorRed_MouseUp(null, null);
        }

        private void KeyChangeToPen2(object sender, ExecutedRoutedEventArgs e)
        {
            BorderPenColorGreen_MouseUp(null, null);
        }

        private void KeyChangeToPen3(object sender, ExecutedRoutedEventArgs e)
        {
            BorderPenColorBlue_MouseUp(null, null);
        }

        private void KeyChangeToPen4(object sender, ExecutedRoutedEventArgs e)
        {
            BorderPenColorYellow_MouseUp(null, null);
        }

        private void KeyChangeToPen5(object sender, ExecutedRoutedEventArgs e)
        {
            BorderPenColorWhite_MouseUp(null, null);
        }

        private void KeyDrawLine(object sender, ExecutedRoutedEventArgs e)
        {
            SetColorByIndex();
            BtnDrawLine_Click(lastMouseDownSender, e);
        }

        private void KeyDrawRectangle(object sender, ExecutedRoutedEventArgs e)
        {
            SetColorByIndex();
            BtnDrawRectangle_Click(lastMouseDownSender, e);
        }

        private void KeyDrawCircle(object sender, ExecutedRoutedEventArgs e)
        {
            SetColorByIndex();
            BtnDrawCircle_Click(lastMouseDownSender, e);
        }

        private void KeyHide(object sender, ExecutedRoutedEventArgs e)
        {
            SymbolIconEmoji_MouseUp(sender, null);
        }

        private void KeySwitchToBoard(object sender, ExecutedRoutedEventArgs e)
        {
            ImageBlackboard_MouseUp(null, null);
        }

        private void KeyClearAll(object sender, ExecutedRoutedEventArgs e)
        {
            SymbolIconDelete_MouseUp(lastBorderMouseDownObject, null);
        }

        private void KeyToggleFloatBarPosition(object sender, ExecutedRoutedEventArgs e)
        {
            BorderSettings.ToggleSwitchFloatBarShowOnRight.IsOn = !BorderSettings.ToggleSwitchFloatBarShowOnRight.IsOn;
        }

        private void KeyToggleMarker(object sender, ExecutedRoutedEventArgs e)
        {
            BorderMarker_MouseUp(null, null);
        }

        private void ComponentDispatcher_ThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message == WM_HOTKEY && msg.wParam.ToInt32() == HOTKEY_ID)
            {
                this.Activate();
                KeyChangeMode(null, null);
                handled = true;
            }
        }

        #endregion Hotkeys

        #region TimeMachine

        private readonly InkHistoryController _inkHistory = new InkHistoryController();

        private TimeMachine timeMachine => _inkHistory.TimeMachine;

        private CommitReason _currentCommitType
        {
            get { return _inkHistory.CurrentCommitType; }
            set { _inkHistory.CurrentCommitType = value; }
        }

        private StrokeCollection CuboidStrokeCollection;

        private Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>> StrokeManipulationHistory
        {
            get { return _inkHistory.StrokeManipulationHistory; }
            set { _inkHistory.StrokeManipulationHistory = value; }
        }

        private Dictionary<Stroke, StylusPointCollection> StrokeInitialHistory
        {
            get { return _inkHistory.StrokeInitialHistory; }
        }

        private Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>> DrawingAttributesHistory
        {
            get { return _inkHistory.DrawingAttributesHistory; }
            set { _inkHistory.DrawingAttributesHistory = value; }
        }

        private Dictionary<Guid, List<Stroke>> DrawingAttributesHistoryFlag
        {
            get { return _inkHistory.DrawingAttributesHistoryFlag; }
        }

        private void TimeMachine_OnUndoStateChanged(bool status)
        {
            var result = status ? Visibility.Visible : Visibility.Collapsed;
            BtnUndo.Visibility = result;
            BtnUndo.IsEnabled = status;
        }

        private void TimeMachine_OnRedoStateChanged(bool status)
        {
            var result = status ? Visibility.Visible : Visibility.Collapsed;
            BtnRedo.Visibility = result;
            BtnRedo.IsEnabled = status;
        }
        #endregion

        #region Definations and Loading

        private static readonly object SettingsStoreLock = new object();
        private static SettingsStore _settingsStore;

        internal static SettingsStore AppSettingsStore
        {
            get
            {
                if (_settingsStore != null) return _settingsStore;
                lock (SettingsStoreLock)
                {
                    if (_settingsStore == null)
                    {
                        _settingsStore = new SettingsStore(App.RootPath, true);
                    }
                    return _settingsStore;
                }
            }
        }

        public static Settings Settings
        {
            get { return AppSettingsStore.Settings; }
            set { AppSettingsStore.Settings = value; }
        }

        public static string settingsFileName = "Settings.json";
        public static string positionFileName = "FloatBarPosition.txt";
        internal bool isLoaded = false;
        //bool isAutoUpdateEnabled = false;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_D);
            ComponentDispatcher.ThreadPreprocessMessage += ComponentDispatcher_ThreadPreprocessMessage;

            loadPenCanvas();

            //加载设置
            LoadSettings();
            if (Environment.Is64BitProcess)
            {
                BorderSettings.GroupBoxInkRecognition.Visibility = Visibility.Collapsed;
            }

            BorderSettings.TextBlockVersion.Text = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            LogHelper.WriteLogToFile("Ink Canvas Loaded", LogHelper.LogType.Event);

            PreloadIALibrary();

            ApplyMarkerMode();

            isLoaded = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateFloatBarExpansionDirection();
                RestoreFloatBarPosition();
            }), DispatcherPriority.Render);

            if (Settings.Appearance.IsAutoCollapseFloatBar)
            {
                _autoCollapseFloatBarTimer?.Stop();
                _autoCollapseFloatBarTimer = _ui.RunOnce(TimeSpan.FromSeconds(3), () =>
                {
                    if (Main_Grid.Background == Brushes.Transparent)
                    {
                        SetBorderFloatingBarMainControlsVisibility(false);
                    }
                });
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SettingsStore.FlushCurrentIfAny();
            if (CloseIsFromButton)
            {
                CancelBackgroundWork();
                var helper = new WindowInteropHelper(this);
                UnregisterHotKey(helper.Handle, HOTKEY_ID);
                ComponentDispatcher.ThreadPreprocessMessage -= ComponentDispatcher_ThreadPreprocessMessage;
                _pptSession.Dispose();

                e.Cancel = false;
                return;
            }
            e.Cancel = true;
            ShowNotification("如果关闭 Ink Canvas Plus，你将丢失所有未保存的工作。如要继续，请从“Ink Canvas Plus 设置”面板关闭 Ink Canvas Plus。");
        }

        private void RestoreFloatBarPosition()
        {
            if (!Settings.Appearance.IsRememberFloatBarPosition) return;
            string posFile = App.RootPath + positionFileName;
            if (!File.Exists(posFile)) return;
            try
            {
                string[] parts = File.ReadAllText(posFile).Trim().Split(',');
                if (parts.Length != 2) return;
                double x = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
                double y = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);

                double barWidth = ViewboxFloatingBar.ActualWidth * FloatingBarScale;
                double barHeight = ViewboxFloatingBar.ActualHeight * FloatingBarScale;
                if (barWidth <= 0 || barHeight <= 0) return;

                Rect workArea = SystemParameters.WorkArea;
                double minX = workArea.Left;
                double minY = workArea.Top;
                double maxX = workArea.Right - barWidth;
                double maxY = workArea.Bottom - barHeight;
                if (x >= minX && x <= maxX && y >= minY && y <= maxY)
                {
                    ViewboxFloatingBar.Margin = new Thickness(x, y, -2000, -200);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile("RestoreFloatBarPosition", LogHelper.LogType.Error);
                LogHelper.NewLog(ex);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            CancelBackgroundWork();
            LogHelper.WriteLogToFile("Ink Canvas closed", LogHelper.LogType.Event);
        }

        private static void PreloadIALibrary()
        {
            GC.KeepAlive(typeof(InkAnalyzer));
            GC.KeepAlive(typeof(AnalysisAlternate));
            GC.KeepAlive(typeof(InkDrawingNode));
            var analyzer = new InkAnalyzer();
            analyzer.AddStrokes(new StrokeCollection() {
                new Stroke(new StylusPointCollection() {
                    new StylusPoint(114,514),
                    new StylusPoint(191,9810),
                    new StylusPoint(7,21),
                    new StylusPoint(123,789),
                })
            });
            analyzer.Analyze();
        }

        private void LoadSettings(bool isStartup = true)
        {
            AppSettingsStore.Load();

            if (Settings.Startup.IsAutoEnterModeFinger)
            {
                ToggleSwitchModeFinger.IsOn = true;
                BorderSettings.ToggleSwitchAutoEnterModeFinger.IsOn = true;
            }
            else
            {
                BorderSettings.ToggleSwitchAutoEnterModeFinger.IsOn = false;
            }
            if (Settings.Startup.IsAutoHideCanvas)
            {
                if (isStartup)
                {
                    BtnHideInkCanvas_Click(BtnHideInkCanvas, null);
                }
                BorderSettings.ToggleSwitchAutoHideCanvas.IsOn = true;
            }
            else
            {
                if (isStartup)
                {
                    BtnHideInkCanvas_Click(BtnHideInkCanvas, null);
                    BtnHideInkCanvas_Click(BtnHideInkCanvas, null);
                }
                BorderSettings.ToggleSwitchAutoHideCanvas.IsOn = false;
            }

            if (Settings.Appearance.IsAutoCollapseFloatBar)
            {
                BorderSettings.ToggleSwitchAutoCollapseFloatBar.IsOn = true;
            }
            else
            {
                BorderSettings.ToggleSwitchAutoCollapseFloatBar.IsOn = false;
            }
            if (Settings.Appearance.IsFloatBarShowOnRight)
            {
                BorderSettings.ToggleSwitchFloatBarShowOnRight.IsOn = true;
            }
            else
            {
                BorderSettings.ToggleSwitchFloatBarShowOnRight.IsOn = false;
            }
            if (Settings.Appearance.IsRememberFloatBarPosition)
            {
                BorderSettings.ToggleSwitchRememberFloatBarPosition.IsOn = true;
            }
            else
            {
                BorderSettings.ToggleSwitchRememberFloatBarPosition.IsOn = false;
            }
            if (Settings.Appearance.IsShowEraserButton)
            {
                BtnErase.Visibility = Visibility.Visible;
                BorderSettings.ToggleSwitchShowButtonEraser.IsOn = true;
            }
            else
            {
                BtnErase.Visibility = Visibility.Collapsed;
                BorderSettings.ToggleSwitchShowButtonEraser.IsOn = false;
            }
            if (Settings.Appearance.IsShowExitButton)
            {
                BtnExit.Visibility = Visibility.Visible;
                BorderSettings.ToggleSwitchShowButtonExit.IsOn = true;
            }
            else
            {
                BtnExit.Visibility = Visibility.Collapsed;
                BorderSettings.ToggleSwitchShowButtonExit.IsOn = false;
            }

            ViewboxPPTSidesControl.Visibility =
                Settings.PowerPointSettings.IsShowPPTNavigation ? Visibility.Visible : Visibility.Collapsed;
            ViewboxPPTRightBottom.Visibility =
                Settings.PowerPointSettings.IsShowPPTNavigation ? Visibility.Visible : Visibility.Collapsed;
            BorderSettings.ToggleSwitchShowButtonPPTNavigation.IsOn = Settings.PowerPointSettings.IsShowPPTNavigation;

            ViewboxPPTLeftCenter.Visibility =
                Settings.PowerPointSettings.IsShowVerticalPPTNavigation ? Visibility.Visible : Visibility.Collapsed;
            ViewboxPPTRightCenter.Visibility =
                Settings.PowerPointSettings.IsShowVerticalPPTNavigation ? Visibility.Visible : Visibility.Collapsed;
            BorderSettings.ToggleSwitchShowVerticalPPTNavigation.IsOn = Settings.PowerPointSettings.IsShowVerticalPPTNavigation;

            BorderSettings.ComboBoxTheme.SelectedIndex = Settings.Appearance.Theme;

            if (Settings.Appearance.ViewboxFloatingBarScaleTransformValue != 0)
            {
                double userVal = Settings.Appearance.ViewboxFloatingBarScaleTransformValue;
                double clampedUserVal = userVal < 0.5 ? 0.5 : userVal > 1.0 ? 1.0 : userVal;
                ViewboxFloatingBarScaleTransform.ScaleX = clampedUserVal;
                ViewboxFloatingBarScaleTransform.ScaleY = clampedUserVal;
                BorderSettings.FloatingBarScaleSlider.Value = clampedUserVal * 100;
            }

            if (Settings.Appearance.IsShowHideControlButton)
            {
                BtnHideControl.Visibility = Visibility.Visible;
                BorderSettings.ToggleSwitchShowButtonHideControl.IsOn = true;
            }
            else
            {
                BtnHideControl.Visibility = Visibility.Collapsed;
                BorderSettings.ToggleSwitchShowButtonHideControl.IsOn = false;
            }
            if (Settings.Appearance.IsShowLRSwitchButton)
            {
                BtnSwitchSide.Visibility = Visibility.Visible;
                BorderSettings.ToggleSwitchShowButtonLRSwitch.IsOn = true;
            }
            else
            {
                BtnSwitchSide.Visibility = Visibility.Collapsed;
                BorderSettings.ToggleSwitchShowButtonLRSwitch.IsOn = false;
            }
            if (Settings.Appearance.IsShowModeFingerToggleSwitch)
            {
                StackPanelModeFinger.Visibility = Visibility.Visible;
                BorderSettings.ToggleSwitchShowButtonModeFinger.IsOn = true;
            }
            else
            {
                StackPanelModeFinger.Visibility = Visibility.Collapsed;
                BorderSettings.ToggleSwitchShowButtonModeFinger.IsOn = false;
            }
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

            if (Settings.PowerPointSettings.PowerPointSupport)
            {
                BorderSettings.ToggleSwitchSupportPowerPoint.IsOn = true;
                timerCheckPPT.Start();
            }
            else
            {
                BorderSettings.ToggleSwitchSupportPowerPoint.IsOn = false;
                timerCheckPPT.Stop();
            }
            if (Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow)
            {
                BorderSettings.ToggleSwitchShowCanvasAtNewSlideShow.IsOn = true;
            }
            else
            {
                BorderSettings.ToggleSwitchShowCanvasAtNewSlideShow.IsOn = false;
            }

            if (Settings.Gesture == null)
            {
                Settings.Gesture = new Gesture();
            }
            if (Settings.Gesture.IsDisableLockSmithByDefault)
            {
                BorderSettings.ToggleSwitchDisableLockSmithByDefault.IsOn = true;
                lockSmithDesktop = false;
                ChangeLockSmithState(false);
            }
            else
            {
                BorderSettings.ToggleSwitchDisableLockSmithByDefault.IsOn = false;
                lockSmithDesktop = true;
                ChangeLockSmithState(true);
            }
            if (Settings.Gesture.IsEnableTwoFingerZoom)
            {
                BorderSettings.ToggleSwitchEnableTwoFingerZoom.IsOn = true;
            }
            else
            {
                BorderSettings.ToggleSwitchEnableTwoFingerZoom.IsOn = false;
            }
            if (Settings.Gesture.IsEnableTwoFingerTranslate)
            {
                BorderSettings.ToggleSwitchEnableTwoFingerTranslate.IsOn = true;
            }
            else
            {
                BorderSettings.ToggleSwitchEnableTwoFingerTranslate.IsOn = false;
            }
            if (Settings.Gesture.IsEnableTwoFingerRotation)
            {
                BorderSettings.ToggleSwitchEnableTwoFingerRotation.IsOn = true;
            }
            else
            {
                BorderSettings.ToggleSwitchEnableTwoFingerRotation.IsOn = false;
            }
            if (Settings.Gesture.IsEnableTwoFingerRotationOnSelection)
            {
                BorderSettings.ToggleSwitchEnableTwoFingerRotationOnSelection.IsOn = true;
            }
            else
            {
                BorderSettings.ToggleSwitchEnableTwoFingerRotationOnSelection.IsOn = false;
            }
            if (Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode)
            {
                BorderSettings.ToggleSwitchEnableTwoFingerGestureInPresentationMode.IsOn = true;
            }
            else
            {
                BorderSettings.ToggleSwitchEnableTwoFingerGestureInPresentationMode.IsOn = false;
            }
            if (Settings.PowerPointSettings.IsEnableFingerGestureSlideShowControl)
            {
                BorderSettings.ToggleSwitchEnableFingerGestureSlideShowControl.IsOn = true;
            }
            else
            {
                BorderSettings.ToggleSwitchEnableFingerGestureSlideShowControl.IsOn = false;
            }

            if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "\\InkCanvas" + ".lnk"))
            {
                BorderSettings.ToggleSwitchRunAtStartup.IsOn = true;
            }

            if (Settings.Canvas != null)
            {
                drawingAttributes.Height = Settings.Canvas.InkWidth;
                drawingAttributes.Width = Settings.Canvas.InkWidth;

                BorderSettings.InkWidthSlider.Value = Settings.Canvas.InkWidth * 2;

                if (Settings.Canvas.IsShowCursor)
                {
                    BorderSettings.ToggleSwitchShowCursor.IsOn = true;
                    inkCanvas.ForceCursor = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchShowCursor.IsOn = false;
                    inkCanvas.ForceCursor = false;
                }

                BorderSettings.ComboBoxPenStyle.SelectedIndex = Settings.Canvas.InkStyle;

                BorderSettings.ComboBoxEraserSize.SelectedIndex = Settings.Canvas.EraserSize + 2;

                BorderSettings.ComboBoxHyperbolaAsymptoteOption.SelectedIndex = (int)Settings.Canvas.HyperbolaAsymptoteOption;
            }
            else
            {
                Settings.Canvas = new Canvas();
            }

            if (Settings.Automation != null)
            {
                SyncProcessWatchdog();

                if (Settings.Automation.IsAutoKillEasiNote)
                {
                    BorderSettings.ToggleSwitchAutoKillEasiNote.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchAutoKillEasiNote.IsOn = false;
                }

                if (Settings.Automation.IsAutoClearWhenExitingWritingMode)
                {
                    BorderSettings.ToggleSwitchClearExitingWritingMode.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchClearExitingWritingMode.IsOn = false;
                }


                if (Settings.Automation.IsAutoSaveStrokesAtClear)
                {
                    BorderSettings.ToggleSwitchAutoSaveStrokesAtClear.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchAutoSaveStrokesAtClear.IsOn = false;
                }



                if (Settings.Automation.IsAutoKillPptService)
                {
                    BorderSettings.ToggleSwitchAutoKillPptService.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchAutoKillPptService.IsOn = false;
                }

                if (Settings.Automation.IsSaveScreenshotsInDateFolders)
                {
                    BorderSettings.ToggleSwitchSaveScreenshotsInDateFolders.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchSaveScreenshotsInDateFolders.IsOn = false;
                }

                if (Settings.Automation.IsAutoSaveStrokesAtScreenshot)
                {
                    BorderSettings.ToggleSwitchAutoSaveStrokesAtScreenshot.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchAutoSaveStrokesAtScreenshot.IsOn = false;
                }

                if (Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint)
                {
                    BorderSettings.ToggleSwitchAutoSaveStrokesInPowerPoint.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchAutoSaveStrokesInPowerPoint.IsOn = false;
                }

                if (Settings.PowerPointSettings.IsNotifyPreviousPage)
                {
                    BorderSettings.ToggleSwitchNotifyPreviousPage.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchNotifyPreviousPage.IsOn = false;
                }

                if (Settings.PowerPointSettings.IsNotifyHiddenPage)
                {
                    BorderSettings.ToggleSwitchNotifyHiddenPage.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchNotifyHiddenPage.IsOn = false;
                }

                if (Settings.PowerPointSettings.IsNoClearStrokeOnSelectWhenInPowerPoint)
                {
                    BorderSettings.ToggleSwitchNoStrokeClearInPowerPoint.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchNoStrokeClearInPowerPoint.IsOn = false;
                }

                if (Settings.PowerPointSettings.IsShowStrokeOnSelectInPowerPoint)
                {
                    BorderSettings.ToggleSwitchShowStrokeOnSelectInPowerPoint.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchShowStrokeOnSelectInPowerPoint.IsOn = false;
                }

                if (Settings.PowerPointSettings.IsSupportWPS)
                {
                    BorderSettings.ToggleSwitchSupportWPS.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchSupportWPS.IsOn = false;
                }

                BorderSettings.SideControlMinimumAutomationSlider.Value = Settings.Automation.MinimumAutomationStrokeNumber;

                if (Settings.Canvas.HideStrokeWhenSelecting)
                {
                    BorderSettings.ToggleSwitchHideStrokeWhenSelecting.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchHideStrokeWhenSelecting.IsOn = false;
                }

                if (Settings.Canvas.UsingWhiteboard)
                {
                    BorderSettings.ToggleSwitchUsingWhiteboard.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchUsingWhiteboard.IsOn = false;
                }
                if (Settings.Canvas.UsingWhiteboard)
                {
                    BtnSwitchTheme.Content = "深色";
                    BtnSwitchTheme_Click(null, null);
                }

                switch (Settings.Canvas.EraserType)
                {
                    case 1:
                        forcePointEraser = true;
                        break;
                    case 2:
                        forcePointEraser = false;
                        break;
                }

                BorderSettings.ComboBoxEraserType.SelectedIndex = Settings.Canvas.EraserType;

                if (Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint)
                {
                    BorderSettings.ToggleSwitchAutoSaveScreenShotInPowerPoint.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchAutoSaveScreenShotInPowerPoint.IsOn = false;
                }
            }
            else
            {
                Settings.Automation = new Automation();
            }

            if (Settings.Advanced != null)
            {
                BorderSettings.TouchMultiplierSlider.Value = Settings.Advanced.TouchMultiplier;
                if (Settings.Advanced.IsLogEnabled)
                {
                    BorderSettings.ToggleSwitchIsLogEnabled.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchIsLogEnabled.IsOn = false;
                }
                if (Settings.Advanced.EraserBindTouchMultiplier)
                {
                    BorderSettings.ToggleSwitchEraserBindTouchMultiplier.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchEraserBindTouchMultiplier.IsOn = false;
                }

                if (Settings.Advanced.IsSpecialScreen)
                {
                    BorderSettings.ToggleSwitchIsSpecialScreen.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchIsSpecialScreen.IsOn = false;
                }

                BorderSettings.ToggleSwitchDisableEdgeGesture.IsOn = Settings.Advanced.DisableEdgeGesture;
                EdgeGesturesUtils.DisableEdgeGestures(new WindowInteropHelper(this).Handle,
                    Settings.Advanced.DisableEdgeGesture && Main_Grid.Background != Brushes.Transparent);
                BorderSettings.TouchMultiplierSlider.Visibility = BorderSettings.ToggleSwitchIsSpecialScreen.IsOn ? Visibility.Visible : Visibility.Collapsed;

                BorderSettings.ToggleSwitchIsQuadIR.IsOn = Settings.Advanced.IsQuadIR;
            }
            else
            {
                Settings.Advanced = new Advanced();
            }

            if (Settings.InkToShape != null)
            {
                if (Settings.InkToShape.IsInkToShapeEnabled)
                {
                    BorderSettings.ToggleSwitchEnableInkToShape.IsOn = true;
                }
                else
                {
                    BorderSettings.ToggleSwitchEnableInkToShape.IsOn = false;
                }
                BorderSettings.LineNormalizationThresholdSlider.Value = Settings.InkToShape.LineNormalizationThreshold;
            }
            else
            {
                Settings.InkToShape = new InkToShape();
            }

            //ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
            SystemEvents_UserPreferenceChanged(null, null);
        }

        private void CheckForUpdate()
        {
            AutoUpdater.Start($"https://dl.inkcanvasplus.khyan.top/autoupdate.xml?version={Application.ResourceAssembly.GetName().Version}");
        }

        #endregion Definations and Loading

        #region Right Side Panel

        public static bool CloseIsFromButton = false;

        internal void BtnLearnMore_Click(object sender, RoutedEventArgs e)
        {
            new WelcomeWindow { Owner = this }.Show();
        }

        internal void BtnCheckForUpdate_Click(object sender, RoutedEventArgs e)
        {
            BorderSettings.BtnCheckForUpdate.IsEnabled = false;
            AutoUpdater.Mandatory = true;
            AutoUpdater.ReportErrors = true;
            CheckForUpdate();
            BorderSettings.CheckingUpdatesTip.Visibility = Visibility.Visible;
            BorderSettings.AssemblyVersionInfoPanel.Visibility = Visibility.Collapsed;
            _checkUpdateButtonTimer?.Stop();
            _checkUpdateButtonTimer = _ui.RunOnce(TimeSpan.FromSeconds(5), () =>
            {
                BorderSettings.CheckingUpdatesTip.Visibility = Visibility.Collapsed;
                BorderSettings.AssemblyVersionInfoPanel.Visibility = Visibility.Visible;
                BorderSettings.BtnCheckForUpdate.IsEnabled = true;
            });

        }
        internal void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            CloseIsFromButton = true;
            SettingsStore.FlushCurrentIfAny();
            Close();
        }

        internal void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(System.Windows.Forms.Application.ExecutablePath, "-m");

            CloseIsFromButton = true;
            SettingsStore.FlushCurrentIfAny();
            CancelBackgroundWork();
            _pptSession.Dispose();
            Application.Current.Shutdown();
        }

        internal async void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (BorderSettings.Tag as Visibility? == Visibility.Visible)
            {
                BorderSettings.Tag = Visibility.Collapsed;
                BorderSettings.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(50)));
                await Task.Delay(60);
                BorderSettings.Visibility = Visibility.Collapsed;
            }
            else
            {
                BorderSettings.Tag = Visibility.Visible;
                BorderSettings.Visibility = Visibility.Visible;
                BorderSettings.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));

                // 不要问为什么
                await Task.Delay(160);
                BorderSettings.Visibility = Visibility.Visible;
            }
        }

        private void BtnThickness_Click(object sender, RoutedEventArgs e)
        {

        }

        bool forceEraser = false;

        private void BtnErase_Click(object sender, RoutedEventArgs e)
        {
            forcePointEraser = !forcePointEraser;
            switch (Settings.Canvas.EraserType)
            {
                case 1:
                    forcePointEraser = true;
                    break;
                case 2:
                    forcePointEraser = false;
                    break;
            }
            var eraserShape = forcePointEraser
                ? new EllipseStylusShape(50 * GetEraserSizeCoefficient(), 50 * GetEraserSizeCoefficient())
                : new EllipseStylusShape(5, 5);
            ApplyTool(forcePointEraser ? InkTool.PointEraser : InkTool.StrokeEraser, eraserShape: eraserShape);
            GeometryDrawingEraser.Brush = forcePointEraser
                ? new SolidColorBrush(Color.FromRgb(0x23, 0xA9, 0xF2))
                : new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
            ImageEraser.Visibility = Visibility.Collapsed;
            inkCanvas_EditingModeChanged(inkCanvas, null);
            CancelSingleFingerDragMode();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            forceEraser = false;
            BorderClearInDelete.Visibility = Visibility.Collapsed;

            if (Surface != AppSurface.Whiteboard)
            {
                BorderPenColorRed_MouseUp(BorderPenColorRed, null);
            }
            else
            {
                if (Settings.Canvas.UsingWhiteboard)
                {
                    BorderPenColorBlack_MouseUp(BorderPenColorBlack, null);
                }
                else
                {
                    BorderPenColorWhite_MouseUp(BorderPenColorWhite, null);
                }
            }

            ClearStrokes(false);
            inkCanvas.Children.Clear();

            CancelSingleFingerDragMode();
        }

        private void BtnClear_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            BtnHideInkCanvas_Click(BtnHideInkCanvas, null);
        }

        private void CancelSingleFingerDragMode()
        {
            if (ToggleSwitchDrawShapeBorderAutoHide.IsOn)
            {
                BorderDrawShape.Visibility = Visibility.Collapsed;
            }
            GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
            //Label.Content = "isSingleFingerDragMode=" + isSingleFingerDragMode.ToString();
            if (isSingleFingerDragMode)
            {
                BtnFingerDragMode_Click(BtnFingerDragMode, null);
            }
            isLongPressSelected = false;
        }

        private void BtnHideControl_Click(object sender, RoutedEventArgs e)
        {
            if (StackPanelControl.Visibility == Visibility.Visible)
            {
                StackPanelControl.Visibility = Visibility.Hidden;
            }
            else
            {
                StackPanelControl.Visibility = Visibility.Visible;
            }
        }

        AppSurface Surface = AppSurface.Desktop;
        bool IsPptShowActive { get; set; }

        private void LeaveWhiteboardSurface()
        {
            Surface = IsPptShowActive ? AppSurface.PptShow : AppSurface.Desktop;
        }

        private void BtnSwitch_Click(object sender, RoutedEventArgs e)
        {
            if (Main_Grid.Background == Brushes.Transparent)
            {
                if (Surface != AppSurface.Whiteboard)
                {
                    Surface = AppSurface.Whiteboard;
                    GridBackgroundCover.Visibility = Visibility.Collapsed;

                    SaveStrokes(true);
                    ClearStrokes(true);
                    RestoreStrokes();

                    if (BtnSwitchTheme.Content.ToString() == "浅色")
                    {
                        BtnSwitch.Content = "黑板";
                        BtnExit.Foreground = Brushes.White;
                    }
                    else
                    {
                        BtnSwitch.Content = "白板";
                        if (isPresentationHaveBlackSpace)
                        {
                            BtnExit.Foreground = Brushes.White;
                            SymbolIconBtnColorBlackContent.Foreground = Brushes.White;
                            //ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                        }
                        else
                        {
                            BtnExit.Foreground = Brushes.Black;
                            SymbolIconBtnColorBlackContent.Foreground = Brushes.White;
                            //ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                        }
                    }
                    StackPanelPPTButtons.Visibility = Visibility.Visible;
                }
                Topmost = true;
                BtnHideInkCanvas_Click(BtnHideInkCanvas, e);
            }
            else
            {
                switch (Surface == AppSurface.Whiteboard ? 0 : 1)
                {
                    case 0: //屏幕模式
                        LeaveWhiteboardSurface();
                        GridBackgroundCover.Visibility = Visibility.Collapsed;

                        SaveStrokes();
                        ClearStrokes(true);
                        RestoreStrokes(true);

                        if (BtnSwitchTheme.Content.ToString() == "浅色")
                        {
                            BtnSwitch.Content = "黑板";
                            BtnExit.Foreground = Brushes.White;
                            SymbolIconBtnColorBlackContent.Foreground = Brushes.Black;
                            //ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                        }
                        else
                        {
                            BtnSwitch.Content = "白板";
                            if (isPresentationHaveBlackSpace)
                            {
                                BtnExit.Foreground = Brushes.White;
                                SymbolIconBtnColorBlackContent.Foreground = Brushes.White;
                                //ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                            }
                            else
                            {
                                BtnExit.Foreground = Brushes.Black;
                                SymbolIconBtnColorBlackContent.Foreground = Brushes.White;
                                //ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                            }
                        }

                        StackPanelPPTButtons.Visibility = Visibility.Visible;
                        Topmost = true;
                        break;
                    case 1: //黑板或白板模式
                        Surface = AppSurface.Whiteboard;
                        GridBackgroundCover.Visibility = Visibility.Visible;

                        SaveStrokes(true);
                        ClearStrokes(true);
                        RestoreStrokes();

                        BtnSwitch.Content = "屏幕";
                        if (BtnSwitchTheme.Content.ToString() == "浅色")
                        {
                            BtnExit.Foreground = Brushes.White;
                            SymbolIconBtnColorBlackContent.Foreground = Brushes.Black;
                            //ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                        }
                        else
                        {
                            BtnExit.Foreground = Brushes.Black;
                            SymbolIconBtnColorBlackContent.Foreground = Brushes.White;
                            //ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                        }

                        StackPanelPPTButtons.Visibility = Visibility.Collapsed;
                        Topmost = false;
                        break;
                }
            }

            UpdateWindowTitle();
        }

        private void BtnSwitchTheme_Click(object sender, RoutedEventArgs e)
        {
            if (BtnSwitchTheme.Content.ToString() == "深色")
            {
                BtnSwitchTheme.Content = "浅色";
                if (BtnSwitch.Content.ToString() != "屏幕")
                {
                    BtnSwitch.Content = "黑板";
                }
                BtnExit.Foreground = Brushes.White;
                GridBackgroundCover.Background = new SolidColorBrush(StringToColor("#FFF2F2F2"));
                //ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
            }
            else
            {
                BtnSwitchTheme.Content = "深色";
                if (BtnSwitch.Content.ToString() != "屏幕")
                {
                    BtnSwitch.Content = "白板";
                }
                BtnExit.Foreground = Brushes.Black;
                GridBackgroundCover.Background = new SolidColorBrush(StringToColor("#FF1A1A1A"));
                //ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
            }
            SetColorByIndex();
            if (!Settings.Appearance.IsTransparentButtonBackground)
            {
                ToggleSwitchTransparentButtonBackground_Toggled(BorderSettings.ToggleSwitchTransparentButtonBackground, null);
            }
        }
        private void SetColorByIndex(bool forced = false)
        {
            if (forced || Surface == AppSurface.Whiteboard || GridInkCanvasSelectionCover.Visibility != Visibility.Collapsed)
                if (inkColor == 0)
                {
                    BtnColorBlack_Click(null, null);
                }
                else if (inkColor == 1)
                {
                    BtnColorRed_Click(null, null);
                }
                else if (inkColor == 2)
                {
                    BtnColorGreen_Click(null, null);
                }
                else if (inkColor == 3)
                {
                    BtnColorBlue_Click(null, null);
                }
                else if (inkColor == 4)
                {
                    BtnColorYellow_Click(null, null);
                }
                else if (inkColor == 5)
                {
                    BorderPenColorWhite_MouseUp(null, null);
                }
        }

        int BoundsWidth = 5;
        private void ToggleSwitchModeFinger_Toggled(object sender, RoutedEventArgs e)
        {
            BorderSettings.ToggleSwitchAutoEnterModeFinger.IsOn = ToggleSwitchModeFinger.IsOn;
            if (ToggleSwitchModeFinger.IsOn)
            {
                BoundsWidth = 15; //35
            }
            else
            {
                BoundsWidth = 5; //20
            }
        }

        private void BtnHideInkCanvas_Click(object sender, RoutedEventArgs e)
        {
            if (Main_Grid.Background == Brushes.Transparent)
            {
                Main_Grid.Background = new SolidColorBrush(StringToColor("#01FFFFFF"));
                if (Settings.Advanced.DisableEdgeGesture)
                {
                    EdgeGesturesUtils.DisableEdgeGestures(new WindowInteropHelper(this).Handle, true);
                }
                if (Settings.Canvas.HideStrokeWhenSelecting)
                {
                    inkCanvas.Visibility = Visibility.Visible;
                    inkCanvas.IsHitTestVisible = true;
                }
                else
                {
                    inkCanvas.IsHitTestVisible = true;
                    inkCanvas.Visibility = Visibility.Visible;
                }
                GridBackgroundCoverHolder.Visibility = Visibility.Visible;
                GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

                if (ImageEraserMask.Visibility == Visibility.Visible || inkCanvas.EditingMode == InkCanvasEditingMode.Select)
                {
                    SetColorByIndex(true);
                }

                if (GridBackgroundCover.Visibility == Visibility.Collapsed)
                {
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
                else
                {
                    BtnSwitch.Content = "屏幕";
                    StackPanelPPTButtons.Visibility = Visibility.Collapsed;
                }

                BtnHideInkCanvas.Content = "隐藏\n画板";
            }
            else
            {


                // Auto-clear Strokes
                // 很烦, 要重新来, 要等待截图完成再清理笔迹
                if (!IsPptShowActive)
                {
                    if (isLoaded && Settings.Automation.IsAutoClearWhenExitingWritingMode)
                    {
                        if (inkCanvas.Strokes.Count > 0)
                        {
                            if (Settings.Automation.IsAutoSaveStrokesAtClear && inkCanvas.Strokes.Count >
                                Settings.Automation.MinimumAutomationStrokeNumber)
                            {
                                SaveScreenShot(true);
                            }

                            BtnClear_Click(BtnClear, null);
                        }
                    }
                    if (Settings.Canvas.HideStrokeWhenSelecting)
                        inkCanvas.Visibility = Visibility.Collapsed;
                    else
                    {
                        inkCanvas.IsHitTestVisible = false;
                        inkCanvas.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    if (isLoaded && Settings.Automation.IsAutoClearWhenExitingWritingMode && !Settings.PowerPointSettings.IsNoClearStrokeOnSelectWhenInPowerPoint)
                    {
                        if (inkCanvas.Strokes.Count > 0)
                        {
                            if (Settings.Automation.IsAutoSaveStrokesAtClear && inkCanvas.Strokes.Count >
                                Settings.Automation.MinimumAutomationStrokeNumber)
                            {
                                SaveScreenShot(true);
                            }

                            BtnClear_Click(BtnClear, null);
                        }
                    }


                    if (Settings.PowerPointSettings.IsShowStrokeOnSelectInPowerPoint)
                    {
                        inkCanvas.Visibility = Visibility.Visible;
                        inkCanvas.IsHitTestVisible = true;
                    }
                    else
                    {
                        if (Settings.Canvas.HideStrokeWhenSelecting)
                            inkCanvas.Visibility = Visibility.Collapsed;
                        else
                        {
                            inkCanvas.IsHitTestVisible = false;
                            inkCanvas.Visibility = Visibility.Visible;
                        }
                    }
                }



                Main_Grid.Background = Brushes.Transparent;

                if (Settings.Advanced.DisableEdgeGesture)
                {
                    EdgeGesturesUtils.DisableEdgeGestures(new WindowInteropHelper(this).Handle, false);
                }


                GridBackgroundCoverHolder.Visibility = Visibility.Collapsed;
                if (Surface == AppSurface.Whiteboard)
                {
                    SaveStrokes();
                    RestoreStrokes(true);
                }

                if (BtnSwitchTheme.Content.ToString() == "浅色")
                {
                    BtnSwitch.Content = "黑板";
                }
                else
                {
                    BtnSwitch.Content = "白板";
                }

                StackPanelPPTButtons.Visibility = Visibility.Visible;
                BtnHideInkCanvas.Content = "显示\n画板";
            }

            if (Main_Grid.Background == Brushes.Transparent)
            {
                StackPanelCanvasControls.Visibility = Visibility.Collapsed;
                StackPanelCanvacMain.Visibility = Visibility.Visible;
            }
            else
            {
                StackPanelCanvasControls.Visibility = Visibility.Visible;
                StackPanelCanvacMain.Visibility = Visibility.Collapsed;
            }

            UpdateWindowTitle();
        }

        private void BtnSwitchSide_Click(object sender, RoutedEventArgs e)
        {
            if (ViewBoxStackPanelMain.HorizontalAlignment == HorizontalAlignment.Right)
            {
                ViewBoxStackPanelMain.HorizontalAlignment = HorizontalAlignment.Left;
                ViewBoxStackPanelShapes.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                ViewBoxStackPanelMain.HorizontalAlignment = HorizontalAlignment.Right;
                ViewBoxStackPanelShapes.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }


        private void StackPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (((StackPanel)sender).Visibility == Visibility.Visible)
            {
                GridForLeftSideReservedSpace.Visibility = Visibility.Collapsed;
            }
            else
            {
                GridForLeftSideReservedSpace.Visibility = Visibility.Visible;
            }
        }

        #endregion

        #region Right Side Panel (Buttons - Color)

        int inkColor = 1;
        bool isMarkerMode = false;

        const int ColorSwiftOpacityDurationOn = 150;
        const int ColorSwiftOpacityDurationOff = 50;
        private Color GetBaseColor()
        {
            switch (inkColor)
            {
                case 0: return Colors.Black;
                case 1: return ((SolidColorBrush)BtnColorRed.Background).Color;
                case 2: return ((SolidColorBrush)BtnColorGreen.Background).Color;
                case 3: return ((SolidColorBrush)BtnColorBlue.Background).Color;
                case 4: return ((SolidColorBrush)BtnColorYellow.Background).Color;
                case 5: return StringToColor("#FFFEFEFE");
                default: return Colors.Black;
            }
        }

        private void ApplyMarkerMode()
        {
            var baseWidth = Settings.Canvas.InkWidth;
            var baseColor = GetBaseColor();
            if (isMarkerMode)
            {
                drawingAttributes.Width = baseWidth * 10;
                drawingAttributes.Height = baseWidth * 10;
                drawingAttributes.Color = Color.FromArgb((byte)(baseColor.A * 0.5), baseColor.R, baseColor.G, baseColor.B);
            }
            else
            {
                drawingAttributes.Width = baseWidth;
                drawingAttributes.Height = baseWidth;
                drawingAttributes.Color = baseColor;
            }
        }

        private void ColorSwitchCheck()
        {
            //EraserContainer.Background = null;
            ImageEraser.Visibility = Visibility.Visible;
            if (Main_Grid.Background == Brushes.Transparent)
            {
                if (Surface == AppSurface.Whiteboard)
                {
                    LeaveWhiteboardSurface();
                    GridBackgroundCover.Visibility = Visibility.Collapsed;
                }
                BtnHideInkCanvas_Click(BtnHideInkCanvas, null);
            }

            StrokeCollection strokes = inkCanvas.GetSelectedStrokes();
            if (strokes.Count != 0)
            {
                foreach (Stroke stroke in strokes)
                {
                    try
                    {
                        stroke.DrawingAttributes.Color = inkCanvas.DefaultDrawingAttributes.Color;
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile("ColorSwitchCheck", LogHelper.LogType.Error);
                        LogHelper.NewLog(ex);
                    }
                }
            }
            if (DrawingAttributesHistory.Count > 0)
            {
                timeMachine.CommitStrokeDrawingAttributesHistory(DrawingAttributesHistory);
                DrawingAttributesHistory = new Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>>();
                foreach (var item in DrawingAttributesHistoryFlag)
                {
                    item.Value.Clear();
                }
            }
            else
            {
                ApplyInkOrMarkerTool();
                CancelSingleFingerDragMode();

                // 改变选中提示
                ViewboxBtnColorBlackContent.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(ColorSwiftOpacityDurationOff)));
                ViewboxBtnColorBlueContent.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(ColorSwiftOpacityDurationOff)));
                ViewboxBtnColorGreenContent.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(ColorSwiftOpacityDurationOff)));
                ViewboxBtnColorRedContent.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(ColorSwiftOpacityDurationOff)));
                ViewboxBtnColorYellowContent.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(ColorSwiftOpacityDurationOff)));
                ViewboxBtnColorWhiteContent.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(ColorSwiftOpacityDurationOff)));
                switch (inkColor)
                {
                    case 0:
                        ViewboxBtnColorBlackContent.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(ColorSwiftOpacityDurationOn)));
                        break;
                    case 1:
                        ViewboxBtnColorRedContent.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(ColorSwiftOpacityDurationOn)));
                        break;
                    case 2:
                        ViewboxBtnColorGreenContent.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(ColorSwiftOpacityDurationOn)));
                        break;
                    case 3:
                        ViewboxBtnColorBlueContent.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(ColorSwiftOpacityDurationOn)));
                        break;
                    case 4:
                        ViewboxBtnColorYellowContent.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(ColorSwiftOpacityDurationOn)));
                        break;
                    case 5:
                        ViewboxBtnColorWhiteContent.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(ColorSwiftOpacityDurationOn)));
                        break;
                }
            }

            isLongPressSelected = false;
            ApplyMarkerMode();
        }

        private void BtnColorBlack_Click(object sender, RoutedEventArgs e)
        {
            inkColor = 0;
            forceEraser = false;
            inkCanvas.DefaultDrawingAttributes.Color = Colors.Black;

            ColorSwitchCheck();
        }

        private void BtnColorRed_Click(object sender, RoutedEventArgs e)
        {
            inkColor = 1;
            forceEraser = false;
            inkCanvas.DefaultDrawingAttributes.Color = ((SolidColorBrush)BtnColorRed.Background).Color;
            ColorSwitchCheck();
        }

        private void BtnColorGreen_Click(object sender, RoutedEventArgs e)
        {
            inkColor = 2;
            forceEraser = false;
            inkCanvas.DefaultDrawingAttributes.Color = ((SolidColorBrush)BtnColorGreen.Background).Color;
            ColorSwitchCheck();
        }

        private void BtnColorBlue_Click(object sender, RoutedEventArgs e)
        {
            inkColor = 3;
            forceEraser = false;
            inkCanvas.DefaultDrawingAttributes.Color = ((SolidColorBrush)BtnColorBlue.Background).Color;
            ColorSwitchCheck();
        }

        private void BtnColorYellow_Click(object sender, RoutedEventArgs e)
        {
            inkColor = 4;
            forceEraser = false;
            inkCanvas.DefaultDrawingAttributes.Color = ((SolidColorBrush)BtnColorYellow.Background).Color;
            ColorSwitchCheck();
        }

        private Color StringToColor(string colorStr)
        {
            Byte[] argb = new Byte[4];
            for (int i = 0; i < 4; i++)
            {
                char[] charArray = colorStr.Substring(i * 2 + 1, 2).ToCharArray();
                //string str = "11";
                Byte b1 = toByte(charArray[0]);
                Byte b2 = toByte(charArray[1]);
                argb[i] = (Byte)(b2 | (b1 << 4));
            }
            return Color.FromArgb(argb[0], argb[1], argb[2], argb[3]);//#FFFFFFFF
        }

        private static byte toByte(char c)
        {
            byte b = (byte)"0123456789ABCDEF".IndexOf(c);
            return b;
        }

        #endregion

    }
}

using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace InkCanvasPlus
{
    /// <summary>
    /// Settings overlay visual tree, cut from MainWindow.xaml BorderSettings.
    /// Coupled actions (ink canvas, PPT, chrome) stay on MainWindow via Host.
    /// </summary>
    public partial class SettingsView : Border
    {
        internal MainWindow Host { get; set; }

        public static readonly DependencyProperty FingerModeIsOnProperty = DependencyProperty.Register(
            nameof(FingerModeIsOn),
            typeof(bool),
            typeof(SettingsView),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public bool FingerModeIsOn
        {
            get { return (bool)GetValue(FingerModeIsOnProperty); }
            set { SetValue(FingerModeIsOnProperty, value); }
        }

        /// <summary>
        /// Exposes the settings ink-width slider so MainWindow namescope bindings can reach it.
        /// </summary>
        public Slider HostedInkWidthSlider
        {
            get { return InkWidthSlider; }
        }

        /// <summary>
        /// Exposes the ink-to-shape toggle so the floating bar can stay in sync.
        /// </summary>
        public ToggleSwitch HostedEnableInkToShapeToggle
        {
            get { return ToggleSwitchEnableInkToShape; }
        }

        public SettingsView()
        {
            InitializeComponent();
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        private void HyperlinkWebsite_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://cloveryan.com/apps/Ink-Canvas-Plus");
        }

        private void HyperlinkQQGroup_Click(object sender, RoutedEventArgs e)
        {
            FlyoutQQGroup.Hide();
            Process.Start("https://qm.qq.com/q/I6OCRh38oU");
        }

        private void HyperlinkSource_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/clover-yan/Ink-Canvas-Plus");
        }

        private void BorderCalculateMultiplier_TouchDown(object sender, TouchEventArgs e)
        {
            var args = e.GetTouchPoint(null).Bounds;
            double value;
            if (!MainWindow.Settings.Advanced.IsQuadIR) value = args.Width;
            else value = Math.Sqrt(args.Width * args.Height);

            TextBlockShowCalculatedMultiplier.Text = (5 / (value * 1.1)).ToString();
        }

        private void BtnLearnMore_Click(object sender, RoutedEventArgs e)
        {
            Host?.BtnLearnMore_Click(sender, e);
        }

        private void BtnCheckForUpdate_Click(object sender, RoutedEventArgs e)
        {
            Host?.BtnCheckForUpdate_Click(sender, e);
        }

        private void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            Host?.BtnRestart_Click(sender, e);
        }

        private void BtnResetToDefault_Click(object sender, RoutedEventArgs e)
        {
            Host?.BtnResetToDefault_Click(sender, e);
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Host?.BtnExit_Click(sender, e);
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            Host?.BtnSettings_Click(sender, e);
        }

        private void BtnColorConfig_Click(object sender, RoutedEventArgs e)
        {
            Host?.BtnColorConfig_Click(sender, e);
        }

        private void ToggleSwitchRunAtStartup_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchRunAtStartup_Toggled(sender, e);
        }

        private void ToggleSwitchAutoHideCanvas_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchAutoHideCanvas_Toggled(sender, e);
        }

        private void ToggleSwitchAutoEnterModeFinger_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchAutoEnterModeFinger_Toggled(sender, e);
        }

        private void ToggleSwitchShowCursor_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchShowCursor_Toggled(sender, e);
        }

        private void ToggleSwitchHideStrokeWhenSelecting_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchHideStrokeWhenSelecting_Toggled(sender, e);
        }

        private void ToggleSwitchUsingWhiteboard_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchUsingWhiteboard_Toggled(sender, e);
        }

        private void ToggleSwitchDisableLockSmithByDefault_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchDisableLockSmithByDefault_Toggled(sender, e);
        }

        private void ToggleSwitchEnableTwoFingerZoom_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchEnableTwoFingerZoom_Toggled(sender, e);
        }

        private void ToggleSwitchEnableTwoFingerTranslate_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchEnableTwoFingerTranslate_Toggled(sender, e);
        }

        private void ToggleSwitchEnableTwoFingerRotation_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchEnableTwoFingerRotation_Toggled(sender, e);
        }

        private void ToggleSwitchEnableInkToShape_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchEnableInkToShape_Toggled(sender, e);
        }

        private void ToggleSwitchTransparentButtonBackground_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchTransparentButtonBackground_Toggled(sender, e);
        }

        private void ToggleSwitchShowButtonExit_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchShowButtonExit_Toggled(sender, e);
        }

        private void ToggleSwitchShowButtonEraser_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchShowButtonEraser_Toggled(sender, e);
        }

        private void ToggleSwitchShowButtonHideControl_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchShowButtonHideControl_Toggled(sender, e);
        }

        private void ToggleSwitchShowButtonLRSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchShowButtonLRSwitch_Toggled(sender, e);
        }

        private void ToggleSwitchShowButtonModeFinger_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchShowButtonModeFinger_Toggled(sender, e);
        }

        private void ToggleSwitchAutoCollapseFloatBar_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchAutoCollapseFloatBar_Toggled(sender, e);
        }

        private void ToggleSwitchFloatBarShowOnRight_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchFloatBarShowOnRight_Toggled(sender, e);
        }

        private void ToggleSwitchRememberFloatBarPosition_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchRememberFloatBarPosition_Toggled(sender, e);
        }

        private void ToggleSwitchShowButtonPPTNavigation_OnToggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchShowButtonPPTNavigation_OnToggled(sender, e);
        }

        private void ToggleSwitchShowVerticalPPTNavigation_OnToggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchShowVerticalPPTNavigation_OnToggled(sender, e);
        }

        private void ToggleSwitchSupportPowerPoint_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchSupportPowerPoint_Toggled(sender, e);
        }

        private void ToggleSwitchSupportWPS_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchSupportWPS_Toggled(sender, e);
        }

        private void ToggleSwitchShowCanvasAtNewSlideShow_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchShowCanvasAtNewSlideShow_Toggled(sender, e);
        }

        private void ToggleSwitchEnableTwoFingerGestureInPresentationMode_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchEnableTwoFingerGestureInPresentationMode_Toggled(sender, e);
        }

        private void ToggleSwitchEnableFingerGestureSlideShowControl_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchEnableFingerGestureSlideShowControl_Toggled(sender, e);
        }

        private void ToggleSwitchAutoSaveScreenShotInPowerPoint_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchAutoSaveScreenShotInPowerPoint_Toggled(sender, e);
        }

        private void ToggleSwitchAutoSaveStrokesInPowerPoint_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchAutoSaveStrokesInPowerPoint_Toggled(sender, e);
        }

        private void ToggleSwitchNotifyPreviousPage_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchNotifyPreviousPage_Toggled(sender, e);
        }

        private void ToggleSwitchNotifyHiddenPage_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchNotifyHiddenPage_Toggled(sender, e);
        }

        private void ToggleSwitchNoStrokeClearInPowerPoint_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchNoStrokeClearInPowerPoint_Toggled(sender, e);
        }

        private void ToggleSwitchShowStrokeOnSelectInPowerPoint_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchShowStrokeOnSelectInPowerPoint_Toggled(sender, e);
        }

        private void ToggleSwitchAutoKillPptService_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchAutoKillPptService_Toggled(sender, e);
        }

        private void ToggleSwitchAutoKillEasiNote_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchAutoKillEasiNote_Toggled(sender, e);
        }

        private void ToggleSwitchSaveScreenshotsInDateFolders_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchSaveScreenshotsInDateFolders_Toggled(sender, e);
        }

        private void ToggleSwitchAutoSaveStrokesAtScreenshot_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchAutoSaveStrokesAtScreenshot_Toggled(sender, e);
        }

        private void ToggleSwitchAutoSaveStrokesAtClear_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchAutoSaveStrokesAtClear_Toggled(sender, e);
        }

        private void ToggleSwitchExitingWritingMode_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchExitingWritingMode_Toggled(sender, e);
        }

        private void ToggleSwitchIsSpecialScreen_OnToggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchIsSpecialScreen_OnToggled(sender, e);
        }

        private void ToggleSwitchEraserBindTouchMultiplier_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchEraserBindTouchMultiplier_Toggled(sender, e);
        }

        private void ToggleSwitchIsQuadIR_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchIsQuadIR_Toggled(sender, e);
        }

        private void ToggleSwitchIsLogEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchIsLogEnabled_Toggled(sender, e);
        }

        private void ToggleSwitchDisableEdgeGesture_Toggled(object sender, RoutedEventArgs e)
        {
            Host?.ToggleSwitchDisableEdgeGesture_Toggled(sender, e);
        }

        private void ComboBoxPenStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Host?.ComboBoxPenStyle_SelectionChanged(sender, e);
        }

        private void ComboBoxEraserType_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Host?.ComboBoxEraserType_OnSelectionChanged(sender, e);
        }

        private void ComboBoxEraserSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Host?.ComboBoxEraserSize_SelectionChanged(sender, e);
        }

        private void ComboBoxHyperbolaAsymptoteOption_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Host?.ComboBoxHyperbolaAsymptoteOption_SelectionChanged(sender, e);
        }

        private void ComboBoxTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Host?.ComboBoxTheme_SelectionChanged(sender, e);
        }

        private void InkWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Host?.InkWidthSlider_ValueChanged(sender, e);
        }

        private void SideControlOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
        }

        private void FloatingBarScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Host?.FloatingBarScaleSlider_ValueChanged(sender, e);
        }

        private void TouchMultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Host?.TouchMultiplierSlider_ValueChanged(sender, e);
        }

        private void LineNormalizationThresholdSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            Host?.LineNormalizationThresholdSlider_ValueChanged(sender, e);
        }

        private void SideControlMinimumAutomationSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            Host?.SideControlMinimumAutomationSlider_ValueChanged(sender, e);
        }
    }
}

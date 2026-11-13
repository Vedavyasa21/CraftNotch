using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ReNotchPixel.Models;
using ReNotchPixel.Services;
using ReNotchPixel.ViewModels;

namespace ReNotchPixel;

public partial class MainWindow : Window
{
    private const int HOTKEY_ID = 9001;
    private IntPtr _hwnd;
    private HwndSource? _hwndSource;
    private readonly DispatcherTimer _cursorTracker;
    private bool _isMouseInsideNotch;
    private TrayService? _trayService;

    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel();
        DataContext = ViewModel;

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        _cursorTracker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _cursorTracker.Tick += CursorTracker_Tick;
    }

    protected override async void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        try
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            _hwndSource = HwndSource.FromHwnd(_hwnd);
            _hwndSource?.AddHook(HwndMessageHook);

            // Position window at primary screen edge
            UpdateWindowPosition();

            // Apply Win32 Styles: ToolWindow, TopMost, Layered, NoActivate, Transparent (click-through)
            var exStyle = Win32Helper.GetWindowLongPtr(_hwnd, Win32Helper.GWL_EXSTYLE).ToInt64();
            exStyle |= Win32Helper.WS_EX_TOOLWINDOW | Win32Helper.WS_EX_TOPMOST | Win32Helper.WS_EX_LAYERED | Win32Helper.WS_EX_NOACTIVATE;
            Win32Helper.SetWindowLongPtr(_hwnd, Win32Helper.GWL_EXSTYLE, new IntPtr(exStyle));

            // Start with click-through enabled
            Win32Helper.EnableClickThrough(_hwnd);
            Win32Helper.MakeTopMost(_hwnd);

            // Register global hotkey (Win + Alt + N)
            Win32Helper.RegisterHotKey(_hwnd, HOTKEY_ID, Win32Helper.MOD_WIN | Win32Helper.MOD_ALT | Win32Helper.MOD_NOREPEAT, 0x4E); // 0x4E = 'N'

            // Register for clipboard updates
            Win32Helper.AddClipboardFormatListener(_hwnd);

            // Initialize System Tray Icon
            _trayService = new TrayService(_hwnd);
            _trayService.CommandSelected += OnTrayCommandSelected;
            _trayService.Initialize();

            // Exclude from Windows Aero Peek
            int excludeFromPeek = 1;
            Win32Helper.DwmSetWindowAttribute(_hwnd, 12, ref excludeFromPeek, sizeof(int));

            // Start cursor hit-testing tracker
            _cursorTracker.Start();

            // Initialize Services
            await ViewModel.InitializeAsync();

            // Initial pill size & shape
            ApplyShapeLayout(ViewModel.Shape);
            ApplyModeLayout(ViewModel.Mode, animate: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnSourceInitialized: {ex}");
        }
    }

    private void UpdateWindowPosition()
    {
        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double screenHeight = SystemParameters.PrimaryScreenHeight;

        Left = ViewModel.Position switch
        {
            IslandPosition.Left => 24,
            IslandPosition.Right => screenWidth - Width - 24,
            _ => (screenWidth - Width) / 2
        };

        Top = ViewModel.Position == IslandPosition.Bottom ? screenHeight - Height : 0;
    }

    private void OnTrayCommandSelected(uint cmd)
    {
        switch (cmd)
        {
            case TrayService.CMD_SHAPE_ISLAND:
                ViewModel.Shape = IslandShape.Island;
                break;
            case TrayService.CMD_SHAPE_NOTCH:
                ViewModel.Shape = IslandShape.Notch;
                break;
            case TrayService.CMD_POS_CENTER:
                ViewModel.Position = IslandPosition.Center;
                UpdateWindowPosition();
                break;
            case TrayService.CMD_POS_LEFT:
                ViewModel.Position = IslandPosition.Left;
                UpdateWindowPosition();
                break;
            case TrayService.CMD_POS_RIGHT:
                ViewModel.Position = IslandPosition.Right;
                UpdateWindowPosition();
                break;
            case TrayService.CMD_TOGGLE_EXPAND:
                ViewModel.Mode = ViewModel.Mode == IslandMode.Expanded ? IslandMode.Idle : IslandMode.Expanded;
                break;
            case TrayService.CMD_SCREENSHOT:
                BtnScreenshot_Click(this, new RoutedEventArgs());
                break;
            case TrayService.CMD_DECK:
                OpenMobileDeck();
                break;
            case TrayService.CMD_SETTINGS:
                OpenSettings();
                break;
            case TrayService.CMD_EXIT:
                Close();
                Application.Current.Shutdown();
                break;
        }
    }

    private IntPtr HwndMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Helper.WM_CLIPBOARDUPDATE)
        {
            ViewModel.ClipboardService.OnClipboardUpdated();
        }
        else if (msg == Win32Helper.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            ViewModel.Mode = ViewModel.Mode == IslandMode.Expanded ? IslandMode.Idle : IslandMode.Expanded;
            handled = true;
        }
        else if (msg == TrayService.WM_TRAYICON)
        {
            _trayService?.HandleWindowMessage(msg, wParam, lParam, ViewModel.Shape, ViewModel.Position);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void CursorTracker_Tick(object? sender, EventArgs e)
    {
        try
        {
            if (_hwnd == IntPtr.Zero || !IsLoaded) return;

            if (Win32Helper.GetCursorPos(out var screenPoint))
            {
                var windowPoint = PointFromScreen(new Point(screenPoint.X, screenPoint.Y));

                if (IslandContainer.ActualWidth <= 0 || IslandContainer.ActualHeight <= 0) return;

                var bounds = new Rect(
                    IslandContainer.TransformToAncestor(this).Transform(new Point(0, 0)),
                    new Size(IslandContainer.ActualWidth, IslandContainer.ActualHeight));

                bool isOverIsland = bounds.Contains(windowPoint);

                if (isOverIsland && !_isMouseInsideNotch)
                {
                    _isMouseInsideNotch = true;
                    Win32Helper.DisableClickThrough(_hwnd);
                    ViewModel.OnPointerEnter();
                }
                else if (!isOverIsland && _isMouseInsideNotch)
                {
                    _isMouseInsideNotch = false;
                    ViewModel.OnPointerLeave();
                }

                if (!isOverIsland && ViewModel.Mode != IslandMode.Expanded)
                {
                    Win32Helper.EnableClickThrough(_hwnd);
                }
            }
        }
        catch { }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Mode))
        {
            ApplyModeLayout(ViewModel.Mode, animate: true);
        }
        else if (e.PropertyName == nameof(MainViewModel.Shape))
        {
            ApplyShapeLayout(ViewModel.Shape);
        }
        else if (e.PropertyName == nameof(MainViewModel.Position))
        {
            UpdateWindowPosition();
            ApplyShapeLayout(ViewModel.Shape);
        }
    }

    private void ApplyShapeLayout(IslandShape shape)
    {
        bool isBottom = ViewModel.Position == IslandPosition.Bottom;

        if (shape == IslandShape.Notch)
        {
            IslandContainer.Margin = new Thickness(0);
            IslandContainer.CornerRadius = isBottom 
                ? new CornerRadius(18, 18, 0, 0) 
                : new CornerRadius(0, 0, 18, 18);
        }
        else
        {
            IslandContainer.Margin = isBottom ? new Thickness(0, 0, 0, 8) : new Thickness(0, 8, 0, 0);
            IslandContainer.CornerRadius = new CornerRadius(18);
        }
    }

    private void ApplyModeLayout(IslandMode mode, bool animate)
    {
        double targetWidth;
        double targetHeight;
        CornerRadius targetRadius;

        IdleContentGrid.Visibility = Visibility.Collapsed;
        CompactContentGrid.Visibility = Visibility.Collapsed;
        HudContentGrid.Visibility = Visibility.Collapsed;
        ExpandedContentGrid.Visibility = Visibility.Collapsed;

        bool isBottom = ViewModel.Position == IslandPosition.Bottom;

        switch (mode)
        {
            case IslandMode.Expanded:
                targetWidth = 460;
                targetHeight = 280;
                targetRadius = ViewModel.Shape == IslandShape.Notch 
                    ? (isBottom ? new CornerRadius(24, 24, 0, 0) : new CornerRadius(0, 0, 24, 24))
                    : new CornerRadius(24);
                ExpandedContentGrid.Visibility = Visibility.Visible;
                break;

            case IslandMode.Compact:
                targetWidth = 230;
                targetHeight = 36;
                targetRadius = ViewModel.Shape == IslandShape.Notch 
                    ? (isBottom ? new CornerRadius(18, 18, 0, 0) : new CornerRadius(0, 0, 18, 18))
                    : new CornerRadius(18);
                CompactContentGrid.Visibility = Visibility.Visible;
                break;

            case IslandMode.Hud:
                targetWidth = 240;
                targetHeight = 38;
                targetRadius = ViewModel.Shape == IslandShape.Notch 
                    ? (isBottom ? new CornerRadius(19, 19, 0, 0) : new CornerRadius(0, 0, 19, 19))
                    : new CornerRadius(19);
                HudContentGrid.Visibility = Visibility.Visible;
                break;

            case IslandMode.Slim:
                targetWidth = 80;
                targetHeight = 6;
                targetRadius = new CornerRadius(3);
                break;

            case IslandMode.Hidden:
                targetWidth = 0;
                targetHeight = 0;
                targetRadius = new CornerRadius(0);
                break;

            case IslandMode.Idle:
            default:
                targetWidth = 200;
                targetHeight = 36;
                targetRadius = ViewModel.Shape == IslandShape.Notch 
                    ? (isBottom ? new CornerRadius(18, 18, 0, 0) : new CornerRadius(0, 0, 18, 18))
                    : new CornerRadius(18);
                IdleContentGrid.Visibility = Visibility.Visible;
                break;
        }

        if (animate && targetWidth > 0)
        {
            var ease = new QuinticEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(mode == IslandMode.Expanded ? 320 : 240);

            var wAnim = new DoubleAnimation(targetWidth, duration) { EasingFunction = ease };
            var hAnim = new DoubleAnimation(targetHeight, duration) { EasingFunction = ease };

            IslandContainer.BeginAnimation(WidthProperty, wAnim);
            IslandContainer.BeginAnimation(HeightProperty, hAnim);
        }
        else
        {
            IslandContainer.Width = targetWidth;
            IslandContainer.Height = targetHeight;
        }

        IslandContainer.CornerRadius = targetRadius;
    }

    // ==========================================
    // TAB SWITCHING
    // ==========================================
    private void TabMedia_Click(object sender, RoutedEventArgs e) => ShowTab(PanelMedia);
    private void TabWidgets_Click(object sender, RoutedEventArgs e) => ShowTab(PanelWidgets);
    private void TabControls_Click(object sender, RoutedEventArgs e) => ShowTab(PanelControls);
    private void TabClipboard_Click(object sender, RoutedEventArgs e) => ShowTab(PanelClipboard);
    private void TabShelf_Click(object sender, RoutedEventArgs e) => ShowTab(PanelShelf);
    private void TabAgents_Click(object sender, RoutedEventArgs e) => ShowTab(PanelAgents);

    private void ShowTab(UIElement activeTab)
    {
        PanelMedia.Visibility = Visibility.Collapsed;
        PanelWidgets.Visibility = Visibility.Collapsed;
        PanelControls.Visibility = Visibility.Collapsed;
        PanelClipboard.Visibility = Visibility.Collapsed;
        PanelShelf.Visibility = Visibility.Collapsed;
        PanelAgents.Visibility = Visibility.Collapsed;

        activeTab.Visibility = Visibility.Visible;
    }

    // ==========================================
    // MEDIA ACTIONS
    // ==========================================
    private async void BtnPlayPause_Click(object sender, RoutedEventArgs e) => await ViewModel.MediaService.TogglePlayPauseAsync();
    private async void BtnNext_Click(object sender, RoutedEventArgs e) => await ViewModel.MediaService.NextTrackAsync();
    private async void BtnPrev_Click(object sender, RoutedEventArgs e) => await ViewModel.MediaService.PreviousTrackAsync();

    private void SeekSlider_MouseDown(object sender, MouseButtonEventArgs e) { }
    private async void SeekSlider_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Slider slider)
        {
            await ViewModel.MediaService.SeekAsync(slider.Value);
        }
    }

    // ==========================================
    // CONTROLS & WIDGETS ACTIONS
    // ==========================================
    private void BtnToggleMute_Click(object sender, RoutedEventArgs e) => ViewModel.AudioService.ToggleMute();
    private void BtnToggleMic_Click(object sender, RoutedEventArgs e) => ViewModel.AudioService.ToggleMicMute();

    private void BtnScreenshot_Click(object sender, RoutedEventArgs e)
    {
        string path = ScreenshotService.CaptureFullScreen();
        if (!string.IsNullOrEmpty(path))
        {
            ViewModel.TriggerHud("📸", "Screenshot Saved & Copied", Path.GetFileName(path), 100);
        }
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        OpenSettings();
    }

    private void OpenSettings()
    {
        var win = new SettingsWindow(ViewModel.SettingsService, ViewModel.DeckServer);
        win.SettingsApplied += (s, appSettings) =>
        {
            ViewModel.ApplySettings(appSettings);
            ViewModel.TriggerHud("⚙️", "Settings Applied", "Preferences updated", 100);
        };
        win.Show();
        win.Activate();
    }

    private void OpenMobileDeck()
    {
        try
        {
            var url = ViewModel.DeckServer?.ServerUrl ?? "http://127.0.0.1:47823/";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    private void BtnPomodoro_Click(object sender, RoutedEventArgs e) => ViewModel.TogglePomodoro();
    private void BtnResetPomodoro_Click(object sender, RoutedEventArgs e) => ViewModel.ResetPomodoro();

    // ==========================================
    // CLIPBOARD ACTIONS
    // ==========================================
    private void ClipItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is ClipboardItem item)
        {
            ViewModel.ClipboardService.CopyItem(item);
            ViewModel.TriggerHud("📋", "Re-copied to Clipboard", item.PreviewText, 100);
        }
    }

    private void BtnClearClip_Click(object sender, RoutedEventArgs e) => ViewModel.ClipboardService.Clear();

    // ==========================================
    // DRAG & DROP SHELF
    // ==========================================
    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[]? files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files != null)
            {
                foreach (var file in files)
                {
                    ViewModel.AddShelfFile(file);
                }
                ShowTab(PanelShelf);
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _cursorTracker.Stop();
        _trayService?.Dispose();
        if (_hwnd != IntPtr.Zero)
        {
            Win32Helper.UnregisterHotKey(_hwnd, HOTKEY_ID);
            Win32Helper.RemoveClipboardFormatListener(_hwnd);
        }
        ViewModel.AudioService.Dispose();
        ViewModel.SystemMonitor.Stop();
        ViewModel.AgentMonitor.Stop();
        ViewModel.DeckServer.Dispose();
        ViewModel.OcclusionService.Dispose();
        ViewModel.WeatherService.Dispose();
        base.OnClosed(e);
    }
}

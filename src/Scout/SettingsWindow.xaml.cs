using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Scout.Models;
using Scout.Services;

namespace Scout;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly DeckServerService? _deckService;

    public event EventHandler<AppSettings>? SettingsApplied;

    public SettingsWindow(SettingsService settingsService, DeckServerService? deckService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _deckService = deckService;

        LoadValues();
    }

    private void LoadValues()
    {
        var s = _settingsService.Settings;

        if (s.Shape == IslandShape.Notch)
            RbNotch.IsChecked = true;
        else
            RbIsland.IsChecked = true;

        if (s.Position == IslandPosition.Bottom)
            RbBottom.IsChecked = true;
        else
            RbTop.IsChecked = true;

        ChkWaveform.IsChecked = s.AudioVisualizerEnabled;
        ChkPet.IsChecked = s.VirtualPetEnabled;

        ChkDeck.IsChecked = s.DeckServerEnabled;
        if (_deckService != null)
        {
            TxtDeckUrl.Text = _deckService.ServerUrl;
        }

        ChkStartup.IsChecked = s.StartWithWindows;
        ChkAutoHide.IsChecked = s.AutoHideInFullscreen;
        ChkHotkey.IsChecked = s.GlobalHotkeyEnabled;
        ChkAgents.IsChecked = s.AgentMonitorEnabled;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var s = _settingsService.Settings;

        s.Shape = RbNotch.IsChecked == true ? IslandShape.Notch : IslandShape.Island;
        s.Position = RbBottom.IsChecked == true ? IslandPosition.Bottom : IslandPosition.Top;

        s.AudioVisualizerEnabled = ChkWaveform.IsChecked == true;
        s.VirtualPetEnabled = ChkPet.IsChecked == true;
        s.DeckServerEnabled = ChkDeck.IsChecked == true;
        s.StartWithWindows = ChkStartup.IsChecked == true;
        s.AutoHideInFullscreen = ChkAutoHide.IsChecked == true;
        s.GlobalHotkeyEnabled = ChkHotkey.IsChecked == true;
        s.AgentMonitorEnabled = ChkAgents.IsChecked == true;

        _settingsService.Save();
        SettingsApplied?.Invoke(this, s);

        Close();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void BtnOpenDeck_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var url = _deckService?.ServerUrl ?? "http://127.0.0.1:47823/";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    private void BtnGitHub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/VEDANT1109/Scout") { UseShellExecute = true });
        }
        catch { }
    }
}

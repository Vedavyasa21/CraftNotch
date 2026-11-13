using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using Scout.Models;
using Scout.Services;

namespace Scout.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DispatcherTimer _hudTimer;
    private readonly DispatcherTimer _collapseTimer;
    private readonly DispatcherTimer _pomodoroTimer;

    public MediaService MediaService { get; }
    public AudioService AudioService { get; }
    public SystemMonitorService SystemMonitor { get; }
    public ClipboardService ClipboardService { get; }
    public AgentMonitorService AgentMonitor { get; }
    public WeatherService WeatherService { get; }
    public SettingsService SettingsService { get; }
    public DeckServerService DeckServer { get; }
    public OcclusionService OcclusionService { get; }

    // State properties
    private IslandMode _mode = IslandMode.Idle;
    public IslandMode Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsExpanded));
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(IsHud));
                OnPropertyChanged(nameof(IsCompact));
                OnPropertyChanged(nameof(IsSlim));
            }
        }
    }

    public bool IsExpanded => Mode == IslandMode.Expanded;
    public bool IsIdle => Mode == IslandMode.Idle;
    public bool IsHud => Mode == IslandMode.Hud;
    public bool IsCompact => Mode == IslandMode.Compact;
    public bool IsSlim => Mode == IslandMode.Slim;

    private IslandShape _shape = IslandShape.Island;
    public IslandShape Shape
    {
        get => _shape;
        set => SetProperty(ref _shape, value);
    }

    private IslandPosition _position = IslandPosition.Top;
    public IslandPosition Position
    {
        get => _position;
        set => SetProperty(ref _position, value);
    }

    private CardPanel _selectedPanel = CardPanel.Media;
    public CardPanel SelectedPanel
    {
        get => _selectedPanel;
        set => SetProperty(ref _selectedPanel, value);
    }

    private IdleFaceKind _idleFace = IdleFaceKind.Time;
    public IdleFaceKind IdleFace
    {
        get => _idleFace;
        set => SetProperty(ref _idleFace, value);
    }

    private bool _isPetEnabled = true;
    public bool IsPetEnabled
    {
        get => _isPetEnabled;
        set => SetProperty(ref _isPetEnabled, value);
    }

    private bool _isWaveformEnabled = true;
    public bool IsWaveformEnabled
    {
        get => _isWaveformEnabled;
        set => SetProperty(ref _isWaveformEnabled, value);
    }

    // Media
    private MediaInfo _media = new MediaInfo();
    public MediaInfo Media
    {
        get => _media;
        set
        {
            if (SetProperty(ref _media, value))
            {
                OnPropertyChanged(nameof(HasMediaPlaying));
                if (_media.IsPlaying && Mode == IslandMode.Idle)
                {
                    Mode = IslandMode.Compact;
                }
                else if (!_media.IsPlaying && Mode == IslandMode.Compact)
                {
                    Mode = IslandMode.Idle;
                }
            }
        }
    }

    public bool HasMediaPlaying => Media.IsPlaying;

    // Audio & Volume
    private float _volume = 50f;
    public float Volume
    {
        get => _volume;
        set
        {
            if (SetProperty(ref _volume, value))
            {
                AudioService.SetVolume(value);
            }
        }
    }

    private bool _isMuted;
    public bool IsMuted
    {
        get => _isMuted;
        set => SetProperty(ref _isMuted, value);
    }

    private bool _isMicMuted;
    public bool IsMicMuted
    {
        get => _isMicMuted;
        set => SetProperty(ref _isMicMuted, value);
    }

    // Performance & Battery
    private int _cpu;
    public int Cpu
    {
        get => _cpu;
        set => SetProperty(ref _cpu, value);
    }

    private int _ram;
    public int Ram
    {
        get => _ram;
        set => SetProperty(ref _ram, value);
    }

    private int _battery = 100;
    public int Battery
    {
        get => _battery;
        set => SetProperty(ref _battery, value);
    }

    private bool _isCharging;
    public bool IsCharging
    {
        get => _isCharging;
        set => SetProperty(ref _isCharging, value);
    }

    // Date, Time & Weather
    public string CurrentTime => DateTime.Now.ToString("HH:mm");
    public string CurrentDate => DateTime.Now.ToString("ddd, MMM d");

    private WeatherInfo _weather = new WeatherInfo();
    public WeatherInfo Weather
    {
        get => _weather;
        set => SetProperty(ref _weather, value);
    }

    // Pomodoro Timer
    private TimeSpan _timerRemaining = TimeSpan.FromMinutes(25);
    public TimeSpan TimerRemaining
    {
        get => _timerRemaining;
        set
        {
            if (SetProperty(ref _timerRemaining, value))
            {
                OnPropertyChanged(nameof(FormattedTimer));
            }
        }
    }

    public string FormattedTimer => $"{(int)TimerRemaining.TotalMinutes:D2}:{TimerRemaining.Seconds:D2}";
    private bool _isTimerRunning;
    public bool IsTimerRunning
    {
        get => _isTimerRunning;
        set => SetProperty(ref _isTimerRunning, value);
    }

    // Shelf Items
    public ObservableCollection<ShelfItem> ShelfFiles { get; } = new ObservableCollection<ShelfItem>();

    // Agent
    private AgentStatus _agent = new AgentStatus();
    public AgentStatus Agent
    {
        get => _agent;
        set => SetProperty(ref _agent, value);
    }

    // HUD Toast properties
    private string _hudIcon = "🔊";
    public string HudIcon
    {
        get => _hudIcon;
        set => SetProperty(ref _hudIcon, value);
    }

    private string _hudTitle = "Volume";
    public string HudTitle
    {
        get => _hudTitle;
        set => SetProperty(ref _hudTitle, value);
    }

    private string _hudValue = "50%";
    public string HudValue
    {
        get => _hudValue;
        set => SetProperty(ref _hudValue, value);
    }

    private double _hudProgress = 50.0;
    public double HudProgress
    {
        get => _hudProgress;
        set => SetProperty(ref _hudProgress, value);
    }

    public MainViewModel()
    {
        SettingsService = new SettingsService();
        SettingsService.Load();

        MediaService = new MediaService();
        AudioService = new AudioService();
        SystemMonitor = new SystemMonitorService();
        ClipboardService = new ClipboardService();
        AgentMonitor = new AgentMonitorService();
        WeatherService = new WeatherService();
        DeckServer = new DeckServerService(SettingsService.Settings.DeckPort);
        OcclusionService = new OcclusionService();

        // Apply settings
        ApplySettings(SettingsService.Settings);

        _hudTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
        _hudTimer.Tick += (s, e) =>
        {
            _hudTimer.Stop();
            if (Mode == IslandMode.Hud)
            {
                Mode = Media.IsPlaying ? IslandMode.Compact : IslandMode.Idle;
            }
        };

        _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _collapseTimer.Tick += (s, e) =>
        {
            _collapseTimer.Stop();
            if (Mode == IslandMode.Expanded)
            {
                Mode = Media.IsPlaying ? IslandMode.Compact : IslandMode.Idle;
            }
        };

        _pomodoroTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _pomodoroTimer.Tick += (s, e) =>
        {
            if (TimerRemaining.TotalSeconds > 1)
            {
                TimerRemaining = TimerRemaining.Subtract(TimeSpan.FromSeconds(1));
            }
            else
            {
                _pomodoroTimer.Stop();
                IsTimerRunning = false;
                TimerRemaining = TimeSpan.FromMinutes(25);
                TriggerHud("🔔", "Focus Session Done!", "Take a 5m break", 100);
            }
        };

        // Wire service events
        MediaService.MediaChanged += m => App.Current.Dispatcher.Invoke(() => Media = m);

        AudioService.VolumeChanged += (vol, muted) => App.Current.Dispatcher.Invoke(() =>
        {
            _volume = vol;
            _isMuted = muted;
            OnPropertyChanged(nameof(Volume));
            OnPropertyChanged(nameof(IsMuted));
            TriggerHud(muted ? "🔇" : (vol > 50 ? "🔊" : "🔉"), muted ? "Muted" : "Volume", $"{(int)vol}%", vol);
        });

        AudioService.MicMuteChanged += micMuted => App.Current.Dispatcher.Invoke(() =>
        {
            IsMicMuted = micMuted;
            TriggerHud(micMuted ? "🎤❌" : "🎤", "Microphone", micMuted ? "Muted" : "Live", micMuted ? 0 : 100);
        });

        SystemMonitor.PerformanceUpdated += (c, r) => App.Current.Dispatcher.Invoke(() =>
        {
            Cpu = c;
            Ram = r;
            OnPropertyChanged(nameof(CurrentTime));
            OnPropertyChanged(nameof(CurrentDate));
        });

        SystemMonitor.BatteryUpdated += (b, c) => App.Current.Dispatcher.Invoke(() =>
        {
            Battery = b;
            IsCharging = c;
        });

        SystemMonitor.LockKeyToggled += (key, on) => App.Current.Dispatcher.Invoke(() =>
        {
            TriggerHud("⌨️", key, on ? "ON" : "OFF", on ? 100 : 0);
        });

        ClipboardService.ItemAdded += item => App.Current.Dispatcher.Invoke(() =>
        {
            TriggerHud("📋", "Copied to Clipboard", item.PreviewText, 100);
        });

        AgentMonitor.StatusChanged += st => App.Current.Dispatcher.Invoke(() =>
        {
            Agent = st;
            if (st.State == AgentState.WaitingForUser || st.State == AgentState.Done)
            {
                TriggerHud("🤖", st.Summary, st.Detail, 100);
            }
        });

        WeatherService.WeatherUpdated += (s, w) => App.Current.Dispatcher.Invoke(() => Weather = w);

        // Deck remote callbacks
        DeckServer.GetStatusCallback = () => new
        {
            cpu = Cpu,
            ram = Ram,
            battery = Battery,
            isCharging = IsCharging,
            volume = (int)Volume,
            isMuted = IsMuted,
            media = new
            {
                title = Media.Title,
                artist = Media.Artist,
                isPlaying = Media.IsPlaying,
                progress = (int)Media.ProgressPercentage
            },
            agent = new
            {
                name = Agent.AgentName,
                status = Agent.State.ToString(),
                action = Agent.Summary
            }
        };

        DeckServer.OnMouseMove = (dx, dy) =>
        {
            Win32Helper.mouse_event(Win32Helper.MOUSEEVENTF_MOVE, dx, dy, 0, UIntPtr.Zero);
        };

        DeckServer.OnMouseClick = btn =>
        {
            if (btn == "right")
            {
                Win32Helper.mouse_event(Win32Helper.MOUSEEVENTF_RIGHTDOWN | Win32Helper.MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
            }
            else
            {
                Win32Helper.mouse_event(Win32Helper.MOUSEEVENTF_LEFTDOWN | Win32Helper.MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            }
        };

        DeckServer.OnMediaCommand = cmd =>
        {
            if (cmd == "playpause") MediaService.PlayPause();
            else if (cmd == "next") MediaService.Next();
            else if (cmd == "prev") MediaService.Previous();
        };

        DeckServer.OnVolumeChange = level =>
        {
            App.Current.Dispatcher.Invoke(() => Volume = level);
        };

        DeckServer.OnActionCommand = cmd =>
        {
            if (cmd == "mute") AudioService.ToggleMute();
            else if (cmd == "lock") Win32Helper.LockWorkStation();
            else if (cmd == "snip") ScreenshotService.TriggerSnip();
            else if (cmd == "expand")
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    Mode = Mode == IslandMode.Expanded ? IslandMode.Idle : IslandMode.Expanded;
                });
            }
        };

        DeckServer.OnAgentWebhook = (name, action, status) => App.Current.Dispatcher.Invoke(() =>
        {
            Agent = new AgentStatus
            {
                AgentName = name,
                Summary = action,
                Detail = status,
                State = AgentState.Working,
                LastActivity = DateTime.Now
            };
            TriggerHud("🤖", name, action, 100);
        });

        // Occlusion detection
        OcclusionService.FullscreenChanged += (s, isFullscreen) => App.Current.Dispatcher.Invoke(() =>
        {
            if (SettingsService.Settings.AutoHideInFullscreen)
            {
                if (isFullscreen && Mode != IslandMode.Hidden)
                {
                    Mode = IslandMode.Slim;
                }
                else if (!isFullscreen && Mode == IslandMode.Slim)
                {
                    Mode = Media.IsPlaying ? IslandMode.Compact : IslandMode.Idle;
                }
            }
        });
    }

    public void ApplySettings(AppSettings s)
    {
        Shape = s.Shape;
        Position = s.Position;
        IsPetEnabled = s.VirtualPetEnabled;
        IsWaveformEnabled = s.AudioVisualizerEnabled;
        OcclusionService.Enabled = s.AutoHideInFullscreen;

        if (s.DeckServerEnabled && !DeckServer.IsRunning)
        {
            DeckServer.Start(s.DeckPort);
        }
        else if (!s.DeckServerEnabled && DeckServer.IsRunning)
        {
            DeckServer.Stop();
        }
    }

    public async Task InitializeAsync()
    {
        AudioService.Initialize();
        Volume = AudioService.CurrentVolume;
        IsMuted = AudioService.IsMuted;
        IsMicMuted = AudioService.IsMicMuted;

        SystemMonitor.Start();
        AgentMonitor.Start();
        WeatherService.Start();
        OcclusionService.Start();

        if (SettingsService.Settings.DeckServerEnabled)
        {
            DeckServer.Start(SettingsService.Settings.DeckPort);
        }

        await MediaService.InitializeAsync();
    }

    public void TriggerHud(string icon, string title, string val, double progress)
    {
        HudIcon = icon;
        HudTitle = title;
        HudValue = val;
        HudProgress = progress;

        if (Mode != IslandMode.Expanded)
        {
            Mode = IslandMode.Hud;
            _hudTimer.Stop();
            _hudTimer.Start();
        }
    }

    public void OnPointerEnter()
    {
        _collapseTimer.Stop();
        if (Mode != IslandMode.Expanded)
        {
            Mode = IslandMode.Expanded;
        }
    }

    public void OnPointerLeave()
    {
        _collapseTimer.Stop();
        _collapseTimer.Start();
    }

    public void TogglePomodoro()
    {
        if (IsTimerRunning)
        {
            _pomodoroTimer.Stop();
            IsTimerRunning = false;
        }
        else
        {
            _pomodoroTimer.Start();
            IsTimerRunning = true;
        }
    }

    public void ResetPomodoro()
    {
        _pomodoroTimer.Stop();
        IsTimerRunning = false;
        TimerRemaining = TimeSpan.FromMinutes(25);
    }

    public void AddShelfFile(string path)
    {
        if (System.IO.File.Exists(path))
        {
            var info = new System.IO.FileInfo(path);
            ShelfFiles.Insert(0, new ShelfItem
            {
                FilePath = path,
                FileSizeBytes = info.Length,
                AddedAt = DateTime.Now
            });
            TriggerHud("📁", "Added to Shelf", info.Name, 100);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

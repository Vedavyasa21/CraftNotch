using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ReNotchPixel.Models;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace ReNotchPixel.Services;

public class MediaService
{
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;

    public event Action<MediaInfo>? MediaChanged;
    public MediaInfo CurrentMedia { get; private set; } = new MediaInfo();

    public async Task InitializeAsync()
    {
        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (_sessionManager != null)
            {
                _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
                UpdateCurrentSession(_sessionManager.GetCurrentSession());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaService] Init failed: {ex.Message}");
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        UpdateCurrentSession(sender.GetCurrentSession());
    }

    private void UpdateCurrentSession(GlobalSystemMediaTransportControlsSession? session)
    {
        if (_currentSession != null)
        {
            _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        }

        _currentSession = session;

        if (_currentSession != null)
        {
            _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
            _currentSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
            _ = RefreshMediaInfoAsync();
        }
        else
        {
            CurrentMedia = new MediaInfo();
            MediaChanged?.Invoke(CurrentMedia);
        }
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = RefreshMediaInfoAsync();
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        _ = RefreshMediaInfoAsync();
    }

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        _ = RefreshMediaInfoAsync();
    }

    public async Task RefreshMediaInfoAsync()
    {
        if (_currentSession == null) return;

        try
        {
            var props = await _currentSession.TryGetMediaPropertiesAsync();
            var playback = _currentSession.GetPlaybackInfo();
            var timeline = _currentSession.GetTimelineProperties();

            var info = new MediaInfo
            {
                Title = string.IsNullOrWhiteSpace(props?.Title) ? "Unknown Track" : props.Title,
                Artist = string.IsNullOrWhiteSpace(props?.Artist) ? "Unknown Artist" : props.Artist,
                IsPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                Position = timeline?.Position ?? TimeSpan.Zero,
                Duration = (timeline?.EndTime ?? TimeSpan.FromSeconds(1)) == TimeSpan.Zero ? TimeSpan.FromSeconds(1) : (timeline?.EndTime ?? TimeSpan.FromSeconds(1)),
                SourceApp = _currentSession.SourceAppUserModelId ?? ""
            };

            if (props?.Thumbnail != null)
            {
                try
                {
                    using var stream = await props.Thumbnail.OpenReadAsync();
                    var netStream = stream.AsStreamForRead();
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = netStream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    info.Thumbnail = bitmap;
                }
                catch { }
            }

            CurrentMedia = info;
            MediaChanged?.Invoke(info);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MediaService] Refresh error: {ex.Message}");
        }
    }

    public async Task TogglePlayPauseAsync()
    {
        if (_currentSession != null)
        {
            await _currentSession.TryTogglePlayPauseAsync();
        }
    }

    public async Task NextTrackAsync()
    {
        if (_currentSession != null)
        {
            await _currentSession.TrySkipNextAsync();
        }
    }

    public async Task PreviousTrackAsync()
    {
        if (_currentSession != null)
        {
            await _currentSession.TrySkipPreviousAsync();
        }
    }

    public void PlayPause() => _ = TogglePlayPauseAsync();
    public void Next() => _ = NextTrackAsync();
    public void Previous() => _ = PreviousTrackAsync();

    public async Task SeekAsync(double percentage)
    {
        if (_currentSession != null && CurrentMedia.Duration.TotalSeconds > 0)
        {
            var targetTicks = (long)(CurrentMedia.Duration.Ticks * (percentage / 100.0));
            await _currentSession.TryChangePlaybackPositionAsync(targetTicks);
        }
    }
}

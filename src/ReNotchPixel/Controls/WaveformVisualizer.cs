using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ReNotchPixel.Controls;

public class WaveformVisualizer : FrameworkElement
{
    private readonly DispatcherTimer _timer;
    private readonly Random _rand = new Random();
    private readonly double[] _currentHeights = new double[4] { 4, 8, 12, 6 };
    private readonly double[] _targetHeights = new double[4] { 4, 8, 12, 6 };
    private double _phase = 0;

    public static readonly DependencyProperty IsPlayingProperty =
        DependencyProperty.Register(nameof(IsPlaying), typeof(bool), typeof(WaveformVisualizer),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnIsPlayingChanged));

    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public static readonly DependencyProperty BarBrushProperty =
        DependencyProperty.Register(nameof(BarBrush), typeof(Brush), typeof(WaveformVisualizer),
            new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(52, 211, 153)), FrameworkPropertyMetadataOptions.AffectsRender)); // Mint Green

    public Brush BarBrush
    {
        get => (Brush)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    public WaveformVisualizer()
    {
        Width = 22;
        Height = 16;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += OnTick;
    }

    private static void OnIsPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WaveformVisualizer viz)
        {
            if ((bool)e.NewValue) viz._timer.Start();
            else viz._timer.Stop();
            viz.InvalidateVisual();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _phase += 0.2;
        for (int i = 0; i < 4; i++)
        {
            if (IsPlaying)
            {
                double wave = Math.Sin(_phase + (i * 1.2));
                _targetHeights[i] = 4 + (Math.Abs(wave) * 11) + (_rand.NextDouble() * 3);
            }
            else
            {
                _targetHeights[i] = 3;
            }

            // Smooth interpolation
            _currentHeights[i] += (_targetHeights[i] - _currentHeights[i]) * 0.35;
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double barWidth = 2.5;
        double gap = 2.5;
        double startX = (ActualWidth - (4 * barWidth + 3 * gap)) / 2;

        for (int i = 0; i < 4; i++)
        {
            double h = Math.Clamp(_currentHeights[i], 2, ActualHeight);
            double x = startX + i * (barWidth + gap);
            double y = (ActualHeight - h) / 2;

            var rect = new Rect(x, y, barWidth, h);
            dc.DrawRoundedRectangle(BarBrush, null, rect, barWidth / 2, barWidth / 2);
        }
    }
}

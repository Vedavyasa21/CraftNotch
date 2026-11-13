using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ReNotchPixel.Models;

namespace ReNotchPixel.Controls;

public class VirtualPetControl : FrameworkElement
{
    private readonly DispatcherTimer _timer;
    private readonly Random _rand = new Random();
    private PetAction _action = PetAction.Sit;
    private int _frame = 0;
    private int _actionTicks = 0;

    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(nameof(Kind), typeof(PetKind), typeof(VirtualPetControl),
            new FrameworkPropertyMetadata(PetKind.Cat, FrameworkPropertyMetadataOptions.AffectsRender));

    public PetKind Kind
    {
        get => (PetKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public VirtualPetControl()
    {
        Width = 36;
        Height = 20;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _timer.Tick += (s, e) =>
        {
            _frame++;
            _actionTicks++;
            if (_actionTicks > 12)
            {
                _actionTicks = 0;
                int r = _rand.Next(10);
                if (r < 4) _action = PetAction.Sit;
                else if (r < 7) _action = PetAction.Look;
                else if (r < 9) _action = PetAction.Walk;
                else _action = PetAction.Sleep;
            }
            InvalidateVisual();
        };
        _timer.Start();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        string face = Kind switch
        {
            PetKind.Cat => _action switch
            {
                PetAction.Sit => (_frame % 6 == 0) ? "(=^..^=)" : "(=^-..-^=)",
                PetAction.Look => "(=^o.o^=)",
                PetAction.Walk => (_frame % 2 == 0) ? "(=^. .^=)~" : "~(=^. .^=)",
                PetAction.Sleep => "(= - . - =)...z",
                _ => "(=^..^=)"
            },
            _ => _action switch
            {
                PetAction.Sit => (_frame % 6 == 0) ? "(U・ᴥ・U)" : "(U・-・U)",
                PetAction.Look => "(U・o・U)",
                PetAction.Walk => (_frame % 2 == 0) ? "(U・ᴥ・U)ﾉ" : "(U・ᴥ・U)",
                PetAction.Sleep => "(U - . - U)...z",
                _ => "(U・ᴥ・U)"
            }
        };

        var brush = new SolidColorBrush(Color.FromArgb(220, 243, 244, 246));
        var formattedText = new FormattedText(
            face,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI, Consolas, sans-serif"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            11,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        double x = (ActualWidth - formattedText.Width) / 2;
        double y = (ActualHeight - formattedText.Height) / 2;
        dc.DrawText(formattedText, new Point(x, y));
    }
}

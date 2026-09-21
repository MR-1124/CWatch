using System.Windows;
using System.Windows.Media.Animation;

namespace CWatch.UI;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        // Render resources are StaticResource-resolved before ThemeManager runs;
        // the dark defaults in App.xaml are the correct startup look.
        Opacity = 0;
        Loaded += (_, _) =>
        {
            var story = (Storyboard)Resources["SplashFadeIn"];
            story.Begin(this);
        };
    }

    /// <summary>Update the staged status line. Safe from any thread via dispatcher.</summary>
    public void SetStatus(string message)
    {
        Dispatcher.Invoke(() => StatusText.Text = message);
    }

    /// <summary>Fade out and close. Returns when the window is gone.</summary>
    public void CloseWithFade()
    {
        Dispatcher.Invoke(() =>
        {
            var fade = new DoubleAnimation(1, 0, System.TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            fade.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, fade);
            IsHitTestVisible = false;
        });
    }
}

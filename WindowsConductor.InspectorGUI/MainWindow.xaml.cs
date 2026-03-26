using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WindowsConductor.InspectorGUI;

[ExcludeFromCodeCoverage]
public partial class MainWindow : Window, ICommandOutput
{
    private readonly CommandExecutor _executor;
    private readonly CommandHistory _history = new();
    private DispatcherTimer? _blinkTimer;
    private bool _blinkVisible = true;
    private DateTime _lastTabTime = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        var session = new WcInspectorSession();
        _executor = new CommandExecutor(session, this);
        CommandInput.Focus();
    }

    private async void CommandInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            e.Handled = true;
            HandleTab();
            return;
        }

        if (e.Key == Key.Up)
        {
            e.Handled = true;
            var entry = _history.NavigateUp(CommandInput.Text);
            if (entry is not null)
            {
                CommandInput.Text = entry;
                CommandInput.CaretIndex = entry.Length;
            }
            return;
        }

        if (e.Key == Key.Down)
        {
            e.Handled = true;
            var entry = _history.NavigateDown();
            if (entry is not null)
            {
                CommandInput.Text = entry;
                CommandInput.CaretIndex = entry.Length;
            }
            return;
        }

        if (e.Key != Key.Enter) return;

        var input = CommandInput.Text.Trim();
        if (string.IsNullOrEmpty(input)) return;

        _history.Add(input);
        CommandInput.Text = "";
        AppendLog($"> {input}");

        CommandInput.IsEnabled = false;
        try
        {
            await _executor.ExecuteAsync(input);
        }
        finally
        {
            CommandInput.IsEnabled = true;
            CommandInput.Focus();
        }
    }

    private void HandleTab()
    {
        var now = DateTime.UtcNow;
        bool isDoubleTab = (now - _lastTabTime).TotalMilliseconds < 500;
        _lastTabTime = now;

        var result = CommandCompleter.Complete(CommandInput.Text);

        if (result.Applied)
        {
            CommandInput.Text = result.Text;
            CommandInput.CaretIndex = CommandInput.Text.Length;
            return;
        }

        // Update text to longest common prefix (may be unchanged)
        if (result.Text != CommandInput.Text)
        {
            CommandInput.Text = result.Text;
            CommandInput.CaretIndex = CommandInput.Text.Length;
        }

        // Double-tab: show matching commands
        if (isDoubleTab && result.Matches.Length > 0)
        {
            AppendLog(string.Join("  ", result.Matches));
        }
    }

    void ICommandOutput.WriteInfo(string message) =>
        Dispatcher.Invoke(() => AppendLog(message));

    void ICommandOutput.WriteError(string message) =>
        Dispatcher.Invoke(() => AppendLog($"ERROR: {message}"));

    void ICommandOutput.ShowScreenshot(byte[] imageData, HighlightInfo? highlight)
    {
        Dispatcher.Invoke(() =>
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = new MemoryStream(imageData);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            ScreenshotImage.Source = bitmap;

            if (highlight is not null)
                ShowHighlight(bitmap, highlight);
            else
                HideHighlight();
        });
    }

    void ICommandOutput.ClearHighlight() =>
        Dispatcher.Invoke(HideHighlight);

    private void ShowHighlight(BitmapImage bitmap, HighlightInfo highlight)
    {
        // With Stretch="Uniform", the image is scaled uniformly and centered
        // within the control. We need to compute the actual rendered image
        // offset and scale to position the highlight correctly.
        ScreenshotImage.UpdateLayout();
        var controlWidth = ScreenshotImage.ActualWidth;
        var controlHeight = ScreenshotImage.ActualHeight;
        if (controlWidth <= 0 || controlHeight <= 0) return;

        var scale = Math.Min(controlWidth / bitmap.PixelWidth, controlHeight / bitmap.PixelHeight);
        var renderedWidth = bitmap.PixelWidth * scale;
        var renderedHeight = bitmap.PixelHeight * scale;

        // Centering offset (letterbox/pillarbox margins)
        var offsetX = (controlWidth - renderedWidth) / 2;
        var offsetY = (controlHeight - renderedHeight) / 2;

        // UIAutomation bounding rects may be in logical (DPI-virtualized) pixels
        // while the screenshot is in physical pixels. Use the ratio between the
        // image pixel dimensions and the window rect dimensions to compensate.
        var dpiScaleX = (highlight.WindowWidth > 0) ? bitmap.PixelWidth / highlight.WindowWidth : 1.0;
        var dpiScaleY = (highlight.WindowHeight > 0) ? bitmap.PixelHeight / highlight.WindowHeight : 1.0;

        var physX = highlight.X * dpiScaleX;
        var physY = highlight.Y * dpiScaleY;
        var physW = highlight.Width * dpiScaleX;
        var physH = highlight.Height * dpiScaleY;

        System.Windows.Controls.Canvas.SetLeft(HighlightRect, offsetX + physX * scale);
        System.Windows.Controls.Canvas.SetTop(HighlightRect, offsetY + physY * scale);
        HighlightRect.Width = physW * scale;
        HighlightRect.Height = physH * scale;
        HighlightRect.Visibility = Visibility.Visible;

        StartBlinking();
    }

    private void HideHighlight()
    {
        StopBlinking();
        HighlightRect.Visibility = Visibility.Collapsed;
    }

    private void StartBlinking()
    {
        StopBlinking();
        _blinkVisible = true;
        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _blinkTimer.Tick += (_, _) =>
        {
            _blinkVisible = !_blinkVisible;
            HighlightRect.Stroke = _blinkVisible
                ? Brushes.Red
                : Brushes.Transparent;
        };
        _blinkTimer.Start();
    }

    private void StopBlinking()
    {
        _blinkTimer?.Stop();
        _blinkTimer = null;
    }

    private void AppendLog(string text)
    {
        OutputLog.AppendText(text + Environment.NewLine);
        OutputLog.ScrollToEnd();
    }
}

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
    private BitmapImage? _currentBitmap;
    private HighlightInfo? _currentHighlight;
    private WindowDimensions? _windowDimensions;
    private Border? _lastFocusedBorder;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        var session = new WcInspectorSession();
        _executor = new CommandExecutor(session, this);
        _focusablePanels = [CommandInput, OutputLog, ScreenshotImage, AttributesGrid];
        _panelBorders = new Dictionary<UIElement, Border>
        {
            [CommandInput] = CommandInputBorder,
            [OutputLog] = OutputLogBorder,
            [ScreenshotImage] = ScreenshotBorder,
            [AttributesGrid] = AttributesBorder,
        };
        AppendLog(string.Join("  ", CommandHelp.AllCommandNames));
        AppendLog("");
        AppendLog(CommandHelp.KeyBindingsText);
        CommandInput.Focus();
    }

    private readonly UIElement[] _focusablePanels;
    private readonly Dictionary<UIElement, Border> _panelBorders;

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.System && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            if (e.SystemKey == Key.Left && PrevMatchButton.IsEnabled)
            {
                e.Handled = true;
                PrevMatchButton_Click(PrevMatchButton, new RoutedEventArgs());
                return;
            }

            if (e.SystemKey == Key.Right && NextMatchButton.IsEnabled)
            {
                e.Handled = true;
                NextMatchButton_Click(NextMatchButton, new RoutedEventArgs());
                return;
            }
        }

        if (Keyboard.Modifiers == ModifierKeys.Shift && e.Key is Key.PageUp or Key.PageDown)
        {
            e.Handled = true;
            if (e.Key == Key.PageUp)
                OutputLog.PageUp();
            else
                OutputLog.PageDown();
            return;
        }

        if (e.Key != Key.Tab || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            return;

        e.Handled = true;
        var panels = _focusablePanels;
        int current = Array.IndexOf(panels, Keyboard.FocusedElement);
        if (current < 0)
        {
            // Find the panel that contains the focused element
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] is FrameworkElement fe && Keyboard.FocusedElement is DependencyObject focused && fe.IsAncestorOf(focused))
                {
                    current = i;
                    break;
                }
            }
        }

        int direction = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1;
        int next = ((current < 0 ? 0 : current) + direction + panels.Length) % panels.Length;
        panels[next].Focus();
    }

    private void Window_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var focused = e.NewFocus as DependencyObject;
        if (focused is null) return;

        Border? border = null;
        foreach (var (panel, b) in _panelBorders)
        {
            if (focused == panel || (panel is FrameworkElement fe && fe.IsAncestorOf(focused)))
            {
                border = b;
                break;
            }

            if (b.IsAncestorOf(focused))
            {
                border = b;
                break;
            }
        }

        if (border == _lastFocusedBorder) return;

        if (_lastFocusedBorder is not null)
            _lastFocusedBorder.BorderBrush = DefaultBorderBrush;

        if (border is not null)
            border.BorderBrush = FocusBorderBrush;

        _lastFocusedBorder = border;
    }

    private async void CommandInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                return; // Let window-level handler deal with Ctrl+Tab
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
        _history.ResetCursor();
        CommandInput.Text = "";
        AppendLog($"> {input}", bold: true);

        SetBusy(true);
        try
        {
            await _executor.ExecuteAsync(input);
        }
        finally
        {
            SetBusy(false);
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

    void ICommandOutput.ClearLog() =>
        Dispatcher.Invoke(() => OutputLog.Document.Blocks.Clear());

    void ICommandOutput.WriteInfo(string message) =>
        Dispatcher.Invoke(() => AppendLog(message));

    void ICommandOutput.WriteError(string message) =>
        Dispatcher.Invoke(() => AppendLog($"ERROR: {message}"));

    void ICommandOutput.ShowScreenshot(byte[] imageData, HighlightInfo? highlight, WindowDimensions? windowDimensions)
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

            if (windowDimensions is not null)
                _windowDimensions = windowDimensions;

            if (highlight is not null)
                ShowHighlight(bitmap, highlight);
            else
                HideHighlight();
        });
    }

    void ICommandOutput.ClearScreenshot() =>
        Dispatcher.Invoke(() =>
        {
            ScreenshotImage.Source = null;
            _windowDimensions = null;
            HideHighlight();
        });

    void ICommandOutput.ClearHighlight() =>
        Dispatcher.Invoke(HideHighlight);

    void ICommandOutput.ShowAttributes(string locatorChain, Dictionary<string, object?> attributes) =>
        Dispatcher.Invoke(() =>
        {
            if (!string.IsNullOrEmpty(locatorChain))
            {
                LocatorChainText.Text = locatorChain;
                LocatorChainPanel.Visibility = Visibility.Visible;
                BackLocatorButton.IsEnabled = _executor.CanGoBack;
            }

            AttributesGrid.ItemsSource = attributes
                .OrderBy(kv => kv.Key, StringComparer.InvariantCultureIgnoreCase)
                .Select(kv => new { Name = kv.Key, Value = kv.Value?.ToString() ?? "" })
                .ToList();
        });

    void ICommandOutput.ClearAttributes() =>
        Dispatcher.Invoke(() =>
        {
            LocatorChainText.Text = "";
            LocatorChainPanel.Visibility = Visibility.Collapsed;
            BackLocatorButton.IsEnabled = false;
            AttributesGrid.ItemsSource = null;
        });

    void ICommandOutput.UpdateMatchNavigation(int currentIndex, int totalCount) =>
        Dispatcher.Invoke(() =>
        {
            bool hasMultiple = totalCount > 1;
            PrevMatchButton.IsEnabled = hasMultiple;
            NextMatchButton.IsEnabled = hasMultiple;
            MatchCountLabel.Text = hasMultiple ? $"({currentIndex + 1}/{totalCount})" : "";
        });

    void ICommandOutput.RequestExit() =>
        Dispatcher.Invoke(Close);

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var items = AttributesGrid.SelectedItems.Count > 0
            ? AttributesGrid.SelectedItems.Cast<dynamic>()
            : AttributesGrid.ItemsSource?.Cast<dynamic>() ?? Enumerable.Empty<dynamic>();

        var lines = items.Select(item => $"{item.Name}\t{item.Value}");
        var text = string.Join(Environment.NewLine, lines);
        if (!string.IsNullOrEmpty(text))
            Clipboard.SetText(text);
    }

    private async void PrevMatchButton_Click(object sender, RoutedEventArgs e) =>
        await NavigateMatchAsync(-1);

    private async void NextMatchButton_Click(object sender, RoutedEventArgs e) =>
        await NavigateMatchAsync(1);

    private async Task NavigateMatchAsync(int direction)
    {
        PrevMatchButton.IsEnabled = false;
        NextMatchButton.IsEnabled = false;
        try
        {
            await _executor.NavigateMatchAsync(direction);
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR: {ex.Message}");
        }
    }

    private async void BackLocatorButton_Click(object sender, RoutedEventArgs e)
    {
        BackLocatorButton.IsEnabled = false;
        try
        {
            await _executor.GoBackAsync();
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR: {ex.Message}");
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshButton.IsEnabled = false;
        try
        {
            await _executor.RefreshAsync();
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR: {ex.Message}");
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private void ShowHighlight(BitmapImage bitmap, HighlightInfo highlight)
    {
        _currentBitmap = bitmap;
        _currentHighlight = highlight;
        PositionHighlight();
        StartBlinking();
    }

    private void ScreenshotContainer_SizeChanged(object sender, SizeChangedEventArgs e) =>
        PositionHighlight();

    private void PositionHighlight()
    {
        if (_currentBitmap is null || _currentHighlight is null)
            return;

        // Use the container Grid dimensions — it always fills the Border cell.
        var containerWidth = ScreenshotContainer.ActualWidth;
        var containerHeight = ScreenshotContainer.ActualHeight;
        if (containerWidth <= 0 || containerHeight <= 0) return;

        var bitmap = _currentBitmap;
        var highlight = _currentHighlight;

        // Compute the same uniform scale that Image Stretch="Uniform" applies.
        var scaleX = containerWidth / bitmap.PixelWidth;
        var scaleY = containerHeight / bitmap.PixelHeight;
        var scale = Math.Min(scaleX, scaleY);

        var renderedWidth = bitmap.PixelWidth * scale;
        var renderedHeight = bitmap.PixelHeight * scale;

        // Centering offset — the Image centers content within its layout slot.
        var offsetX = (containerWidth - renderedWidth) / 2;
        var offsetY = (containerHeight - renderedHeight) / 2;

        // UIAutomation rects are in logical pixels; screenshot is in physical pixels.
        var dpiScaleX = (highlight.WindowWidth > 0) ? bitmap.PixelWidth / highlight.WindowWidth : 1.0;
        var dpiScaleY = (highlight.WindowHeight > 0) ? bitmap.PixelHeight / highlight.WindowHeight : 1.0;

        // Convert element coords to physical pixel space, then scale to rendered size.
        var left = offsetX + highlight.X * dpiScaleX * scale;
        var top = offsetY + highlight.Y * dpiScaleY * scale;

        Canvas.SetLeft(HighlightRect, left);
        Canvas.SetTop(HighlightRect, top);
        HighlightRect.Width = highlight.Width * dpiScaleX * scale;
        HighlightRect.Height = highlight.Height * dpiScaleY * scale;
        HighlightRect.Visibility = Visibility.Visible;
    }

    private void HideHighlight()
    {
        _currentBitmap = null;
        _currentHighlight = null;
        StopBlinking();
        HighlightRect.Visibility = Visibility.Collapsed;
    }

    private void StartBlinking()
    {
        StopBlinking();
        _blinkVisible = true;
        HighlightRect.Stroke = Brushes.Red;
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

    private async void ScreenshotImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_busy) return;
        if (_currentBitmap is null || _windowDimensions is null) return;

        var containerWidth = ScreenshotContainer.ActualWidth;
        var containerHeight = ScreenshotContainer.ActualHeight;
        if (containerWidth <= 0 || containerHeight <= 0) return;

        var bitmap = _currentBitmap;
        var winDim = _windowDimensions;

        // Reverse of PositionHighlight: WPF click point → window-relative logical coords
        var scaleX = containerWidth / bitmap.PixelWidth;
        var scaleY = containerHeight / bitmap.PixelHeight;
        var scale = Math.Min(scaleX, scaleY);

        var renderedWidth = bitmap.PixelWidth * scale;
        var renderedHeight = bitmap.PixelHeight * scale;
        var offsetX = (containerWidth - renderedWidth) / 2;
        var offsetY = (containerHeight - renderedHeight) / 2;

        var dpiScaleX = winDim.Width > 0 ? bitmap.PixelWidth / winDim.Width : 1.0;
        var dpiScaleY = winDim.Height > 0 ? bitmap.PixelHeight / winDim.Height : 1.0;

        var clickPos = e.GetPosition(ScreenshotContainer);
        var winRelX = (clickPos.X - offsetX) / (dpiScaleX * scale);
        var winRelY = (clickPos.Y - offsetY) / (dpiScaleY * scale);

        // Ignore clicks outside the rendered image area
        if (winRelX < 0 || winRelY < 0 || winRelX > winDim.Width || winRelY > winDim.Height)
            return;

        var selector = _executor.IsAtRoot
            ? FormattableString.Invariant($"/*[at({winRelX:F0}, {winRelY:F0})]")
            : FormattableString.Invariant($"//frontmost::*[at({winRelX:F0}, {winRelY:F0})]");
        AppendLog($"> {selector}", bold: true);

        SetBusy(true);
        try
        {
            await _executor.ExecuteAsync($"locate {selector}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        CommandInput.IsEnabled = !busy;
        RefreshButton.IsEnabled = !busy;
        BackLocatorButton.IsEnabled = !busy && _executor.CanGoBack;
        PrevMatchButton.IsEnabled = !busy && _executor.HasMultipleMatches;
        NextMatchButton.IsEnabled = !busy && _executor.HasMultipleMatches;
        ScreenshotImage.Cursor = busy ? Cursors.Wait : Cursors.Arrow;
        if (!busy)
            CommandInput.Focus();
    }

    private static readonly SolidColorBrush InputBrush = Frozen(new(Color.FromRgb(0xFF, 0xFF, 0xFF)));
    private static readonly SolidColorBrush ResponseBrush = Frozen(new(Color.FromRgb(0xCC, 0xCC, 0xCC)));
    private static readonly SolidColorBrush FocusBorderBrush = Frozen(new(Color.FromRgb(0x00, 0x78, 0xD4)));
    private static readonly SolidColorBrush DefaultBorderBrush = Frozen(new(Color.FromRgb(0x33, 0x33, 0x33)));

    private static SolidColorBrush Frozen(SolidColorBrush brush) { brush.Freeze(); return brush; }

    private void AppendLog(string text, bool bold = false)
    {
        var paragraph = new Paragraph();
        var run = new Run(text) { Foreground = bold ? InputBrush : ResponseBrush };
        if (bold)
            run.FontWeight = FontWeights.Bold;
        paragraph.Inlines.Add(run);
        OutputLog.Document.Blocks.Add(paragraph);
        OutputLog.ScrollToEnd();
    }
}

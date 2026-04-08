using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments - CharSet.Unicode is set
#pragma warning disable SYSLIB1054 // Use LibraryImportAttribute - requires AllowUnsafeBlocks
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using GdiColor = System.Drawing.Color;
using WpfColor = System.Windows.Media.Color;

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
    private Brush _highlightBrush = Brushes.Red;
    private bool _clicklessMode;
    private DispatcherTimer? _clicklessDebounce;
    private (int X, int Y) _lastClicklessCoords = (-1, -1);
    private bool _clicklessLocated;
    private bool _sleeping;
    private Task? _clicklessTask;

    private const int WM_SYSCOMMAND = 0x0112;
    private const int WM_MEASUREITEM = 0x002C;
    private const int WM_DRAWITEM = 0x002B;
    private const int MF_SEPARATOR = 0x800;
    private const int MF_STRING = 0x0;
    private const int MF_CHECKED = 0x8;
    private const int MF_UNCHECKED = 0x0;
    private const int MF_OWNERDRAW = 0x100;
    private const int MF_GRAYED = 0x1;
    private const int MF_BYCOMMAND = 0x0;
    private const int ODS_SELECTED = 0x0001;
    private const int ODS_CHECKED = 0x0008;

    private const int SC_STOP_ON_ERROR = 0x1000;
    private const int SC_HIGHLIGHT_TITLE = 0x1100;
    private const int SC_HIGHLIGHT_RED = 0x1101;
    private const int SC_HIGHLIGHT_GREEN = 0x1102;
    private const int SC_HIGHLIGHT_BLUE = 0x1103;
    private const int SC_HIGHLIGHT_YELLOW = 0x1104;
    private const int SC_HIGHLIGHT_BLACK = 0x1105;
    private const int SC_HIGHLIGHT_WHITE = 0x1106;

    private static readonly Dictionary<int, (string Label, GdiColor Color, Brush WpfBrush)> HighlightColors = new()
    {
        [SC_HIGHLIGHT_RED] = ("Red", GdiColor.Red, Brushes.Red),
        [SC_HIGHLIGHT_GREEN] = ("Green", GdiColor.Lime, Brushes.Lime),
        [SC_HIGHLIGHT_BLUE] = ("Blue", GdiColor.DodgerBlue, Brushes.DodgerBlue),
        [SC_HIGHLIGHT_YELLOW] = ("Yellow", GdiColor.Yellow, Brushes.Yellow),
        [SC_HIGHLIGHT_BLACK] = ("Black", GdiColor.Black, Brushes.Black),
        [SC_HIGHLIGHT_WHITE] = ("White", GdiColor.White, Brushes.White),
    };

    [DllImport("user32.dll")]
    private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, int uFlags, int uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern int CheckMenuItem(IntPtr hMenu, int uIDCheckItem, int uCheck);

    [DllImport("user32.dll")]
    private static extern bool CheckMenuRadioItem(IntPtr hMenu, int first, int last, int check, int flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEASUREITEMSTRUCT
    {
        public int CtlType;
        public int CtlID;
        public int itemID;
        public int itemWidth;
        public int itemHeight;
        public IntPtr itemData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DRAWITEMSTRUCT
    {
        public int CtlType;
        public int CtlID;
        public int itemID;
        public int itemAction;
        public int itemState;
        public IntPtr hwndItem;
        public IntPtr hDC;
        public int rcLeft, rcTop, rcRight, rcBottom;
        public IntPtr itemData;
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateSolidBrush(int crColor);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreatePen(int fnPenStyle, int nWidth, int crColor);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool Rectangle(IntPtr hdc, int left, int top, int right, int bottom);

    [DllImport("gdi32.dll")]
    private static extern bool Ellipse(IntPtr hdc, int left, int top, int right, int bottom);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern int FillRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int DrawText(IntPtr hDC, string lpString, int nCount, ref RECT lpRect, int uFormat);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(IntPtr hdc, int iBkMode);

    [DllImport("gdi32.dll")]
    private static extern int SetTextColor(IntPtr hdc, int crColor);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFontW(int height, int width, int escapement, int orientation,
        int weight, int italic, int underline, int strikeOut, int charSet, int outputPrecision,
        int clipPrecision, int quality, int pitchAndFamily, string faceName);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    private const int DT_LEFT = 0x0;
    private const int DT_VCENTER = 0x4;
    private const int DT_SINGLELINE = 0x20;
    private const int TRANSPARENT = 1;
    private const int SM_CXMENUCHECK = 71;
    private const int COLOR_HIGHLIGHT = 13;
    private const int COLOR_MENU = 4;
    private const int COLOR_MENUTEXT = 7;
    private const int COLOR_HIGHLIGHTTEXT = 14;

    [DllImport("user32.dll")]
    private static extern IntPtr GetSysColorBrush(int nIndex);

    [DllImport("user32.dll")]
    private static extern int GetSysColor(int nIndex);

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
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

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var sysMenu = GetSystemMenu(hwnd, false);
        AppendMenu(sysMenu, MF_SEPARATOR, 0, string.Empty);
        AppendMenu(sysMenu, MF_STRING | MF_UNCHECKED, SC_STOP_ON_ERROR, "Stop on error");
        AppendMenu(sysMenu, MF_SEPARATOR, 0, string.Empty);
        AppendMenu(sysMenu, MF_OWNERDRAW | MF_GRAYED, SC_HIGHLIGHT_TITLE, string.Empty);
        foreach (var id in HighlightColors.Keys)
            AppendMenu(sysMenu, MF_OWNERDRAW, id, string.Empty);
        CheckMenuRadioItem(sysMenu, SC_HIGHLIGHT_RED, SC_HIGHLIGHT_WHITE, SC_HIGHLIGHT_RED, MF_BYCOMMAND);
        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_SYSCOMMAND:
                var id = wParam.ToInt32();
                if (id == SC_STOP_ON_ERROR)
                {
                    _executor.StopChainOnError = !_executor.StopChainOnError;
                    var sysMenu = GetSystemMenu(hwnd, false);
                    CheckMenuItem(sysMenu, SC_STOP_ON_ERROR,
                        _executor.StopChainOnError ? MF_CHECKED : MF_UNCHECKED);
                    handled = true;
                }
                else if (HighlightColors.ContainsKey(id))
                {
                    SetHighlightColor(hwnd, id);
                    handled = true;
                }
                break;

            case WM_MEASUREITEM:
                handled = HandleMeasureItem(lParam);
                break;

            case WM_DRAWITEM:
                handled = HandleDrawItem(lParam);
                break;
        }

        return IntPtr.Zero;
    }

    private void SetHighlightColor(IntPtr hwnd, int menuId)
    {
        _highlightBrush = HighlightColors[menuId].WpfBrush;
        var sysMenu = GetSystemMenu(hwnd, false);
        CheckMenuRadioItem(sysMenu, SC_HIGHLIGHT_RED, SC_HIGHLIGHT_WHITE, menuId, MF_BYCOMMAND);
        if (_currentHighlight is not null)
            HighlightRect.Stroke = _highlightBrush;
    }

    private static bool HandleMeasureItem(IntPtr lParam)
    {
        var mis = Marshal.PtrToStructure<MEASUREITEMSTRUCT>(lParam);
        if (mis.itemID < SC_HIGHLIGHT_TITLE || mis.itemID > SC_HIGHLIGHT_WHITE)
            return false;
        mis.itemWidth = 120;
        mis.itemHeight = mis.itemID == SC_HIGHLIGHT_TITLE ? 20 : 22;
        Marshal.StructureToPtr(mis, lParam, false);
        return true;
    }

    private static bool HandleDrawItem(IntPtr lParam)
    {
        var dis = Marshal.PtrToStructure<DRAWITEMSTRUCT>(lParam);
        if (dis.itemID < SC_HIGHLIGHT_TITLE || dis.itemID > SC_HIGHLIGHT_WHITE)
            return false;

        var hdc = dis.hDC;
        var rc = new RECT { Left = dis.rcLeft, Top = dis.rcTop, Right = dis.rcRight, Bottom = dis.rcBottom };
        bool selected = (dis.itemState & ODS_SELECTED) != 0 && dis.itemID != SC_HIGHLIGHT_TITLE;

        // Background
        var bgBrush = GetSysColorBrush(selected ? COLOR_HIGHLIGHT : COLOR_MENU);
        FillRect(hdc, ref rc, bgBrush);

        SetBkMode(hdc, TRANSPARENT);

        if (dis.itemID == SC_HIGHLIGHT_TITLE)
        {
            // Bold title
            var font = CreateFontW(-14, 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 0, 0, "Segoe UI");
            var oldFont = SelectObject(hdc, font);
            SetTextColor(hdc, GetSysColor(COLOR_MENUTEXT));
            var textRc = new RECT { Left = rc.Left + 8, Top = rc.Top, Right = rc.Right, Bottom = rc.Bottom };
            DrawText(hdc, "Highlight Color", -1, ref textRc, DT_LEFT | DT_VCENTER | DT_SINGLELINE);
            SelectObject(hdc, oldFont);
            DeleteObject(font);
        }
        else if (HighlightColors.TryGetValue(dis.itemID, out var entry))
        {
            int checkWidth = GetSystemMetrics(SM_CXMENUCHECK);
            bool isChecked = (dis.itemState & ODS_CHECKED) != 0;

            // Radio bullet
            if (isChecked)
            {
                int bulletSize = 6;
                int bulletLeft = rc.Left + (checkWidth - bulletSize) / 2;
                int bulletTop = rc.Top + (rc.Bottom - rc.Top - bulletSize) / 2;
                int textColor = GetSysColor(selected ? COLOR_HIGHLIGHTTEXT : COLOR_MENUTEXT);
                var bulletBrush = CreateSolidBrush(textColor);
                var bulletPen = CreatePen(0, 1, textColor);
                var oldBulletPen = SelectObject(hdc, bulletPen);
                var oldBulletBrush = SelectObject(hdc, bulletBrush);
                Ellipse(hdc, bulletLeft, bulletTop, bulletLeft + bulletSize, bulletTop + bulletSize);
                SelectObject(hdc, oldBulletBrush);
                SelectObject(hdc, oldBulletPen);
                DeleteObject(bulletBrush);
                DeleteObject(bulletPen);
            }

            // Color square
            int squareSize = 12;
            int squareLeft = rc.Left + checkWidth + 2;
            int squareTop = rc.Top + (rc.Bottom - rc.Top - squareSize) / 2;

            // Color square with 1px black border
            var pen = CreatePen(0, 1, ColorToInt(GdiColor.Black));
            var brush = CreateSolidBrush(ColorToInt(entry.Color));
            var oldPen = SelectObject(hdc, pen);
            var oldBrush = SelectObject(hdc, brush);
            Rectangle(hdc, squareLeft, squareTop, squareLeft + squareSize, squareTop + squareSize);
            SelectObject(hdc, oldBrush);
            SelectObject(hdc, oldPen);
            DeleteObject(brush);
            DeleteObject(pen);

            // Label text
            var font = CreateFontW(-12, 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 0, 0, "Segoe UI");
            var oldFont = SelectObject(hdc, font);
            SetTextColor(hdc, GetSysColor(selected ? COLOR_HIGHLIGHTTEXT : COLOR_MENUTEXT));
            var textRc = new RECT
            {
                Left = squareLeft + squareSize + 6,
                Top = rc.Top,
                Right = rc.Right,
                Bottom = rc.Bottom
            };
            DrawText(hdc, entry.Label, -1, ref textRc, DT_LEFT | DT_VCENTER | DT_SINGLELINE);
            SelectObject(hdc, oldFont);
            DeleteObject(font);
        }

        return true;
    }

    private static int ColorToInt(GdiColor c) => c.R | (c.G << 8) | (c.B << 16);

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
            _clicklessLocated = false;
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

    void ICommandOutput.WriteCommand(string command) =>
        Dispatcher.Invoke(() => AppendLog($"> {command}", italic: true, brush: CommandBrush));

    void ICommandOutput.WriteError(string message) =>
        Dispatcher.Invoke(() => AppendLog($"ERROR: {message}", brush: ErrorBrush));

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

    private Action? _sleepStopAction;
    private DispatcherTimer? _sleepCountdownTimer;
    private DateTime _sleepEndTime;

    void ICommandOutput.ShowSleepCancel(int totalMilliseconds, Action cancelAction)
    {
        Dispatcher.Invoke(() =>
        {
            _sleepStopAction = cancelAction;
            _sleepEndTime = DateTime.UtcNow.AddMilliseconds(totalMilliseconds);
            UpdateSleepButtonLabel();
            SleepStopButton.Visibility = Visibility.Visible;
            _sleepCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _sleepCountdownTimer.Tick += (_, _) => UpdateSleepButtonLabel();
            _sleepCountdownTimer.Start();
            _sleeping = true;
        });
    }

    async Task ICommandOutput.HideSleepCancelAsync()
    {
        Dispatcher.Invoke(() =>
        {
            _sleeping = false;
            _clicklessDebounce?.Stop();
            _sleepCountdownTimer?.Stop();
            _sleepCountdownTimer = null;
            SleepStopButton.Visibility = Visibility.Collapsed;
            _sleepStopAction = null;
        });
        if (_clicklessTask is not null)
        {
            await _clicklessTask;
            _clicklessTask = null;
        }
    }

    private void UpdateSleepButtonLabel()
    {
        var remaining = Math.Max(0, (int)(_sleepEndTime - DateTime.UtcNow).TotalMilliseconds);
        SleepStopButton.Content = $"{remaining} - Stop";
    }

    private void SleepStopButton_Click(object sender, RoutedEventArgs e) =>
        _sleepStopAction?.Invoke();

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
        HighlightRect.Stroke = _highlightBrush;
        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _blinkTimer.Tick += (_, _) =>
        {
            _blinkVisible = !_blinkVisible;
            HighlightRect.Stroke = _blinkVisible
                ? _highlightBrush
                : Brushes.Transparent;
        };
        _blinkTimer.Start();
    }

    private void StopBlinking()
    {
        _blinkTimer?.Stop();
        _blinkTimer = null;
    }

    private (double X, double Y)? ScreenPointToWindowRelative(System.Windows.Point pos)
    {
        if (_currentBitmap is null || _windowDimensions is null) return null;

        var containerWidth = ScreenshotContainer.ActualWidth;
        var containerHeight = ScreenshotContainer.ActualHeight;
        if (containerWidth <= 0 || containerHeight <= 0) return null;

        var bitmap = _currentBitmap;
        var winDim = _windowDimensions;

        var scaleX = containerWidth / bitmap.PixelWidth;
        var scaleY = containerHeight / bitmap.PixelHeight;
        var scale = Math.Min(scaleX, scaleY);

        var renderedWidth = bitmap.PixelWidth * scale;
        var renderedHeight = bitmap.PixelHeight * scale;
        var offsetX = (containerWidth - renderedWidth) / 2;
        var offsetY = (containerHeight - renderedHeight) / 2;

        var dpiScaleX = winDim.Width > 0 ? bitmap.PixelWidth / winDim.Width : 1.0;
        var dpiScaleY = winDim.Height > 0 ? bitmap.PixelHeight / winDim.Height : 1.0;

        var winRelX = (pos.X - offsetX) / (dpiScaleX * scale);
        var winRelY = (pos.Y - offsetY) / (dpiScaleY * scale);

        if (winRelX < 0 || winRelY < 0 || winRelX > winDim.Width || winRelY > winDim.Height)
            return null;

        return (winRelX, winRelY);
    }

    private async void ScreenshotImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_busy) return;
        var coords = ScreenPointToWindowRelative(e.GetPosition(ScreenshotContainer));
        if (coords is null) return;

        var (winRelX, winRelY) = coords.Value;
        var selector = _executor.IsAtRoot
            ? FormattableString.Invariant($"/*[at({winRelX:F0}, {winRelY:F0})]")
            : FormattableString.Invariant($"//frontmost::*[at({winRelX:F0}, {winRelY:F0})]");
        AppendLog($"> {selector}", bold: true);

        SetBusy(true);
        try
        {
            await _executor.ExecuteAsync($"locate {selector}");
            _clicklessLocated = false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ClicklessCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _clicklessMode = ClicklessCheckBox.IsChecked == true;
        if (!_clicklessMode)
        {
            _clicklessDebounce?.Stop();
            _clicklessDebounce = null;
            _lastClicklessCoords = (-1, -1);
        }
    }

    private void ScreenshotImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_clicklessMode || (_busy && !_sleeping)) return;

        var coords = ScreenPointToWindowRelative(e.GetPosition(ScreenshotContainer));
        if (coords is null) return;

        var rounded = ((int)coords.Value.X, (int)coords.Value.Y);
        if (rounded == _lastClicklessCoords) return;
        _lastClicklessCoords = rounded;

        _clicklessDebounce?.Stop();
        _clicklessDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _clicklessDebounce.Tick += (_, _) =>
        {
            _clicklessDebounce?.Stop();
            if (_busy && !_sleeping) return;
            if (_clicklessTask is { IsCompleted: false }) return;

            var (winRelX, winRelY) = (rounded.Item1, rounded.Item2);
            var selector = _executor.IsAtRoot
                ? FormattableString.Invariant($"/*[at({winRelX}, {winRelY})]")
                : FormattableString.Invariant($"//frontmost::*[at({winRelX}, {winRelY})]");

            _clicklessTask = RunClicklessLocateAsync(selector);
        };
        _clicklessDebounce.Start();
    }

    private async Task RunClicklessLocateAsync(string selector)
    {
        bool wasSleeping = _sleeping;
        if (!wasSleeping) SetBusy(true);
        try
        {
            if (_clicklessLocated && _executor.CanGoBack)
                await _executor.GoBackAsync();
            AppendLog($"> {selector}", italic: true, brush: CommandBrush);
            await _executor.ExecuteAsync($"locate {selector}");
            _clicklessLocated = true;
        }
        finally
        {
            if (!wasSleeping) SetBusy(false);
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

    private static readonly SolidColorBrush InputBrush = Frozen(new(WpfColor.FromRgb(0xFF, 0xFF, 0xFF)));
    private static readonly SolidColorBrush ResponseBrush = Frozen(new(WpfColor.FromRgb(0xCC, 0xCC, 0xCC)));
    private static readonly SolidColorBrush CommandBrush = Frozen(new(WpfColor.FromRgb(0x80, 0xC0, 0xFF)));
    private static readonly SolidColorBrush ErrorBrush = Frozen(new(WpfColor.FromRgb(0xFF, 0x80, 0x80)));
    private static readonly SolidColorBrush FocusBorderBrush = Frozen(new(WpfColor.FromRgb(0x40, 0xA0, 0xF0)));
    private static readonly SolidColorBrush DefaultBorderBrush = Frozen(new(WpfColor.FromRgb(0x33, 0x33, 0x33)));

    private static SolidColorBrush Frozen(SolidColorBrush brush) { brush.Freeze(); return brush; }

    private void AppendLog(string text, bool bold = false, bool italic = false, SolidColorBrush? brush = null)
    {
        brush ??= bold ? InputBrush : ResponseBrush;
        var weight = bold ? FontWeights.Bold : FontWeights.Normal;
        var style = italic ? FontStyles.Italic : FontStyles.Normal;
        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            var paragraph = new Paragraph();
            var run = new Run(line.TrimEnd('\r')) { Foreground = brush, FontWeight = weight, FontStyle = style };
            paragraph.Inlines.Add(run);
            OutputLog.Document.Blocks.Add(paragraph);
        }
        OutputLog.ScrollToEnd();
    }
}

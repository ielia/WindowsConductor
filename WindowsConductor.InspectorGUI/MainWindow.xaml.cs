using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using WindowsConductor.Client;
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
#pragma warning disable CA1001 // _snapshotCts is disposed in ExitSnapshotMode lifecycle method
public partial class MainWindow : Window, ICommandOutput
#pragma warning restore CA1001
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
    private bool _snapshotMode;
    private SnapshotCapture? _snapshotCapture;
    private CancellationTokenSource? _snapshotCts;
    private TaskCompletionSource? _snapshotTcs;
    private string? _preSnapshotTitle;
    private bool _preSnapshotClickless;
    private List<TreeViewItem> _snapshotHitItems = [];
    private int _snapshotHitIndex;

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
    private const int SC_ALLOW_SELF_SIGNED = 0x1001;
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
        _normalPanels = [CommandInput, ScreenshotImage, AttributesGrid, OutputLog];
        _snapshotPanels = [SnapshotTree, ScreenshotImage, AttributesGrid, OutputLog];
        _panelBorders = new Dictionary<UIElement, Border>
        {
            [CommandInput] = CommandInputBorder,
            [OutputLog] = OutputLogBorder,
            [ScreenshotImage] = ScreenshotBorder,
            [AttributesGrid] = AttributesBorder,
            [SnapshotTree] = SnapshotPanel,
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
        AppendMenu(sysMenu, MF_STRING | MF_CHECKED, SC_ALLOW_SELF_SIGNED, "Allow self-signed certs");
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
                    _ = CheckMenuItem(sysMenu, SC_STOP_ON_ERROR,
                        _executor.StopChainOnError ? MF_CHECKED : MF_UNCHECKED);
                    handled = true;
                }
                else if (id == SC_ALLOW_SELF_SIGNED)
                {
                    var session = _executor.Session;
                    session.AllowSelfSignedCerts = !session.AllowSelfSignedCerts;
                    var sysMenu = GetSystemMenu(hwnd, false);
                    _ = CheckMenuItem(sysMenu, SC_ALLOW_SELF_SIGNED,
                        session.AllowSelfSignedCerts ? MF_CHECKED : MF_UNCHECKED);
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
        _ = FillRect(hdc, ref rc, bgBrush);

        _ = SetBkMode(hdc, TRANSPARENT);

        if (dis.itemID == SC_HIGHLIGHT_TITLE)
        {
            // Bold title
            var font = CreateFontW(-14, 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 0, 0, "Segoe UI");
            var oldFont = SelectObject(hdc, font);
            _ = SetTextColor(hdc, GetSysColor(COLOR_MENUTEXT));
            var textRc = new RECT { Left = rc.Left + 8, Top = rc.Top, Right = rc.Right, Bottom = rc.Bottom };
            _ = DrawText(hdc, "Highlight Color", -1, ref textRc, DT_LEFT | DT_VCENTER | DT_SINGLELINE);
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
            _ = SetTextColor(hdc, GetSysColor(selected ? COLOR_HIGHLIGHTTEXT : COLOR_MENUTEXT));
            var textRc = new RECT
            {
                Left = squareLeft + squareSize + 6,
                Top = rc.Top,
                Right = rc.Right,
                Bottom = rc.Bottom
            };
            _ = DrawText(hdc, entry.Label, -1, ref textRc, DT_LEFT | DT_VCENTER | DT_SINGLELINE);
            SelectObject(hdc, oldFont);
            DeleteObject(font);
        }

        return true;
    }

    private static int ColorToInt(GdiColor c) => c.R | (c.G << 8) | (c.B << 16);

    private readonly UIElement[] _normalPanels;
    private readonly UIElement[] _snapshotPanels;
    private UIElement[] _focusablePanels => _snapshotMode ? _snapshotPanels : _normalPanels;
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

            if (e.SystemKey == Key.B && BackLocatorButton.IsEnabled)
            {
                e.Handled = true;
                BackLocatorButton_Click(BackLocatorButton, new RoutedEventArgs());
                return;
            }

            if (e.SystemKey == Key.S && SleepStopButton.Visibility == Visibility.Visible)
            {
                e.Handled = true;
                SleepStopButton_Click(SleepStopButton, new RoutedEventArgs());
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

        var fullText = CommandInput.Text;
        var lastSemicolon = fullText.LastIndexOf(';');
        var prefix = lastSemicolon >= 0 ? fullText[..(lastSemicolon + 1)] : "";
        var segment = lastSemicolon >= 0 ? fullText[(lastSemicolon + 1)..].TrimStart() : fullText;

        var result = CommandCompleter.Complete(segment);

        if (result.Applied)
        {
            CommandInput.Text = prefix + (prefix.Length > 0 ? " " : "") + result.Text;
            CommandInput.CaretIndex = CommandInput.Text.Length;
            return;
        }

        var newText = prefix + (prefix.Length > 0 ? " " : "") + result.Text;
        if (newText != fullText)
        {
            CommandInput.Text = newText;
            CommandInput.CaretIndex = CommandInput.Text.Length;
        }

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

    void ICommandOutput.WriteBulletInfo(string message) =>
        Dispatcher.Invoke(() =>
        {
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run("* ") { Foreground = CommandBrush, FontStyle = FontStyles.Italic });
            paragraph.Inlines.Add(new Run(message) { Foreground = ResponseBrush });
            OutputLog.Document.Blocks.Add(paragraph);
            OutputLog.ScrollToEnd();
        });

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
                .Select(kv => new { Name = kv.Key, Value = FormatAttrValue(kv.Value), IsNull = kv.Value is null })
                .ToList();

            SnapshotButton.IsEnabled = true;
        });

    void ICommandOutput.ClearAttributes() =>
        Dispatcher.Invoke(() =>
        {
            LocatorChainText.Text = "";
            LocatorChainPanel.Visibility = Visibility.Collapsed;
            BackLocatorButton.IsEnabled = false;
            AttributesGrid.ItemsSource = null;
            SnapshotButton.IsEnabled = false;
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

    // ── Snapshot mode ────────────────────────────────────────────────────

    private async void SnapshotButton_Click(object sender, RoutedEventArgs e) =>
        await StartSnapshotAsync();

    Task ICommandOutput.RunSnapshotAsync() => StartSnapshotAsync();

    private async Task StartSnapshotAsync()
    {
        if (_snapshotMode || !_executor.Session.HasSelectedElement) return;

        _snapshotCts = new CancellationTokenSource();
        _snapshotTcs = new TaskCompletionSource();
        var ct = _snapshotCts.Token;

        EnterSnapshotMode();

        try
        {
            var session = _executor.Session;
            var elementsById = new Dictionary<string, WcElement>();

            var rootNode = await BuildSnapshotTreeAsync(session, elementsById, ct);
            ct.ThrowIfCancellationRequested();

            var screenshotBytes = await session.DesktopScreenshotAsync(ct);
            ct.ThrowIfCancellationRequested();

            var unionRect = ComputeUnionRect(rootNode);
            byte[] croppedBytes;
            if (unionRect is not null)
                croppedBytes = CropScreenshot(screenshotBytes, unionRect);
            else
                croppedBytes = screenshotBytes;

            _snapshotCapture = new SnapshotCapture(
                croppedBytes,
                unionRect ?? new BoundingRect(0, 0, 0, 0),
                rootNode,
                elementsById);

            ShowSnapshotScreenshot(croppedBytes, unionRect);
            SnapshotTree.IsEnabled = true;
            if (SnapshotTree.Items[0] is TreeViewItem rootItem)
                rootItem.IsSelected = true;
        }
        catch (OperationCanceledException)
        {
            ExitSnapshotMode();
            return;
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR: Snapshot failed: {ex.Message}", brush: ErrorBrush);
            ExitSnapshotMode();
            return;
        }

        await _snapshotTcs.Task;
    }

    private async Task<SnapshotNode> BuildSnapshotTreeAsync(
        IInspectorSession session, Dictionary<string, WcElement> elementsById, CancellationToken ct)
    {
        var attrs = await session.GetAttributesAsync(ct);
        BoundingRect? rect = null;
        try { rect = await session.GetElementBoundingRectAsync(ct); } catch { }

        var label = ComputeSnapshotLabel(attrs);
        var children = new List<SnapshotNode>();
        var node = new SnapshotNode(label, rect, attrs, children);

        var treeItem = new TreeViewItem { Header = label, Tag = node, Style = SnapshotTreeItemStyle };
        SnapshotTree.Items.Add(treeItem);

#pragma warning disable CS0162 // Unreachable code — SnapshotGetDescendantsInBulk is a compile-time toggle
        if (WcInspectorSession.SnapshotGetDescendantsInBulk)
        {
            var tree = await session.GetDescendantsAsync(ct);
            await PopulateFromTreeNodeAsync(tree, treeItem, elementsById, ct);
        }
        else
        {
            var childElements = await session.GetChildrenAsync(ct);
            foreach (var childEl in childElements)
            {
                ct.ThrowIfCancellationRequested();
                elementsById[childEl.ElementId] = childEl;
                await BuildChildSnapshotAsync(childEl, treeItem, elementsById, ct);
            }
        }
#pragma warning restore CS0162

        return node;
    }

    private static async Task BuildChildSnapshotAsync(
        WcElement element, TreeViewItem parentItem,
        Dictionary<string, WcElement> elementsById, CancellationToken ct)
    {
        var attrs = await element.GetAttributesAsync(ct);
        BoundingRect? rect = null;
        try { rect = await element.GetBoundingRectAsync(ct); } catch { }

        var label = ComputeSnapshotLabel(attrs);
        var children = new List<SnapshotNode>();
        var node = new SnapshotNode(label, rect, attrs, children);

        var treeItem = new TreeViewItem { Header = label, Tag = node, Style = SnapshotTreeItemStyle };
        parentItem.Items.Add(treeItem);
        parentItem.IsExpanded = true;

        var parentNode = (SnapshotNode)parentItem.Tag;
        parentNode.Children.Add(node);

        var childElements = await element.ChildrenAsync(ct);
        foreach (var childEl in childElements)
        {
            ct.ThrowIfCancellationRequested();
            elementsById[childEl.ElementId] = childEl;
            await BuildChildSnapshotAsync(childEl, treeItem, elementsById, ct);
        }
    }

    private static async Task PopulateFromTreeNodeAsync(
        IReadOnlyTreeNode<WcElement> tree, TreeViewItem parentItem,
        Dictionary<string, WcElement> elementsById, CancellationToken ct)
    {
        foreach (var childTree in tree.Children)
        {
            ct.ThrowIfCancellationRequested();
            var el = childTree.Value;
            elementsById[el.ElementId] = el;

            var attrs = await el.GetAttributesAsync(ct);
            BoundingRect? rect = null;
            try { rect = await el.GetBoundingRectAsync(ct); } catch { }

            var label = ComputeSnapshotLabel(attrs);
            var children = new List<SnapshotNode>();
            var node = new SnapshotNode(label, rect, attrs, children);

            var treeItem = new TreeViewItem { Header = label, Tag = node, Style = SnapshotTreeItemStyle };
            parentItem.Items.Add(treeItem);
            parentItem.IsExpanded = true;

            var parentNode = (SnapshotNode)parentItem.Tag;
            parentNode.Children.Add(node);

            await PopulateFromTreeNodeAsync(childTree, treeItem, elementsById, ct);
        }
    }

    private static string FormatAttrValue(object? value) => value switch
    {
        System.Drawing.Point p => $"{{x:{p.X},y:{p.Y}}}",
        System.Drawing.Rectangle r => $"{{x:{r.X},y:{r.Y},width:{r.Width},height:{r.Height}}}",
        null => "null",
        _ => value.ToString() ?? ""
    };

    private static string ComputeSnapshotLabel(Dictionary<string, object?> attrs)
    {
        var controlType = attrs.TryGetValue("controltype", out var ct) && ct is string ctStr && !string.IsNullOrEmpty(ctStr)
            ? ctStr : "<Unknown Type>";

        string? identifier = null;
        if (attrs.TryGetValue("automationid", out var aid) && aid is string aidStr && !string.IsNullOrEmpty(aidStr))
            identifier = aidStr;
        else if (attrs.TryGetValue("name", out var name) && name is string ns && !string.IsNullOrEmpty(ns))
            identifier = ns;

        return identifier is not null ? $"{controlType} [{identifier}]" : controlType;
    }

    private static BoundingRect? ComputeUnionRect(SnapshotNode node)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        bool found = false;
        AccumulateRects(node, ref minX, ref minY, ref maxX, ref maxY, ref found);
        if (!found) return null;
        return new BoundingRect(minX, minY, maxX - minX, maxY - minY);
    }

    private static void AccumulateRects(SnapshotNode node,
        ref double minX, ref double minY, ref double maxX, ref double maxY, ref bool found)
    {
        if (node.BoundingRect is { Width: > 0, Height: > 0 } r)
        {
            minX = Math.Min(minX, r.X);
            minY = Math.Min(minY, r.Y);
            maxX = Math.Max(maxX, r.X + r.Width);
            maxY = Math.Max(maxY, r.Y + r.Height);
            found = true;
        }
        foreach (var child in node.Children)
            AccumulateRects(child, ref minX, ref minY, ref maxX, ref maxY, ref found);
    }

    private static byte[] CropScreenshot(byte[] screenshotBytes, BoundingRect unionRect)
    {
        using var bitmap = SkiaSharp.SKBitmap.Decode(screenshotBytes);
        var cropX = Math.Max(0, (int)unionRect.X);
        var cropY = Math.Max(0, (int)unionRect.Y);
        var cropW = Math.Min((int)unionRect.Width, bitmap.Width - cropX);
        var cropH = Math.Min((int)unionRect.Height, bitmap.Height - cropY);
        if (cropW <= 0 || cropH <= 0) return screenshotBytes;

        var subset = new SkiaSharp.SKBitmap(cropW, cropH);
        using var canvas = new SkiaSharp.SKCanvas(subset);
        canvas.DrawBitmap(bitmap, new SkiaSharp.SKRect(cropX, cropY, cropX + cropW, cropY + cropH),
            new SkiaSharp.SKRect(0, 0, cropW, cropH));
        using var image = SkiaSharp.SKImage.FromBitmap(subset);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private void ShowSnapshotScreenshot(byte[] imageBytes, BoundingRect? unionRect)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = new MemoryStream(imageBytes);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        ScreenshotImage.Source = bitmap;
        _currentBitmap = bitmap;

        if (unionRect is not null)
            _windowDimensions = new WindowDimensions(unionRect.X, unionRect.Y, unionRect.Width, unionRect.Height);

        StopBlinking();
        _currentHighlight = null;
        HighlightRect.Visibility = Visibility.Collapsed;
    }

    private void EnterSnapshotMode()
    {
        _snapshotMode = true;
        _preSnapshotTitle = Title;
        _preSnapshotClickless = ClicklessCheckBox.IsChecked == true;

        Title += " [snapshot]";
        ClicklessCheckBox.IsChecked = false;
        ClicklessCheckBox.IsEnabled = false;
        CommandInput.IsEnabled = false;
        SnapshotButton.IsEnabled = false;
        RefreshButton.IsEnabled = false;
        BackLocatorButton.IsEnabled = false;
        PrevMatchButton.IsEnabled = false;
        NextMatchButton.IsEnabled = false;
        MatchCountLabel.Text = "";

        LocatorChainPanel.Visibility = Visibility.Collapsed;

        SnapshotPanel.Visibility = Visibility.Visible;
        SnapshotSplitter.Visibility = Visibility.Visible;
        SnapshotColumn.Width = new GridLength(280);
        SnapshotSplitterColumn.Width = GridLength.Auto;
        SnapshotPanel.Focus();

        SetSnapshotTint(true);
    }

    private void ExitSnapshotMode()
    {
        _snapshotMode = false;
        _snapshotCapture = null;
        _snapshotCts?.Dispose();
        _snapshotCts = null;
        _snapshotTcs?.TrySetResult();
        _snapshotTcs = null;
        _snapshotHitItems = [];
        _snapshotHitIndex = 0;

        SnapshotTree.IsEnabled = false;
        SnapshotTree.Items.Clear();
        SnapshotPanel.Visibility = Visibility.Collapsed;
        SnapshotSplitter.Visibility = Visibility.Collapsed;
        SnapshotColumn.Width = new GridLength(0);
        SnapshotSplitterColumn.Width = new GridLength(0);

        Title = _preSnapshotTitle ?? BaseTitle;
        ClicklessCheckBox.IsEnabled = true;
        if (_preSnapshotClickless) ClicklessCheckBox.IsChecked = true;
        CommandInput.IsEnabled = !_busy;
        SnapshotButton.IsEnabled = _executor.Session.HasSelectedElement;

        SetSnapshotTint(false);
    }

    private void SetSnapshotTint(bool tinted)
    {
        var bg = tinted
            ? new SolidColorBrush(WpfColor.FromRgb(0x24, 0x24, 0x1A))
            : new SolidColorBrush(WpfColor.FromRgb(0x1E, 0x1E, 0x1E));
        var altBg = tinted
            ? new SolidColorBrush(WpfColor.FromRgb(0x2B, 0x2B, 0x1F))
            : new SolidColorBrush(WpfColor.FromRgb(0x25, 0x25, 0x25));

        SnapshotPanel.Background = bg;
        ScreenshotBorder.Background = bg;
        OutputLogBorder.Background = bg;
        OutputLog.Background = bg;
        AttributesBorder.Background = bg;
        AttributesGrid.Background = bg;
        AttributesGrid.RowBackground = bg;
        AttributesGrid.AlternatingRowBackground = altBg;
    }

    private void SnapshotCloseButton_Click(object sender, RoutedEventArgs e)
    {
        _snapshotCts?.Cancel();
        if (_snapshotCapture is not null)
        {
            ExitSnapshotMode();
            _ = RestoreAfterSnapshotAsync();
        }
    }

    private async Task RestoreAfterSnapshotAsync()
    {
        var ownsBusy = !_busy;
        if (ownsBusy) SetBusy(true);
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
            if (ownsBusy) SetBusy(false);
        }
    }

    private void SnapshotTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeViewItem { Tag: SnapshotNode node } || _snapshotCapture is null) return;

        AttributesGrid.ItemsSource = node.Attributes
            .OrderBy(kv => kv.Key, StringComparer.InvariantCultureIgnoreCase)
            .Select(kv => new { Name = kv.Key, Value = kv.Value?.ToString() ?? "" })
            .ToList();

        if (node.BoundingRect is { Width: > 0, Height: > 0 } rect)
        {
            var union = _snapshotCapture.UnionRect;
            var highlight = new HighlightInfo(
                rect.X - union.X, rect.Y - union.Y, rect.Width, rect.Height,
                union.Width, union.Height);
            _currentHighlight = highlight;
            PositionHighlight();
            StartBlinking();
        }
        else
        {
            StopBlinking();
            _currentHighlight = null;
            HighlightRect.Visibility = Visibility.Collapsed;
        }
    }

    private const string BaseTitle = "WindowsConductor Inspector";

    void ICommandOutput.SetConnectionUrl(string? url) =>
        Dispatcher.Invoke(() => Title = url is not null ? $"{BaseTitle} - {url}" : BaseTitle);

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

    private async void PrevMatchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshotMode) { NavigateSnapshotHit(-1); return; }
        await NavigateMatchAsync(-1);
    }

    private async void NextMatchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshotMode) { NavigateSnapshotHit(1); return; }
        await NavigateMatchAsync(1);
    }

    private void NavigateSnapshotHit(int direction)
    {
        if (_snapshotHitItems.Count <= 1) return;
        _snapshotHitIndex = (_snapshotHitIndex + direction + _snapshotHitItems.Count) % _snapshotHitItems.Count;
        SelectSnapshotHit();
    }

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
        if (_snapshotMode)
        {
            HandleSnapshotClick(e);
            return;
        }

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

    private void HandleSnapshotClick(MouseButtonEventArgs e)
    {
        if (_snapshotCapture is null || !SnapshotTree.IsEnabled) return;

        var coords = ScreenPointToWindowRelative(e.GetPosition(ScreenshotContainer));
        if (coords is null) return;

        var (relX, relY) = coords.Value;
        var union = _snapshotCapture.UnionRect;
        var absX = relX + union.X;
        var absY = relY + union.Y;

        var hits = new List<TreeViewItem>();
        CollectHitItems(SnapshotTree.Items, absX, absY, hits);

        if (hits.Count == 0) return;

        hits.Sort((a, b) =>
        {
            var ra = ((SnapshotNode)a.Tag).BoundingRect!;
            var rb = ((SnapshotNode)b.Tag).BoundingRect!;
            return (ra.Width * ra.Height).CompareTo(rb.Width * rb.Height);
        });

        _snapshotHitItems = hits;
        _snapshotHitIndex = 0;
        SelectSnapshotHit();
    }

    private static void CollectHitItems(ItemCollection items, double x, double y, List<TreeViewItem> hits)
    {
        foreach (var item in items)
        {
            if (item is not TreeViewItem { Tag: SnapshotNode node } tvi) continue;
            if (node.BoundingRect is { Width: > 0, Height: > 0 } r
                && x >= r.X && y >= r.Y && x <= r.X + r.Width && y <= r.Y + r.Height)
            {
                hits.Add(tvi);
            }
            CollectHitItems(tvi.Items, x, y, hits);
        }
    }

    private void SelectSnapshotHit()
    {
        var tvi = _snapshotHitItems[_snapshotHitIndex];
        tvi.IsSelected = true;
        tvi.BringIntoView();

        var hasMultiple = _snapshotHitItems.Count > 1;
        PrevMatchButton.IsEnabled = hasMultiple;
        NextMatchButton.IsEnabled = hasMultiple;
        MatchCountLabel.Text = hasMultiple
            ? $"{_snapshotHitIndex + 1}/{_snapshotHitItems.Count}"
            : "";
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
        if (_snapshotMode || !_clicklessMode || (_busy && !_sleeping)) return;

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

    private static readonly Style SnapshotTreeItemStyle = CreateSnapshotTreeItemStyle();

    private static Style CreateSnapshotTreeItemStyle()
    {
        var style = new Style(typeof(TreeViewItem));
        style.Setters.Add(new Setter(ForegroundProperty, ResponseBrush));
        style.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty, true));
        var selectedTrigger = new Trigger { Property = TreeViewItem.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(ForegroundProperty, Brushes.Black));
        style.Triggers.Add(selectedTrigger);
        style.Seal();
        return style;
    }

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

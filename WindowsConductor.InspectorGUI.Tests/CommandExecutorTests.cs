using WindowsConductor.Client;
using WindowsConductor.InspectorGUI;

namespace WindowsConductor.InspectorGUI.Tests;

[TestFixture]
[Category("Unit")]
public class CommandExecutorTests
{
    private FakeInspectorSession _session = null!;
    private FakeCommandOutput _output = null!;
    private CommandExecutor _executor = null!;

    [SetUp]
    public void SetUp()
    {
        _session = new FakeInspectorSession();
        _output = new FakeCommandOutput();
        _executor = new CommandExecutor(_session, _output);
    }

    // ── Parse errors ────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_InvalidCommand_WritesError()
    {
        await _executor.ExecuteAsync("bogus");
        Assert.That(_output.ErrorMessages, Has.Count.EqualTo(1));
        Assert.That(_output.ErrorMessages[0], Does.Contain("Unknown command"));
    }

    [Test]
    public async Task Execute_EmptyInput_WritesError()
    {
        await _executor.ExecuteAsync("");
        Assert.That(_output.ErrorMessages, Has.Count.EqualTo(1));
    }

    // ── connect ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Connect_CallsSessionConnect()
    {
        await _executor.ExecuteAsync("connect ws://localhost:8765/");
        Assert.That(_session.Calls[0].Method, Is.EqualTo("Connect"));
        Assert.That(_session.Calls[0].Args[0], Is.EqualTo("ws://localhost:8765/"));
        Assert.That(_output.InfoMessages[0], Does.Contain("Connected"));
    }

    // ── launch ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Launch_NotConnected_WritesError()
    {
        await _executor.ExecuteAsync("launch calc.exe");
        Assert.That(_output.ErrorMessages, Has.Count.EqualTo(1));
        Assert.That(_output.ErrorMessages[0], Does.Contain("Not connected"));
    }

    [Test]
    public async Task Execute_Launch_CallsSessionLaunch()
    {
        _session.IsConnected = true;
        await _executor.ExecuteAsync("launch calc.exe Calculator.* 3000");
        Assert.That(_session.Calls.Any(c => c.Method == "Launch"), Is.True);
        Assert.That(_output.InfoMessages[0], Does.Contain("Launched"));
        Assert.That(_output.Screenshots, Has.Count.EqualTo(1));
    }

    // ── attach ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Attach_NotConnected_WritesError()
    {
        await _executor.ExecuteAsync("attach Calc.*");
        Assert.That(_output.ErrorMessages[0], Does.Contain("Not connected"));
    }

    [Test]
    public async Task Execute_Attach_CallsSessionAttach()
    {
        _session.IsConnected = true;
        await _executor.ExecuteAsync("attach Calculator.* 2000");
        Assert.That(_session.Calls.Any(c => c.Method == "Attach"), Is.True);
        Assert.That(_output.InfoMessages[0], Does.Contain("Attached"));
        Assert.That(_output.Screenshots, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Execute_Attach_WithoutTimeout_PassesZero()
    {
        _session.IsConnected = true;
        await _executor.ExecuteAsync("attach Calculator.*");
        var call = _session.Calls.First(c => c.Method == "Attach");
        Assert.That(call.Args[1], Is.EqualTo(0u));
    }

    // ── close ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Close_NoApp_WritesError()
    {
        _session.IsConnected = true;
        await _executor.ExecuteAsync("close");
        Assert.That(_output.ErrorMessages[0], Does.Contain("No application"));
    }

    [Test]
    public async Task Execute_Close_CallsCloseApp()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        await _executor.ExecuteAsync("close");
        Assert.That(_session.Calls[0].Method, Is.EqualTo("CloseApp"));
        Assert.That(_output.InfoMessages[0], Does.Contain("closed"));
        Assert.That(_output.ClearScreenshotCount, Is.EqualTo(1));
    }

    // ── detach ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Detach_NoApp_WritesError()
    {
        _session.IsConnected = true;
        await _executor.ExecuteAsync("detach");
        Assert.That(_output.ErrorMessages[0], Does.Contain("No application"));
    }

    [Test]
    public async Task Execute_Detach_CallsDetachApp()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        await _executor.ExecuteAsync("detach");
        Assert.That(_session.Calls[0].Method, Is.EqualTo("DetachApp"));
        Assert.That(_output.InfoMessages[0], Does.Contain("Detached"));
        Assert.That(_output.ClearScreenshotCount, Is.EqualTo(1));
    }

    // ── disconnect ──────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Disconnect_NotConnected_WritesError()
    {
        await _executor.ExecuteAsync("disconnect");
        Assert.That(_output.ErrorMessages[0], Does.Contain("Not connected"));
    }

    [Test]
    public async Task Execute_Disconnect_CallsDisconnect()
    {
        _session.IsConnected = true;
        await _executor.ExecuteAsync("disconnect");
        Assert.That(_session.Calls[0].Method, Is.EqualTo("Disconnect"));
        Assert.That(_output.InfoMessages[0], Does.Contain("Disconnected"));
        Assert.That(_output.ClearScreenshotCount, Is.EqualTo(1));
    }

    // ── wscreenshot ─────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Wscreenshot_NoApp_WritesError()
    {
        _session.IsConnected = true;
        await _executor.ExecuteAsync("wscreenshot");
        Assert.That(_output.ErrorMessages[0], Does.Contain("No application"));
    }

    [Test]
    public async Task Execute_Wscreenshot_ShowsScreenshot()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        await _executor.ExecuteAsync("wscreenshot");
        Assert.That(_output.Screenshots, Has.Count.EqualTo(1));
        Assert.That(_output.Screenshots[0].Highlight, Is.Null);
    }

    // ── locate ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Locate_NoApp_WritesError()
    {
        _session.IsConnected = true;
        await _executor.ExecuteAsync("locate [name=OK]");
        Assert.That(_output.ErrorMessages[0], Does.Contain("No application"));
    }

    [Test]
    public async Task Execute_Locate_CallsLocate_ShowsHighlight()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        await _executor.ExecuteAsync("locate [name=OK]");
        Assert.That(_session.Calls.Any(c => c.Method == "Locate"), Is.True);
        Assert.That(_output.InfoMessages[0], Does.Contain("Located"));
        // Should show screenshot with highlight
        Assert.That(_output.Screenshots, Has.Count.EqualTo(1));
        Assert.That(_output.Screenshots[0].Highlight, Is.Not.Null);
    }

    [Test]
    public async Task Execute_Locate_ChainedSelectors_PassesAllSelectors()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        await _executor.ExecuteAsync("locate type=Panel >> [name=OK]");
        var locateCall = _session.Calls.First(c => c.Method == "Locate");
        var selectors = locateCall.Args[0] as string[];
        Assert.That(selectors, Is.Not.Null);
        Assert.That(selectors!, Has.Length.EqualTo(2));
    }

    // ── unselect ────────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Unselect_CallsUnselect()
    {
        await _executor.ExecuteAsync("unselect");
        Assert.That(_session.Calls[0].Method, Is.EqualTo("Unselect"));
        Assert.That(_output.ClearHighlightCount, Is.EqualTo(1));
        Assert.That(_output.InfoMessages[0], Does.Contain("unselected"));
    }

    // ── attribute ───────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Attribute_NoElement_WritesError()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        await _executor.ExecuteAsync("attribute name");
        Assert.That(_output.ErrorMessages[0], Does.Contain("No element selected"));
    }

    [Test]
    public async Task Execute_Attribute_ReturnsValue()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        _session.GetAttributeResult = "okBtn";
        await _executor.ExecuteAsync("attribute automationid");
        Assert.That(_session.Calls[0].Method, Is.EqualTo("GetAttribute"));
        Assert.That(_output.InfoMessages[0], Does.Contain("okBtn"));
    }

    // ── click ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Click_NoElement_WritesError()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        await _executor.ExecuteAsync("click");
        Assert.That(_output.ErrorMessages[0], Does.Contain("No element selected"));
    }

    [Test]
    public async Task Execute_Click_CallsClick_RefreshesScreenshot()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        await _executor.ExecuteAsync("click");
        Assert.That(_session.Calls.Any(c => c.Method == "Click"), Is.True);
        Assert.That(_output.InfoMessages[0], Does.Contain("Clicked"));
        Assert.That(_output.Screenshots, Has.Count.EqualTo(1));
    }

    // ── doubleclick ─────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_DoubleClick_CallsDoubleClick()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        await _executor.ExecuteAsync("doubleclick");
        Assert.That(_session.Calls.Any(c => c.Method == "DoubleClick"), Is.True);
        Assert.That(_output.InfoMessages[0], Does.Contain("Double-clicked"));
    }

    // ── rightclick ───────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_RightClick_CallsRightClick()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        await _executor.ExecuteAsync("rightclick");
        Assert.That(_session.Calls.Any(c => c.Method == "RightClick"), Is.True);
        Assert.That(_output.InfoMessages[0], Does.Contain("Right-clicked"));
    }

    // ── type ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Type_NoElement_WritesError()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        await _executor.ExecuteAsync("type Hello");
        Assert.That(_output.ErrorMessages[0], Does.Contain("No element selected"));
    }

    [Test]
    public async Task Execute_Type_CallsType()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        await _executor.ExecuteAsync("type Hello World");
        var typeCall = _session.Calls.First(c => c.Method == "Type");
        Assert.That(typeCall.Args[0], Is.EqualTo("Hello World"));
        Assert.That(_output.InfoMessages[0], Does.Contain("Typed"));
    }

    // ── focus ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Focus_CallsFocus()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        await _executor.ExecuteAsync("focus");
        Assert.That(_session.Calls[0].Method, Is.EqualTo("Focus"));
        Assert.That(_output.InfoMessages[0], Does.Contain("Focused"));
    }

    // ── text ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Text_ReturnsText()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        _session.GetTextResult = "Display Value";
        await _executor.ExecuteAsync("text");
        Assert.That(_session.Calls[0].Method, Is.EqualTo("GetText"));
        Assert.That(_output.InfoMessages[0], Is.EqualTo("Display Value"));
    }

    // ── screenshot ──────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Screenshot_ShowsElementScreenshot()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        await _executor.ExecuteAsync("screenshot");
        Assert.That(_session.Calls.Any(c => c.Method == "ScreenshotElement"), Is.True);
        Assert.That(_output.Screenshots, Has.Count.EqualTo(1));
        Assert.That(_output.InfoMessages[0], Does.Contain("screenshot captured"));
    }

    // ── exception handling ──────────────────────────────────────────────────

    [Test]
    public async Task Execute_SessionThrows_WritesError()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        _session.ThrowOnNext = new InvalidOperationException("Connection lost");
        await _executor.ExecuteAsync("click");
        Assert.That(_output.ErrorMessages[0], Is.EqualTo("Connection lost"));
    }

    // ── precondition checks ─────────────────────────────────────────────────

    [Test]
    public async Task Execute_Launch_NotConnected_RequiresConnection()
    {
        _session.IsConnected = false;
        await _executor.ExecuteAsync("launch calc.exe");
        Assert.That(_output.ErrorMessages[0], Does.Contain("Not connected"));
    }

    [Test]
    public async Task Execute_Attach_NotConnected_RequiresConnection()
    {
        _session.IsConnected = false;
        await _executor.ExecuteAsync("attach Calc.*");
        Assert.That(_output.ErrorMessages[0], Does.Contain("Not connected"));
    }

    [Test]
    public async Task Execute_Wscreenshot_NotConnected_RequiresConnection()
    {
        _session.IsConnected = false;
        await _executor.ExecuteAsync("wscreenshot");
        Assert.That(_output.ErrorMessages[0], Does.Contain("Not connected"));
    }

    [Test]
    public async Task Execute_Focus_NoElement_RequiresElement()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = false;
        await _executor.ExecuteAsync("focus");
        Assert.That(_output.ErrorMessages[0], Does.Contain("No element selected"));
    }

    [Test]
    public async Task Execute_Text_NoElement_RequiresElement()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = false;
        await _executor.ExecuteAsync("text");
        Assert.That(_output.ErrorMessages[0], Does.Contain("No element selected"));
    }

    [Test]
    public async Task Execute_Screenshot_NoElement_RequiresElement()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = false;
        await _executor.ExecuteAsync("screenshot");
        Assert.That(_output.ErrorMessages[0], Does.Contain("No element selected"));
    }

    [Test]
    public async Task Execute_DoubleClick_NoElement_RequiresElement()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = false;
        await _executor.ExecuteAsync("doubleclick");
        Assert.That(_output.ErrorMessages[0], Does.Contain("No element selected"));
    }

    [Test]
    public async Task Execute_RightClick_NoElement_RequiresElement()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = false;
        await _executor.ExecuteAsync("rightclick");
        Assert.That(_output.ErrorMessages[0], Does.Contain("No element selected"));
    }

    // ── exit / quit ─────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Exit_RequestsExit()
    {
        await _executor.ExecuteAsync("exit");
        Assert.That(_output.RequestExitCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Execute_Quit_RequestsExit()
    {
        await _executor.ExecuteAsync("quit");
        Assert.That(_output.RequestExitCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Execute_Exit_DisconnectsIfConnected()
    {
        _session.IsConnected = true;
        await _executor.ExecuteAsync("exit");
        Assert.That(_session.Calls[0].Method, Is.EqualTo("Disconnect"));
        Assert.That(_output.RequestExitCount, Is.EqualTo(1));
    }

    // ── highlight coordinate translation ────────────────────────────────────

    [Test]
    public async Task Execute_Locate_HighlightIsWindowRelative()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.WindowBoundingRectResult = new BoundingRect(100, 200, 800, 600);
        _session.ElementBoundingRectResult = new BoundingRect(150, 250, 50, 30);

        await _executor.ExecuteAsync("locate [name=OK]");

        var highlight = _output.Screenshots[0].Highlight!;
        Assert.That(highlight.X, Is.EqualTo(50));  // 150 - 100
        Assert.That(highlight.Y, Is.EqualTo(50));  // 250 - 200
        Assert.That(highlight.Width, Is.EqualTo(50));
        Assert.That(highlight.Height, Is.EqualTo(30));
        Assert.That(highlight.WindowWidth, Is.EqualTo(800));
        Assert.That(highlight.WindowHeight, Is.EqualTo(600));
    }
}

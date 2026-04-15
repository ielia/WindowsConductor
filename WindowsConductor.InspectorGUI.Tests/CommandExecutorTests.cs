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
        Assert.That(_session.Calls[0].Args[1], Is.Null);
        Assert.That(_output.InfoMessages[0], Does.Contain("Connected"));
    }

    [Test]
    public async Task Execute_Connect_WithAuthToken_PassesToken()
    {
        await _executor.ExecuteAsync("connect ws://localhost:8765/ my-token");
        Assert.That(_session.Calls[0].Method, Is.EqualTo("Connect"));
        Assert.That(_session.Calls[0].Args[0], Is.EqualTo("ws://localhost:8765/"));
        Assert.That(_session.Calls[0].Args[1], Is.EqualTo("my-token"));
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
        Assert.That(_output.ClearAttributesCount, Is.EqualTo(1));
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
        Assert.That(_output.ClearAttributesCount, Is.EqualTo(1));
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
        Assert.That(_output.ClearAttributesCount, Is.EqualTo(1));
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
        Assert.That(_session.Calls.Any(c => c.Method == "LocateAll"), Is.True);
        Assert.That(_output.InfoMessages[0], Does.Contain("Located"));
        // Should show screenshot with highlight
        Assert.That(_output.Screenshots, Has.Count.EqualTo(1));
        Assert.That(_output.Screenshots[0].Highlight, Is.Not.Null);
        Assert.That(_output.AttributesSets, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Execute_Locate_ChainedSelectors_PassesAllSelectors()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        await _executor.ExecuteAsync("locate type=Panel >> [name=OK]");
        var locateCall = _session.Calls.First(c => c.Method == "LocateAll");
        var selectors = locateCall.Args[0] as string[];
        Assert.That(selectors, Is.Not.Null);
        Assert.That(selectors!, Has.Length.EqualTo(2));
    }

    [Test]
    public async Task Execute_Locate_WithElementSelected_RelativeSelector_UsesLocateFromElement()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        await _executor.ExecuteAsync("locate ./type=Button");
        Assert.That(_session.Calls.Any(c => c.Method == "LocateAllFromElement"), Is.True);
    }

    [Test]
    public async Task Execute_Locate_WithElementSelected_AbsoluteSelector_UsesLocate()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        await _executor.ExecuteAsync("locate type=Button");
        Assert.That(_session.Calls.Any(c => c.Method == "LocateAll"), Is.True);
        Assert.That(_session.Calls.All(c => c.Method != "LocateAllFromElement"), Is.True);
    }

    [Test]
    public async Task Execute_Locate_WithElementSelected_RelativeSelector_AppendsSelectorChain()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        await _executor.ExecuteAsync("locate type=Panel");
        _output.AttributesSets.Clear();

        await _executor.ExecuteAsync("locate ./[name=OK]");
        Assert.That(_output.AttributesSets[0].LocatorChain, Is.EqualTo("type=Panel >> ./[name=OK]"));
    }

    [Test]
    public async Task Execute_Locate_RootThenDescendantSequence_CombinesCorrectly()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        await _executor.ExecuteAsync("locate /");
        await _executor.ExecuteAsync("locate //button[@automationid=num3Button]");
        await _executor.ExecuteAsync("locate ./..");
        _output.AttributesSets.Clear();

        await _executor.ExecuteAsync("locate //button[@automationid=num2Button]");
        Assert.That(_output.AttributesSets[0].LocatorChain,
            Is.EqualTo("//button[@automationid=num3Button]/..//button[@automationid=num2Button]"));
    }

    [Test]
    public async Task Execute_Locate_DescendantXPathOnXPath_CombinesWithoutSlash()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        await _executor.ExecuteAsync("locate //button[@automationid=num3Button]");
        _output.AttributesSets.Clear();

        await _executor.ExecuteAsync("locate //button[@automationid=num2Button]");
        Assert.That(_output.AttributesSets[0].LocatorChain,
            Is.EqualTo("//button[@automationid=num3Button]//button[@automationid=num2Button]"));
        Assert.That(_session.Calls.Any(c => c.Method == "LocateAllFromElement"), Is.True);
    }

    [Test]
    public async Task Execute_Locate_XPathRelativeOnXPath_CombinesWithSlash()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        await _executor.ExecuteAsync("locate //button[@automationid=num3Button]");
        _output.AttributesSets.Clear();

        await _executor.ExecuteAsync("locate ../Window");
        Assert.That(_output.AttributesSets[0].LocatorChain,
            Is.EqualTo("//button[@automationid=num3Button]/../Window"));
    }

    [Test]
    public async Task Execute_Parent_OnXPathElement_AppendsSlashDotDot()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        await _executor.ExecuteAsync("locate //button[@automationid=num3Button]");
        _output.AttributesSets.Clear();

        await _executor.ExecuteAsync("parent");
        Assert.That(_output.AttributesSets[0].LocatorChain,
            Is.EqualTo("//button[@automationid=num3Button]/.."));
    }

    [Test]
    public async Task Execute_Parent_OnNonXPathElement_AppendsWithArrows()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        await _executor.ExecuteAsync("locate type=Panel");
        _output.AttributesSets.Clear();

        await _executor.ExecuteAsync("parent");
        Assert.That(_output.AttributesSets[0].LocatorChain, Is.EqualTo("type=Panel >> .."));
    }

    [Test]
    public async Task Execute_Locate_WithoutElementSelected_UsesLocate()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = false;
        await _executor.ExecuteAsync("locate type=Button");
        Assert.That(_session.Calls.Any(c => c.Method == "LocateAll"), Is.True);
        Assert.That(_session.Calls.All(c => c.Method != "LocateAllFromElement"), Is.True);
    }

    // ── match navigation ──────────────────────────────────────────────────

    [Test]
    public async Task Locate_MultipleMatches_ShowsCount()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.LocateAllResult = 5;
        await _executor.ExecuteAsync("locate //Button");
        Assert.That(_output.InfoMessages[0], Does.Contain("5 elements"));
        Assert.That(_output.MatchNavigationUpdates.Last(), Is.EqualTo((0, 5)));
    }

    [Test]
    public async Task Locate_SingleMatch_NoNavigation()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.LocateAllResult = 1;
        await _executor.ExecuteAsync("locate //Button");
        Assert.That(_output.InfoMessages[0], Does.Contain("1 element"));
        Assert.That(_output.MatchNavigationUpdates.Last(), Is.EqualTo((0, 1)));
    }

    [Test]
    public async Task NavigateMatch_Forward_CyclesIndex()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.LocateAllResult = 3;
        await _executor.ExecuteAsync("locate //Button");
        _output.AttributesSets.Clear();

        await _executor.NavigateMatchAsync(1);
        Assert.That(_session.Calls.Any(c => c.Method == "SelectMatch"), Is.True);
        var selectCall = _session.Calls.Last(c => c.Method == "SelectMatch");
        Assert.That(selectCall.Args[0], Is.EqualTo(1));
        Assert.That(_output.MatchNavigationUpdates.Last(), Is.EqualTo((1, 3)));
        Assert.That(_output.AttributesSets[0].LocatorChain, Does.EndWith("[2]"));
    }

    [Test]
    public async Task NavigateMatch_Backward_WrapsAround()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.LocateAllResult = 3;
        await _executor.ExecuteAsync("locate //Button");

        await _executor.NavigateMatchAsync(-1);
        var selectCall = _session.Calls.Last(c => c.Method == "SelectMatch");
        Assert.That(selectCall.Args[0], Is.EqualTo(2));
        Assert.That(_output.MatchNavigationUpdates.Last(), Is.EqualTo((2, 3)));
    }

    [Test]
    public async Task Locate_MultipleMatches_FirstMatch_NoIndexInChain()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.LocateAllResult = 4;
        await _executor.ExecuteAsync("locate //Button");
        Assert.That(_output.AttributesSets[0].LocatorChain, Is.EqualTo("//Button"));
    }

    [Test]
    public async Task Locate_MultipleMatches_NavigatedMatch_ShowsIndexInChain()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.LocateAllResult = 4;
        await _executor.ExecuteAsync("locate //Button");
        _output.AttributesSets.Clear();

        await _executor.NavigateMatchAsync(1);
        Assert.That(_output.AttributesSets[0].LocatorChain, Is.EqualTo("//Button[2]"));
    }

    [Test]
    public async Task Locate_NavigatedMatch_ThenParent_BakesIndexIntoChain()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        _session.LocateAllResult = 5;
        await _executor.ExecuteAsync("locate //Button");
        await _executor.NavigateMatchAsync(1);
        await _executor.NavigateMatchAsync(1);
        await _executor.NavigateMatchAsync(1);
        await _executor.NavigateMatchAsync(1);
        _output.AttributesSets.Clear();

        await _executor.ExecuteAsync("locate ..");
        Assert.That(_output.AttributesSets[0].LocatorChain, Is.EqualTo("//Button[5]/.."));
    }

    [Test]
    public async Task Parent_ResetsMatchNavigation()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        _session.LocateAllResult = 3;
        await _executor.ExecuteAsync("locate //Button");

        await _executor.ExecuteAsync("parent");
        Assert.That(_output.MatchNavigationUpdates.Last(), Is.EqualTo((0, 0)));
    }

    // ── parent ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Parent_NoElement_WritesError()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = false;
        await _executor.ExecuteAsync("parent");
        Assert.That(_output.ErrorMessages[0], Does.Contain("No element selected"));
    }

    [Test]
    public async Task Execute_Parent_CallsParent_ShowsHighlightAndAttributes()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        _session.ParentResult = "parent-el-1";
        await _executor.ExecuteAsync("parent");
        Assert.That(_session.Calls.Any(c => c.Method == "Parent"), Is.True);
        Assert.That(_output.InfoMessages[0], Does.Contain("parent-el-1"));
        Assert.That(_output.Screenshots, Has.Count.EqualTo(1));
        Assert.That(_output.Screenshots[0].Highlight, Is.Not.Null);
        Assert.That(_output.AttributesSets, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Execute_Parent_AppendsDoubleDotToLocatorChain()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        await _executor.ExecuteAsync("locate [name=OK]");
        _output.AttributesSets.Clear();

        await _executor.ExecuteAsync("parent");
        Assert.That(_output.AttributesSets[0].LocatorChain, Is.EqualTo("[name=OK] >> .."));
    }

    [Test]
    public async Task Execute_Parent_NoLocatorChain_ShowsDoubleDotOnly()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        await _executor.ExecuteAsync("parent");
        Assert.That(_output.AttributesSets[0].LocatorChain, Is.EqualTo(".."));
    }

    // ── unselect ────────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Unselect_CallsUnselect()
    {
        await _executor.ExecuteAsync("unselect");
        Assert.That(_session.Calls[0].Method, Is.EqualTo("Unselect"));
        Assert.That(_output.ClearHighlightCount, Is.EqualTo(1));
        Assert.That(_output.ClearAttributesCount, Is.EqualTo(1));
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

    // ── foreground ──────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Foreground_CallsSetForeground()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        await _executor.ExecuteAsync("foreground");
        Assert.That(_session.Calls[0].Method, Is.EqualTo("SetForeground"));
        Assert.That(_output.InfoMessages[0], Does.Contain("foreground"));
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

    // ── help ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Execute_Help_ShowsAllCommands()
    {
        await _executor.ExecuteAsync("help");
        Assert.That(_output.InfoMessages, Has.Count.EqualTo(1));
        Assert.That(_output.InfoMessages[0], Does.Contain("Available commands:"));
        Assert.That(_output.InfoMessages[0], Does.Contain("connect"));
        Assert.That(_output.InfoMessages[0], Does.Contain("launch"));
        Assert.That(_output.InfoMessages[0], Does.Contain("exit"));
    }

    [Test]
    public async Task Execute_Help_SpecificCommand_ShowsCommandHelp()
    {
        await _executor.ExecuteAsync("help connect");
        Assert.That(_output.InfoMessages, Has.Count.EqualTo(1));
        Assert.That(_output.InfoMessages[0], Does.Contain("connect"));
        Assert.That(_output.InfoMessages[0], Does.Not.Contain("launch"));
    }

    [Test]
    public async Task Execute_Help_UnknownCommand_ShowsError()
    {
        await _executor.ExecuteAsync("help bogus");
        Assert.That(_output.InfoMessages[0], Does.Contain("Unknown command: 'bogus'"));
    }

    // ── attributes panel ───────────────────────────────────────────────────

    [Test]
    public async Task Execute_Locate_ShowsAttributesWithLocatorChain()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.GetAttributesResult = new Dictionary<string, object?> { ["name"] = "OK" };
        await _executor.ExecuteAsync("locate [name=OK]");
        Assert.That(_output.AttributesSets, Has.Count.EqualTo(1));
        Assert.That(_output.AttributesSets[0].LocatorChain, Is.EqualTo("[name=OK]"));
        Assert.That(_output.AttributesSets[0].Attributes["name"], Is.EqualTo("OK"));
    }

    [Test]
    public async Task Execute_Locate_ChainedSelectors_ShowsFullChain()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        await _executor.ExecuteAsync("locate type=Panel >> [name=OK]");
        Assert.That(_output.AttributesSets[0].LocatorChain, Is.EqualTo("type=Panel >> [name=OK]"));
    }

    [Test]
    public async Task Execute_Click_RefreshesAttributes()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        await _executor.ExecuteAsync("click");
        Assert.That(_output.AttributesSets, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Execute_DoubleClick_RefreshesAttributes()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        await _executor.ExecuteAsync("doubleclick");
        Assert.That(_output.AttributesSets, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Execute_RightClick_RefreshesAttributes()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        await _executor.ExecuteAsync("rightclick");
        Assert.That(_output.AttributesSets, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Execute_Type_RefreshesAttributes()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        await _executor.ExecuteAsync("type Hello");
        Assert.That(_output.AttributesSets, Has.Count.EqualTo(1));
    }

    // ── refresh ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Refresh_WithElement_RefreshesScreenshotAndAttributes()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        await _executor.RefreshAsync();
        Assert.That(_output.Screenshots, Has.Count.EqualTo(1));
        Assert.That(_output.Screenshots[0].Highlight, Is.Not.Null);
        Assert.That(_output.AttributesSets, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Refresh_NoApp_DoesNothing()
    {
        _session.IsConnected = true;
        await _executor.RefreshAsync();
        Assert.That(_output.Screenshots, Has.Count.EqualTo(0));
        Assert.That(_output.AttributesSets, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task Refresh_AppButNoElement_ShowsScreenshotOnly()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        await _executor.RefreshAsync();
        Assert.That(_output.Screenshots, Has.Count.EqualTo(1));
        Assert.That(_output.AttributesSets, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task Refresh_PreservesLocatorChain()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.HasSelectedElement = true;
        await _executor.ExecuteAsync("locate type=Panel >> [name=OK]");
        _output.AttributesSets.Clear();
        _output.Screenshots.Clear();

        await _executor.RefreshAsync();
        Assert.That(_output.AttributesSets[0].LocatorChain, Is.EqualTo("type=Panel >> [name=OK]"));
    }

    // ── highlight coordinate translation ────────────────────────────────────

    [Test]
    public async Task Execute_Locate_HighlightIsWindowRelative()
    {
        _session.IsConnected = true;
        _session.HasApp = true;
        _session.ElementWindowBoundingRectResult = new BoundingRect(100, 200, 800, 600);
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

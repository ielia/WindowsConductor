using WindowsConductor.Client;
using WindowsConductor.InspectorGUI;

namespace WindowsConductor.InspectorGUI.Tests;

[TestFixture]
[Category("Unit")]
public class CommandParserTests
{
    // ── Empty / invalid input ───────────────────────────────────────────────

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void Parse_EmptyOrWhitespace_Throws(string? input)
    {
        Assert.Throws<ArgumentException>(() => CommandParser.Parse(input!));
    }

    [Test]
    public void Parse_UnknownCommand_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => CommandParser.Parse("bogus"));
        Assert.That(ex!.Message, Does.Contain("Unknown command"));
    }

    // ── connect ─────────────────────────────────────────────────────────────

    [Test]
    public void Parse_Connect_ReturnsConnectCommand()
    {
        var cmd = CommandParser.Parse("connect ws://localhost:8765/");
        Assert.That(cmd, Is.InstanceOf<ConnectCommand>());
        Assert.That(((ConnectCommand)cmd).Url, Is.EqualTo("ws://localhost:8765/"));
    }

    [Test]
    public void Parse_Connect_NoUrl_UsesDefault()
    {
        var cmd = (ConnectCommand)CommandParser.Parse("connect");
        Assert.That(cmd.Url, Is.EqualTo(WcDefaults.WebSocketUrl));
    }

    [Test]
    public void Parse_Connect_CaseInsensitive()
    {
        var cmd = CommandParser.Parse("CONNECT ws://localhost/");
        Assert.That(cmd, Is.InstanceOf<ConnectCommand>());
    }

    // ── launch ──────────────────────────────────────────────────────────────

    [Test]
    public void Parse_Launch_PathOnly()
    {
        var cmd = (LaunchCommand)CommandParser.Parse("launch calc.exe");
        Assert.That(cmd.Path, Is.EqualTo("calc.exe"));
        Assert.That(cmd.Args, Is.Empty);
        Assert.That(cmd.DetachedTitleRegex, Is.Null);
        Assert.That(cmd.MainWindowTimeout, Is.Null);
    }

    [Test]
    public void Parse_Launch_PathAndTimeout()
    {
        var cmd = (LaunchCommand)CommandParser.Parse("launch \"edge.exe\" 2000");
        Assert.That(cmd.Path, Is.EqualTo("edge.exe"));
        Assert.That(cmd.Args, Is.Empty);
        Assert.That(cmd.DetachedTitleRegex, Is.Null);
        Assert.That(cmd.MainWindowTimeout, Is.EqualTo(2000u));
    }

    [Test]
    public void Parse_Launch_WithDetachedTitleRegexAndTimeout()
    {
        var cmd = (LaunchCommand)CommandParser.Parse("launch calc.exe Calculator.* 3000");
        Assert.That(cmd.Path, Is.EqualTo("calc.exe"));
        Assert.That(cmd.Args, Is.Empty);
        Assert.That(cmd.DetachedTitleRegex, Is.EqualTo("Calculator.*"));
        Assert.That(cmd.MainWindowTimeout, Is.EqualTo(3000u));
    }

    [Test]
    public void Parse_Launch_WithArgsArray()
    {
        var cmd = (LaunchCommand)CommandParser.Parse("launch \"edge.exe\" [\"https://www.google.com\"]");
        Assert.That(cmd.Path, Is.EqualTo("edge.exe"));
        Assert.That(cmd.Args, Is.EqualTo(new[] { "https://www.google.com" }));
        Assert.That(cmd.DetachedTitleRegex, Is.Null);
        Assert.That(cmd.MainWindowTimeout, Is.Null);
    }

    [Test]
    public void Parse_Launch_WithArgsAndDetachedTitleRegexAndTimeout()
    {
        var cmd = (LaunchCommand)CommandParser.Parse(
            "launch \"edge.exe\" [\"https://www.google.com\", \"--new-window\"] \"Google Search\" 2000");
        Assert.That(cmd.Path, Is.EqualTo("edge.exe"));
        Assert.That(cmd.Args, Is.EqualTo(new[] { "https://www.google.com", "--new-window" }));
        Assert.That(cmd.DetachedTitleRegex, Is.EqualTo("Google Search"));
        Assert.That(cmd.MainWindowTimeout, Is.EqualTo(2000u));
    }

    [Test]
    public void Parse_Launch_WithArgsAndDetachedTitleRegex()
    {
        var cmd = (LaunchCommand)CommandParser.Parse(
            "launch notepad.exe [\"--flag\", \"file.txt\"] Title.*");
        Assert.That(cmd.Path, Is.EqualTo("notepad.exe"));
        Assert.That(cmd.Args, Is.EqualTo(new[] { "--flag", "file.txt" }));
        Assert.That(cmd.DetachedTitleRegex, Is.EqualTo("Title.*"));
        Assert.That(cmd.MainWindowTimeout, Is.Null);
    }

    [Test]
    public void Parse_Launch_MissingPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => CommandParser.Parse("launch"));
    }

    // ── attach ──────────────────────────────────────────────────────────────

    [Test]
    public void Parse_Attach_RegexOnly()
    {
        var cmd = (AttachCommand)CommandParser.Parse("attach Calculator.*");
        Assert.That(cmd.MainWindowTitleRegex, Is.EqualTo("Calculator.*"));
        Assert.That(cmd.MainWindowTimeout, Is.Null);
    }

    [Test]
    public void Parse_Attach_WithTimeout()
    {
        var cmd = (AttachCommand)CommandParser.Parse("attach Calculator.* 2000");
        Assert.That(cmd.MainWindowTitleRegex, Is.EqualTo("Calculator.*"));
        Assert.That(cmd.MainWindowTimeout, Is.EqualTo(2000u));
    }

    [Test]
    public void Parse_Attach_MissingRegex_Throws()
    {
        Assert.Throws<ArgumentException>(() => CommandParser.Parse("attach"));
    }

    // ── close ───────────────────────────────────────────────────────────────

    [Test]
    public void Parse_Close_ReturnsCloseCommand()
    {
        Assert.That(CommandParser.Parse("close"), Is.InstanceOf<CloseCommand>());
    }

    // ── detach ──────────────────────────────────────────────────────────────

    [Test]
    public void Parse_Detach_ReturnsDetachCommand()
    {
        Assert.That(CommandParser.Parse("detach"), Is.InstanceOf<DetachCommand>());
    }

    // ── disconnect ──────────────────────────────────────────────────────────

    [Test]
    public void Parse_Disconnect_ReturnsDisconnectCommand()
    {
        Assert.That(CommandParser.Parse("disconnect"), Is.InstanceOf<DisconnectCommand>());
    }

    // ── wscreenshot ─────────────────────────────────────────────────────────

    [Test]
    public void Parse_Wscreenshot_ReturnsWindowScreenshotCommand()
    {
        Assert.That(CommandParser.Parse("wscreenshot"), Is.InstanceOf<WindowScreenshotCommand>());
    }

    // ── locate ──────────────────────────────────────────────────────────────

    [Test]
    public void Parse_Locate_SingleSelector()
    {
        var cmd = (LocateCommand)CommandParser.Parse("locate [name=OK]");
        Assert.That(cmd.Selectors, Is.EqualTo(new[] { "[name=OK]" }));
    }

    [Test]
    public void Parse_Locate_ChainedSelectors()
    {
        var cmd = (LocateCommand)CommandParser.Parse("locate [name=Panel] >> [name=OK]");
        Assert.That(cmd.Selectors, Has.Length.EqualTo(2));
        Assert.That(cmd.Selectors[0], Is.EqualTo("[name=Panel]"));
        Assert.That(cmd.Selectors[1], Is.EqualTo("[name=OK]"));
    }

    [Test]
    public void Parse_Locate_ThreeChainedSelectors()
    {
        var cmd = (LocateCommand)CommandParser.Parse("locate type=Window >> type=Panel >> [name=OK]");
        Assert.That(cmd.Selectors, Has.Length.EqualTo(3));
    }

    [Test]
    public void Parse_Locate_MissingSelector_Throws()
    {
        Assert.Throws<ArgumentException>(() => CommandParser.Parse("locate"));
    }

    // ── parent ─────────────────────────────────────────────────────────────

    [Test]
    public void Parse_Parent_ReturnsParentCommand()
    {
        Assert.That(CommandParser.Parse("parent"), Is.InstanceOf<ParentCommand>());
    }

    // ── unselect ────────────────────────────────────────────────────────────

    [Test]
    public void Parse_Unselect_ReturnsUnselectCommand()
    {
        Assert.That(CommandParser.Parse("unselect"), Is.InstanceOf<UnselectCommand>());
    }

    // ── attribute ───────────────────────────────────────────────────────────

    [Test]
    public void Parse_Attribute_ReturnsAttributeCommand()
    {
        var cmd = (AttributeCommand)CommandParser.Parse("attribute automationid");
        Assert.That(cmd.AttributeName, Is.EqualTo("automationid"));
    }

    [Test]
    public void Parse_Attribute_MissingName_Throws()
    {
        Assert.Throws<ArgumentException>(() => CommandParser.Parse("attribute"));
    }

    // ── click ───────────────────────────────────────────────────────────────

    [Test]
    public void Parse_Click_ReturnsClickCommand()
    {
        Assert.That(CommandParser.Parse("click"), Is.InstanceOf<ClickCommand>());
    }

    // ── doubleclick ─────────────────────────────────────────────────────────

    [Test]
    public void Parse_DoubleClick_ReturnsDoubleClickCommand()
    {
        Assert.That(CommandParser.Parse("doubleclick"), Is.InstanceOf<DoubleClickCommand>());
    }

    // ── rightclick ───────────────────────────────────────────────────────────

    [Test]
    public void Parse_RightClick_ReturnsRightClickCommand()
    {
        Assert.That(CommandParser.Parse("rightclick"), Is.InstanceOf<RightClickCommand>());
    }

    // ── type ────────────────────────────────────────────────────────────────

    [Test]
    public void Parse_Type_ReturnsTypeCommand()
    {
        var cmd = (TypeCommand)CommandParser.Parse("type Hello World");
        Assert.That(cmd.Text, Is.EqualTo("Hello World"));
    }

    [Test]
    public void Parse_Type_QuotedText()
    {
        var cmd = (TypeCommand)CommandParser.Parse("type \"Hello World\"");
        Assert.That(cmd.Text, Is.EqualTo("Hello World"));
    }

    [Test]
    public void Parse_Type_MissingText_Throws()
    {
        Assert.Throws<ArgumentException>(() => CommandParser.Parse("type"));
    }

    [Test]
    public void Parse_Type_NoModifiers_DefaultsToNone()
    {
        var cmd = (TypeCommand)CommandParser.Parse("type hello");
        Assert.That(cmd.Modifiers, Is.EqualTo(KeyModifiers.None));
    }

    [Test]
    public void Parse_Type_WithModifiers_ParsesBitmask()
    {
        var cmd = (TypeCommand)CommandParser.Parse("type \"a\" [ctrl alt]");
        Assert.That(cmd.Text, Is.EqualTo("a"));
        Assert.That(cmd.Modifiers, Is.EqualTo(KeyModifiers.Ctrl | KeyModifiers.Alt));
    }

    [Test]
    public void Parse_Type_AllModifiers()
    {
        var cmd = (TypeCommand)CommandParser.Parse("type \"x\" [ctrl alt shift meta]");
        Assert.That(cmd.Modifiers, Is.EqualTo(KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta));
    }

    [Test]
    public void Parse_Type_ModifiersCaseInsensitive()
    {
        var cmd = (TypeCommand)CommandParser.Parse("type \"a\" [Ctrl SHIFT]");
        Assert.That(cmd.Modifiers, Is.EqualTo(KeyModifiers.Ctrl | KeyModifiers.Shift));
    }

    [Test]
    public void Parse_Type_UnknownModifier_Throws()
    {
        Assert.Throws<ArgumentException>(() => CommandParser.Parse("type \"a\" [ctrl bogus]"));
    }

    [Test]
    public void Parse_Type_EmptyModifiers_Throws()
    {
        Assert.Throws<ArgumentException>(() => CommandParser.Parse("type \"a\" []"));
    }

    // ── focus ───────────────────────────────────────────────────────────────

    [Test]
    public void Parse_Focus_ReturnsFocusCommand()
    {
        Assert.That(CommandParser.Parse("focus"), Is.InstanceOf<FocusCommand>());
    }

    // ── text ────────────────────────────────────────────────────────────────

    [Test]
    public void Parse_Text_ReturnsTextCommand()
    {
        Assert.That(CommandParser.Parse("text"), Is.InstanceOf<TextCommand>());
    }

    // ── screenshot ──────────────────────────────────────────────────────────

    [Test]
    public void Parse_Screenshot_ReturnsScreenshotCommand()
    {
        Assert.That(CommandParser.Parse("screenshot"), Is.InstanceOf<ScreenshotCommand>());
    }

    // ── exit / quit ──────────────────────────────────────────────────────────

    [Test]
    public void Parse_Exit_ReturnsExitCommand()
    {
        Assert.That(CommandParser.Parse("exit"), Is.InstanceOf<ExitCommand>());
    }

    [Test]
    public void Parse_Quit_ReturnsExitCommand()
    {
        Assert.That(CommandParser.Parse("quit"), Is.InstanceOf<ExitCommand>());
    }

    // ── help ──────────────────────────────────────────────────────────────────

    [Test]
    public void Parse_Help_NoArg_ReturnsHelpCommandWithNull()
    {
        var cmd = CommandParser.Parse("help");
        Assert.That(cmd, Is.InstanceOf<HelpCommand>());
        Assert.That(((HelpCommand)cmd).CommandName, Is.Null);
    }

    [Test]
    public void Parse_Help_WithArg_ReturnsHelpCommandWithName()
    {
        var cmd = CommandParser.Parse("help connect");
        Assert.That(cmd, Is.InstanceOf<HelpCommand>());
        Assert.That(((HelpCommand)cmd).CommandName, Is.EqualTo("connect"));
    }

    [Test]
    public void Parse_Help_ArgIsLowerCased()
    {
        var cmd = (HelpCommand)CommandParser.Parse("help Connect");
        Assert.That(cmd.CommandName, Is.EqualTo("connect"));
    }

    // ── Tokenize ────────────────────────────────────────────────────────────

    [Test]
    public void Tokenize_SimpleTokens()
    {
        var tokens = CommandParser.Tokenize("connect ws://localhost/");
        Assert.That(tokens, Is.EqualTo(new[] { "connect", "ws://localhost/" }));
    }

    [Test]
    public void Tokenize_DoubleQuoted()
    {
        var tokens = CommandParser.Tokenize("type \"Hello World\"");
        Assert.That(tokens, Is.EqualTo(new[] { "type", "Hello World" }));
    }

    [Test]
    public void Tokenize_SingleQuoted()
    {
        var tokens = CommandParser.Tokenize("type 'Hello World'");
        Assert.That(tokens, Is.EqualTo(new[] { "type", "Hello World" }));
    }

    [Test]
    public void Tokenize_MixedQuotesAndTokens()
    {
        var tokens = CommandParser.Tokenize("launch app.exe \"my arg\" flag");
        Assert.That(tokens, Is.EqualTo(new[] { "launch", "app.exe", "my arg", "flag" }));
    }

    [Test]
    public void Tokenize_ExtraWhitespace()
    {
        var tokens = CommandParser.Tokenize("  click   ");
        Assert.That(tokens, Is.EqualTo(new[] { "click" }));
    }

    [Test]
    public void Tokenize_EmptyInput()
    {
        var tokens = CommandParser.Tokenize("");
        Assert.That(tokens, Is.Empty);
    }
}

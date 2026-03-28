using WindowsConductor.InspectorGUI;

namespace WindowsConductor.InspectorGUI.Tests;

[TestFixture]
[Category("Unit")]
public class ParsedCommandTests
{
    [Test]
    public void ConnectCommand_StoresUrl()
    {
        var cmd = new ConnectCommand("ws://localhost/");
        Assert.That(cmd.Url, Is.EqualTo("ws://localhost/"));
    }

    [Test]
    public void LaunchCommand_StoresAllFields()
    {
        var cmd = new LaunchCommand("app.exe", ["--flag"], "Title.*", 3000u);
        Assert.That(cmd.Path, Is.EqualTo("app.exe"));
        Assert.That(cmd.Args, Is.EqualTo(new[] { "--flag" }));
        Assert.That(cmd.DetachedTitleRegex, Is.EqualTo("Title.*"));
        Assert.That(cmd.MainWindowTimeout, Is.EqualTo(3000u));
    }

    [Test]
    public void AttachCommand_StoresFields()
    {
        var cmd = new AttachCommand("Calc.*", 2000u);
        Assert.That(cmd.MainWindowTitleRegex, Is.EqualTo("Calc.*"));
        Assert.That(cmd.MainWindowTimeout, Is.EqualTo(2000u));
    }

    [Test]
    public void LocateCommand_StoresSelectors()
    {
        var cmd = new LocateCommand(["[name=OK]", "type=Button"]);
        Assert.That(cmd.Selectors, Has.Length.EqualTo(2));
    }

    [Test]
    public void AttributeCommand_StoresName()
    {
        var cmd = new AttributeCommand("automationid");
        Assert.That(cmd.AttributeName, Is.EqualTo("automationid"));
    }

    [Test]
    public void TypeCommand_StoresText()
    {
        var cmd = new TypeCommand("Hello");
        Assert.That(cmd.Text, Is.EqualTo("Hello"));
    }

    [Test]
    public void HighlightInfo_StoresValues()
    {
        var info = new HighlightInfo(10, 20, 100, 50, 800, 600);
        Assert.That(info.X, Is.EqualTo(10));
        Assert.That(info.Y, Is.EqualTo(20));
        Assert.That(info.Width, Is.EqualTo(100));
        Assert.That(info.Height, Is.EqualTo(50));
        Assert.That(info.WindowWidth, Is.EqualTo(800));
        Assert.That(info.WindowHeight, Is.EqualTo(600));
    }

    // Verify records support equality
    [Test]
    public void Records_SupportEquality()
    {
        Assert.That(new CloseCommand(), Is.EqualTo(new CloseCommand()));
        Assert.That(new ClickCommand(), Is.EqualTo(new ClickCommand()));
        Assert.That(new UnselectCommand(), Is.EqualTo(new UnselectCommand()));
        Assert.That(new FocusCommand(), Is.EqualTo(new FocusCommand()));
        Assert.That(new TextCommand(), Is.EqualTo(new TextCommand()));
        Assert.That(new ScreenshotCommand(), Is.EqualTo(new ScreenshotCommand()));
        Assert.That(new WindowScreenshotCommand(), Is.EqualTo(new WindowScreenshotCommand()));
        Assert.That(new DoubleClickCommand(), Is.EqualTo(new DoubleClickCommand()));
        Assert.That(new RightClickCommand(), Is.EqualTo(new RightClickCommand()));
        Assert.That(new DetachCommand(), Is.EqualTo(new DetachCommand()));
        Assert.That(new DisconnectCommand(), Is.EqualTo(new DisconnectCommand()));
        Assert.That(new ExitCommand(), Is.EqualTo(new ExitCommand()));
        Assert.That(new ParentCommand(), Is.EqualTo(new ParentCommand()));
    }
}

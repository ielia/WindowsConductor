using System.Text.Json;
using NUnit.Framework;
using WindowsConductor.MCP.Tools;

namespace WindowsConductor.MCP.Tests;

[TestFixture]
[Category("Unit")]
public class ElementToolsTests
{
    private ConductorState _state = null!;
    private FakeTransport _transport = null!;
    private ElementTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        _state = new ConductorState();
        _transport = new FakeTransport();
        _tools = new ElementTools(_state);
    }

    [TearDown]
    public async Task TearDown() => await _state.DisposeAsync();

    // ── Not-connected error paths ────────────────────────────────────────────

    [Test]
    public void ClickElement_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.ClickElement("el-1"));
    }

    [Test]
    public void DoubleClickElement_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.DoubleClickElement("el-1"));
    }

    [Test]
    public void RightClickElement_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.RightClickElement("el-1"));
    }

    [Test]
    public void HoverElement_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.HoverElement("el-1"));
    }

    [Test]
    public void TypeText_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.TypeText("el-1", "hello"));
    }

    [Test]
    public void HitKeys_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.HitKeys("el-1", new[] { "ENTER" }));
    }

    [Test]
    public void FocusElement_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.FocusElement("el-1"));
    }

    [Test]
    public void SetForeground_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.SetForeground("el-1"));
    }

    [Test]
    public void WaitForElementVanish_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.WaitForElementVanish("el-1", 3000));
    }

    [Test]
    public void GetText_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.GetText("el-1"));
    }

    [Test]
    public void GetAttribute_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.GetAttribute("el-1", "Name"));
    }

    [Test]
    public void GetAttributes_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.GetAttributes("el-1"));
    }

    [Test]
    public void SetAttribute_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.SetAttribute("el-1", "toggle_togglestate", "On"));
    }

    [Test]
    public void IsStale_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.IsStale("el-1"));
    }

    [Test]
    public void IsEnabled_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.IsEnabled("el-1"));
    }

    [Test]
    public void IsVisible_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.IsVisible("el-1"));
    }

    [Test]
    public void GetBoundingRect_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.GetBoundingRect("el-1"));
    }

    [Test]
    public void ScreenshotElement_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.ScreenshotElement("el-1"));
    }

    // ── Connected happy paths ────────────────────────────────────────────────

    [Test]
    public void HitKeys_InvalidKeyName_Throws()
    {
        ConnectFakeTransport();
        Assert.ThrowsAsync<ArgumentException>(
            () => _tools.HitKeys("el-1", new[] { "INVALID_KEY_NAME" }));
    }

    [Test]
    public async Task ClickElement_SendsClickCommand()
    {
        ConnectFakeTransport();
        var result = await _tools.ClickElement("el-1");

        Assert.That(result, Is.EqualTo("Clicked."));
        Assert.That(_transport.Calls, Has.Count.EqualTo(1));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("click"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"elementId\":\"el-1\""));
    }

    [Test]
    public async Task DoubleClickElement_SendsDoubleClickCommand()
    {
        ConnectFakeTransport();
        var result = await _tools.DoubleClickElement("el-1");

        Assert.That(result, Is.EqualTo("Double-clicked."));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("doubleClick"));
    }

    [Test]
    public async Task RightClickElement_SendsRightClickCommand()
    {
        ConnectFakeTransport();
        var result = await _tools.RightClickElement("el-1");

        Assert.That(result, Is.EqualTo("Right-clicked."));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("rightClick"));
    }

    [Test]
    public async Task HoverElement_SendsHoverCommand()
    {
        ConnectFakeTransport();
        var result = await _tools.HoverElement("el-1");

        Assert.That(result, Is.EqualTo("Hovered."));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("hover"));
    }

    [Test]
    public async Task ScrollElement_SendsScrollCommand()
    {
        ConnectFakeTransport();
        var result = await _tools.ScrollElement("el-1", 3);

        Assert.That(result, Does.Contain("3"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("scroll"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"lines\":3"));
    }

    [Test]
    public async Task ScrollElement_Horizontal_SendsScrollCommand()
    {
        ConnectFakeTransport();
        var result = await _tools.ScrollElement("el-1", -2, horizontal: true);

        Assert.That(result, Does.Contain("horizontally"));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("scroll"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"horizontal\":true"));
    }

    [Test]
    public async Task TypeText_SendsTypeTextCommand()
    {
        ConnectFakeTransport();
        var result = await _tools.TypeText("el-1", "hello world");

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("typeText"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"text\":\"hello world\""));
        Assert.That(result, Does.Contain("hello world"));
    }

    [Test]
    public async Task TypeText_WithModifiers_SendsModifiers()
    {
        ConnectFakeTransport();
        await _tools.TypeText("el-1", "a", new[] { "Ctrl", "Shift" });

        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"modifiers\":3"));
    }

    [Test]
    public async Task TypeText_WithoutModifiers_SendsNone()
    {
        ConnectFakeTransport();
        await _tools.TypeText("el-1", "hello");

        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"modifiers\":0"));
    }

    [Test]
    public async Task HitKeys_SendsHitKeysCommand()
    {
        ConnectFakeTransport();
        var result = await _tools.HitKeys("el-1", new[] { "CONTROL", "KEY_A" });

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("hitKeys"));
        Assert.That(result, Does.Contain("CONTROL+KEY_A"));
    }

    [Test]
    public async Task FocusElement_SendsFocusCommand()
    {
        ConnectFakeTransport();
        var result = await _tools.FocusElement("el-1");

        Assert.That(result, Is.EqualTo("Focused."));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("focus"));
    }

    [Test]
    public async Task SetForeground_SendsSetForegroundCommand()
    {
        ConnectFakeTransport();
        var result = await _tools.SetForeground("el-1");

        Assert.That(result, Is.EqualTo("Brought to foreground."));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("setForeground"));
    }

    [Test]
    public async Task WaitForElementVanish_SendsCommand()
    {
        ConnectFakeTransport();
        var result = await _tools.WaitForElementVanish("el-1", 3000);

        Assert.That(result, Is.EqualTo("Element vanished."));
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("waitForElementVanish"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"elementId\":\"el-1\""));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"timeout\":3000"));
    }

    [Test]
    public async Task GetText_SendsGetTextCommand()
    {
        ConnectFakeTransport();
        _transport.Enqueue("Hello");

        var result = await _tools.GetText("el-1");

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getText"));
        Assert.That(result, Is.EqualTo("Hello"));
    }

    [Test]
    public async Task GetAttribute_SendsGetAttributeCommand()
    {
        ConnectFakeTransport();
        _transport.Enqueue("Button");

        var result = await _tools.GetAttribute("el-1", "ControlType");

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getAttribute"));
        Assert.That(result, Is.EqualTo("Button"));
    }

    [Test]
    public async Task GetAttributes_SendsGetAttributesCommand()
    {
        ConnectFakeTransport();
        _transport.Enqueue(new { Name = "OK", ControlType = "Button" });

        var result = await _tools.GetAttributes("el-1");

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getAttributes"));
        Assert.That(result, Does.Contain("Name"));
    }

    [Test]
    public async Task SetAttribute_SendsSetAttributeCommand()
    {
        ConnectFakeTransport();
        _transport.Enqueue((object?)null);

        var result = await _tools.SetAttribute("el-1", "toggle_togglestate", "On");

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("setAttribute"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"attribute\":\"toggle_togglestate\""));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"value\":\"On\""));
        Assert.That(result, Does.Contain("Set toggle_togglestate"));
    }

    [Test]
    public async Task IsStale_ReturnsFalseWhenAlive()
    {
        ConnectFakeTransport();
        _transport.Enqueue(false);

        var result = await _tools.IsStale("el-1");
        Assert.That(result, Is.False);
        Assert.That(_transport.Calls[0].Command, Is.EqualTo("isStale"));
    }

    [Test]
    public async Task IsEnabled_ReturnsTrueWhenEnabled()
    {
        ConnectFakeTransport();
        _transport.Enqueue(true);

        var result = await _tools.IsEnabled("el-1");
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task IsVisible_ReturnsFalseWhenNotVisible()
    {
        ConnectFakeTransport();
        _transport.Enqueue(false);

        var result = await _tools.IsVisible("el-1");
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task GetBoundingRect_ReturnsJson()
    {
        ConnectFakeTransport();
        _transport.Enqueue(new { x = 10.0, y = 20.0, width = 100.0, height = 50.0 });

        var result = await _tools.GetBoundingRect("el-1");
        var doc = JsonDocument.Parse(result);

        Assert.That(doc.RootElement.GetProperty("X").GetDouble(), Is.EqualTo(10.0));
        Assert.That(doc.RootElement.GetProperty("Y").GetDouble(), Is.EqualTo(20.0));
        Assert.That(doc.RootElement.GetProperty("Width").GetDouble(), Is.EqualTo(100.0));
        Assert.That(doc.RootElement.GetProperty("Height").GetDouble(), Is.EqualTo(50.0));
    }

    [Test]
    public async Task ScreenshotElement_ReturnsBase64()
    {
        ConnectFakeTransport();
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        _transport.Enqueue(Convert.ToBase64String(pngBytes));

        var result = await _tools.ScreenshotElement("el-1");
        var decoded = Convert.FromBase64String(result);

        Assert.That(decoded, Is.EqualTo(pngBytes));
    }

    [Test]
    public async Task ScreenshotElement_WithOutputPath_SavesFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "wc-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            _state.ScreenshotDir = tempDir;
            ConnectFakeTransport();
            var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
            _transport.Enqueue(Convert.ToBase64String(pngBytes));

            var result = await _tools.ScreenshotElement("el-1", "element.png");

            Assert.That(File.Exists(result), Is.True);
            Assert.That(File.ReadAllBytes(result), Is.EqualTo(pngBytes));
            Assert.That(result, Does.StartWith(Path.GetFullPath(tempDir)));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task HitKeys_CaseInsensitive()
    {
        ConnectFakeTransport();
        var result = await _tools.HitKeys("el-1", new[] { "enter" });

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("hitKeys"));
        Assert.That(result, Does.Contain("enter"));
    }

    // ── Tree navigation ────────────────────────────────────────────────────

    [Test]
    public void GetChildren_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.GetChildren("el-1"));
    }

    [Test]
    public void GetDescendants_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.GetDescendants("el-1"));
    }

    [Test]
    public void GetParent_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.GetParent("el-1"));
    }

    [Test]
    public void GetTopLevelWindow_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.GetTopLevelWindow("el-1"));
    }

    [Test]
    public async Task GetChildren_ReturnsChildIds()
    {
        ConnectFakeTransport();
        _transport.Enqueue(new[] { "child-1", "child-2", "child-3" });

        var result = await _tools.GetChildren("el-1");
        var ids = JsonSerializer.Deserialize<string[]>(result);

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getChildren"));
        Assert.That(ids, Is.EqualTo(new[] { "child-1", "child-2", "child-3" }));
    }

    [Test]
    public async Task GetDescendants_ReturnsTree()
    {
        ConnectFakeTransport();
        _transport.Enqueue(new
        {
            id = "el-1",
            children = new object[]
            {
                new { id = "child-1", children = Array.Empty<object>() },
                new { id = "child-2", children = new object[] { new { id = "grandchild-1", children = Array.Empty<object>() } } }
            }
        });

        var result = await _tools.GetDescendants("el-1");
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getDescendants"));
        Assert.That(root.GetProperty("id").GetString(), Is.EqualTo("el-1"));
        Assert.That(root.GetProperty("children").GetArrayLength(), Is.EqualTo(2));
        Assert.That(root.GetProperty("children")[1].GetProperty("children")[0].GetProperty("id").GetString(),
            Is.EqualTo("grandchild-1"));
    }

    [Test]
    public async Task GetParent_ReturnsParentId()
    {
        ConnectFakeTransport();
        _transport.Enqueue("parent-1");

        var result = await _tools.GetParent("el-1");

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getParent"));
        Assert.That(result, Is.EqualTo("parent-1"));
    }

    [Test]
    public async Task GetParent_WhenNoParent_ReturnsNull()
    {
        ConnectFakeTransport();
        _transport.Enqueue(null);

        var result = await _tools.GetParent("el-1");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetTopLevelWindow_ReturnsWindowId()
    {
        ConnectFakeTransport();
        _transport.Enqueue("window-1");

        var result = await _tools.GetTopLevelWindow("el-1");

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getTopLevelWindow"));
        Assert.That(result, Is.EqualTo("window-1"));
    }

    [Test]
    public async Task GetTopLevelWindow_WhenNoWindow_ReturnsNull()
    {
        ConnectFakeTransport();
        _transport.Enqueue(null);

        var result = await _tools.GetTopLevelWindow("el-1");

        Assert.That(result, Is.Null);
    }

    // ── Window state ──────────────────────────────────────────────────────

    [Test]
    public void GetWindowState_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.GetWindowState("el-1"));
    }

    [Test]
    public void SetWindowState_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.SetWindowState("el-1", "Maximized"));
    }

    [Test]
    public async Task GetWindowState_ReturnsStateName()
    {
        ConnectFakeTransport();
        _transport.Enqueue(1); // Maximized = 1

        var result = await _tools.GetWindowState("el-1");

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getWindowState"));
        Assert.That(result, Is.EqualTo("Maximized"));
    }

    [Test]
    public async Task SetWindowState_SendsCommand()
    {
        ConnectFakeTransport();

        var result = await _tools.SetWindowState("el-1", "Minimized");

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("setWindowState"));
        Assert.That(result, Does.Contain("Minimized"));
    }

    [Test]
    public async Task SetWindowState_CaseInsensitive()
    {
        ConnectFakeTransport();

        var result = await _tools.SetWindowState("el-1", "maximized");

        Assert.That(result, Does.Contain("Maximized"));
    }

    [Test]
    public void SetWindowState_InvalidState_Throws()
    {
        ConnectFakeTransport();
        Assert.ThrowsAsync<ArgumentException>(
            () => _tools.SetWindowState("el-1", "InvalidState"));
    }

    [Test]
    public void SetWindowState_Hidden_Throws()
    {
        ConnectFakeTransport();
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.SetWindowState("el-1", "Hidden"));
    }

    // ── Anchor/offset interactions ──────────────────────────────────────────

    [Test]
    public void ClickElementAt_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.ClickElementAt("el-1", "Center", 0, 0));
    }

    [Test]
    public async Task ClickElementAt_SendsClickWithAnchor()
    {
        ConnectFakeTransport();
        var result = await _tools.ClickElementAt("el-1", "NorthWest", 5, 10);

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("click"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"anchor\":\"NorthWest\""));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"x\":5"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"y\":10"));
        Assert.That(result, Does.Contain("NorthWest"));
    }

    [Test]
    public async Task ClickElementAt_CaseInsensitive()
    {
        ConnectFakeTransport();
        var result = await _tools.ClickElementAt("el-1", "center", 0, 0);

        Assert.That(result, Does.Contain("center"));
    }

    [Test]
    public void ClickElementAt_InvalidAnchor_Throws()
    {
        ConnectFakeTransport();
        Assert.ThrowsAsync<ArgumentException>(
            () => _tools.ClickElementAt("el-1", "InvalidAnchor", 0, 0));
    }

    [Test]
    public async Task DoubleClickElementAt_SendsDoubleClickWithAnchor()
    {
        ConnectFakeTransport();
        var result = await _tools.DoubleClickElementAt("el-1", "East", 3, 7);

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("doubleClick"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"anchor\":\"East\""));
        Assert.That(result, Does.Contain("Double-clicked"));
    }

    [Test]
    public async Task RightClickElementAt_SendsRightClickWithAnchor()
    {
        ConnectFakeTransport();
        var result = await _tools.RightClickElementAt("el-1", "South", -2, 4);

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("rightClick"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"anchor\":\"South\""));
        Assert.That(result, Does.Contain("Right-clicked"));
    }

    [Test]
    public async Task HoverElementAt_SendsHoverWithAnchor()
    {
        ConnectFakeTransport();
        var result = await _tools.HoverElementAt("el-1", "SouthEast", 1, 2);

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("hover"));
        Assert.That(_transport.Calls[0].ParamsJson, Does.Contain("\"anchor\":\"SouthEast\""));
        Assert.That(result, Does.Contain("Hovered"));
    }

    // ── OCR ─────────────────────────────────────────────────────────────────

    [Test]
    public void GetOcrText_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.GetOcrText("el-1"));
    }

    [Test]
    public async Task GetOcrText_ReturnsStructuredResult()
    {
        ConnectFakeTransport();
        _transport.Enqueue(new
        {
            text = "Hello World",
            angle = 0.0,
            boundingRect = new { x = 0.0, y = 0.0, width = 100.0, height = 20.0 },
            lines = new object[]
            {
                new
                {
                    text = "Hello World",
                    boundingRect = new { x = 0.0, y = 0.0, width = 100.0, height = 20.0 },
                    words = new object[]
                    {
                        new { text = "Hello", boundingRect = new { x = 0.0, y = 0.0, width = 45.0, height = 20.0 } },
                        new { text = "World", boundingRect = new { x = 50.0, y = 0.0, width = 50.0, height = 20.0 } }
                    }
                }
            }
        });

        var result = await _tools.GetOcrText("el-1");
        var doc = JsonDocument.Parse(result);

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getOcrText"));
        Assert.That(doc.RootElement.GetProperty("text").GetString(), Is.EqualTo("Hello World"));
        Assert.That(doc.RootElement.GetProperty("angle").GetDouble(), Is.EqualTo(0.0));

        var lines = doc.RootElement.GetProperty("lines");
        Assert.That(lines.GetArrayLength(), Is.EqualTo(1));

        var words = lines[0].GetProperty("words");
        Assert.That(words.GetArrayLength(), Is.EqualTo(2));
        Assert.That(words[0].GetProperty("text").GetString(), Is.EqualTo("Hello"));
        Assert.That(words[1].GetProperty("text").GetString(), Is.EqualTo("World"));
    }

    [Test]
    public async Task GetOcrText_NullAngle_ReturnsNullAngle()
    {
        ConnectFakeTransport();
        _transport.Enqueue(new
        {
            text = "",
            angle = (double?)null,
            boundingRect = new { x = 0.0, y = 0.0, width = 0.0, height = 0.0 },
            lines = Array.Empty<object>()
        });

        var result = await _tools.GetOcrText("el-1");
        var doc = JsonDocument.Parse(result);

        Assert.That(doc.RootElement.GetProperty("angle").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(doc.RootElement.GetProperty("lines").GetArrayLength(), Is.EqualTo(0));
    }

    // ── FindOcrText ──────────────────────────────────────────────────────────

    [Test]
    public void FindOcrText_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.FindOcrText("el-1", "hello"));
    }

    [Test]
    public async Task FindOcrText_ExactMatch_ReturnsMatchWithLineContext()
    {
        ConnectFakeTransport();
        EnqueueOcrResponse("Claimant Spouse\nClaimant",
            new[]
            {
                ("Claimant Spouse", 0.0, 0.0, 150.0, 20.0, new[] { ("Claimant", 0.0, 0.0, 80.0, 20.0), ("Spouse", 85.0, 0.0, 65.0, 20.0) }),
                ("Claimant", 0.0, 25.0, 80.0, 20.0, new[] { ("Claimant", 0.0, 25.0, 80.0, 20.0) })
            });

        var result = await _tools.FindOcrText("el-1", "Claimant");
        var doc = JsonDocument.Parse(result);

        Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(2));

        var first = doc.RootElement[0];
        Assert.That(first.GetProperty("matchedText").GetString(), Is.EqualTo("Claimant"));
        Assert.That(first.GetProperty("editDistance").GetInt32(), Is.EqualTo(0));
        Assert.That(first.GetProperty("lineText").GetString(), Is.EqualTo("Claimant Spouse"));

        var second = doc.RootElement[1];
        Assert.That(second.GetProperty("matchedText").GetString(), Is.EqualTo("Claimant"));
        Assert.That(second.GetProperty("lineText").GetString(), Is.EqualTo("Claimant"));
    }

    [Test]
    public async Task FindOcrText_NoMatch_ReturnsEmptyArray()
    {
        ConnectFakeTransport();
        EnqueueOcrResponse("Hello World",
            new[] { ("Hello World", 0.0, 0.0, 100.0, 20.0, new[] { ("Hello", 0.0, 0.0, 45.0, 20.0), ("World", 50.0, 0.0, 50.0, 20.0) }) });

        var result = await _tools.FindOcrText("el-1", "Missing");
        var doc = JsonDocument.Parse(result);

        Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task FindOcrText_FuzzyMatch_ReturnsWithEditDistance()
    {
        ConnectFakeTransport();
        EnqueueOcrResponse("Claimnt",
            new[] { ("Claimnt", 0.0, 0.0, 70.0, 20.0, new[] { ("Claimnt", 0.0, 0.0, 70.0, 20.0) }) });

        var result = await _tools.FindOcrText("el-1", "Claimant", maxEdits: 1);
        var doc = JsonDocument.Parse(result);

        Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(1));
        Assert.That(doc.RootElement[0].GetProperty("editDistance").GetInt32(), Is.EqualTo(1));
    }

    // ── OCR action tools ──────────────────────────────────────────────────────

    [Test]
    public void ClickOcrText_WhenNotConnected_Throws()
    {
        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.ClickOcrText("el-1", "hello"));
    }

    [Test]
    public async Task ClickOcrText_DefaultAnchor_ClicksAtMatchCenter()
    {
        ConnectFakeTransport();
        EnqueueOcrResponse("Hello World",
            new[] { ("Hello World", 0.0, 0.0, 100.0, 20.0, new[] { ("Hello", 0.0, 0.0, 45.0, 20.0), ("World", 50.0, 0.0, 50.0, 20.0) }) });

        var result = await _tools.ClickOcrText("el-1", "Hello");

        Assert.That(_transport.Calls[0].Command, Is.EqualTo("getOcrText"));
        Assert.That(_transport.Calls[1].Command, Is.EqualTo("click"));
        Assert.That(_transport.Calls[1].ParamsJson, Does.Contain("\"anchor\":\"NorthWest\""));
        Assert.That(result, Does.Contain("Clicked OCR text"));
        Assert.That(result, Does.Contain("Hello"));
    }

    [Test]
    public async Task ClickOcrText_WithAnchorAndOffset_PassesCorrectly()
    {
        ConnectFakeTransport();
        EnqueueOcrResponse("Hello",
            new[] { ("Hello", 10.0, 20.0, 60.0, 20.0, new[] { ("Hello", 10.0, 20.0, 60.0, 20.0) }) });

        var result = await _tools.ClickOcrText("el-1", "Hello", anchor: "NorthWest", offsetX: 5, offsetY: 3);

        Assert.That(_transport.Calls[1].Command, Is.EqualTo("click"));
        Assert.That(_transport.Calls[1].ParamsJson, Does.Contain("\"anchor\":\"NorthWest\""));
        Assert.That(result, Does.Contain("NorthWest"));
    }

    [Test]
    public void ClickOcrText_NoMatch_Throws()
    {
        ConnectFakeTransport();
        EnqueueOcrResponse("Hello World",
            new[] { ("Hello World", 0.0, 0.0, 100.0, 20.0, new[] { ("Hello", 0.0, 0.0, 45.0, 20.0), ("World", 50.0, 0.0, 50.0, 20.0) }) });

        Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.ClickOcrText("el-1", "Missing"));
    }

    [Test]
    public void ClickOcrText_InvalidMatchIndex_Throws()
    {
        ConnectFakeTransport();
        EnqueueOcrResponse("Hello",
            new[] { ("Hello", 0.0, 0.0, 45.0, 20.0, new[] { ("Hello", 0.0, 0.0, 45.0, 20.0) }) });

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _tools.ClickOcrText("el-1", "Hello", matchIndex: 1));
    }

    [Test]
    public async Task ClickOcrText_MatchIndex_SelectsCorrectMatch()
    {
        ConnectFakeTransport();
        EnqueueOcrResponse("Claimant Spouse\nClaimant",
            new[]
            {
                ("Claimant Spouse", 0.0, 0.0, 150.0, 20.0, new[] { ("Claimant", 0.0, 0.0, 80.0, 20.0), ("Spouse", 85.0, 0.0, 65.0, 20.0) }),
                ("Claimant", 0.0, 25.0, 80.0, 20.0, new[] { ("Claimant", 0.0, 25.0, 80.0, 20.0) })
            });

        var result = await _tools.ClickOcrText("el-1", "Claimant", matchIndex: 1);

        Assert.That(result, Does.Contain("Clicked OCR text"));
    }

    [Test]
    public async Task DoubleClickOcrText_SendsDoubleClick()
    {
        ConnectFakeTransport();
        EnqueueOcrResponse("Hello",
            new[] { ("Hello", 0.0, 0.0, 45.0, 20.0, new[] { ("Hello", 0.0, 0.0, 45.0, 20.0) }) });

        var result = await _tools.DoubleClickOcrText("el-1", "Hello");

        Assert.That(_transport.Calls[1].Command, Is.EqualTo("doubleClick"));
        Assert.That(result, Does.Contain("Double-clicked OCR text"));
    }

    [Test]
    public async Task RightClickOcrText_SendsRightClick()
    {
        ConnectFakeTransport();
        EnqueueOcrResponse("Hello",
            new[] { ("Hello", 0.0, 0.0, 45.0, 20.0, new[] { ("Hello", 0.0, 0.0, 45.0, 20.0) }) });

        var result = await _tools.RightClickOcrText("el-1", "Hello");

        Assert.That(_transport.Calls[1].Command, Is.EqualTo("rightClick"));
        Assert.That(result, Does.Contain("Right-clicked OCR text"));
    }

    [Test]
    public async Task HoverOcrText_SendsHover()
    {
        ConnectFakeTransport();
        EnqueueOcrResponse("Hello",
            new[] { ("Hello", 0.0, 0.0, 45.0, 20.0, new[] { ("Hello", 0.0, 0.0, 45.0, 20.0) }) });

        var result = await _tools.HoverOcrText("el-1", "Hello");

        Assert.That(_transport.Calls[1].Command, Is.EqualTo("hover"));
        Assert.That(result, Does.Contain("Hovered OCR text"));
    }

    private void EnqueueOcrResponse(string fullText,
        (string Text, double X, double Y, double W, double H, (string Text, double X, double Y, double W, double H)[] Words)[] lines)
    {
        _transport.Enqueue(new
        {
            text = fullText,
            angle = (double?)0.0,
            boundingRect = new { x = 0.0, y = 0.0, width = 200.0, height = 50.0 },
            lines = lines.Select(l => new
            {
                text = l.Text,
                boundingRect = new { x = l.X, y = l.Y, width = l.W, height = l.H },
                words = l.Words.Select(w => new
                {
                    text = w.Text,
                    boundingRect = new { x = w.X, y = w.Y, width = w.W, height = w.H }
                }).ToArray()
            }).ToArray()
        });
    }

    private void ConnectFakeTransport() =>
        _state.SetTransportForTesting(_transport);
}

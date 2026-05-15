using System.ComponentModel;
using ModelContextProtocol.Server;

namespace WindowsConductor.MCP.Prompts;

[McpServerPromptType]
public static class ConductorPrompts
{
    [McpServerPrompt, Description(
        "Step-by-step guide to automate filling a form in a Windows application.")]
    public static string FormFill(
        [Description("Application path or window title regex to launch/attach")] string app,
        [Description("JSON object mapping selectors to values, e.g. {\"[automationid=firstName]\": \"John\", \"[automationid=lastName]\": \"Doe\"}")] string fieldMap)
    {
        return $"""
            Automate filling a form in a Windows desktop application.

            1. Connect to the WindowsConductor driver (if not already connected).
            2. Launch or attach to the application: {app}
               - If it looks like a file path or executable name, use LaunchApp.
               - If it looks like a window title pattern, use AttachApp.
            3. Parse the field map: {fieldMap}
               For each entry (selector → value):
               a. Use FindElement with the selector to locate the input field.
               b. Use ClickElement to focus it.
               c. Use HitKeys with ["CONTROL", "KEY_A"] to select existing text.
               d. Use TypeText to type the new value.
            4. After all fields are filled, look for a Submit/OK/Save button using FindElement.
            5. Use ClickElement to submit the form.
            6. Use ScreenshotApp to capture the result and confirm success.
            """;
    }

    [McpServerPrompt, Description(
        "Inspect a UI element and report its properties, children, and position.")]
    public static string InspectElement(
        [Description("The appId of a tracked application")] string appId,
        [Description("Element selector (CSS-like attributes, XPath, text, or control type)")] string selector)
    {
        return $"""
            Inspect a UI element in detail.

            1. Use FindElement with appId "{appId}" and selector "{selector}" to locate the element.
            2. Use GetAttributes to retrieve all properties of the element.
            3. Use GetBoundingRect to get its screen position and size.
            4. Use GetText to read its text content.
            5. Use GetChildren to list its direct children and their IDs.
            6. Use GetParent to identify the parent element.
            7. Summarize the findings: control type, name, automation ID, bounding rect,
               text content, number of children, and parent element info.
            """;
    }

    [McpServerPrompt, Description(
        "Take screenshots before and after an action to visually verify the result.")]
    public static string ScreenshotComparison(
        [Description("The appId of a tracked application")] string appId,
        [Description("Description of the action to perform between screenshots (e.g. 'click the Save button')")] string action)
    {
        return $"""
            Perform a visual before/after comparison of an action.

            1. Use ScreenshotApp with appId "{appId}" to capture the "before" state.
            2. Perform the described action: {action}
               - Parse the action description and use the appropriate tools
                 (FindElement, ClickElement, TypeText, HitKeys, etc.).
            3. Use ScreenshotApp again to capture the "after" state.
            4. Compare the two screenshots and describe what changed visually.
            """;
    }

    [McpServerPrompt, Description(
        "Wait for a UI element to appear, then interact with it.")]
    public static string WaitAndInteract(
        [Description("The appId of a tracked application")] string appId,
        [Description("Element selector to wait for")] string selector,
        [Description("Interaction to perform: click, double-click, right-click, type, or hit-keys")] string interaction,
        [Description("Timeout in milliseconds (default: 10000)")] string timeout = "10000")
    {
        return $"""
            Wait for a UI element and then interact with it.

            1. Use WaitForElement with appId "{appId}", selector "{selector}",
               and timeout {timeout}ms.
            2. Once the element appears, perform the interaction: {interaction}
               - "click" → Use ClickElement
               - "double-click" → Use DoubleClickElement
               - "right-click" → Use RightClickElement
               - "type <text>" → Use TypeText with the specified text
               - "hit-keys <keys>" → Use HitKeys with the specified key names
            3. Use ScreenshotApp to capture the result.
            """;
    }

    [McpServerPrompt, Description(
        "Use OCR to read text from a UI element or region of the screen.")]
    public static string OcrRead(
        [Description("The appId of a tracked application")] string appId,
        [Description("Element selector for the region to OCR, or 'window' for the entire app window")] string selector)
    {
        return $"""
            Read text from a UI element using OCR.

            1. Use FindElement with appId "{appId}" and selector "{selector}" to locate
               the target element. If selector is "window", skip this step.
            2. Use GetOcrText on the element ID to perform OCR.
               The result includes the full text, individual lines, and word-level
               bounding rectangles.
            3. If looking for specific text, use FindOcrText with a search term
               for exact or fuzzy matching. This returns matched text with
               bounding rectangles and edit distances.
            4. Report the extracted text, organized by lines.
            """;
    }
}

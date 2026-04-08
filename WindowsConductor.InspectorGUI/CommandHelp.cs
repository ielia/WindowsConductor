using System.Reflection;

namespace WindowsConductor.InspectorGUI;

internal static class CommandHelp
{
    private static readonly ParsedCommand[] AllCommands = Assembly
        .GetExecutingAssembly()
        .GetTypes()
        .Where(t => t.IsSubclassOf(typeof(ParsedCommand)) && !t.IsAbstract)
        .Select(t =>
        {
            var ctor = t.GetConstructors()[0];
            var args = ctor.GetParameters()
                .Select(p => p.HasDefaultValue ? p.DefaultValue : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)
                .ToArray();
            return (ParsedCommand)ctor.Invoke(args);
        })
        .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    internal static readonly string[] AllCommandNames = AllCommands
        .Select(c => c.Name)
        .ToArray();

    internal static string GetAll()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Available commands:");
        sb.AppendLine();
        foreach (var cmd in AllCommands)
        {
            sb.AppendLine($"  {cmd.Usage}");
            foreach (var line in cmd.Description.Split('\n'))
                sb.AppendLine($"    {line}");
            if (cmd.Examples.Length > 0)
                sb.AppendLine($"    Example: {cmd.Examples[0]}");
            sb.AppendLine();
        }
        sb.Append(KeyBindingsText);
        return sb.ToString().TrimEnd();
    }

    internal const string KeyBindingsText =
        """
        Key bindings:
          Shift+PgUp / Shift+PgDown   Scroll log
          Up Arrow / Down Arrow       Previous / next command in history
          Tab                         Autocomplete command
          Ctrl+Tab / Shift+Ctrl+Tab   Cycle panels
          Alt+Left / Alt+Right        Previous / next match
          Alt+B                       Go back (<<)
          Alt+C                       Copy attributes
          Alt+L                       Toggle clickless mode
          Alt+R                       Refresh
          Alt+S                       Stop sleep and remaining commands
        """;

    internal static string? GetFor(string commandName)
    {
        var cmd = AllCommands.FirstOrDefault(c =>
            string.Equals(c.Name, commandName, StringComparison.OrdinalIgnoreCase)
            || (c.Name == "exit" && string.Equals("quit", commandName, StringComparison.OrdinalIgnoreCase)));

        if (cmd is null) return null;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"  {cmd.Usage}");
        foreach (var line in cmd.Description.Split('\n'))
            sb.AppendLine($"    {line}");
        if (cmd.Examples.Length > 0)
        {
            sb.AppendLine("  Examples:");
            foreach (var example in cmd.Examples)
                sb.AppendLine($"    {example}");
        }
        return sb.ToString().TrimEnd();
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WindowsConductor.MCP;

var builder = Host.CreateApplicationBuilder(args);

var state = new ConductorState();
var videoDirIndex = Array.IndexOf(args, "--video-dir");
if (videoDirIndex >= 0 && videoDirIndex + 1 < args.Length)
    state.VideoDir = args[videoDirIndex + 1];

var screenshotDirIndex = Array.IndexOf(args, "--screenshot-dir");
if (screenshotDirIndex >= 0 && screenshotDirIndex + 1 < args.Length)
    state.ScreenshotDir = args[screenshotDirIndex + 1];

builder.Services.AddSingleton(state);

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "WindowsConductor",
            Version = "0.8.0"
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithPromptsFromAssembly();

await builder.Build().RunAsync();

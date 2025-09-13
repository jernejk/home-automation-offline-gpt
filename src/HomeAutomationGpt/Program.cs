using HomeAutomationGpt;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure AI provider from configuration (wwwroot/appsettings.json)
var provider = builder.Configuration["AI:Provider"] ?? "LMStudio";
var endpointStr = builder.Configuration["AI:Endpoint"] ?? "http://localhost:1234/v1";
var modelId = builder.Configuration["AI:Model"] ?? "qwen/qwen3-coder-30b";
var endpointUri = new Uri(endpointStr);

// Configure V1 JSON path to use the configured endpoint's /chat/completions
HomeAutomationGpt.Services.HomeAssistanceService.ChatUrl = endpointStr.TrimEnd('/') + "/chat/completions";

// Configure OpenAI-compatible client for LM Studio or Ollama
var credential = new ApiKeyCredential(provider.Equals("LMStudio", StringComparison.OrdinalIgnoreCase) ? "lm-studio" : "ollama");
var openAIOptions = new OpenAIClientOptions()
{
    Endpoint = endpointUri
};

var openAIClient = new OpenAIClient(credential, openAIOptions);
var chatClient = openAIClient.GetChatClient(modelId).AsIChatClient();

// If using GitHub Models, set required headers for the V1 path
if (string.Equals(provider, "GitHub", StringComparison.OrdinalIgnoreCase))
{
    var apiKey = builder.Configuration["AI:ApiKey"];
    var headers = new Dictionary<string, string>
    {
        { "Accept", "application/vnd.github+json" },
        { "X-GitHub-Api-Version", "2022-11-28" }
    };
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        headers["Authorization"] = $"Bearer {apiKey}";
    }
    HomeAutomationGpt.Services.HomeAssistanceService.AdditionalHeaders = headers;
}
else
{
    HomeAutomationGpt.Services.HomeAssistanceService.AdditionalHeaders = null;
}

builder.Services.AddChatClient(chatClient)
    .UseFunctionInvocation()
    .UseLogging();

builder.Services.AddSingleton<IMcpClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

    var uriStr = config["HomeAssistant:HASS"] ?? config["McpServers:HASS"]; // Prefer HomeAssistant:HASS, fallback to McpServers:HASS
    if (string.IsNullOrWhiteSpace(uriStr))
    {
        // If not configured, return a no-op client by pointing to a non-routable endpoint (won't be used in the demo by default)
        uriStr = "http://127.0.0.1:9";
    }
    var uri = new Uri(uriStr);
    var accessToken = config["HomeAssistant:AccessToken"];

    var clientTransportOptions = new SseClientTransportOptions()
    {
        // Update endpoint to use Home Assistant MCP server proxy
        Endpoint = new Uri($"{uri.AbsoluteUri.TrimEnd('/')}/api/mcp"),
        AdditionalHeaders = accessToken != null ? new Dictionary<string, string> {
            { "Authorization", $"Bearer {accessToken}" }
        } : null
        // If OAuth is needed, configure it here. For now, only Bearer token is set.
    };
    
    var clientTransport = new SseClientTransport(clientTransportOptions, loggerFactory);
    var clientOptions = new McpClientOptions()
    {
        ClientInfo = new Implementation()
        {
            // TODO: Update this with correct values for HASS MCP Proxy
            Name = "MCP Todo Client",
            Version = "1.0.0",
        }
    };

    return McpClientFactory.CreateAsync(clientTransport, clientOptions, loggerFactory).GetAwaiter().GetResult();
});

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();

// public partial class Program { }

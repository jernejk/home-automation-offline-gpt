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

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();

// public partial class Program { }

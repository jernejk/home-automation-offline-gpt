using HomeAutomationGpt.Core.Models;
using HomeAutomationGpt.Core.Services;
using HomeAutomationGpt.Universal.Mcp;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using System.ClientModel;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AiSettings>(builder.Configuration.GetSection("AI"));

builder.Services.AddChatClient(sp =>
{
    var settings = sp.GetRequiredService<IOptions<AiSettings>>().Value ?? new AiSettings();
    var endpoint = string.IsNullOrWhiteSpace(settings.Endpoint) ? "http://localhost:1234/v1" : settings.Endpoint.TrimEnd('/');

    HomeAssistanceService.ChatUrl = endpoint + "/chat/completions";

    var credentialValue = settings.Provider switch
    {
        var p when string.Equals(p, "Ollama", StringComparison.OrdinalIgnoreCase) => "ollama",
        var p when string.Equals(p, "LMStudio", StringComparison.OrdinalIgnoreCase) => "lm-studio",
        _ => string.IsNullOrWhiteSpace(settings.ApiKey) ? string.Empty : settings.ApiKey
    };

    var client = new OpenAIClient(new ApiKeyCredential(credentialValue), new OpenAIClientOptions
    {
        Endpoint = new Uri(endpoint)
    });

    var modelId = string.IsNullOrWhiteSpace(settings.Model) ? "qwen/qwen3-coder-30b" : settings.Model;
    return client.GetChatClient(modelId).AsIChatClient();
}).UseFunctionInvocation();

builder.Services.AddSingleton<McpClientManager>();

builder.Services.AddSingleton<IReadOnlyDictionary<string, IMcpClient>>(
    sp => sp.GetRequiredService<McpClientManager>().GetClientsAsync().GetAwaiter().GetResult());

var app = builder.Build();

app.MapGet("/api/mcp", (IReadOnlyDictionary<string, IMcpClient> mcpClients) =>
    Results.Ok(mcpClients.Keys));

app.MapPost("/api/prompt", async (PromptRequest request, IChatClient client) =>
{
    var service = new HomeAssistanceServiceV5(client);
    var response = await service.ExecuteCommandAsync(request.Prompt, DemoDevices());
    return Results.Ok(response);
});

app.MapPost("/api/prompt-with-mcp", async (
    PromptRequest request,
    IChatClient client,
    IReadOnlyDictionary<string, IMcpClient> mcpClients) =>
{
    var service = new HomeAssistanceServiceV6(client, mcpClients);
    var response = await service.ExecuteCommandAsync(request.Prompt, DemoDevices());
    return Results.Ok(response);
});

app.MapPost("/api/prompt-with-mcp-v6_5", async (
    PromptRequest request,
    IChatClient client,
    IReadOnlyDictionary<string, IMcpClient> mcpClients) =>
{
    var service = new HomeAssistanceServiceV6_5(client, mcpClients);
    var response = await service.ExecuteCommandAsync(request.Prompt, DemoDevices());
    return Results.Ok(response);
});

app.Run();

static List<Device> DemoDevices() =>
[
    new Device { Name = "Kitchen lights" },
    new Device { Name = "Living room lights" },
    new Device { Name = "TV" },
    new Device { Name = "A/C" }
];

public record PromptRequest(string Prompt);

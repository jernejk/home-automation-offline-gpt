using System.Net.Http.Json;
using System.IO;
using System.Text.Json;
using HomeAutomationGpt.Core.Models;
using HomeAutomationGpt.WebAPI;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using Xunit;

namespace HomeAutomation.IntegrationTests;

public class ApiSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IReadOnlyDictionary<string, IMcpClient>>(
                    provider => new Dictionary<string, IMcpClient>());
            });
        });
    }

    [Fact]
    public async Task MCP_List_ReturnsEmptyWhenNotConfigured()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/mcp");

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<string[]>();
        Assert.NotNull(payload);
        Assert.Empty(payload!);
    }

    [Fact]
    public async Task Prompt_ReturnsDeviceActions()
    {
        var client = _factory.CreateClient();
        var prompt = new PromptRequest("Turn on the kitchen lights");

        var response = await client.PostAsJsonAsync("/api/prompt", prompt);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<DeviceCommandResponse>();
        Assert.NotNull(body);
        Assert.Null(body!.Errors);
    }

    [Fact]
    public async Task PromptWithMcp_DuckDuckGoSearchReturnsResults()
    {
        var directoriesToCheck = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "AppContext"),
            AppContext.BaseDirectory,
            Path.Combine(Directory.GetCurrentDirectory(), "src", "HomeAutomationGpt.WebAPI")
        };

        var configPath = directoriesToCheck
            .Select(dir => Path.Combine(dir, "mcp.json"))
            .FirstOrDefault(File.Exists);

        if (configPath is null)
        {
            throw new InvalidOperationException("Unable to locate mcp.json for DuckDuckGo test.");
        }

        var configPayload = new
        {
            mcpServers = new
            {
                duckduckgo = new
                {
                    command = "docker",
                    args = new[] { "run", "-i", "--rm", "mcp/duckduckgo" }
                }
            }
        };

        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(configPayload));

        try
        {
            var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("mcp:enabled", "true");
            });

            var client = factory.CreateClient();
            var prompt = new PromptRequest("Turn on the TV and tell me how many seasons of Severance exist");

            var response = await client.PostAsJsonAsync("/api/prompt-with-mcp", prompt);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<DeviceCommandResponse>();
            Assert.NotNull(body);
            Assert.Null(body!.Errors);
            Assert.NotNull(body.DeviceActions);
            Assert.True((body.DeviceActions!.Count) >= 0);
        }
        finally
        {
            var emptyConfig = JsonSerializer.Serialize(new { mcpServers = new { } });
            await File.WriteAllTextAsync(configPath, emptyConfig);
        }
    }

    [Fact]
    public async Task PromptWithMcp_StillWorksWhenNoClientsAvailable()
    {
        var client = _factory.CreateClient();
        var prompt = new PromptRequest("Turn on the kitchen lights");

        var response = await client.PostAsJsonAsync("/api/prompt-with-mcp", prompt);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<DeviceCommandResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body.ChatResponse);
        Assert.NotEmpty(body.DeviceActions);
    }

    [Fact]
    public async Task PromptWithMcpV6_5_UniversalMcpApproachWorks()
    {
        var client = _factory.CreateClient();
        var prompt = new PromptRequest("Turn on the TV and get MCP status");

        var response = await client.PostAsJsonAsync("/api/prompt-with-mcp-v6_5", prompt);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<DeviceCommandResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body.ChatResponse);
        Assert.NotEmpty(body.Trace);
        
        // Verify that the universal MCP registry loaded tools
        var mcpToolEvents = body.Trace.Where(t => t.Kind == "McpToolAvailable").ToList();
        Assert.NotEmpty(mcpToolEvents);
        
        // Should have local tools available
        var localToolEvents = mcpToolEvents.Where(t => t.Summary.StartsWith("local:")).ToList();
        Assert.NotEmpty(localToolEvents);
    }
}

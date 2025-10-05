using System.Net.Http.Json;
using System.Text.Json;
using HomeAutomationGpt.Core.Models;
using HomeAutomationGpt.WebAPI;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using HomeAutomationGpt.Universal.Mcp;
using Xunit;

namespace HomeAutomation.IntegrationTests;

public class YouTubeTranscriptTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public YouTubeTranscriptTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory; // Use the default setup which loads mcp.json properly
    }

    [Fact]
    public async Task YouTubeTranscript_V6_5_UniversalMcpApproach_NoProxyMethodNeeded()
    {
        // This test demonstrates the power of V6.5 - we can add YouTube transcript MCP
        // without writing any proxy methods. The universal registry handles it automatically.
        
        var client = _factory.CreateClient();
        var prompt = new PromptRequest("Get the transcript from this YouTube video: https://www.youtube.com/watch?v=qRUW3zes4Gk");

        var response = await client.PostAsJsonAsync("/api/prompt-with-mcp-v6_5", prompt);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<DeviceCommandResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body.ChatResponse);
        Assert.NotEmpty(body.Trace);

        // Verify that YouTube transcript MCP was loaded automatically
        var mcpToolEvents = body.Trace.Where(t => t.Kind == "McpToolAvailable").ToList();
        Assert.NotEmpty(mcpToolEvents);
        
        // Should have YouTube transcript tools available
        var youtubeToolEvents = mcpToolEvents.Where(t => t.Summary.StartsWith("youtube_transcript:")).ToList();
        Assert.NotEmpty(youtubeToolEvents);
        
        // Verify MCP call was made to YouTube transcript service
        var mcpCallEvents = body.Trace.Where(t => t.Kind == "McpCall" && t.Summary.Contains("youtube_transcript")).ToList();
        Assert.NotEmpty(mcpCallEvents);
        
        // Should have received transcript content
        var mcpResponseEvents = body.Trace.Where(t => t.Kind == "McpResponse" && t.Summary.Contains("youtube_transcript")).ToList();
        Assert.NotEmpty(mcpResponseEvents);
        
        // The response should contain transcript content
        Assert.Contains("transcript", body.ChatResponse, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task V6_5_LoadsAllMcpToolsFromConfiguration()
    {
        // This test verifies that V6.5 automatically loads all MCP tools from mcp.json
        // without requiring individual proxy method implementations
        
        var client = _factory.CreateClient();
        var prompt = new PromptRequest("What MCP tools are available?");

        var response = await client.PostAsJsonAsync("/api/prompt-with-mcp-v6_5", prompt);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<DeviceCommandResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body.Trace);

        // Verify that tools were loaded from multiple MCP servers
        var mcpToolEvents = body.Trace.Where(t => t.Kind == "McpToolAvailable").ToList();
        Assert.NotEmpty(mcpToolEvents);
        
        // Should have local tools (device actions)
        var localToolEvents = mcpToolEvents.Where(t => t.Summary.StartsWith("local:")).ToList();
        Assert.NotEmpty(localToolEvents);
        
        // Should have DuckDuckGo tools
        var duckduckgoToolEvents = mcpToolEvents.Where(t => t.Summary.StartsWith("duckduckgo:")).ToList();
        Assert.NotEmpty(duckduckgoToolEvents);
        
        // Should have YouTube transcript tools
        var youtubeToolEvents = mcpToolEvents.Where(t => t.Summary.StartsWith("youtube_transcript:")).ToList();
        Assert.NotEmpty(youtubeToolEvents);
        
        // The beauty of V6.5: all these tools are available without writing proxy methods!
        Console.WriteLine($"✅ V6.5 Universal MCP loaded {mcpToolEvents.Count} tools automatically:");
        foreach (var toolEvent in mcpToolEvents)
        {
            Console.WriteLine($"   - {toolEvent.Summary}: {toolEvent.Details}");
        }
    }
}
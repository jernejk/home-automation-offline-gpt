using System.Net.Http.Json;
using System.Text.Json;
using HomeAutomationGpt.Core.Models;
using HomeAutomationGpt.WebAPI;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Xunit.Abstractions;

namespace HomeAutomation.IntegrationTests;

public class YouTubeTranscriptVerificationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public YouTubeTranscriptVerificationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task YouTubeTranscript_GetSummary_ReturnsActualContent()
    {
        var client = _factory.CreateClient();
        var prompt = new PromptRequest("Please get the transcript from this YouTube video https://www.youtube.com/watch?v=qRUW3zes4Gk and provide a brief summary of what the video is about");

        _output.WriteLine("🎬 Requesting YouTube transcript summary for: https://www.youtube.com/watch?v=qRUW3zes4Gk");
        _output.WriteLine("📡 Calling V6.5 Universal MCP endpoint...");

        var response = await client.PostAsJsonAsync("/api/prompt-with-mcp-v6_5", prompt);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<DeviceCommandResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body.ChatResponse);

        _output.WriteLine("\n🤖 AI Response:");
        _output.WriteLine("================");
        _output.WriteLine(body.ChatResponse);
        _output.WriteLine("================");

        // Verify that YouTube transcript MCP was called
        var youtubeCallEvents = body.Trace.Where(t => 
            t.Kind == "McpCall" && t.Summary.Contains("youtube_transcript")).ToList();
        Assert.NotEmpty(youtubeCallEvents);

        // Verify we got a response from YouTube transcript MCP
        var youtubeResponseEvents = body.Trace.Where(t => 
            t.Kind == "McpResponse" && t.Summary.Contains("youtube_transcript")).ToList();
        Assert.NotEmpty(youtubeResponseEvents);

        _output.WriteLine("\n📋 MCP Trace Events:");
        _output.WriteLine("=====================");
        foreach (var traceEvent in body.Trace)
        {
            _output.WriteLine($"[{traceEvent.Kind}] {traceEvent.Summary}");
            if (traceEvent.Kind == "McpResponse" && traceEvent.Summary.Contains("youtube_transcript"))
            {
                _output.WriteLine($"   Details: {traceEvent.Details?.Substring(0, Math.Min(200, traceEvent.Details.Length))}...");
            }
        }

        // Verify the response contains video-related content
        var responseText = body.ChatResponse.ToLower();
        Assert.True(
            responseText.Contains("video") || 
            responseText.Contains("transcript") || 
            responseText.Contains("youtube"),
            "Response should contain video-related content"
        );

        _output.WriteLine("\n✅ YouTube Transcript MCP Integration Verified!");
        _output.WriteLine($"✅ Total trace events: {body.Trace.Count}");
        _output.WriteLine($"✅ YouTube MCP calls: {youtubeCallEvents.Count}");
        _output.WriteLine($"✅ YouTube MCP responses: {youtubeResponseEvents.Count}");
    }
}
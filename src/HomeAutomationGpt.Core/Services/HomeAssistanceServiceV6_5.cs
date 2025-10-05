using HomeAutomationGpt.Core.Models;
using HomeAutomationGpt.Core.Mcp;
using Microsoft.Extensions.AI;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;

namespace HomeAutomationGpt.Core.Services;

/// <summary>
/// V6.5 - Universal MCP Service where ALL functionality is treated as MCP tools.
/// This eliminates the need to write proxy methods for each new MCP server.
/// Simply register new MCP servers in mcp.json and they'll be automatically available.
/// </summary>
public class HomeAssistanceServiceV6_5 : IHomeAssistanceService
{
    private readonly IChatClient _client;
    private readonly IReadOnlyDictionary<string, ModelContextProtocol.Client.IMcpClient> _mcpClients;
    private readonly LocalMcpProvider _localMcpProvider;

    public HomeAssistanceServiceV6_5(IChatClient client, IReadOnlyDictionary<string, ModelContextProtocol.Client.IMcpClient> mcpClients)
    {
        _client = client;
        _mcpClients = mcpClients;
        _localMcpProvider = new LocalMcpProvider();
    }

    private readonly List<DeviceAction> _actions = [];
    private readonly List<TraceEvent> _trace = [];

    public async Task<DeviceCommandResponse> ExecuteCommandAsync(string command, List<Device> devices, bool cleanUpJsonWell = true, string? systemPrompt = null)
    {
        _actions.Clear();
        _trace.Clear();

        try
        {
            // Initialize local provider with current devices and action list
            _localMcpProvider.SetDevices(devices);
            _localMcpProvider.SetActionsList(_actions);
            _localMcpProvider.SetTraceList(_trace);

            // Use universal MCP registry to load all tools dynamically
            var registry = new UniversalMcpToolRegistry(_mcpClients, _localMcpProvider, _trace);
            var toolbelt = await registry.LoadAllToolsAsync();

            string systemMessage = systemPrompt ??
                BuildSystemPrompt(devices, toolbelt.Count);

            _trace.Add(new TraceEvent
            {
                Kind = "SystemPrompt",
                Summary = "System prompt set for LLM",
                Details = systemMessage
            });

            var messages = new[]
            {
                new ChatMessage(ChatRole.System, systemMessage),
                new ChatMessage(ChatRole.User, command)
            };

            _trace.Add(new TraceEvent
            {
                Kind = "UserPrompt",
                Summary = "User command received",
                Details = command
            });

            _trace.Add(new TraceEvent
            {
                Kind = "LlmRequest",
                Summary = $"Sending request to LLM with {toolbelt.Count} universal MCP tools",
                Details = $"Tool count: {toolbelt.Count}"
            });

            var response = await _client.GetResponseAsync(messages, new ChatOptions
            {
                Tools = toolbelt
            });

            _trace.Add(new TraceEvent
            {
                Kind = "ModelResponse",
                Summary = "Model response with universal MCP integration",
                Details = response.ToString()
            });

            return new DeviceCommandResponse
            {
                ChatResponse = response.ToString(),
                DeviceActions = _actions,
                Trace = new List<TraceEvent>(_trace)
            };
        }
        catch (Exception ex)
        {
            _trace.Add(new TraceEvent
            {
                Kind = "Error",
                Summary = "Error in ExecuteCommandAsync",
                Details = ex.Message
            });

            return new DeviceCommandResponse
            {
                ChatResponse = $"Error processing request: {ex.Message}",
                DeviceActions = _actions,
                Trace = new List<TraceEvent>(_trace)
            };
        }
    }

    private string BuildSystemPrompt(List<Device> devices, int toolCount)
    {
        var deviceList = string.Join(", ", devices.Select(d => d.Name));
        
        return $@"You are a helpful smart home assistant with access to {toolCount} MCP tools for:
- Controlling smart home devices: {deviceList}
- Searching the web for information (DuckDuckGo)
- Fetching content from web pages
- Getting system status information

All functionality is available through MCP tools. Use them as needed to help the user.
When controlling devices, be specific about the action (On/Off/Set) and include values for Set actions.
For web searches, be concise but thorough in your queries.";
    }
}

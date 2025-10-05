
using HomeAutomationGpt.Core.Models;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;

namespace HomeAutomationGpt.Core.Services;

public class HomeAssistanceServiceV7 : IHomeAssistanceService
{
    private readonly IChatClient _client;
    private readonly IReadOnlyDictionary<string, IMcpClient> _mcpClients;

    public HomeAssistanceServiceV7(IChatClient client, IReadOnlyDictionary<string, IMcpClient> mcpClients)
    {
        _client = client;
        _mcpClients = mcpClients;
    }

    private readonly List<DeviceAction> _actions = [];
    private readonly List<TraceEvent> _trace = [];

    [Description("Controls a smart home device by performing the specified action.")]
    private string ExecuteDeviceAction(
        [Description("Name of the device to control (e.g., 'TV', 'A/C', 'Kitchen lights')")] string deviceName,
        [Description("Action to perform: 'On' to turn on, 'Off' to turn off, 'Set' to set a value")] string action,
        [Description("Optional numeric value for 'Set' actions (e.g., temperature: 23)")] float? value = null,
        [Description("Optional text message for 'Speak' actions")] string? text = null)
    {
        try
        {
            var deviceAction = new DeviceAction
            {
                Device = deviceName?.Trim() ?? string.Empty,
                Action = action?.Trim() ?? string.Empty,
                Value = value,
                Text = text?.Trim()
            };
            
            _actions.Add(deviceAction);
            _trace.Add(new TraceEvent { Kind = "ToolCall", Summary = "ExecuteDeviceAction invoked", Details = JsonSerializer.Serialize(deviceAction) });
            _trace.Add(new TraceEvent { Kind = "ActionQueued", Summary = $"{action} -> {deviceName}", Details = JsonSerializer.Serialize(deviceAction) });
            
            return $"Successfully executed {action} on {deviceName}" + (value.HasValue ? $" with value {value}" : "");
        }
        catch (Exception ex)
        {
            _trace.Add(new TraceEvent { Kind = "Error", Summary = "ExecuteDeviceAction failed", Details = ex.Message });
            return $"Failed to execute {action} on {deviceName}: {ex.Message}";
        }
    }

    [Description("Search the web using DuckDuckGo to get current information.")]
    private async Task<string> SearchWebAsync(
        [Description("The search query to look up")] string query)
    {
        try
        {
            if (!_mcpClients.TryGetValue("duckduckgo", out var mcpClient))
            {
                _actions.Add(new DeviceAction
                {
                    Device = "DuckDuckGo",
                    Action = "Error",
                    Text = $"Unable to perform search {query} - MCP server not connected."
                });

                return "DuckDuckGo search is not available - MCP server not connected.";
            }

            _trace.Add(new TraceEvent { Kind = "McpCall", Summary = "DuckDuckGo search initiated", Details = query });

            // Call the MCP tool (assuming the DuckDuckGo MCP server has a 'search' tool)
            var arguments = new Dictionary<string, object?>
            {
                ["query"] = query,
                ["max_results"] = 5
            };

            _actions.Add(new DeviceAction
            {
                Device = "DuckDuckGo",
                Action = "Search",
                Text = query
            });
            
            var result = await mcpClient.CallToolAsync("search", arguments);

            if (result.IsError == true)
            {
                var errorMessage = result.Content?.ToString() ?? "Unknown error";
                _trace.Add(new TraceEvent { Kind = "Error", Summary = "DuckDuckGo search failed", Details = errorMessage });
                return $"Search failed: {errorMessage}";
            }

            _trace.Add(new TraceEvent { Kind = "McpResponse", Summary = "DuckDuckGo search completed", Details = result.Content?.ToString() });
            
            // Extract and format the search results
            var searchResults = result.Content?.ToString() ?? "No results found.";
            return $"Search results for '{query}': {searchResults}";
        }
        catch (Exception ex)
        {
            _trace.Add(new TraceEvent { Kind = "Error", Summary = "DuckDuckGo search error", Details = ex.Message });
            return $"Search error: {ex.Message}";
        }
    }

    [Description("Get information about available MCP tools and their status.")]
    private async Task<string> GetMcpStatusAsync()
    {
        try
        {
            var statusInfo = new List<string>();
            
            foreach (var (serverId, mcpClient) in _mcpClients)
            {
                try
                {
                    var tools = await mcpClient.ListToolsAsync();
                    var toolNames = tools?.Select(t => t.Name) ?? Enumerable.Empty<string>();
                    statusInfo.Add($"{serverId}: Connected, Tools: [{string.Join(", ", toolNames)}]");
                }
                catch (Exception ex)
                {
                    statusInfo.Add($"{serverId}: Error - {ex.Message}");
                }
            }

            var status = string.Join("; ", statusInfo);
            _trace.Add(new TraceEvent { Kind = "McpStatus", Summary = "MCP status checked", Details = status });
            
            return $"MCP Servers Status: {status}";
        }
        catch (Exception ex)
        {
            _trace.Add(new TraceEvent { Kind = "Error", Summary = "MCP status check failed", Details = ex.Message });
            return $"Failed to get MCP status: {ex.Message}";
        }
    }

    [Description("Call Home Assistant API to get information or trigger actions.")]
    private async Task<string> CallHassApiAsync(
        [Description("The endpoint to call, for example /api/services/light/turn_on")] string endpoint,
        [Description("The method to use, for example GET, POST, etc.")] string method = "GET",
        [Description("The body of the request in JSON format")] string? body = null)
    {
        try
        {
            if (!_mcpClients.TryGetValue("hass-proxy", out var mcpClient))
            {
                return "Home Assistant is not available - MCP server not connected.";
            }

            _trace.Add(new TraceEvent { Kind = "McpCall", Summary = "Home Assistant call initiated", Details = $"{method} {endpoint}" });

            var arguments = new Dictionary<string, object?>
            {
                ["endpoint"] = endpoint,
                ["method"] = method,
                ["body"] = body
            };

            var result = await mcpClient.CallToolAsync("call_api", arguments);

            if (result.IsError == true)
            {
                var errorMessage = result.Content?.ToString() ?? "Unknown error";
                _trace.Add(new TraceEvent { Kind = "Error", Summary = "Home Assistant call failed", Details = errorMessage });
                return $"Home Assistant call failed: {errorMessage}";
            }

            var resultContent = result.Content?.ToString() ?? "No results found.";
            _trace.Add(new TraceEvent { Kind = "McpResponse", Summary = "Home Assistant call completed", Details = resultContent });
            return $"Home Assistant response: {resultContent}";
        }
        catch (Exception ex)
        {
            _trace.Add(new TraceEvent { Kind = "Error", Summary = "Home Assistant call error", Details = ex.Message });
            return $"Home Assistant call error: {ex.Message}";
        }
    }

    public async Task<DeviceCommandResponse> ExecuteCommandAsync(string command, List<Device> devices, bool cleanUpJsonWell = true, string? systemPrompt = null)
    {
        _actions.Clear();
        _trace.Clear();

        ChatOptions options = new()
        {
            Tools =
            [
                AIFunctionFactory.Create(
                    ExecuteDeviceAction,
                    "ExecuteDeviceAction"),
                AIFunctionFactory.Create(
                    SearchWebAsync,
                    "SearchWeb"),
                AIFunctionFactory.Create(
                    GetMcpStatusAsync,
                    "GetMcpStatus"),
                AIFunctionFactory.Create(
                    CallHassApiAsync,
                    "CallHassApi")
            ]
        };

        var sysPrompt = systemPrompt ?? (
            "You're a smart home assistant with web search and Home Assistant integration capabilities. " +
            $"Available devices: {string.Join(", ", devices.Select(d => d.Name))}. " +
            "You can: " +
            "1. Control local devices using ExecuteDeviceAction (actions: 'On', 'Off', 'Set' with value for temperature) " +
            "2. Search the web using SearchWeb for current information, news, weather, etc. " +
            "3. Check MCP server status using GetMcpStatus " +
            "4. Call Home Assistant API using CallHassApi. " +
            "For multiple actions, call functions multiple times. " +
            "Always explain what you're doing and combine web search results with device control when helpful.");

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, sysPrompt),
            new(ChatRole.User, command)
        };

        _trace.Add(new TraceEvent { Kind = "SystemPrompt", Summary = "System prompt sent", Details = sysPrompt });
        _trace.Add(new TraceEvent { Kind = "UserPrompt", Summary = "User command", Details = command });

        var response = await _client.GetResponseAsync(messages, options);
        _trace.Add(new TraceEvent { Kind = "ModelResponse", Summary = "Model response with MCP integration", Details = response.ToString() });

        return new DeviceCommandResponse
        {
            ChatResponse = response.ToString(),
            DeviceActions = _actions,
            Trace = new List<TraceEvent>(_trace)
        };
    }
}

using HomeAutomationGpt.Core.Models;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;

namespace HomeAutomationGpt.Core.Services;

public class HomeAssistanceServiceV6 : IHomeAssistanceService
{
    private readonly IChatClient _client;
    private readonly IReadOnlyDictionary<string, IMcpClient> _mcpClients;

    public HomeAssistanceServiceV6(IChatClient client, IReadOnlyDictionary<string, IMcpClient> mcpClients)
    {
        _client = client;
        _mcpClients = mcpClients;
    }

    private readonly List<DeviceAction> _actions = [];
    private readonly List<TraceEvent> _trace = [];
    private readonly Dictionary<string, McpClientTool> _mcpToolLookup = new();

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

    [Description("Search the web using DuckDuckGo MCP service.")]
    private async Task<string> SearchMcpAsync(
        [Description("The search query to look up")] string query,
        [Description("Maximum number of results to return")] int max_results = 5)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["query"] = query,
            ["max_results"] = max_results
        };
        
        return await CallMcpToolAsync("search", parameters);
    }

    [Description("Fetch content from a webpage URL using DuckDuckGo MCP service.")]
    private async Task<string> FetchContentMcpAsync(
        [Description("The webpage URL to fetch content from")] string url)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["url"] = url
        };
        
        return await CallMcpToolAsync("fetch_content", parameters);
    }

    public async Task<DeviceCommandResponse> ExecuteCommandAsync(string command, List<Device> devices, bool cleanUpJsonWell = true, string? systemPrompt = null)
    {
        _actions.Clear();
        _trace.Clear();
        _mcpToolLookup.Clear();

        var mcpTools = await LoadMcpToolsAsync();

        List<AITool> toolbelt =
        [
            AIFunctionFactory.Create(
                ExecuteDeviceAction,
                "ExecuteDeviceAction"),
            AIFunctionFactory.Create(
                GetMcpStatusAsync,
                "GetMcpStatus")
        ];

        if (mcpTools.Count > 0)
        {
            // Add specific MCP tool wrappers based on known tools
            foreach (var mcpTool in mcpTools)
            {
                _mcpToolLookup[mcpTool.Name] = mcpTool;
                
                // Create wrappers for known MCP tools
                if (mcpTool.Name == "search")
                {
                    toolbelt.Add(AIFunctionFactory.Create(SearchMcpAsync, "search"));
                }
                else if (mcpTool.Name == "fetch_content")
                {
                    toolbelt.Add(AIFunctionFactory.Create(FetchContentMcpAsync, "fetch_content"));
                }
                // Add more tool wrappers as needed
            }
        }

        ChatOptions options = new()
        {
            Tools = toolbelt
        };

        var sysPrompt = systemPrompt ?? BuildSystemPrompt(devices, mcpTools);

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

    private async Task<List<McpClientTool>> LoadMcpToolsAsync()
    {
        var tools = new List<McpClientTool>();

        foreach (var (serverId, mcpClient) in _mcpClients)
        {
            try
            {
                var advertised = await mcpClient.ListToolsAsync();

                foreach (var tool in advertised)
                {
                    tools.Add(tool);
                    _trace.Add(new TraceEvent
                    {
                        Kind = "McpToolAvailable",
                        Summary = $"{serverId}:{tool.Name}",
                        Details = tool.Description ?? string.Empty
                    });
                }
            }
            catch (Exception ex)
            {
                _trace.Add(new TraceEvent
                {
                    Kind = "Error",
                    Summary = $"Failed to enumerate MCP tools for {serverId}",
                    Details = ex.Message
                });
            }
        }

        return tools;
    }

    private async Task<string> CallMcpToolAsync(string toolName, IDictionary<string, object?> parameters)
    {
        try
        {
            // Find the MCP client that has this tool
            foreach (var (serverId, mcpClient) in _mcpClients)
            {
                try
                {
                    var serverTools = await mcpClient.ListToolsAsync();
                    if (serverTools.Any(t => t.Name == toolName))
                    {
                        _trace.Add(new TraceEvent 
                        { 
                            Kind = "McpCall", 
                            Summary = $"Calling MCP tool: {toolName}", 
                            Details = JsonSerializer.Serialize(parameters) 
                        });

                        var readOnlyParams = new Dictionary<string, object?>(parameters);
                        var result = await mcpClient.CallToolAsync(toolName, readOnlyParams);
                        
                        // Extract the actual content from the MCP response
                        string responseContent = "No response from MCP tool";
                        if (result.Content != null)
                        {
                            // Handle different content types - could be ContentBlock list or string
                            if (result.Content is IEnumerable<object> contentBlocks)
                            {
                                var textParts = new List<string>();
                                foreach (var block in contentBlocks)
                                {
                                    if (block != null)
                                    {
                                        // Try to extract text content from ContentBlock
                                        var blockText = ExtractTextFromContentBlock(block);
                                        if (!string.IsNullOrEmpty(blockText))
                                        {
                                            textParts.Add(blockText);
                                        }
                                    }
                                }
                                responseContent = string.Join("\n", textParts);
                            }
                            else
                            {
                                responseContent = result.Content.ToString() ?? "No content";
                            }
                        }
                        
                        _trace.Add(new TraceEvent 
                        { 
                            Kind = "McpResponse", 
                            Summary = $"MCP tool response: {toolName}", 
                            Details = responseContent 
                        });

                        return responseContent;
                    }
                }
                catch (Exception ex)
                {
                    _trace.Add(new TraceEvent 
                    { 
                        Kind = "Error", 
                        Summary = $"MCP tool call failed: {toolName}", 
                        Details = ex.Message 
                    });
                }
            }

            return $"MCP tool '{toolName}' not found or unavailable";
        }
        catch (Exception ex)
        {
            _trace.Add(new TraceEvent 
            { 
                Kind = "Error", 
                Summary = $"MCP tool wrapper error: {toolName}", 
                Details = ex.Message 
            });
            return $"Error calling MCP tool '{toolName}': {ex.Message}";
        }
    }

    private static string ExtractTextFromContentBlock(object contentBlock)
    {
        try
        {
            // Use reflection to extract text content from ContentBlock
            var type = contentBlock.GetType();
            
            // Look for common text properties
            var textProperty = type.GetProperty("Text");
            if (textProperty != null)
            {
                var textValue = textProperty.GetValue(contentBlock);
                return textValue?.ToString() ?? string.Empty;
            }
            
            // Look for content property
            var contentProperty = type.GetProperty("Content");
            if (contentProperty != null)
            {
                var contentValue = contentProperty.GetValue(contentBlock);
                return contentValue?.ToString() ?? string.Empty;
            }
            
            // Fallback to ToString()
            return contentBlock.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string BuildSystemPrompt(IEnumerable<Device> devices, IReadOnlyList<McpClientTool> mcpTools)
    {
        var deviceList = string.Join(", ", devices.Select(d => d.Name));

        var capabilities = new List<string>
        {
            "1. Control devices using ExecuteDeviceAction (actions: 'On', 'Off', 'Set' with value for temperature)."
        };

        if (mcpTools.Count > 0)
        {
            capabilities.Add($"2. Use available MCP tools ({string.Join(", ", mcpTools.Select(t => t.Name))}) when you need external information or actions.");
            capabilities.Add("3. Call GetMcpStatus when you need to report which MCP servers or tools are connected.");
        }
        else
        {
            capabilities.Add("2. Use GetMcpStatus if the user asks about MCP availability; otherwise stay focused on local device control.");
        }

        return
            "You're a smart home assistant with function calling and MCP connectivity. " +
            $"Available devices: {deviceList}. " +
            string.Join(' ', capabilities) +
            " For multiple actions, call functions multiple times and explain each step.";
    }
}

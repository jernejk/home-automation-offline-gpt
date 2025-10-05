using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using System.ComponentModel;
using System.Text.Json;
using HomeAutomationGpt.Core.Models;

namespace HomeAutomationGpt.Core.Mcp;

/// <summary>
/// Universal MCP tool registry that can dynamically create AI function wrappers for any MCP tool
/// without requiring specific proxy methods
/// </summary>
public class UniversalMcpToolRegistry
{
    private readonly IReadOnlyDictionary<string, IMcpClient> _externalMcpClients;
    private readonly LocalMcpProvider _localMcpProvider;
    private readonly List<TraceEvent> _trace;
    private readonly Dictionary<string, object> _allTools = new(); // Tool name -> tool info
    private readonly Dictionary<string, string> _toolToServerMapping = new();

    public UniversalMcpToolRegistry(
        IReadOnlyDictionary<string, IMcpClient> externalMcpClients,
        LocalMcpProvider localMcpProvider,
        List<TraceEvent> trace)
    {
        _externalMcpClients = externalMcpClients;
        _localMcpProvider = localMcpProvider;
        _trace = trace;
    }

    public async Task<List<AITool>> LoadAllToolsAsync()
    {
        var aiTools = new List<AITool>();
        
        // Load local tools
        var localTools = _localMcpProvider.GetAvailableTools();
        foreach (var (name, description) in localTools)
        {
            _allTools[name] = (name, description);
            _toolToServerMapping[name] = "local";
            
            _trace.Add(new TraceEvent
            {
                Kind = "McpToolAvailable",
                Summary = $"local:{name}",
                Details = description
            });

            // Create dynamic AI tool wrapper
            aiTools.Add(CreateUniversalToolWrapper(name, description));
        }

        // Load external MCP tools
        foreach (var (serverId, mcpClient) in _externalMcpClients)
        {
            try
            {
                var serverTools = await mcpClient.ListToolsAsync();
                foreach (var tool in serverTools)
                {
                    _allTools[tool.Name] = tool;
                    _toolToServerMapping[tool.Name] = serverId;
                    
                    _trace.Add(new TraceEvent
                    {
                        Kind = "McpToolAvailable",
                        Summary = $"{serverId}:{tool.Name}",
                        Details = tool.Description ?? string.Empty
                    });

                    // Create dynamic AI tool wrapper
                    aiTools.Add(CreateUniversalToolWrapper(tool.Name, tool.Description ?? string.Empty));
                }
            }
            catch (Exception ex)
            {
                _trace.Add(new TraceEvent
                {
                    Kind = "Error",
                    Summary = $"Failed to load tools from {serverId}",
                    Details = ex.Message
                });
            }
        }

        return aiTools;
    }

    private AITool CreateUniversalToolWrapper(string toolName, string description)
    {
        // Create a universal async wrapper function that properly handles tool parameters
        if (toolName == "get_transcript" || toolName == "get_timed_transcript" || toolName == "get_video_info")
        {
            // YouTube transcript tools need URL parameter
            async Task<string> YouTubeToolFunction(
                [Description("The YouTube video URL")] string url)
            {
                var parameters = new Dictionary<string, object?> { ["url"] = url };
                return await CallToolAsync(toolName, parameters);
            }
            
            return AIFunctionFactory.Create(YouTubeToolFunction, toolName, description);
        }
        else if (toolName == "search")
        {
            // DuckDuckGo search tool
            async Task<string> SearchToolFunction(
                [Description("The search query")] string query,
                [Description("Maximum results to return")] int max_results = 10)
            {
                var parameters = new Dictionary<string, object?> 
                { 
                    ["query"] = query,
                    ["max_results"] = max_results
                };
                return await CallToolAsync(toolName, parameters);
            }
            
            return AIFunctionFactory.Create(SearchToolFunction, toolName, description);
        }
        else if (toolName == "fetch_content")
        {
            // DuckDuckGo fetch content tool
            async Task<string> FetchContentToolFunction(
                [Description("The webpage URL to fetch content from")] string url)
            {
                var parameters = new Dictionary<string, object?> { ["url"] = url };
                return await CallToolAsync(toolName, parameters);
            }
            
            return AIFunctionFactory.Create(FetchContentToolFunction, toolName, description);
        }
        else
        {
            // Generic tool wrapper for tools without parameters or local tools
            async Task<string> GenericToolFunction(IDictionary<string, object?>? parameters = null)
            {
                return await CallToolAsync(toolName, parameters ?? new Dictionary<string, object?>());
            }

            return AIFunctionFactory.Create(GenericToolFunction, toolName, description);
        }
    }

    public async Task<string> CallToolAsync(string toolName, IDictionary<string, object?> parameters)
    {
        try
        {
            if (!_toolToServerMapping.TryGetValue(toolName, out var serverId))
            {
                return $"Tool '{toolName}' not found in registry";
            }

            _trace.Add(new TraceEvent
            {
                Kind = "McpCall",
                Summary = $"Calling MCP tool: {toolName} on {serverId}",
                Details = JsonSerializer.Serialize(parameters)
            });

            string result;

            if (serverId == "local")
            {
                // Call local MCP provider
                result = await _localMcpProvider.CallToolAsync(toolName, parameters);
            }
            else
            {
                // Call external MCP client
                if (!_externalMcpClients.TryGetValue(serverId, out var mcpClient))
                {
                    return $"MCP server '{serverId}' not found";
                }

                var readOnlyParams = new Dictionary<string, object?>(parameters);
                var mcpResult = await mcpClient.CallToolAsync(toolName, readOnlyParams);
                
                // Extract content from MCP response
                result = ExtractContentFromMcpResponse(mcpResult);
            }

            _trace.Add(new TraceEvent
            {
                Kind = "McpResponse",
                Summary = $"MCP tool response: {toolName} from {serverId}",
                Details = result
            });

            return result;
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error calling MCP tool '{toolName}': {ex.Message}";
            _trace.Add(new TraceEvent
            {
                Kind = "Error",
                Summary = $"MCP tool call failed: {toolName}",
                Details = errorMsg
            });
            return errorMsg;
        }
    }

    private static string ExtractContentFromMcpResponse(object mcpResult)
    {
        try
        {
            // Use reflection to get Content property
            var resultType = mcpResult.GetType();
            var contentProperty = resultType.GetProperty("Content");
            
            if (contentProperty == null)
            {
                return mcpResult.ToString() ?? "No content";
            }

            var content = contentProperty.GetValue(mcpResult);
            if (content == null)
            {
                return "No content";
            }

            // Handle different content types
            if (content is IEnumerable<object> contentBlocks)
            {
                var textParts = new List<string>();
                foreach (var block in contentBlocks)
                {
                    if (block != null)
                    {
                        var blockText = ExtractTextFromContentBlock(block);
                        if (!string.IsNullOrEmpty(blockText))
                        {
                            textParts.Add(blockText);
                        }
                    }
                }
                return string.Join("\n", textParts);
            }

            return content.ToString() ?? "No content";
        }
        catch (Exception ex)
        {
            return $"Error extracting MCP response content: {ex.Message}";
        }
    }

    private static string ExtractTextFromContentBlock(object contentBlock)
    {
        try
        {
            var type = contentBlock.GetType();
            
            // Look for common text properties
            var textProperty = type.GetProperty("Text");
            if (textProperty != null)
            {
                var textValue = textProperty.GetValue(contentBlock);
                return textValue?.ToString() ?? string.Empty;
            }
            
            var contentProperty = type.GetProperty("Content");
            if (contentProperty != null)
            {
                var contentValue = contentProperty.GetValue(contentProperty);
                return contentValue?.ToString() ?? string.Empty;
            }
            
            return contentBlock.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public List<object> GetAllTools()
    {
        return _allTools.Values.ToList();
    }
}
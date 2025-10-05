using HomeAutomationGpt.Core.Models;
using ModelContextProtocol.Client;
using System.Text.Json;

namespace HomeAutomationGpt.Core.Mcp;

/// <summary>
/// A local MCP-like provider for internal tools (device actions, status, etc.)
/// This can be extended to use shared memory, files, or other mechanisms for tool communication
/// </summary>
public class LocalMcpProvider
{
    private List<DeviceAction> _actions = [];
    private List<TraceEvent> _trace = [];
    private List<Device> _devices = [];

    // Default constructor
    public LocalMcpProvider()
    {
    }

    // Constructor with dependencies
    public LocalMcpProvider(List<DeviceAction> actions, List<TraceEvent> trace, List<Device> devices)
    {
        _actions = actions;
        _trace = trace;
        _devices = devices;
    }

    // Setters for dependency injection
    public void SetDevices(List<Device> devices) => _devices = devices;
    public void SetActionsList(List<DeviceAction> actions) => _actions = actions;
    public void SetTraceList(List<TraceEvent> trace) => _trace = trace;

    public List<(string Name, string Description)> GetAvailableTools()
    {
        return new List<(string, string)>
        {
            ("ExecuteDeviceAction", 
             $"Controls smart home devices. Available devices: {string.Join(", ", _devices.Select(d => d.Name))}. Actions: 'On', 'Off', 'Set' with value for temperature."),
            ("GetMcpStatus", 
             "Gets the status of MCP tools and available devices")
        };
    }

    public async Task<string> CallToolAsync(string toolName, IDictionary<string, object?> parameters)
    {
        await Task.CompletedTask; // Remove warning
        return toolName switch
        {
            "ExecuteDeviceAction" => await ExecuteDeviceActionAsync(parameters),
            "GetMcpStatus" => await GetMcpStatusAsync(),
            _ => $"Unknown local MCP tool: {toolName}"
        };
    }

    private async Task<string> ExecuteDeviceActionAsync(IDictionary<string, object?> parameters)
    {
        try
        {
            var deviceName = parameters.TryGetValue("deviceName", out var nameValue) ? nameValue?.ToString() ?? string.Empty : string.Empty;
            var action = parameters.TryGetValue("action", out var actionValue) ? actionValue?.ToString() ?? string.Empty : string.Empty;
            var value = parameters.TryGetValue("value", out var valueParam) ? valueParam as float? : null;
            var text = parameters.TryGetValue("text", out var textValue) ? textValue?.ToString() : null;

            if (string.IsNullOrEmpty(deviceName))
            {
                return "Device name is required";
            }

            if (string.IsNullOrEmpty(action))
            {
                return "Action is required";
            }

            var deviceAction = new DeviceAction
            {
                Device = deviceName,
                Action = action,
                Value = value,
                Text = text
            };

            _actions.Add(deviceAction);

            _trace.Add(new TraceEvent
            {
                Kind = "DeviceAction",
                Summary = $"Device action executed: {deviceName} {action}",
                Details = JsonSerializer.Serialize(deviceAction)
            });

            // Simulate device response
            var response = action.ToLower() switch
            {
                "on" => $"{deviceName} turned on successfully",
                "off" => $"{deviceName} turned off successfully",
                "set" when value.HasValue => $"{deviceName} set to {value.Value}",
                "speak" when !string.IsNullOrEmpty(text) => $"{deviceName} speaking: {text}",
                _ => $"Unknown action {action} for {deviceName}"
            };

            return response;
        }
        catch (Exception ex)
        {
            return $"Error executing device action: {ex.Message}";
        }
    }

    private async Task<string> GetMcpStatusAsync()
    {
        await Task.CompletedTask; // Remove warning
        try
        {
            var status = new
            {
                AvailableDevices = _devices.Select(d => new { d.Name }).ToList(),
                ExecutedActions = _actions.Count,
                LocalTools = GetAvailableTools().Select(t => new { t.Name, Description = t.Item2 }).ToList()
            };

            return JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error getting MCP status: {ex.Message}";
        }
    }
}
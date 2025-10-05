using HomeAutomationGpt.Core.Models;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace HomeAutomationGpt.Core.Services;

public class HomeAssistanceServiceV5(IChatClient client) : IHomeAssistanceService
{
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
            _trace.Add(new TraceEvent { Kind = "ToolCall", Summary = "ExecuteDeviceAction invoked", Details = System.Text.Json.JsonSerializer.Serialize(deviceAction) });
            _trace.Add(new TraceEvent { Kind = "ActionQueued", Summary = $"{action} -> {deviceName}", Details = System.Text.Json.JsonSerializer.Serialize(deviceAction) });
            
            return $"Successfully executed {action} on {deviceName}" + (value.HasValue ? $" with value {value}" : "");
        }
        catch (Exception ex)
        {
            _trace.Add(new TraceEvent { Kind = "Error", Summary = "ExecuteDeviceAction failed", Details = ex.Message });
            return $"Failed to execute {action} on {deviceName}: {ex.Message}";
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
                    "ExecuteDeviceAction")
            ]
        };

        var sysPrompt = systemPrompt ?? (
            "You're a smart home assistant. Use ExecuteDeviceAction to control devices. " +
            $"Available devices: {string.Join(", ", devices.Select(d => d.Name))}. " +
            "Actions: 'On' to turn on, 'Off' to turn off, 'Set' to set values (include numeric value parameter). " +
            "For temperature controls like A/C, use 'Set' action with value parameter (e.g., value=23). " +
            "Always call ExecuteDeviceAction for each device operation. " +
            "If multiple devices need control, make multiple function calls.");
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, sysPrompt),
            new(ChatRole.User, command)
        };

        _trace.Add(new TraceEvent { Kind = "SystemPrompt", Summary = "System prompt sent", Details = sysPrompt });
        _trace.Add(new TraceEvent { Kind = "UserPrompt", Summary = "User command", Details = command });

        var response = await client.GetResponseAsync(messages, options);
        _trace.Add(new TraceEvent { Kind = "ModelResponse", Summary = "Model response after tool execution", Details = response.ToString() });

        return new DeviceCommandResponse
        {
            ChatResponse = response.ToString(),
            DeviceActions = _actions,
            Trace = new List<TraceEvent>(_trace)
        };
    }
}

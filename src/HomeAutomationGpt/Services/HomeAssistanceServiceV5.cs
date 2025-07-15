using HomeAutomationGpt.Models;
using Microsoft.Extensions.AI;

namespace HomeAutomationGpt.Services;

public class HomeAssistanceServiceV5 : IHomeAssistanceService
{
    private readonly IChatClient _client;
    private readonly List<DeviceAction> _actions = new();

    private string ExecuteDeviceAction(string deviceName, string action, float? value, string? text)
    {
        _actions.Add(new DeviceAction
        {
            Device = deviceName,
            Action = action,
            Value = value,
            Text = text
        });

        return $"Executed {action} on {deviceName}";
    }

    // Accept IChatClient via dependency injection
    public HomeAssistanceServiceV5(IChatClient client)
    {
        _client = client;
    }

    public async Task<DeviceCommandResponse> ExecuteCommandAsync(string command, List<Device> devices, bool cleanUpJsonWell = true)
    {
        _actions.Clear();

        ChatOptions options = new()
        {
            Tools =
            [
                AIFunctionFactory.Create(
                    ExecuteDeviceAction,
                    "ExecuteDeviceAction",
                    "Controls a device by performing the specified action with optional parameters.")
            ]
        };

        var systemPrompt = "You're a home assistant AI. Use ExecuteDeviceAction to control devices.";
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, command)
        };

        var response = await _client.GetResponseAsync(messages, options);

        return new DeviceCommandResponse
        {
            ChatResponse = response.ToString(),
            DeviceActions = _actions
        };
    }
}

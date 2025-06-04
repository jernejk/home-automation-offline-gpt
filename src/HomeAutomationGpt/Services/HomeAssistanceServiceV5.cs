using HomeAutomationGpt.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.OpenAI;
using OpenAI.Chat;

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

    public HomeAssistanceServiceV5()
    {
        var chatClient = new ChatClient(new Uri(HomeAssistanceService.ChatUrl), "model-identifier");
        _client = new OpenAIChatClient(chatClient).AsIChatClient();
    }

    public async Task<DeviceCommandResponse> ExecuteCommandAsync(string command, List<Device> devices, bool cleanUpJsonWell = true)
    {
        _actions.Clear();

        ChatOptions options = new()
        {
            Tools =
            [
                AIFunctionFactory.Create(ExecuteDeviceAction,
                "ExecuteDeviceAction",
                "Controls a device by performing the specified action with optional parameters.")
            ]
        };

        var systemPrompt = "You're a home assistant AI. Use ExecuteDeviceAction to control devices.";
        var messages = new[]
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, command)
        };

        var response = await _client.GetResponseAsync(messages, options);

        return new DeviceCommandResponse
        {
            ChatResponse = response.Message,
            DeviceActions = _actions
        };
    }
}

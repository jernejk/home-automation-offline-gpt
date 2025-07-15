using HomeAutomationGpt.Models;
using Microsoft.Extensions.AI;

namespace HomeAutomationGpt.Services;

public class HomeAssistanceServiceV4 : IHomeAssistanceService
{
    private readonly IChatClient _client;

    // Accept IChatClient via dependency injection
    public HomeAssistanceServiceV4(IChatClient client)
    {
        _client = client;
    }

    public async Task<DeviceCommandResponse> ExecuteCommandAsync(string command, List<Device> devices, bool cleanUpJsonWell = true)
    {
        var systemPrompt = "You're a home assistant AI. Here are the list of supported devices: " +
                            string.Join(", ", devices.Select(d => d.Name)) + "." +
                            "Available commands: On, Off, Speak, SetValue";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, command)
        };

        ChatOptions options = new();
        var response = await _client.GetResponseAsync(messages, options);

        // Pass the response back to the existing parser
        // Use response.ToString() as the chat response content
        return await new HomeAssistanceService().ExecuteCommandAsync(response.ToString(), devices, cleanUpJsonWell);
    }
}

using HomeAutomationGpt.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.OpenAI;
using OpenAI.Chat;

namespace HomeAutomationGpt.Services;

public class HomeAssistanceServiceV4 : IHomeAssistanceService
{
    private readonly IChatClient _client;

    public HomeAssistanceServiceV4()
    {
        // Use the new OpenAI client targeting the local LM Studio server
        var chatClient = new ChatClient(new Uri(HomeAssistanceService.ChatUrl), "model-identifier");
        _client = new OpenAIChatClient(chatClient).AsIChatClient();
    }

    public async Task<DeviceCommandResponse> ExecuteCommandAsync(string command, List<Device> devices, bool cleanUpJsonWell = true)
    {
        var systemPrompt = "You're a home assistant AI. Here are the list of supported devices: " +
                            string.Join(", ", devices.Select(d => d.Name)) + "." +
                            "Available commands: On, Off, Speak, SetValue";

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, command)
        };

        ChatOptions options = new();
        var response = await _client.GetResponseAsync(messages, options);

        // Pass the response back to the existing parser
        return await new HomeAssistanceService().ExecuteCommandAsync(response.Message, devices, cleanUpJsonWell);
    }
}

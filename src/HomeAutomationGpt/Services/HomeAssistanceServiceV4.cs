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

    public async Task<DeviceCommandResponse> ExecuteCommandAsync(string command, List<Device> devices, bool cleanUpJsonWell = true, string? systemPrompt = null)
    {
        var trace = new List<TraceEvent>();
        var sysPrompt = systemPrompt ?? (
            "You're a home assistant AI. Here are the list of supported devices: " +
            string.Join(", ", devices.Select(d => d.Name)) + ". " +
            "Available commands: On, Off, Speak, SetValue\n" +
            "You always reply only with a JSON response with a list of commands, do not add any additional text. Example: \n" +
            "```json" +
            "[{ \"Device\": \"TV\", \"Action\": \"On\" }, {\"Action\": \"Speak\", \"Text\": \"Dinner is ready\"}, {\"Action\":\"SetValue\", \"Device\": \"A/C\", \"Value\": 18 }]" +
            "```");
        trace.Add(new TraceEvent { Kind = "SystemPrompt", Summary = "System prompt sent", Details = sysPrompt });
        trace.Add(new TraceEvent { Kind = "UserPrompt", Summary = "User command", Details = command });

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, sysPrompt),
            new(ChatRole.User, command)
        };

        ChatOptions options = new()
        {
            Temperature = 0.6f
        };
        var response = await _client.GetResponseAsync(messages, options);
        trace.Add(new TraceEvent { Kind = "ModelResponse", Summary = "Model response (to be parsed)", Details = response.ToString() });

        // Pass the response back to the existing parser
        // Use response.ToString() as the chat response content
        var parsed = await new HomeAssistanceService().ExecuteCommandAsync(response.ToString(), devices, cleanUpJsonWell);
        if (parsed.Trace != null) trace.AddRange(parsed.Trace);
        parsed.Trace = trace;
        return parsed;
    }
}

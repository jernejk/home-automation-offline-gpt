using HomeAutomationGpt.Models;
using System.Text.Json;
using System.Text;

namespace HomeAutomationGpt.Services;

public class HomeAssistanceService : IHomeAssistanceService
{
    public const string ChatUrl = "http://localhost:1234/v1/chat/completions";

    public async Task<DeviceCommandResponse> ExecuteCommandAsync(string command, List<Device> devices, bool cleanUpJsonWell = true)
    {
        var systemContent = "You're a home assistant AI. Here are the list of supported devices: " +
                            string.Join(", ", devices.Select(d => d.Name)) + ". " +
                            "Available commands: On, Off, Speak, SetValue\n" +
                            "You always reply only with a JSON response with a list of commands, do not add any additional text. Example: \n" +
                            "```json" +
                            "[{ \"Device\": \"TV\", \"Action\": \"On\" }, {\"Action\": \"Speak\", \"Text\": \"Dinner is ready\"}, {\"Action\":\"SetValue\", \"Device\": \"A/C\", \"Value\": 18 }]" +
                            "```";

        var requestContent = new
        {
            model = "model-identifier",
            messages = new[]
            {
                new { role = "system", content = systemContent },
                new { role = "user", content = command }
            },
            temperature = 0.6,
            max_tokens = -1,
            stream = false
        };

        StringContent jsonContent = new(JsonSerializer.Serialize(requestContent), Encoding.UTF8, "application/json");

        using HttpClient httpClient = new();
        HttpResponseMessage response = await httpClient.PostAsync(ChatUrl, jsonContent);

        if (!response.IsSuccessStatusCode)
        {
            return new() { Errors = $"Error: {response.ReasonPhrase}" };
        }

        string? responseContent = await response.Content.ReadAsStringAsync();

        // For demo purposes, allow simple clean up to demonstrate how chaotic LLMs can be.
        string? deviceActionJson = cleanUpJsonWell
            ? JsonCleanUp(responseContent)
            : SimpleCleanUp(responseContent);

        if (string.IsNullOrWhiteSpace(deviceActionJson))
        {
            return new()
            {
                ChatResponse = deviceActionJson,
                Errors = "Incorrect response from chat API"
            };
        }

        try
        {
            List<DeviceAction>? deviceActions = JsonSerializer.Deserialize<List<DeviceAction>>(deviceActionJson);

            return new()
            {
                ChatResponse = deviceActionJson,
                DeviceActions = deviceActions
            };
        }
        catch (Exception ex)
        {

            return new()
            {
                ChatResponse = deviceActionJson,
                Errors = ex.Message
            };
        }
    }

    private static string? JsonCleanUp(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        // Fetch response from chat.
        ChatResponse? response = JsonSerializer.Deserialize<ChatResponse>(content);
        if (response?.choices.Any() != true && string.IsNullOrWhiteSpace(response?.choices[0]?.message?.content))
        {
            return null;
        }

        content = response.choices[0].message.content;

        // Clean up the response content to get valid JSON array.
        // Some SLMs/LLMs makes odd formatting for JSON response.
        content = content.Trim().Replace("\\\"", "\"");
        if (content.Contains("```") || content.Contains("`` "))
        {
            // Gemma 2B sometimes talks before "```json" because why should it follow instructions?
            content = ClearBeforeTag(content, ["```json", "`` json"]);

            content = content
                // Phi-3 prefers "```json"
                .Replace("```json", "")
                // Llama 3.1 8B and Gemma 2B for some reason prefers "`` json"
                .Replace("`` json", "");

            // Delete everything after ```
            int index = content.IndexOf("```", 1, StringComparison.OrdinalIgnoreCase);
            if (index > 1)
            {
                content = content[..index];
            }

            // Llama 3.1 prefers "```" instead of "```json"
            content = content.Replace("```", string.Empty);
        }

        content = content.Replace("\n", "")
            .Trim()
            .TrimStart('=')
            // Sometimes Llama 3.1 responds with "`" around JSON.
            .Trim('`')
            .Trim()
            // Sometimes it doesn't respond as a list of device actions.
            // This will remove array characters and later add them back.
            .TrimStart('[')
            .TrimEnd(']');

        content = $"[{content}]";
        return content;
    }

    private static string SimpleCleanUp(string content)
    {
        // LLM Studio like to contain JSON responses within ```
        if (content.Contains("```"))
        {
            content = content.Replace("```json", "");

            // Delete everything after ```
            var index = content.IndexOf("```", StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                content = content.Substring(0, index);
            }
        }

        return content;
    }

    private static string ClearBeforeTag(string content, string[] tags)
    {
        foreach (string tag in tags)
        {
            int index = content.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                index += tag.Length;
                content = content[index..];
            }
        }

        return content;
    }
}

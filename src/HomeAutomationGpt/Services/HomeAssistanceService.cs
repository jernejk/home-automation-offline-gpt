using HomeAutomationGpt.Models;
using HomeAutomationGpt.Utils;
using System.Text.Json;
using System.Text;

namespace HomeAutomationGpt.Services;

public class HomeAssistanceService : IHomeAssistanceService
{
    public static string ChatUrl = "http://localhost:1234/v1/chat/completions";
    public static Dictionary<string, string>? AdditionalHeaders { get; set; }

    public async Task<DeviceCommandResponse> ExecuteCommandAsync(string command, List<Device> devices, bool cleanUpJsonWell = true, string? systemPrompt = null)
    {
        var trace = new List<TraceEvent>();
        var systemContent = systemPrompt ?? (
                            "You're a home assistant AI. Here are the list of supported devices: " +
                            string.Join(", ", devices.Select(d => d.Name)) + ". " +
                            "Available commands: On, Off, Speak, SetValue\n" +
                            "You always reply only with a JSON response with a list of commands, do not add any additional text. Example: \n" +
                            "```json" +
                            "[{ \"Device\": \"TV\", \"Action\": \"On\" }, {\"Action\": \"Speak\", \"Text\": \"Dinner is ready\"}, {\"Action\":\"SetValue\", \"Device\": \"A/C\", \"Value\": 18 }]" +
                            "```");
        trace.Add(new TraceEvent { Kind = "SystemPrompt", Summary = "System prompt sent", Details = systemContent });
        trace.Add(new TraceEvent { Kind = "UserPrompt", Summary = "User command", Details = command });

        var requestContent = new
        {
            model = "qwen/qwen3-coder-30b",
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
        using var req = new HttpRequestMessage(HttpMethod.Post, ChatUrl) { Content = jsonContent };
        if (AdditionalHeaders != null)
        {
            foreach (var kv in AdditionalHeaders)
            {
                // Avoid duplicates
                if (!req.Headers.TryAddWithoutValidation(kv.Key, kv.Value))
                {
                    req.Content?.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                }
            }
        }
        HttpResponseMessage response = await httpClient.SendAsync(req);

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync();
            string userFriendlyError = ApiErrorParser.ParseErrorResponse(errorContent, 
                $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            return new() { Errors = userFriendlyError };
        }

        string? responseContent = await response.Content.ReadAsStringAsync();
        trace.Add(new TraceEvent { Kind = "ModelResponse", Summary = "Raw model response", Details = responseContent });

        // Check if the response contains an error object even with HTTP 200
        if (responseContent.Contains("\"error\"") && responseContent.Contains("\"message\""))
        {
            string userFriendlyError = ApiErrorParser.ParseErrorResponse(responseContent, "API error occurred");
            return new() { Errors = userFriendlyError };
        }

        // For demo purposes, allow simple clean up to demonstrate how chaotic LLMs can be.
        string? deviceActionJson = cleanUpJsonWell
            ? JsonCleanUp(responseContent)
            : SimpleCleanUp(responseContent);

        if (string.IsNullOrWhiteSpace(deviceActionJson))
        {
            // If JsonCleanUp failed and we have an error response, parse it properly
            if (responseContent.Contains("\"error\"") && responseContent.Contains("\"message\""))
            {
                string userFriendlyError = ApiErrorParser.ParseErrorResponse(responseContent, "API error occurred");
                return new() { Errors = userFriendlyError };
            }
            
            return new()
            {
                ChatResponse = deviceActionJson,
                Errors = "Incorrect response from chat API",
                Trace = trace
            };
        }

        try
        {
            List<DeviceAction>? deviceActions = JsonSerializer.Deserialize<List<DeviceAction>>(deviceActionJson);
            if (deviceActions != null)
            {
                foreach (var a in deviceActions)
                {
                    trace.Add(new TraceEvent { Kind = "ActionQueued", Summary = $"{a.Action} -> {a.Device}", Details = System.Text.Json.JsonSerializer.Serialize(a) });
                }
            }

            return new()
            {
                ChatResponse = deviceActionJson,
                DeviceActions = deviceActions,
                Trace = trace
            };
        }
        catch (Exception ex)
        {
            trace.Add(new TraceEvent { Kind = "Error", Summary = "Failed to parse actions", Details = ex.Message });
            return new()
            {
                ChatResponse = deviceActionJson,
                Errors = ex.Message,
                Trace = trace
            };
        }
    }

    private static string? JsonCleanUp(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        try
        {
            // Fetch response from chat.
            ChatResponse? response = JsonSerializer.Deserialize<ChatResponse>(content);
            if (response?.choices?.Any() != true || string.IsNullOrWhiteSpace(response.choices[0]?.message?.content))
            {
                return null;
            }

            content = response.choices[0].message.content;
        }
        catch (JsonException)
        {
            // If we can't deserialize as ChatResponse, it might be an error response or malformed JSON
            // Return null so it gets handled by the error checking in the calling method
            return null;
        }

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

using HomeAutomationGpt.Models;
using HomeAutomationGpt.Utils;
using System.Text;
using System.Text.Json;

namespace HomeAutomationGpt.Services;

public class HomeAssistanceServiceV3 : IHomeAssistanceService
{
    private readonly HomeAssistanceService _homeAssistanceService = new();

    public async Task<DeviceCommandResponse> ExecuteCommandAsync(string command, List<Device> devices, bool cleanUpJsonWell = true)
    {
        DeviceCommandResponse? lastSuccess = null;

        int tries = 3;
        while (--tries >= 0)
        {
            try
            {
                DeviceCommandResponse result = await _homeAssistanceService.ExecuteCommandAsync(command, devices);
                if (result.Errors?.Any() == true)
                {
                    Console.WriteLine("Failed request, moving on...");
                    continue;
                }

                var systemPrompt = "Respond only with Yes or No if the response makes sense based on original system prompt, user input and LLM's response.\n" +
                    "Do not hallucinate.\n" +
                    "Response example:" +
                    "Yes";
                var originalSysPrompt = "You're a home assistant AI. Here are the list of supported devices: " +
                            string.Join(", ", devices.Select(d => d.Name)) + ". " +
                            "Available commands: On, Off, Speak, SetValue\n" +
                            "You always reply only with a JSON response with a list of commands, do not add any additional text. Example: \n" +
                            "```json" +
                            "[{ \"Device\": \"TV\", \"Action\": \"On\" }, {\"Action\": \"Speak\", \"Text\": \"Dinner is ready\"}, {\"Action\":\"SetValue\", \"Device\": \"A/C\", \"Value\": 18 }]" +
                            "```";

                var content = $"User Request:\n{command}\n\n"
                    + $"System Prompt:\n{originalSysPrompt}\n\n"
                    + $"Response:\n{result.ChatResponse}";

                var requestContent = new
                {
                    model = "model-identifier",
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = content },
                        new { role = "user", content = "Does the response make sense? Answer with yes or no." },
                    },
                    temperature = 0.6,
                    max_tokens = -1,
                    stream = false
                };

                StringContent jsonContent = new(JsonSerializer.Serialize(requestContent), Encoding.UTF8, "application/json");

                using HttpClient httpClient = new();
                HttpResponseMessage response = await httpClient.PostAsync(HomeAssistanceService.ChatUrl, jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    string userFriendlyError = ApiErrorParser.ParseErrorResponse(errorContent, 
                        $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
                    return new() { Errors = userFriendlyError };
                }

                string? responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine("And AI says: " + responseContent);
                if (responseContent.Contains("yes", StringComparison.OrdinalIgnoreCase))
                {
                    // Probably decent enough response.
                    return result;
                }

                lastSuccess = result;

                if (tries <= 0)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                // TODO: Probably should send to Seq or Home Assistant log.
                if (tries <= 0 && lastSuccess == null)
                {
                    return new DeviceCommandResponse { Errors = ex.Message };
                }
            }
        }

        // What's the worst thing that could happen, right?
        return lastSuccess!;
    }
}

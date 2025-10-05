using HomeAutomationGpt.Core.Models;

namespace HomeAutomationGpt.Core.Services;

public class HomeAssistanceServiceV2 : IHomeAssistanceService
{
    private readonly HomeAssistanceService _homeAssistanceService = new();

    public async Task<DeviceCommandResponse> ExecuteCommandAsync(string command, List<Device> devices, bool cleanUpJsonWell = true, string? systemPrompt = null)
    {
        var trace = new List<TraceEvent> { new() { Kind = "Info", Summary = "Using V2 with retry logic" } };
        int tries = 3;
        while (--tries >= 0)
        {
            try
            {
                var result = await _homeAssistanceService.ExecuteCommandAsync(command, devices, false, systemPrompt);
                if (result.Trace != null) trace.AddRange(result.Trace);
                if (result.Errors?.Any() != true)
                {
                    result.Trace = trace;
                    return result;
                }
                trace.Add(new TraceEvent { Kind = "Error", Summary = "Underlying V1 returned errors", Details = result.Errors });
            }
            catch (Exception ex)
            {
                trace.Add(new TraceEvent { Kind = "Error", Summary = "Exception during retry", Details = ex.Message });
                if (tries <= 0)
                {
                    return new DeviceCommandResponse { Errors = ex.Message, Trace = trace };
                }
            }
        }

        return new DeviceCommandResponse { Errors = "Magic: Tries <= 0 and it didn't crash!", Trace = trace };
    }
}

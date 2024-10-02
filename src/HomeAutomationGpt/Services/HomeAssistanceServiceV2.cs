using HomeAutomationGpt.Models;

namespace HomeAutomationGpt.Services;

public class HomeAssistanceServiceV2 : IHomeAssistanceService
{
    private readonly HomeAssistanceService _homeAssistanceService = new();

    public async Task<DeviceCommandResponse> ExecuteCommandAsync(string command, List<Device> devices, bool cleanUpJsonWell = true)
    {
        int tries = 3;
        while (--tries >= 0)
        {
            try
            {
                var result = await _homeAssistanceService.ExecuteCommandAsync(command, devices, false);
                if (result.Errors?.Any() != true)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                // TODO: Probably should send to Seq or Home Assistant log.
                if (tries <= 0)
                {
                    return new DeviceCommandResponse { Errors = ex.Message };
                }
            }
        }

        return new DeviceCommandResponse { Errors = "Magic: Tries <= 0 and it didn't crash!" };
    }
}

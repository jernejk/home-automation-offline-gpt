using HomeAutomationGpt.Models;

namespace HomeAutomationGpt.Services;

public interface IHomeAssistanceService
{
    Task<DeviceCommandResponse> ExecuteCommandAsync(string command, List<Device> devices, bool cleanUpJsonWell = true, string? systemPrompt = null);
}

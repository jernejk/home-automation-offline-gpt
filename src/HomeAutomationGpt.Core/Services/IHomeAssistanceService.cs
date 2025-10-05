using HomeAutomationGpt.Core.Models;

namespace HomeAutomationGpt.Core.Services;

public interface IHomeAssistanceService
{
    Task<DeviceCommandResponse> ExecuteCommandAsync(string command, List<Device> devices, bool cleanUpJsonWell = true, string? systemPrompt = null);
}

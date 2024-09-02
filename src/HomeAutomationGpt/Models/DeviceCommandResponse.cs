namespace HomeAutomationGpt.Models;

public class DeviceCommandResponse
{
    public List<DeviceAction>? DeviceActions { get; set; }
    public string? Errors { get; set; }
    public string? ChatResponse { get; set; }
}

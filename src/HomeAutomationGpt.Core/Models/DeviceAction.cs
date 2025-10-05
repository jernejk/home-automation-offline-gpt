namespace HomeAutomationGpt.Core.Models;

public class DeviceAction
{
    public required string Action { get; set; }
    public required string Device { get; set; }
    public string? Text { get; set; }
    public float? Value { get; set; }
}

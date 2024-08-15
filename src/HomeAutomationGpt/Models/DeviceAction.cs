namespace HomeAutomationGpt.Models;

public class DeviceAction
{
    public string Action { get; set; }
    public string Device { get; set; }
    public string? Text { get; set; }
    public float? Value { get; set; }
}

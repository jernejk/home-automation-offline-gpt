namespace HomeAutomationGpt.Core.Models;

public class Device
{
    public string Name { get; set; } = null!;
    public bool IsOn { get; set; } = false;
    public float? Value { get; set; } = null;
}

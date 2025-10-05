namespace HomeAutomationGpt.Core.Models;

public class AiSettings
{
    public string Provider { get; set; } = "LMStudio";
    public string Endpoint { get; set; } = "http://localhost:1234/v1";
    public string Model { get; set; } = "qwen/qwen3-coder-30b";
    public string ApiKey { get; set; } = string.Empty;
}

namespace HomeAutomationGpt.Models;

public class TraceEvent
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Kind { get; set; } = "Info"; // e.g., SystemPrompt, UserPrompt, ModelResponse, ToolCall, ToolResult, ActionQueued, Error
    public string Summary { get; set; } = string.Empty;
    public string? Details { get; set; }
}
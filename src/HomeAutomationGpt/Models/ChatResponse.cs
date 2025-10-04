namespace HomeAutomationGpt.Models;

public class ChatResponse
{
    public required List<Choice> choices { get; set; }

    public class Choice
    {
        public required Message message { get; set; }
    }

    public class Message
    {
        public required string role { get; set; }
        public required string content { get; set; }
    }
}

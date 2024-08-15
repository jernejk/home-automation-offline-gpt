namespace HomeAutomationGpt.Models;

public class ChatResponse
{
    public List<Choice> choices { get; set; }

    public class Choice
    {
        public Message message { get; set; }
    }

    public class Message
    {
        public string role { get; set; }
        public string content { get; set; }
    }
}

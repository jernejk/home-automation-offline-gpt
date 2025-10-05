using System.Text.Json;

namespace HomeAutomationGpt.Core.Utils
{
    public static class ApiErrorParser
    {
        /// <summary>
        /// Parses error responses from API services and returns user-friendly error messages
        /// </summary>
        public static string ParseErrorResponse(string responseContent, string fallbackMessage = "Unknown error occurred")
        {
            if (string.IsNullOrWhiteSpace(responseContent))
            {
                return fallbackMessage;
            }

            try
            {
                // Try to parse as JSON error response
                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                // Check for "error" object (LM Studio format)
                if (root.TryGetProperty("error", out var errorElement))
                {
                    return ParseErrorObject(errorElement);
                }

                // Check for direct error properties
                if (root.TryGetProperty("message", out var messageElement))
                {
                    return messageElement.GetString() ?? fallbackMessage;
                }

                // Check for other common error formats
                if (root.TryGetProperty("detail", out var detailElement))
                {
                    return detailElement.GetString() ?? fallbackMessage;
                }
            }
            catch (JsonException)
            {
                // Not valid JSON, check for common plain text error patterns
                return ParsePlainTextError(responseContent, fallbackMessage);
            }

            return fallbackMessage;
        }

        private static string ParseErrorObject(JsonElement errorElement)
        {
            // Try to get the error message
            if (errorElement.TryGetProperty("message", out var messageElement))
            {
                var message = messageElement.GetString() ?? "";
                
                // Get error code for more context
                if (errorElement.TryGetProperty("code", out var codeElement))
                {
                    var code = codeElement.GetString();
                    return FormatUserFriendlyMessage(message, code);
                }
                
                return FormatUserFriendlyMessage(message);
            }

            return "API error occurred";
        }

        private static string FormatUserFriendlyMessage(string originalMessage, string? errorCode = null)
        {
            // Handle common error scenarios with user-friendly messages
            var lowerMessage = originalMessage.ToLowerInvariant();

            if (lowerMessage.Contains("no models loaded") || lowerMessage.Contains("model_not_found"))
            {
                return "🤖 No AI model is loaded in LM Studio. Please load a model in the LM Studio interface or use the 'lms load' command.";
            }

            if (lowerMessage.Contains("connection") && lowerMessage.Contains("refused"))
            {
                return "🔌 Cannot connect to LM Studio. Make sure LM Studio is running on http://localhost:1234";
            }

            if (lowerMessage.Contains("timeout"))
            {
                return "⏰ Request timed out. The AI model might be busy or overloaded. Try again in a moment.";
            }

            if (lowerMessage.Contains("rate limit"))
            {
                return "🚦 Too many requests. Please wait a moment before trying again.";
            }

            if (lowerMessage.Contains("invalid") && lowerMessage.Contains("token"))
            {
                return "🔑 Authentication error. Please check your API configuration.";
            }

            if (lowerMessage.Contains("server error") || lowerMessage.Contains("internal error"))
            {
                return "⚠️ Server error occurred. Please try again or restart LM Studio if the problem persists.";
            }

            // If we can't categorize it, return the original message with some formatting
            return $"❌ {originalMessage}";
        }

        private static string ParsePlainTextError(string responseContent, string fallbackMessage)
        {
            var lowerContent = responseContent.ToLowerInvariant();

            if (lowerContent.Contains("connection refused"))
            {
                return "🔌 Cannot connect to LM Studio. Make sure LM Studio is running on http://localhost:1234";
            }

            if (lowerContent.Contains("404") || lowerContent.Contains("not found"))
            {
                return "🔍 API endpoint not found. Please check that LM Studio is running and properly configured.";
            }

            if (lowerContent.Contains("500") || lowerContent.Contains("internal server error"))
            {
                return "⚠️ Server error occurred. Please try again or restart LM Studio if the problem persists.";
            }

            // Return original content if it's reasonably short, otherwise use fallback
            return responseContent.Length <= 200 ? responseContent : fallbackMessage;
        }
    }
}
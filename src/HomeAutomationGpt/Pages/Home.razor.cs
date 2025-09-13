using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HomeAutomationGpt.Models;
using HomeAutomationGpt.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HomeAutomationGpt.Pages
{
    public partial class Home : ComponentBase, IDisposable
    {
        [Inject]
        private IJSRuntime JS { get; set; } = default!;

        [Inject]
        private Microsoft.Extensions.AI.IChatClient ChatClient { get; set; } = default!;
        
        [Inject]
        private Dictionary<string, ModelContextProtocol.Client.IMcpClient> McpClients { get; set; } = default!;

        private string _selectedServiceVersion = "V5";
        private string SelectedServiceVersion
        {
            get => _selectedServiceVersion;
            set
            {
                if (_selectedServiceVersion != value)
                {
                    _selectedServiceVersion = value;
                    AddEvent("System", $"Switched to AI Service {value}");
                    StateHasChanged();
                }
            }
        }

        private Dictionary<string, ServiceInfo> AvailableServices { get; } = new()
        {
            ["V1"] = new("V1 - Basic JSON Parsing", "Direct HTTP calls with manual JSON cleanup. Shows raw challenges of LLM output parsing."),
            ["V2"] = new("V2 - Retry Logic", "Adds retry mechanism (3 attempts) on top of V1. Improves reliability for unstable responses."),
            ["V3"] = new("V3 - Self-Validation", "Uses AI to validate its own responses. Makes additional API calls for quality checking."),
            ["V4"] = new("V4 - Modern Client", "Uses Microsoft.Extensions.AI client but still relies on JSON parsing for actions."),
            ["V5"] = new("V5 - Function Calling (Recommended)", "Native function calling with Microsoft.Extensions.AI. Most reliable and modern approach."),
            ["V6"] = new("V6 - MCP Integration (Advanced)", "Function calling + MCP integration. Adds web search and external tool capabilities via Model Context Protocol.")
        };

        private IHomeAssistanceService _has => SelectedServiceVersion switch
        {
            "V1" => new HomeAssistanceService(),
            "V2" => new HomeAssistanceServiceV2(),
            "V3" => new HomeAssistanceServiceV3(),
            "V4" => new HomeAssistanceServiceV4(ChatClient),
            "V5" => new HomeAssistanceServiceV5(ChatClient),
            "V6" => new HomeAssistanceServiceV6(ChatClient, McpClients),
            _ => new HomeAssistanceServiceV5(ChatClient)
        };

        private List<Device> Devices { get; set; } = new()
        {
            new Device { Name = "TV" },
            new Device { Name = "Kitchen lights" },
            new Device { Name = "A/C" },
            new Device { Name = "Living room lights" },
            new Device { Name = "Bedroom lights" },
            //new Device { Name = "Heater" },
            new Device { Name = "Coffee machine" },
            new Device { Name = "Cleaner Robot" },
        };

        private string? NewDeviceName { get; set; }
        private string? Command { get; set; }
        private string? Debug { get; set; }
        private string? Response { get; set; }
        private string? SpeakText { get; set; }
        private bool ShowDebug { get; set; } = false;
        private bool IsRequestFailed { get; set; } = false;

        // Voice & activity
        private bool AutoSendVoice { get; set; } = true;
        private string ActiveTab { get; set; } = "activity";

        private readonly string[] QuickPrompts = new[]
        {
            "It's really cold – set A/C to 23 and turn on living room lights",
            "Movie time: TV on, lights off",
            "Dinner is ready – announce it",
            "Good night: turn everything off"
        };

        private List<LogEvent> Activity { get; set; } = new();


        private void AddDevice()
        {
            if (!string.IsNullOrWhiteSpace(NewDeviceName))
            {
                Devices.Add(new Device { Name = NewDeviceName });
                AddEvent("System", $"Added device '{NewDeviceName}'.");
                NewDeviceName = string.Empty;
            }
        }

        private async Task SendCommand()
        {
            Debug = string.Empty;
            Response = string.Empty;
            SpeakText = string.Empty;

            if (string.IsNullOrWhiteSpace(Command))
            {
                Debug = "Command is empty";
                AddEvent("Error", "Command is empty");
                return;
            }

            AddEvent("User", Command);
            try
            {
                var result = await _has.ExecuteCommandAsync(Command, Devices);
                Response = result.ChatResponse;

                if (!string.IsNullOrWhiteSpace(result.Errors) || result.DeviceActions == null)
                {
                    Debug = result.Errors;
                    IsRequestFailed = true;
                    ShowDebug = true;
                    AddEvent("Error", "AI returned an error", result.Errors);
                    return;
                }

                AddEvent("AI", "Parsed actions", result.ChatResponse);
                UpdateDeviceStates(result.DeviceActions);
            }
            catch (Exception ex)
            {
                Debug = ex.Message;
                IsRequestFailed = true;
                ShowDebug = true;
                AddEvent("Error", "Failed to execute command", ex.Message);
            }
        }

        private void UpdateDeviceStates(List<DeviceAction> deviceActions)
        {
            try
            {
                foreach (var action in deviceActions)
                {
                    if (!string.IsNullOrEmpty(action?.Device))
                    {
                        var device = FindDevice(action.Device);
                        if (device != null)
                        {
                            switch (action?.Action?.ToLower())
                            {
                                case "turn on":
                                case "turnon":
                                case "on":
                                    device.IsOn = true;
                                    AddEvent("Action", $"Turned on {device.Name}: {device.IsOn}");
                                    break;

                                case "turn off":
                                case "turnoff":
                                case "off":
                                    device.IsOn = false;
                                    device.Value = null;
                                    AddEvent("Action", $"Turned off {device.Name}");
                                    break;

                                case "set":
                                case "setvalue":
                                case "settemperature":
                                    device.Value = action.Value;
                                    device.IsOn = true;
                                    AddEvent("Action", $"Set {device.Name} to {action.Value}");
                                    break;

                                case "search":
                                    // Search action without a device doesn't make sense
                                    AddEvent("Search", $"Web search: {action.Text}");
                                    break;
                            }
                        }
                    }
                    else if (action?.Action?.ToLower() == "speak" && !string.IsNullOrWhiteSpace(action.Text))
                    {
                        SpeakText = action.Text;
                        _ = JS.InvokeVoidAsync("voice.ttsSpeak", SpeakText);
                        AddEvent("Action", $"Speak: {action.Text}");
                    }
                }

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Debug = $"Failed to process response: {ex.Message}";
                IsRequestFailed = true;
                ShowDebug = true;
                AddEvent("Error", "Failed to process response", ex.Message);
            }
        }

        private void ToggleDevice(Device d, bool on)
        {
            d.IsOn = on;
            if (!on) d.Value = null;
            AddEvent("Action", $"{(on ? "Turned on" : "Turned off")} {d.Name} (manual)");
        }

        private void ChangeDeviceValue(Device d, float? value)
        {
            if (value.HasValue)
            {
                d.Value = value.Value;
                d.IsOn = true;
                AddEvent("Action", $"Set {d.Name} to {FormatValue(d)} (manual)");
            }
        }

        private static float? ParseFloat(object? v)
            => v is null ? null : float.TryParse(v.ToString(), out var f) ? f : null;

        private string FormatValue(Device d)
        {
            if (!d.Value.HasValue) return "";
            if (IsAC(d)) return $"{d.Value:0}°C";
            return $"{d.Value:0}";
        }

        private float GetMin(Device d) => IsAC(d) ? 16 : 0;
        private float GetMax(Device d) => IsAC(d) ? 30 : 100;
        private float GetStep(Device d) => IsAC(d) ? 1 : 5;
        private float GetDisplayValue(Device d) => d.Value ?? (IsAC(d) ? 22 : 50);
        private bool SupportsValue(Device d) => IsAC(d);
        private bool IsAC(Device d) => d.Name.Contains("A/C", StringComparison.OrdinalIgnoreCase);

        public Device? FindDevice(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName)) return null;
            var found = Devices.FirstOrDefault(d => d.Name.Equals(deviceName, StringComparison.OrdinalIgnoreCase));
            if (found != null) return found;
            deviceName = deviceName.Replace("/", string.Empty);
            return Devices.FirstOrDefault(d => d.Name.Replace("/", string.Empty).Equals(deviceName, StringComparison.OrdinalIgnoreCase));
        }

        private void UseQuickPrompt(string text)
        {
            Command = text;
            _ = SendCommand();
        }

        private void HandleVoiceError(string error)
        {
            AddEvent("Error", error);
        }

        private void AddEvent(string type, string message, string? details = null)
            => Activity.Add(new LogEvent { Type = type, Message = message, Details = details, Timestamp = DateTime.UtcNow });

        private string GetBadge(string type) => type.ToLower() switch
        {
            "user" => "primary",
            "ai" => "success",
            "action" => "warning",
            "error" => "danger",
            _ => "secondary"
        };

        private void ClearEvents() => Activity.Clear();

        // UI helpers
        private string GetTabClass(string id) => $"tab {(ActiveTab == id ? "active" : string.Empty)}";
        private void SetActiveTab(string id) => ActiveTab = id;
        
        private string GetCurrentServiceDescription()
        {
            return AvailableServices.TryGetValue(SelectedServiceVersion, out var service) 
                ? service.Description 
                : "Unknown service version.";
        }

        public record ServiceInfo(string Name, string Description);

        public class LogEvent
        {
            public DateTime Timestamp { get; set; }
            public string Type { get; set; } = "System";
            public string Message { get; set; } = string.Empty;
            public string? Details { get; set; }
        }

        public void Dispose()
        {
            // Home component no longer manages voice resources directly
        }
    }
}

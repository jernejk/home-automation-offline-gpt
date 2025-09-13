using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HomeAutomationGpt.Components
{
    public partial class VoiceControl : ComponentBase, IDisposable
    {
        [Inject]
        private IJSRuntime JS { get; set; } = default!;

        // Parameters for parent component communication
        [Parameter] public string? Command { get; set; }
        [Parameter] public EventCallback<string> CommandChanged { get; set; }
        [Parameter] public bool AutoSendVoice { get; set; } = true;
        [Parameter] public EventCallback<bool> AutoSendVoiceChanged { get; set; }
        [Parameter] public EventCallback OnSendCommand { get; set; }
        [Parameter] public EventCallback<string> OnError { get; set; }
        [Parameter] public bool ClearCommandAfterSend { get; set; } = true;

        // Voice state
        private bool IsListening { get; set; } = false;
        private bool VoiceSupported { get; set; } = false;
        private DotNetObjectReference<VoiceControl>? _dotRef;
        private string? _currentVoiceText; // Current interim voice text

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _dotRef = DotNetObjectReference.Create(this);
                try
                {
                    await JS.InvokeVoidAsync("voice.init", _dotRef, "en-US");
                    VoiceSupported = await JS.InvokeAsync<bool>("voice.supported");
                }
                catch
                {
                    VoiceSupported = false;
                }
                StateHasChanged();
            }
        }

        public async Task ToggleListening()
        {
            if (!VoiceSupported) return;
            
            if (IsListening)
            {
                await JS.InvokeVoidAsync("voice.stop");
                IsListening = false;
                _currentVoiceText = null;
            }
            else
            {
                // Clear any existing command and start fresh voice input
                _currentVoiceText = null;
                await CommandChanged.InvokeAsync(string.Empty);
                
                await JS.InvokeVoidAsync("voice.start");
                IsListening = true;
            }
            StateHasChanged();
        }

        [JSInvokable]
        public async Task OnSpeechResult(string text, bool isFinal)
        {
            // Store the current voice text
            _currentVoiceText = text;
            
            // Use the voice text directly (no base command to combine with)
            await CommandChanged.InvokeAsync(text);

            if (isFinal && AutoSendVoice)
            {
                // Send the command
                await OnSendCommand.InvokeAsync();
                
                // Clear voice tracking variables and command if requested
                _currentVoiceText = null;
                if (ClearCommandAfterSend)
                {
                    await CommandChanged.InvokeAsync(string.Empty);
                }
            }
            StateHasChanged();
        }

        [JSInvokable]
        public async Task OnSpeechError(string error)
        {
            await OnError.InvokeAsync($"Voice error: {error}");
            IsListening = false;
            StateHasChanged();
        }

        [JSInvokable]
        public Task OnSpeechEnd()
        {
            IsListening = false;
            
            // If we have voice text but didn't auto-send, keep the voice text as the final command
            if (!string.IsNullOrWhiteSpace(_currentVoiceText))
            {
                _ = CommandChanged.InvokeAsync(_currentVoiceText);
            }
            
            // Clear tracking variables
            _currentVoiceText = null;
            
            StateHasChanged();
            return Task.CompletedTask;
        }

        // UI helper methods
        private string GetMicButtonClass() => $"btn btn-lg {(IsListening ? "btn-danger" : "btn-outline-primary")} mic-btn";
        private string GetMicButtonTitle() => IsListening ? "Stop listening" : "Start listening";
        private string GetMicIconClass() => $"bi {(IsListening ? "bi-mic-mute-fill" : "bi-mic-fill")}";
        private string GetVoiceStatusClass() => $"voice-status {(IsListening ? "on" : "off")}";
        private string GetVoiceStatusText() => IsListening ? "Listening…" : (VoiceSupported ? "Voice ready" : "Voice unsupported");

        public void Dispose()
        {
            _dotRef?.Dispose();
        }
    }
}
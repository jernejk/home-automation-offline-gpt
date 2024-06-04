# home-automation-offline-gpt

Demo to show how to use offline GPT with help of LM Studio.

## Setup LM Studio

1. Download and install [LM Studio](https://lmstudio.ai/)
1. Once LM Studio is installed, download Phi-3 model (2GB if machine does not have 8+GB VRAM)
1. Go to "Local Server" tab
1. Enable "Cross-Origin-Resource-Sharing (CORS)"
1. Disable "Verbose Server Logging"
1. Optional: On the right, "GPU Settings" under "Advanced Settings" to set the GPU memory limit to max
1. Stop Server and apply changes (if required)
1. Start Server

## Setup Blazor app

1. Have latest .NET 8 SDK
1. Run in Visual Studio or `dotnet run` in `src/HomeAutomationGpt` folder


# Demos

Try various prompts:

- Turn on the lights in the living room
- Turn off the lights in the kitchen
- Set the temperature to 23 degrees
- Say "It's really cold" and set A/C to 23, TV on and light in living room on as well
- Turn off all devices but keep A/C on
- I need more light

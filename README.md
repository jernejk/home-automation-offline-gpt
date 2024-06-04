# Home Automation Offline GPT

This demo showcases how to use offline GPT with the help of LM Studio within a .NET 8 Blazor app.

## Setup LM Studio

1. Download and install [LM Studio](https://lmstudio.ai/).
2. Once LM Studio is installed, download the Phi-3 model (use the 2GB version if your machine does not have 8+GB VRAM).
3. Go to the "Local Server" tab.
4. Enable "Cross-Origin-Resource-Sharing (CORS)".
5. Disable "Verbose Server Logging".
6. (Optional) Under "Advanced Settings" on the right, set the GPU memory limit to max in "GPU Settings".
7. Stop the server and apply changes if required.
8. Start the server.

## Setup Blazor App

1. Ensure you have the latest .NET 8 SDK installed.
2. Run the application in Visual Studio or use `dotnet run` in the `src/HomeAutomationGpt` folder.

## Demos

Try various prompts to see the home automation in action:

- "Turn on the lights in the living room."
- "Turn off the lights in the kitchen."
- "Set the temperature to 23 degrees."
- "It's really cold" (This will set the A/C to 23, turn on the TV, and turn on the light in the living room).
- "Turn off all devices but keep the A/C on."
- "I need more light."
- "I'm feeling cold."

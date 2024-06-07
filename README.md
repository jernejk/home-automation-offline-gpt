# Home Automation Offline GPT

This demo showcases how to use offline GPT with the help of LM Studio within a .NET 8 Blazor app.

## Setup LM Studio

1. Download and install [LM Studio](https://lmstudio.ai/).
![Download and install LM Studio](/img/lmstudio-website.png)
2. Once LM Studio is installed, download the Phi-3 model (use the 2GB version if your machine does not have 8+GB VRAM).
![alt text](/img/lmstudio-1.png)
3. Go to the "Local Server" tab.
![alt text](/img/lmstudio-2.png)
4. Select model.
![alt text](/img/lmstudio-3.png)
5. Enable "Cross-Origin-Resource-Sharing (CORS)".
6. Disable "Verbose Server Logging".
7. (Optional) Under "Advanced Settings" on the right, set the GPU memory limit to max in "GPU Settings".
![alt text](/img/lmstudio-4.png)
8. Stop the server and apply changes if required.
9. Start the server.

## Setup Blazor App

1. Ensure you have the latest .NET 8 SDK installed. (Windows Winget: `winget install dotnet-sdk-8`)
2. Run the application in Visual Studio or use `dotnet run` in the `src/HomeAutomationGpt` folder.

![alt text](/img/blazor-demo-1.png)

### Alternative: Run on Docker

On the root of the repo run the following commands:
    
```bash
docker build -t home-automation-gpt .\src\HomeAutomationGpt\
docker run -d -p 8080:80 home-automation-gpt
```

Open `http://localhost:8080/` in your browser.

## Prompts to try out

Try various prompts to see the home automation in action:

- "Turn on the lights in the living room."
- "Turn off the lights in the kitchen."
- "Set the temperature to 23 degrees."
- "It's really cold" (This will set the A/C to 23, turn on the TV, and turn on the light in the living room).
- "Turn off all devices but keep the A/C on."
- "I need more light."
- "I'm feeling cold."

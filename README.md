# Home Automation Offline GPT

This demo showcases how to use offline GPT with the help of LM Studio within a .NET 8 Blazor app.

## Setup LM Studio

1. Download and install [LM Studio](https://lmstudio.ai/).
![Download and install LM Studio](/img/lmstudio-website.png)
2. Once LM Studio is installed, download a small model (Phi-3, Qwen, Gemma). If your machine has <8GB VRAM, choose a smaller one.
![alt text](/img/lmstudio-1.png)
3. Go to the "Local Server" tab.
![alt text](/img/lmstudio-2.png)
4. Select model and start the server.
![alt text](/img/lmstudio-3.png)
5. Enable "Cross-Origin-Resource-Sharing (CORS)".
6. Disable "Verbose Server Logging".
7. (Optional) Under "Advanced Settings", set the GPU memory limit to max in "GPU Settings".
![alt text](/img/lmstudio-4.png)
8. Stop the server and apply changes if required, then start the server.

## Setup Blazor App

1. Ensure you have the latest .NET 8 SDK installed. (Windows Winget: `winget install dotnet-sdk-8`)
2. Run the application in Visual Studio or use `dotnet run` in the `src/HomeAutomationGpt` folder.

![alt text](/img/blazor-demo-1.png)

### Alternative: Run with Makefile or Docker (Dev Container included)

From the repository root, run:

- Quickstart (Make) or .NET Aspire:
```bash
# Run with Make (client only)
make build
make run

# Or run with .NET Aspire (AppHost)
AI__Provider=Ollama AI__Model=gemma3:1b AI__Endpoint=http://localhost:11434/v1 dotnet run --project src/HomeAutomationGpt.AppHost/HomeAutomationGpt.AppHost.csproj
```

- macOS/Linux (Docker) or VS Code Dev Container (client only):
```bash
make image
make up
# open http://localhost:8080/
# or open in VS Code and use "Dev Containers: Reopen in Container"
```

- Windows (PowerShell/CMD):
```powershell
docker build -t home-automation-gpt .\src\HomeAutomationGpt\
docker run -d -p 8080:80 --name home-automation-gpt home-automation-gpt
# open http://localhost:8080/
```

Open `http://localhost:8080/` in your browser.

## Using the demo (and switching providers and AI Trace)

- Service version selector: Switch between V1–V5 in the UI to compare approaches. V5 is recommended.
- Advanced options: You can optionally edit the system prompt (hidden by default). Leave it off for the default prompts.
- Voice input: Click the “Speak” button to dictate commands (browser’s Web Speech API).
- Switch provider: `./scripts/switch-provider.sh ollama` or `./scripts/switch-provider.sh lmstudio`. Requires jq. Or use `make provider-ollama` / `make provider-lmstudio`.
- AI Trace: Toggle "Show AI trace" in the UI to see system/user prompts, model output, tool calls, and the queued actions.

## Prompts to try out

- "Turn on the lights in the living room."
- "Turn off the lights in the kitchen."
- "Set the temperature to 23 degrees."
- "It's really cold" (Sets A/C to 23, turns on the TV and living room light).
- "Turn off all devices but keep the A/C on."
- "I need more light."
- "I'm feeling cold."

# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Project Overview

This is a .NET 9 Blazor WebAssembly application that demonstrates local/offline LLM integration with home automation. The application uses Microsoft.Extensions.AI with function invocation to control simulated smart home devices through natural language commands.

## Key Architecture

### Core Components
- **Blazor WebAssembly Frontend**: Interactive UI for device control and command input
- **Local Model Provider**: LM Studio (OpenAI-compatible API) or Ollama
- **Microsoft.Extensions.AI**: Provides standardized AI client interfaces with function calling
- **Model Context Protocol (MCP)**: Future integration path for Home Assistant connectivity
- **Service Architecture**: Multiple service implementations (V1–V5) showing evolution of AI integration patterns

### Service Evolution Pattern
The codebase contains 5 different service implementations (`HomeAssistanceService` through `HomeAssistanceServiceV5`), demonstrating the progression from manual JSON parsing to native function calling with Microsoft.Extensions.AI.

### Device Control Model
- Devices are represented as simple objects with `Name`, `IsOn`, and optional `Value` properties
- Actions are processed through `DeviceAction` objects that specify device, action type, and optional parameters
- The UI updates device states in real-time based on AI-generated actions

## Common Development Commands

### Build and Run
```bash
# Run the application (from src/HomeAutomationGpt directory)
cd src/HomeAutomationGpt
dotnet run

# Build the solution
cd src
dotnet build HomeAutomationGpt.sln

# Restore packages
dotnet restore

# Alternative: Install .NET 9 SDK on Windows
winget install dotnet-sdk-9
```

### Docker Operations
```bash
# Build and run (Make)
make image
make up

# Access at http://localhost:8080/
```

### Provider Switch
```bash
# Switch to Ollama
make provider-ollama
# or switch to LM Studio
make provider-lmstudio
```

## Provider Configuration

Edit `src/HomeAutomationGpt/wwwroot/appsettings.json` to choose your provider:
- `AI.Provider`: `Ollama` or `LMStudio`
- `AI.Endpoint`: e.g., `http://localhost:11434/v1` (Ollama) or `http://localhost:1234/v1` (LM Studio)
- `AI.Model`: e.g., `gemma3:1b`, `phi-3`, `qwen2.5`

## UI Features

- Service version selector (V1–V5) with V5 (function calling) as the recommended default.
- Optional system-prompt editor hidden under “Advanced options”.
- Debug panel to view responses and errors.

## Development Notes

- The app is client-side (WASM). Do not put sensitive tokens in client configuration. Any Home Assistant/MCP integration should use a server proxy.
- MCP wiring is scaffolded but not used by default.

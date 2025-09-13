# Simple helpers for development

# Build and run locally
run:
	dotnet run --project src/HomeAutomationGpt/HomeAutomationGpt.csproj

build:
	dotnet build src/HomeAutomationGpt.sln

# Docker workflows
image:
	docker build -t home-automation-gpt ./src/HomeAutomationGpt/

up:
	docker run -d --rm -p 8080:80 --name home-automation-gpt home-automation-gpt

down:
	-@docker rm -f home-automation-gpt 2>/dev/null || true

# Provider switches (macOS/Linux)
provider-ollama:
	jq '.AI.Provider="Ollama" | .AI.Endpoint="http://localhost:11434/v1" | .AI.Model="gemma3:1b"' \
		src/HomeAutomationGpt/wwwroot/appsettings.json > src/HomeAutomationGpt/wwwroot/appsettings.tmp \
	&& mv src/HomeAutomationGpt/wwwroot/appsettings.tmp src/HomeAutomationGpt/wwwroot/appsettings.json

provider-lmstudio:
	jq '.AI.Provider="LMStudio" | .AI.Endpoint="http://localhost:1234/v1" | .AI.Model="phi-3"' \
		src/HomeAutomationGpt/wwwroot/appsettings.json > src/HomeAutomationGpt/wwwroot/appsettings.tmp \
	&& mv src/HomeAutomationGpt/wwwroot/appsettings.tmp src/HomeAutomationGpt/wwwroot/appsettings.json

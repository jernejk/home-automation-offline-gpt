# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Copy the csproj file and restore the dependencies (better Docker layer caching)
COPY HomeAutomationGpt.csproj ./
RUN dotnet restore HomeAutomationGpt.csproj --nologo

# Copy the rest of the application code
COPY . .

# Build the application
RUN dotnet publish HomeAutomationGpt.csproj -c Release -o /app --nologo

# Stage 2: Serve the application (static files)
FROM nginx:alpine

COPY --from=build /app/wwwroot /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]

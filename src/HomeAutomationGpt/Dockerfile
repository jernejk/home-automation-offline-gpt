# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy the csproj file and restore the dependencies
COPY *.csproj .
RUN dotnet restore

# Copy the rest of the application code
COPY . .

# Build the application
RUN dotnet publish -c Release -o /app

# Stage 2: Serve the application
FROM nginx:alpine

COPY --from=build /app/wwwroot /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace HomeAutomationGpt.Extensions;

public static class McpClientExtensions
{
    /// <summary>
    /// Adds MCP clients to the service collection based on configuration
    /// </summary>
    public static IServiceCollection AddMcpClients(this IServiceCollection services, IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        services.AddSingleton<Dictionary<string, IMcpClient>>(sp =>
        {
            var clients = new Dictionary<string, IMcpClient>();
            var mcpSection = configuration.GetSection("MCP:Servers");
            
            foreach (var serverSection in mcpSection.GetChildren())
            {
                var serverId = serverSection.Key;
                var enabled = serverSection.GetValue<bool>("enabled");
                
                if (!enabled) continue;

                try
                {
                    var transport = serverSection.GetValue<string>("transport") ?? "stdio";
                    var timeout = serverSection.GetValue<int>("timeout");
                    
                    IMcpClient mcpClient = transport.ToLower() switch
                    {
                        "docker" => CreateDockerMcpClient(serverSection, loggerFactory, timeout),
                        "stdio" => CreateStdioMcpClient(serverSection, loggerFactory, timeout), 
                        "sse" or "http" => CreateSseMcpClient(serverSection, loggerFactory, timeout),
                        _ => throw new InvalidOperationException($"Unknown MCP transport type: {transport}")
                    };
                    
                    clients[serverId] = mcpClient;
                }
                catch (Exception ex)
                {
                    var logger = loggerFactory.CreateLogger("MCP");
                    logger.LogWarning(ex, "Failed to create MCP client for server {ServerId}", serverId);
                }
            }

            return clients;
        });

        return services;
    }

    /// <summary>
    /// Creates a Docker-based MCP client using StdioClientTransport
    /// </summary>
    private static IMcpClient CreateDockerMcpClient(IConfigurationSection config, ILoggerFactory loggerFactory, int timeout)
    {
        var command = config.GetValue<string>("config:command") ?? "docker";
        var args = config.GetSection("config:args").Get<string[]>() ?? Array.Empty<string>();
        
        var clientTransportOptions = new StdioClientTransportOptions()
        {
            Command = command,
            Arguments = args,
            // Add timeout if supported in future versions
        };
        
        var clientTransport = new StdioClientTransport(clientTransportOptions, loggerFactory);
        var clientOptions = new McpClientOptions()
        {
            ClientInfo = new Implementation()
            {
                Name = "Home Automation MCP Client",
                Version = "1.0.0",
            }
        };

        return McpClientFactory.CreateAsync(clientTransport, clientOptions, loggerFactory).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Creates a stdio-based MCP client using StdioClientTransport
    /// </summary>
    private static IMcpClient CreateStdioMcpClient(IConfigurationSection config, ILoggerFactory loggerFactory, int timeout)
    {
        var command = config.GetValue<string>("config:command") ?? "node";
        var args = config.GetSection("config:args").Get<string[]>() ?? Array.Empty<string>();
        
        var clientTransportOptions = new StdioClientTransportOptions()
        {
            Command = command,
            Arguments = args,
        };
        
        var clientTransport = new StdioClientTransport(clientTransportOptions, loggerFactory);
        var clientOptions = new McpClientOptions()
        {
            ClientInfo = new Implementation()
            {
                Name = "Home Automation MCP Client",
                Version = "1.0.0",
            }
        };

        return McpClientFactory.CreateAsync(clientTransport, clientOptions, loggerFactory).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Creates an SSE/HTTP-based MCP client using SseClientTransport
    /// </summary>
    private static IMcpClient CreateSseMcpClient(IConfigurationSection config, ILoggerFactory loggerFactory, int timeout)
    {
        var endpoint = config.GetValue<string>("config:endpoint") ?? "http://localhost:8080/mcp";
        
        var clientTransportOptions = new SseClientTransportOptions()
        {
            Endpoint = new Uri(endpoint),
        };
        
        var clientTransport = new SseClientTransport(clientTransportOptions, loggerFactory);
        var clientOptions = new McpClientOptions()
        {
            ClientInfo = new Implementation()
            {
                Name = "Home Automation MCP Client",
                Version = "1.0.0",
            }
        };

        return McpClientFactory.CreateAsync(clientTransport, clientOptions, loggerFactory).GetAwaiter().GetResult();
    }
}
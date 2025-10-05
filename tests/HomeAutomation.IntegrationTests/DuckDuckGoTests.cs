using System;
using System.Diagnostics;
using System.Net.Http.Json;
using HomeAutomationGpt.Core.Models;
using HomeAutomationGpt.Universal.Mcp;
using HomeAutomationGpt.WebAPI;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace HomeAutomation.IntegrationTests;

public class DuckDuckGoTests : IClassFixture<DuckDuckGoFactory>
{
    private readonly DuckDuckGoFactory _factory;
    private readonly ITestOutputHelper _output;

    public DuckDuckGoTests(DuckDuckGoFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task PromptWithDuckDuckGoProducesTrace()
    {
        if (!DuckDuckGoFactory.IsDockerAvailable())
        {
            _output.WriteLine("Skipping DuckDuckGo test because Docker is not available.");
            return;
        }

        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/prompt-with-mcp", new PromptRequest("Turn on the TV and tell me how many seasons of Severance exist"));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<DeviceCommandResponse>();
        Assert.NotNull(body);
        Assert.Null(body!.Errors);
        Assert.Contains(body.Trace ?? new List<TraceEvent>(),
            evt => (evt.Summary?.Contains("duckduckgo", StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (evt.Details?.Contains("duckduckgo", StringComparison.OrdinalIgnoreCase) ?? false));
    }

    [Fact]
    public async Task PromptWithDuckDuckGo_DebugFailure()
    {
        if (!DuckDuckGoFactory.IsDockerAvailable())
        {
            _output.WriteLine("Skipping DuckDuckGo debug test because Docker is unavailable.");
            return;
        }

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/prompt-with-mcp", new PromptRequest("Turn on the TV and tell me how many seasons of Severance exist"));
        var body = await response.Content.ReadFromJsonAsync<DeviceCommandResponse>();

        var payload = System.Text.Json.JsonSerializer.Serialize(body, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        _output.WriteLine("--- DuckDuckGo response payload ---");
        _output.WriteLine(payload ?? "<null>");
        _output.WriteLine("-----------------------------------");

        Assert.True(false, "Intentional failure to inspect DuckDuckGo response. See console output above.");
    }
}

public class DuckDuckGoFactory : WebApplicationFactory<Program>
{
    private IMcpClient? _duckClient;
    private ILoggerFactory? _loggerFactory;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(McpClientManager));
            services.RemoveAll(typeof(IReadOnlyDictionary<string, IMcpClient>));

            if (IsDockerAvailable())
            {
                var dictionary = new Dictionary<string, IMcpClient>(StringComparer.OrdinalIgnoreCase)
                {
                    ["duckduckgo"] = CreateDuckDuckGoClient()
                };

                services.AddSingleton<IReadOnlyDictionary<string, IMcpClient>>(dictionary);
            }
            else
            {
                services.AddSingleton<IReadOnlyDictionary<string, IMcpClient>>(new Dictionary<string, IMcpClient>());
            }
        });
    }

    public static bool IsDockerAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "--version",
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(3000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private IMcpClient CreateDuckDuckGoClient()
    {
        if (_duckClient is not null)
        {
            return _duckClient;
        }

        _loggerFactory ??= LoggerFactory.Create(static builder => builder.AddDebug());

        var transport = new ModelContextProtocol.Client.StdioClientTransport(
            new ModelContextProtocol.Client.StdioClientTransportOptions
            {
                Command = "docker",
                Arguments = new[] { "run", "-i", "--rm", "mcp/duckduckgo" }
            },
            _loggerFactory);

        _duckClient = McpClientFactory.CreateAsync(transport, new McpClientOptions
        {
            ClientInfo = new Implementation
            {
                Name = "IntegrationTests",
                Version = "1.0.0"
            }
        }, _loggerFactory).GetAwaiter().GetResult();

        return _duckClient;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_duckClient is not null)
            {
                _duckClient.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _duckClient = null;
            }

            _loggerFactory?.Dispose();
            _loggerFactory = null;
        }

        base.Dispose(disposing);
    }
}

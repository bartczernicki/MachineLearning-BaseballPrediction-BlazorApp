using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using System.Net;

namespace BaseballAIWorkbench.ApiService
{
    public sealed class WebIqMcpToolProvider(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        private static readonly Uri WebIqMcpEndpoint = new("https://api.microsoft.ai/v3/mcp/");

        public async Task<WebIqMcpToolScope> CreateToolScopeAsync(CancellationToken cancellationToken = default)
        {
            var apiKey = GetRequiredConnectionString(configuration, "WebIQMcpApiKey");
            var transportOptions = new HttpClientTransportOptions
            {
                Name = "WebIQ-MCP",
                Endpoint = WebIqMcpEndpoint,
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["x-apikey"] = apiKey
                }
            };

            IAsyncDisposable? cleanupOnFailure = null;

            var transport = new HttpClientTransport(transportOptions, loggerFactory);
            cleanupOnFailure = transport;

            try
            {
                var mcpClient = await McpClient.CreateAsync(transport, loggerFactory: loggerFactory, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                cleanupOnFailure = mcpClient;

                var tools = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                return new WebIqMcpToolScope(mcpClient, tools.Cast<AITool>().ToArray());
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                await cleanupOnFailure.DisposeAsync().ConfigureAwait(false);
                throw new InvalidOperationException(
                    "WebIQ MCP rejected the API key. Check the AppHost user secret ConnectionStrings:WebIQMcpApiKey and make sure it contains the WebIQ MCP x-apikey value.",
                    ex);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.UnsupportedMediaType)
            {
                await cleanupOnFailure.DisposeAsync().ConfigureAwait(false);
                throw new InvalidOperationException(
                    "WebIQ MCP did not accept the requested HTTP MCP transport content type. The app is configured to use Streamable HTTP only for WebIQ.",
                    ex);
            }
            catch
            {
                await cleanupOnFailure.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        private static string GetRequiredConnectionString(IConfiguration configuration, string name)
        {
            var value = configuration.GetConnectionString(name)
                ?? configuration[$"ConnectionStrings:{name}"]
                ?? Environment.GetEnvironmentVariable($"ConnectionStrings__{name}");

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            throw new InvalidOperationException($"Missing required connection string: ConnectionStrings__{name}.");
        }
    }

    public sealed class WebIqMcpToolScope(IAsyncDisposable mcpClient, IReadOnlyList<AITool> tools) : IAsyncDisposable
    {
        public IReadOnlyList<AITool> Tools { get; } = tools;

        public ValueTask DisposeAsync()
        {
            return mcpClient.DisposeAsync();
        }
    }
}

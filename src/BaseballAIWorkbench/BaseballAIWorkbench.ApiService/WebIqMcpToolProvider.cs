using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace BaseballAIWorkbench.ApiService
{
    public sealed class WebIqMcpToolProvider(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        private const string McpServerName = "MAIGrounding-MCP";
        private const string LegacyApiKeyConnectionStringName = "WebIQMcpApiKey";
        private static readonly Uri DefaultMcpEndpoint = new("https://api.microsoft.ai/v3/mcp/");
        private readonly ILogger<WebIqMcpToolProvider> _logger = loggerFactory.CreateLogger<WebIqMcpToolProvider>();

        public async Task<WebIqMcpToolScope> CreateToolScopeAsync(CancellationToken cancellationToken = default)
        {
            var apiKeyConfig = GetRequiredApiKey(configuration);
            var endpoint = GetMcpEndpoint(configuration);

            _logger.LogInformation(
                "Using MCP API key from {ApiKeySource}: {MaskedApiKey}",
                apiKeyConfig.Source,
                MaskApiKey(apiKeyConfig.Value));

            var transportOptions = new HttpClientTransportOptions
            {
                Name = McpServerName,
                Endpoint = endpoint,
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["x-apikey"] = apiKeyConfig.Value
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
                    "MAI Grounding MCP rejected the API key. Check the AppHost user secret ConnectionStrings:WebIQMcpApiKey or MAIGrounding-MCP:headers:x-apikey and make sure it contains the MCP x-apikey value.",
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

        private static Uri GetMcpEndpoint(IConfiguration configuration)
        {
            var configuredUrl = configuration[$"{McpServerName}:url"];

            if (string.IsNullOrWhiteSpace(configuredUrl))
            {
                return DefaultMcpEndpoint;
            }

            return Uri.TryCreate(configuredUrl, UriKind.Absolute, out var endpoint)
                ? endpoint
                : throw new InvalidOperationException($"Invalid MCP endpoint URL in {McpServerName}:url.");
        }

        private static ApiKeyConfig GetRequiredApiKey(IConfiguration configuration)
        {
            var candidates = new[]
            {
                (Value: configuration[$"{McpServerName}:headers:x-apikey"], Source: $"{McpServerName}:headers:x-apikey"),
                (Value: configuration.GetConnectionString(LegacyApiKeyConnectionStringName), Source: $"ConnectionStrings:{LegacyApiKeyConnectionStringName}"),
                (Value: configuration[$"ConnectionStrings:{LegacyApiKeyConnectionStringName}"], Source: $"ConnectionStrings:{LegacyApiKeyConnectionStringName}"),
                (Value: Environment.GetEnvironmentVariable($"ConnectionStrings__{LegacyApiKeyConnectionStringName}"), Source: $"ConnectionStrings__{LegacyApiKeyConnectionStringName}")
            };

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate.Value))
                {
                    continue;
                }

                var apiKey = TrimPastedQuotes(candidate.Value.Trim());
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    return new ApiKeyConfig(apiKey, candidate.Source);
                }
            }

            throw new InvalidOperationException(
                $"Missing required MCP API key. Set {McpServerName}:headers:x-apikey or ConnectionStrings__{LegacyApiKeyConnectionStringName}.");
        }

        private static string TrimPastedQuotes(string value)
        {
            return value.Trim('"', '\'', '\u201c', '\u201d');
        }

        private static string MaskApiKey(string apiKey)
        {
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)))[..12];

            if (apiKey.Length <= 8)
            {
                return $"<redacted; length={apiKey.Length}; sha256={fingerprint}>";
            }

            return $"{apiKey[..4]}...{apiKey[^4..]} (length={apiKey.Length}; sha256={fingerprint})";
        }

        private sealed record ApiKeyConfig(string Value, string Source);
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

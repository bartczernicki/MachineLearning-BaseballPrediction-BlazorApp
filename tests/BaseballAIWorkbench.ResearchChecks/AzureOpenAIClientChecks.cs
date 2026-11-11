using System.ClientModel.Primitives;
using System.Text.Json;
using BaseballAIWorkbench.ApiService;
using Microsoft.Extensions.AI;
using OpenAI;

namespace BaseballAIWorkbench.ResearchChecks;

internal static class AzureOpenAIClientChecks
{
    private const string FixtureKey = "offline-gateway-key";
    private const string Deployment = "configured-model-deployment";

    public static async Task RunAsync()
    {
        foreach (var (endpoint, expectedUrl) in new[]
        {
            ("https://fixture.invalid", "https://fixture.invalid/openai/v1/responses"),
            ("https://fixture.invalid/", "https://fixture.invalid/openai/v1/responses"),
            ("https://fixture.invalid/gateway", "https://fixture.invalid/gateway/openai/v1/responses"),
            ("https://fixture.invalid/gateway/", "https://fixture.invalid/gateway/openai/v1/responses"),
            ("https://fixture.invalid/openai/v1", "https://fixture.invalid/openai/v1/responses"),
            ("https://fixture.invalid/openai/v1/", "https://fixture.invalid/openai/v1/responses"),
            ("https://fixture.invalid/gateway/openai/v1", "https://fixture.invalid/gateway/openai/v1/responses"),
            ("https://fixture.invalid/gateway/openai/v1/", "https://fixture.invalid/gateway/openai/v1/responses")
        })
        {
            using var handler = new RequestFixture(expectedUrl);
            using var httpClient = new HttpClient(handler);
            var client = AzureOpenAIClientFactory.Create(endpoint, FixtureKey, new OpenAIClientOptions
            {
                Transport = new HttpClientPipelineTransport(httpClient),
                RetryPolicy = new ClientRetryPolicy(0)
            });
#pragma warning disable OPENAI001 // Exercise the same Responses adapter as the application.
            using var chatClient = client.GetResponsesClient().AsIChatClient(Deployment);
#pragma warning restore OPENAI001
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "Reply with OK.")], cancellationToken: timeout.Token);
            Check.That(handler.Calls == 1 && response.Text == "OK",
                $"Configured endpoint {endpoint} completes exactly one model request");
        }

        Console.WriteLine("PASS Azure OpenAI client: resource and gateway paths, existing v1 endpoints, API-key authentication, and configured deployment reach the Responses request.");
    }

    private sealed class RequestFixture(string expectedUrl) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Check.That(request.Method == HttpMethod.Post && request.RequestUri?.AbsoluteUri == expectedUrl,
                "Responses preserves the configured endpoint path and appends its route exactly once");
            Check.That(request.Headers.TryGetValues("api-key", out var keys) && keys.SequenceEqual([FixtureKey]),
                "Responses authenticates using the configured api-key header");
            Check.That(request.Headers.Authorization is null,
                "API-key authentication does not send an incompatible Bearer credential");
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Check.That(body.RootElement.GetProperty("model").GetString() == Deployment,
                "Responses sends the configured Azure deployment name");
            return ScriptedResponses.Message("OK");
        }
    }
}

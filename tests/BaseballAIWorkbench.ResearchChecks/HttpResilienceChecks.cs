using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly.Timeout;

namespace BaseballAIWorkbench.ResearchChecks;

internal static class HttpResilienceChecks
{
    private static readonly HttpStatusCode[] TransientStatuses =
    [
        HttpStatusCode.RequestTimeout, HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError, HttpStatusCode.ServiceUnavailable, HttpStatusCode.GatewayTimeout
    ];

    public static async Task RunAsync()
    {
        using (var fixture = new ResilienceFixture())
        {
            foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete, HttpMethod.Connect })
            {
                foreach (var status in TransientStatuses)
                {
                    fixture.Handler.Prepare((request, _, _) => Task.FromResult(Response(request, status)));
                    using var request = new HttpRequestMessage(method, "/analysis");
                    using var response = await fixture.Client.SendAsync(request).WaitAsync(TimeSpan.FromSeconds(10));
                    Check.That(response.StatusCode == status && fixture.Handler.Attempts == 1,
                        $"{method} does not repeat paid work after HTTP {(int)status}");
                }

                fixture.Handler.Prepare((_, _, _) => throw new HttpRequestException("Offline transport failure"));
                using var failedRequest = new HttpRequestMessage(method, "/analysis");
                await ExpectAsync<HttpRequestException>(() => fixture.Client.SendAsync(failedRequest));
                Check.That(fixture.Handler.Attempts == 1, $"{method} does not retry transport failures");
            }

            foreach (var status in TransientStatuses)
            {
                fixture.Handler.Prepare((request, attempt, _) => Task.FromResult(Response(request,
                    attempt == 1 ? status : HttpStatusCode.OK)));
                using var response = await fixture.Client.GetAsync("/players").WaitAsync(TimeSpan.FromSeconds(10));
                Check.That(response.IsSuccessStatusCode && fixture.Handler.Attempts == 2,
                    $"GET recovers from HTTP {(int)status} with a retry");
            }

            fixture.Handler.Prepare((request, attempt, _) => attempt == 1
                ? throw new HttpRequestException("Offline transport failure")
                : Task.FromResult(Response(request, HttpStatusCode.OK)));
            using (var response = await fixture.Client.GetAsync("/players").WaitAsync(TimeSpan.FromSeconds(10)))
                Check.That(response.IsSuccessStatusCode && fixture.Handler.Attempts == 2, "GET retries transport failures");

            fixture.Handler.Prepare((request, _, _) => Task.FromResult(Response(request, HttpStatusCode.ServiceUnavailable)));
            using (var response = await fixture.Client.GetAsync("/players").WaitAsync(TimeSpan.FromSeconds(10)))
                Check.That(response.StatusCode == HttpStatusCode.ServiceUnavailable && fixture.Handler.Attempts == 6,
                    "GET retains five retries after its initial attempt");

            Check.That(fixture.ProductionOptionsValidated, "The actual service-defaults resilience options were validated");
            Check.That(fixture.Client.Timeout == Timeout.InfiniteTimeSpan, "The resilience pipeline owns the client timeout");
        }

        await CheckDeadlineAsync();
        await CheckCallerCancellationAsync();
        Console.WriteLine("PASS HTTP resilience: unsafe requests are never retried, GET retries recover, production budgets validate, and cancellation stops transport work.");
    }

    private static async Task CheckDeadlineAsync()
    {
        // Leave room for a retry after the test attempt expires, so an unsafe timeout retry cannot hide behind the total budget.
        using var fixture = new ResilienceFixture(TimeSpan.FromMilliseconds(100));
        var canceled = Check.Signal();
        fixture.Handler.Prepare(async (_, _, token) =>
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { canceled.TrySetResult(); throw; }
            throw new InvalidOperationException("The blocked fixture transport unexpectedly completed");
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/analysis");
        await ExpectAsync<TimeoutRejectedException>(() => fixture.Client.SendAsync(request));
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Check.That(fixture.Handler.Attempts == 1, "An expired POST deadline cancels transport work without retrying");
        Check.That(fixture.ProductionOptionsValidated, "Short deadline tests first validate the production timeout settings");
    }

    private static async Task CheckCallerCancellationAsync()
    {
        using var fixture = new ResilienceFixture();
        using var cancellation = new CancellationTokenSource();
        var started = Check.Signal();
        var canceled = Check.Signal();
        fixture.Handler.Prepare(async (_, _, token) =>
        {
            started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { canceled.TrySetResult(); throw; }
            throw new InvalidOperationException("The blocked fixture transport unexpectedly completed");
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/analysis");
        var operation = fixture.Client.SendAsync(request, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await ExpectAsync<OperationCanceledException>(() => operation);
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Check.That(fixture.Handler.Attempts == 1, "Caller cancellation stops a POST without retrying");
    }

    private static HttpResponseMessage Response(HttpRequestMessage request, HttpStatusCode status) => new(status)
    {
        RequestMessage = request
    };

    private static async Task ExpectAsync<TException>(Func<Task<HttpResponseMessage>> operation) where TException : Exception
    {
        try { using var response = await operation().WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (TException) { return; }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}");
    }

    private sealed class ResilienceFixture : IDisposable
    {
        private readonly IHost _host;
        public CountingHandler Handler { get; } = new();
        public HttpClient Client { get; }
        public bool ProductionOptionsValidated { get; private set; }

        public ResilienceFixture(TimeSpan? testDeadline = null)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.AddServiceDefaults();
            builder.Services.AddHttpClient("offline-resilience", client =>
            {
                client.BaseAddress = new Uri("http://fixture.invalid");
                client.Timeout = Timeout.InfiniteTimeSpan;
            }).ConfigurePrimaryHttpMessageHandler(() => Handler);
            builder.Services.PostConfigureAll<HttpStandardResilienceOptions>(options =>
            {
                Check.That(options.TotalRequestTimeout.Timeout == TimeSpan.FromSeconds(210), "Total timeout allows the API's 180-second deadline plus 30 seconds");
                Check.That(options.AttemptTimeout.Timeout == TimeSpan.FromSeconds(210), "An analysis attempt receives the full 210-second client budget");
                Check.That(options.CircuitBreaker.SamplingDuration == TimeSpan.FromSeconds(420), "Circuit-breaker sampling covers twice the attempt timeout");
                Check.That(options.Retry.MaxRetryAttempts == 5 && options.Retry.Delay == TimeSpan.FromSeconds(2), "Safe-method retry settings remain unchanged");
                ProductionOptionsValidated = true;
                // Keep the production predicates and strategy validation; remove waiting only in these offline checks.
                options.Retry.Delay = TimeSpan.Zero;
                options.Retry.UseJitter = false;
                if (testDeadline is { } deadline)
                {
                    options.TotalRequestTimeout.Timeout = deadline * 10;
                    options.AttemptTimeout.Timeout = deadline;
                }
            });
            _host = builder.Build();
            Client = _host.Services.GetRequiredService<IHttpClientFactory>().CreateClient("offline-resilience");
        }

        public void Dispose()
        {
            Client.Dispose();
            _host.Dispose();
            Handler.Dispose();
        }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private int _attempts;
        private Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> _respond =
            (_, _, _) => throw new InvalidOperationException("Prepare the fixture before sending a request");
        public int Attempts => Volatile.Read(ref _attempts);

        public void Prepare(Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> respond)
        {
            _respond = respond;
            Interlocked.Exchange(ref _attempts, 0);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _respond(request, Interlocked.Increment(ref _attempts), cancellationToken);
    }
}

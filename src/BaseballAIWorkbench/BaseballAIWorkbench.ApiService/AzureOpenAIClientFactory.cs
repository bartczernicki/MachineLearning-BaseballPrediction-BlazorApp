using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace BaseballAIWorkbench.ApiService;

internal static class AzureOpenAIClientFactory
{
    internal static OpenAIClient Create(string endpoint, string apiKey, OpenAIClientOptions? options = null)
    {
        var endpointUri = new UriBuilder(new Uri(endpoint, UriKind.Absolute));
        if (endpointUri.Scheme is not ("https" or "http") ||
            !string.IsNullOrEmpty(endpointUri.UserName) || !string.IsNullOrEmpty(endpointUri.Password) ||
            !string.IsNullOrEmpty(endpointUri.Query) || !string.IsNullOrEmpty(endpointUri.Fragment))
        {
            throw new ArgumentException("AOAIEndpoint must be an HTTP(S) base URL without credentials, a query, or a fragment.", nameof(endpoint));
        }

        // Keep gateway prefixes, and accept an already-configured Azure OpenAI v1 base URL.
        var path = endpointUri.Path.TrimEnd('/');
        if (!path.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            path += "/openai/v1";
        }
        endpointUri.Path = path + "/";

        options ??= new OpenAIClientOptions();
        options.Endpoint = endpointUri.Uri;

        // Azure OpenAI and APIM's Azure OpenAI gateway accept the key in this header.
        var authentication = ApiKeyAuthenticationPolicy.CreateHeaderApiKeyPolicy(new ApiKeyCredential(apiKey), "api-key");
#pragma warning disable OPENAI001 // The SDK's custom authentication constructor is experimental, like its Responses client.
        return new OpenAIClient(authentication, options);
#pragma warning restore OPENAI001
    }
}

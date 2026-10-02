using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.Cdn;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.Resources;

// The pinned Aspire Front Door integration uses the preview Azure Provisioning CDN APIs.
#pragma warning disable AZPROVISION001

internal static class FrontDoorConfiguration
{
    public static void Configure(AzureResourceInfrastructure infrastructure)
    {
        var resources = infrastructure.GetProvisionableResources().ToArray();
        var profile = resources.OfType<CdnProfile>().Single();
        var endpoint = resources.OfType<FrontDoorEndpoint>().Single();
        var originGroup = resources.OfType<FrontDoorOriginGroup>().Single();
        var origin = resources.OfType<FrontDoorOrigin>().Single();
        var route = resources.OfType<FrontDoorRoute>().Single();

        profile.SkuName = CdnSkuName.StandardAzureFrontDoor;

        // Fix the public hostname prefix; Azure still adds its generated hash and zone suffix.
        // The route and URL output reference this endpoint, so they follow its returned hostname.
        endpoint.Name = "BaseballAIWorkBench";

        // Allow a sleeping origin up to 240 seconds to respond. This accommodates startup
        // latency but does not keep replicas warm or eliminate cold starts.
        profile.OriginResponseTimeoutSeconds = 240;

        // A single origin has no failover target. Synthetic probes can prevent scale-to-zero,
        // so emit an explicit Bicep null; assigning C# null to this SDK property is unsupported.
        ((IBicepValue)originGroup.HealthProbeSettings).Expression = new NullLiteralExpression();

        // Aspire supplies the deployed ACA hostname for both HostName and OriginHostHeader.
        // Encrypt both network hops and validate the origin certificate against that hostname.
        origin.EnforceCertificateNameCheck = true;
        origin.HttpsPort = 443;
        route.SupportedProtocols = [FrontDoorEndpointProtocol.Http, FrontDoorEndpointProtocol.Https];
        route.HttpsRedirect = HttpsRedirect.Enabled;
        route.ForwardingProtocol = ForwardingProtocol.HttpsOnly;
        route.LinkToDefaultDomain = LinkToDefaultDomain.Enabled;

        // Leave the generated /* route's CacheConfiguration unset: HTML, user-specific
        // responses, and Blazor negotiation/connections must reach the application.
        var ruleSet = new FrontDoorRuleSet("staticAssets")
        {
            Parent = profile,
            Name = "staticAssets"
        };
        infrastructure.Add(ruleSet);

        // Azure permits at most 10 match values per condition. Separate rules provide OR
        // behavior across extension groups; two conditions in one rule would be ANDed.
        var staticAssets = CreateStaticAssetRule(ruleSet, "cacheStaticAssets", 1,
            ["css", "js", "mjs", "png", "jpg", "jpeg", "gif", "svg", "ico", "webp"]);
        var staticFonts = CreateStaticAssetRule(ruleSet, "cacheStaticFonts", 2, ["woff", "woff2"]);

        infrastructure.Add(staticAssets);
        infrastructure.Add(staticFonts);
        route.RuleSets.Add(new WritableSubResource { Id = ruleSet.Id });
        // The route must not become active before both parts of its cache policy exist.
        route.DependsOn.Add(staticAssets);
        route.DependsOn.Add(staticFonts);
    }

    private static FrontDoorRule CreateStaticAssetRule(
        FrontDoorRuleSet ruleSet, string name, int order, string[] extensions)
    {
        return new FrontDoorRule(name)
        {
            Parent = ruleSet,
            Name = name,
            Order = order,
            MatchProcessingBehavior = MatchProcessingBehavior.Stop,
            Conditions =
            [
                new DeliveryRuleRequestMethodCondition
                {
                    Properties = WithParameterTypeName(new RequestMethodMatchCondition
                    {
                        RequestMethodOperator = RequestMethodOperator.Equal,
                        MatchValues = [RequestMethodMatchConditionMatchValue.Get]
                    }, "DeliveryRuleRequestMethodConditionParameters")
                },
                // Conditions are ANDed; values within each condition are ORed. The path
                // prefixes also cover fingerprinted root stylesheets without caching pages.
                new DeliveryRuleUriPathCondition
                {
                    Properties = WithParameterTypeName(new UriPathMatchCondition
                    {
                        UriPathOperator = UriPathOperator.BeginsWith,
                        Transforms = [PreTransformCategory.Lowercase],
                        MatchValues =
                        [
                            "_framework/", "_content/", "lib/", "images/",
                            "site.", "baseballaiworkbench.web.", "favicon."
                        ]
                    }, "DeliveryRuleUrlPathMatchConditionParameters")
                },
                new DeliveryRuleUriFileExtensionCondition
                {
                    Properties = WithParameterTypeName(new UriFileExtensionMatchCondition
                    {
                        UriFileExtensionOperator = UriFileExtensionOperator.Equal,
                        Transforms = [PreTransformCategory.Lowercase],
                        MatchValues = [.. extensions]
                    }, "DeliveryRuleUrlFileExtensionMatchConditionParameters")
                }
            ],
            Actions =
            [
                new DeliveryRuleRouteConfigurationOverrideAction
                {
                    Properties = WithParameterTypeName(new RouteConfigurationOverrideActionProperties
                    {
                        CacheConfiguration = new CacheConfiguration
                        {
                            // Fingerprinted assets keep their origin TTL. The one-hour fallback
                            // applies only without a TTL and is unrelated to the replica cooldown.
                            // Front Door still honors private, no-store, and no-cache responses,
                            // including the older, unfingerprinted Blazored Typeahead assets.
                            CacheBehavior = RuleCacheBehavior.OverrideIfOriginMissing,
                            CacheDuration = TimeSpan.FromHours(1),
                            // Preserve query-string distinctions instead of sharing their entries.
                            QueryStringCachingBehavior = RuleQueryStringCachingBehavior.UseQueryString,
                            IsCompressionEnabled = RuleIsCompressionEnabled.Enabled
                        }
                    }, "DeliveryRuleRouteConfigurationOverrideActionParameters")
                }
            ]
        };

    }

    private static T WithParameterTypeName<T>(T parameters, string typeName) where T : IBicepValue
    {
        // The pinned preview SDK omits ARM-required typeName values. Snapshot each fully
        // configured parameter object and append its discriminator using the SDK expression API.
        var value = (ObjectExpression)parameters.Compile();
        parameters.Expression = new ObjectExpression(
            [.. value.Properties, new PropertyExpression("typeName", new StringLiteralExpression(typeName))]);
        return parameters;
    }
}

#pragma warning restore AZPROVISION001

using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace TradeScanner.Infrastructure.Http;

public static class ResilientHttpClientFactory
{
    public static IHttpClientBuilder AddResilientHttpClient(
        this IServiceCollection services,
        string name,
        int retryCount = 3,
        int timeoutSeconds = 15)
    {
        return services
            .AddHttpClient(name)
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .AddPolicyHandler(GetRetryPolicy(retryCount))
            .AddPolicyHandler(GetCircuitBreakerPolicy())
            .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(timeoutSeconds));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int retryCount) =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(retryCount, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}

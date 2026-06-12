using TradeScanner.Core.Domain.Enums;

namespace TradeScanner.Core.Domain.Entities;

public class ProviderConfig
{
    public int Id { get; set; }
    public MarketDataProvider Provider { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int Priority { get; set; }
    public string EncryptedApiKey { get; set; } = string.Empty;
    public int RateLimitPerMinute { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int TotalRequests { get; set; }
    public int FailedRequests { get; set; }
    public bool IsPremium { get; set; }
}

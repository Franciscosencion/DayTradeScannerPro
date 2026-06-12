namespace TradeScanner.Core.Domain.ValueObjects;

public record RealtimeTrade(
    string Symbol,
    decimal Price,
    long Volume,
    DateTime Timestamp);

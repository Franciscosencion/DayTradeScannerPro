namespace TradeScanner.Core.Domain.ValueObjects;

public record Quote(
    string Symbol,
    decimal Price,
    decimal Open,
    decimal High,
    decimal Low,
    decimal PreviousClose,
    long Volume,
    decimal ChangePercent,
    decimal Change,
    DateTime Timestamp)
{
    public bool IsValid => Price > 0 && Volume >= 0;
}

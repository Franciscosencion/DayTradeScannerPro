namespace TradeScanner.Core.Domain.ValueObjects;

public record PriceBar(
    string Symbol,
    DateTime Timestamp,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume,
    string Interval)
{
    public decimal Range => High - Low;
    public decimal BodySize => Math.Abs(Close - Open);
    public bool IsBullish => Close >= Open;
}

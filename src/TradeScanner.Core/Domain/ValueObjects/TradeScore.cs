namespace TradeScanner.Core.Domain.ValueObjects;

public record TradeScore(
    int Score,
    decimal MomentumComponent,
    decimal VolumeComponent,
    decimal TechnicalComponent,
    decimal NewsComponent,
    DateTime CalculatedAt)
{
    public static readonly TradeScore Empty = new(0, 0, 0, 0, 0, DateTime.UtcNow);

    public bool IsStrong => Score >= 75;
    public bool IsModerate => Score >= 50 && Score < 75;
    public bool IsWeak => Score < 50;
}

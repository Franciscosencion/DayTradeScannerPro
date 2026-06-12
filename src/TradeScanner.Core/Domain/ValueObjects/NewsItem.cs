namespace TradeScanner.Core.Domain.ValueObjects;

public record NewsItem(
    string Id,
    string Symbol,
    string Headline,
    string Summary,
    string Source,
    string Url,
    double SentimentScore,
    DateTime PublishedAt);

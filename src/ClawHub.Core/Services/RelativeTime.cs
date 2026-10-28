namespace ClawHub.Core.Services;

/// <summary>Chiave di traduzione e numero per frasi come "ieri" o "3 giorni fa".</summary>
public readonly record struct RelativeTimeText(string Key, int Value);

public static class RelativeTime
{
    /// <summary>Descrive <paramref name="when"/> rispetto a <paramref name="now"/>, a giorni di calendario locali.</summary>
    public static RelativeTimeText Describe(DateTimeOffset when, DateTimeOffset now)
    {
        var days = (now.LocalDateTime.Date - when.LocalDateTime.Date).Days;
        return days switch
        {
            <= 0 => new("Time.Today", 0),
            1 => new("Time.Yesterday", 1),
            < 7 => new("Time.DaysAgo", days),
            < 30 => days / 7 == 1 ? new("Time.WeekAgo", 1) : new("Time.WeeksAgo", days / 7),
            < 365 => days / 30 == 1 ? new("Time.MonthAgo", 1) : new("Time.MonthsAgo", days / 30),
            _ => days / 365 == 1 ? new("Time.YearAgo", 1) : new("Time.YearsAgo", days / 365),
        };
    }
}

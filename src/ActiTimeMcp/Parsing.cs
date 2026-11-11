using System.Globalization;
using System.Text.RegularExpressions;
using ModelContextProtocol;

namespace ActiTimeMcp;

public static partial class Parsing
{
    /// <summary>
    /// Parses human durations into minutes: "90", "90m", "1.5h", "1,5h", "1h30", "1h 30m", "1:30", "2 hours".
    /// A bare number is minutes if it has no decimal part and is &gt; 12, otherwise hours.
    /// </summary>
    public static int ParseDurationMinutes(string input)
    {
        var s = input.Trim().ToLowerInvariant().Replace(',', '.');
        if (s.Length == 0) throw new McpException("Duration is empty.");

        var hm = HoursColonMinutes().Match(s);
        if (hm.Success)
            return Check(int.Parse(hm.Groups[1].Value) * 60 + int.Parse(hm.Groups[2].Value), input);

        var parts = UnitParts().Matches(s);
        var hasUnit = parts.Any(m => m.Groups[2].Success && m.Groups[2].Length > 0);
        if (hasUnit && UnitParts().Replace(s, "").Trim().Length == 0)
        {
            double total = 0;
            var previousWasHours = false;
            foreach (Match m in parts)
            {
                var n = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                var unit = m.Groups[2].Value;
                // "1h30": a unitless number after hours means minutes.
                var isHours = unit.StartsWith('h') || (unit.Length == 0 && !previousWasHours);
                total += isHours ? n * 60 : n;
                previousWasHours = isHours;
            }
            return Check((int)Math.Round(total), input);
        }

        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var bare))
        {
            var isMinutes = bare == Math.Floor(bare) && bare > 12;
            return Check((int)Math.Round(isMinutes ? bare : bare * 60), input);
        }

        throw new McpException($"Could not understand duration '{input}'. Use e.g. '1h30m', '1.5h', '45m' or '1:30'.");
    }

    private static int Check(int minutes, string input) =>
        minutes is < 0 or > 24 * 60
            ? throw new McpException($"Duration '{input}' resolves to {minutes} minutes, which is outside 0–1440.")
            : minutes;

    public static DateOnly ParseDate(string? input, TimeZoneInfo tz)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime);
        if (string.IsNullOrWhiteSpace(input)) return today;

        var s = input.Trim().ToLowerInvariant();
        switch (s)
        {
            case "today": return today;
            case "yesterday": return today.AddDays(-1);
        }

        var dayName = s.StartsWith("last ") ? s[5..].Trim() : s;
        if (!char.IsDigit(dayName[0]) && Enum.TryParse<DayOfWeek>(dayName, true, out var dow))
        {
            // Most recent occurrence of that weekday, not counting today ("monday" on a Wednesday = 2 days ago).
            var d = today.AddDays(-1);
            while (d.DayOfWeek != dow) d = d.AddDays(-1);
            return d;
        }

        if (DateOnly.TryParseExact(input.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            return exact;

        throw new McpException($"Could not understand date '{input}'. Use yyyy-MM-dd, 'today', 'yesterday' or a weekday name.");
    }

    public static string FormatMinutes(int minutes) =>
        minutes == 0 ? "0m" : minutes % 60 == 0 ? $"{minutes / 60}h" : minutes < 60 ? $"{minutes}m" : $"{minutes / 60}h{minutes % 60:00}m";

    [GeneratedRegex(@"^(\d{1,2}):(\d{2})$")]
    private static partial Regex HoursColonMinutes();

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*(hours?|hrs?|h|minutes?|mins?|m)?(?=\s|\d|$)")]
    private static partial Regex UnitParts();
}

using System.Globalization;

namespace SkyFlipperSolo.Services.Filters;

public static class RangeParser
{
    public static List<(long Min, long Max)> ParseLongRanges(string? raw)
    {
        var ranges = new List<(long Min, long Max)>();
        if (string.IsNullOrWhiteSpace(raw))
            return ranges;

        foreach (var segment in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var value = segment;
            if (value.EndsWith("-", StringComparison.Ordinal))
                value = ">" + value.TrimEnd('-');

            if (value.Equals("any", StringComparison.OrdinalIgnoreCase))
                value = ">0";
            if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
                value = "0";

            if (value.Contains('-'))
            {
                var parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 2 && TryParseLong(parts[0], out var min) && TryParseLong(parts[1], out var max))
                {
                    ranges.Add((Math.Min(min, max), Math.Max(min, max)));
                }
                continue;
            }

            if (value.StartsWith('<'))
            {
                if (TryParseLong(value.Substring(1), out var max))
                    ranges.Add((1, Math.Max(1, max - 1)));
                continue;
            }

            if (value.StartsWith('>'))
            {
                if (TryParseLong(value.Substring(1), out var min))
                    ranges.Add((min + 1, long.MaxValue));
                continue;
            }

            if (TryParseLong(value, out var exact))
                ranges.Add((exact, exact));
        }

        return ranges;
    }

    private static bool TryParseLong(string input, out long value)
    {
        return long.TryParse(input.Replace("_", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}

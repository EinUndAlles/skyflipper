using System.Globalization;

namespace SkyFlipperSolo.Services.Filters;

public sealed class FilterContext
{
    public FilterContext(IDictionary<string, string> filters)
    {
        Filters = filters;
    }

    public IDictionary<string, string> Filters { get; }

    public string? Get(string key)
    {
        return Filters.TryGetValue(key, out var value) ? value : null;
    }

    public bool TryGetLong(string key, out long value)
    {
        value = 0;
        var raw = Get(key);
        return raw != null && long.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}

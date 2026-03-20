using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class FilterEngine
{
    private readonly FilterRegistry _registry;

    public FilterEngine(FilterRegistry registry)
    {
        _registry = registry;
    }

    public IQueryable<Auction> ApplyFilters(IQueryable<Auction> query, IDictionary<string, string> filters)
    {
        var context = new FilterContext(filters);
        foreach (var filter in filters)
        {
            if (string.IsNullOrEmpty(filter.Value))
                continue;

            if (!_registry.TryGet(filter.Key, out var filterImpl))
                continue;

            try
            {
                query = filterImpl.Apply(query, context);
            }
            catch
            {
                // Skip filters that fail (e.g., stale DbContext on parameterized filters)
            }
        }

        return query;
    }
}

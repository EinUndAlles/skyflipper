using System.Collections.Immutable;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class FilterRegistry
{
    private readonly Dictionary<string, IFilter> _filters = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<IFilter> Filters => _filters.Values;

    public void Add(IFilter filter)
    {
        _filters[filter.Name] = filter;
    }

    public bool TryGet(string name, out IFilter filter) => _filters.TryGetValue(name, out filter!);

    public IReadOnlyDictionary<string, IFilter> Snapshot() => _filters.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<IFilter> FiltersFor(FilterApplicabilityContext context)
    {
        foreach (var filter in _filters.Values)
        {
            if (filter is IApplicableFilter applicable)
            {
                if (!applicable.IsApplicable(context))
                    continue;
            }
            else if (!filter.IsApplicable(context.Tag))
            {
                continue;
            }

            yield return filter;
        }
    }
}

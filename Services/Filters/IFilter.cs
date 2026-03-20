using System.Linq.Expressions;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public interface IFilter
{
    string Name { get; }
    FilterType FilterType { get; }
    IEnumerable<string> OptionsGet(FilterContext context);
    IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context);

    /// <summary>
    /// Whether this filter is applicable for items with the given tag.
    /// Default: true (universal). Override for tag-specific filters.
    /// </summary>
    bool IsApplicable(string tag) => true;
}

namespace SkyFlipperSolo.Services.Filters;

public interface IApplicableFilter
{
    bool IsApplicable(FilterApplicabilityContext context);
}

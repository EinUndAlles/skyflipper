using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class EthermergeFilter : BoolNbtFilter
{
    public EthermergeFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "Ethermerge";
    protected override string PropName => "ethermerge";
}

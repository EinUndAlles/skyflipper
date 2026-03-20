using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

/// <summary>
/// Base class for drill part / ability scroll filters.
/// String equality match on NBT key, scoped to specific item tags.
/// </summary>
public abstract class DrillPartFilter : NbtStringFilter
{
    protected DrillPartFilter(AppDbContext dbContext) : base(dbContext) { }
}

public sealed class DrillPartEngineFilter : DrillPartFilter
{
    public DrillPartEngineFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "DrillPartEngine";
    protected override string PropName => "drill_part_engine";
}

public sealed class DrillPartFuelTankFilter : DrillPartFilter
{
    public DrillPartFuelTankFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "DrillPartFuelTank";
    protected override string PropName => "drill_part_fuel_tank";
}

public sealed class DrillPartUpgradeModuleFilter : DrillPartFilter
{
    public DrillPartUpgradeModuleFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "DrillPartUpgradeModule";
    protected override string PropName => "drill_part_upgrade_module";
}

public sealed class PowerAbilityScrollFilter : DrillPartFilter
{
    public PowerAbilityScrollFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "PowerAbilityScroll";
    protected override string PropName => "power_ability_scroll";
}

public sealed class TunedTransmissionFilter : NbtNumberFilter
{
    public TunedTransmissionFilter(AppDbContext dbContext) : base(dbContext, "tuned_transmission") { }
}

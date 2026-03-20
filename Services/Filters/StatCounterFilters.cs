using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class BaseStatBoostFilter : NbtNumberFilter
{
    public BaseStatBoostFilter(AppDbContext dbContext) : base(dbContext, "baseStatBoostPercentage") { }
}

public sealed class ManaDisintegratorFilter : NbtNumberFilter
{
    public ManaDisintegratorFilter(AppDbContext dbContext) : base(dbContext, "mana_disintegrator_count") { }
}

public sealed class FarmedCultivatingFilter : NbtNumberFilter
{
    public FarmedCultivatingFilter(AppDbContext dbContext) : base(dbContext, "farmed_cultivating") { }
}

public sealed class MinedCropsFilter : NbtNumberFilter
{
    public MinedCropsFilter(AppDbContext dbContext) : base(dbContext, "mined_crops") { }
}

public sealed class BlocksBrokenFilter : NbtNumberFilter
{
    public BlocksBrokenFilter(AppDbContext dbContext) : base(dbContext, "blocksBroken") { }
}

public sealed class ThunderChargeFilter : NbtNumberFilter
{
    public ThunderChargeFilter(AppDbContext dbContext) : base(dbContext, "thunder_charge") { }
}

public sealed class CollectedCoinsFilter : NbtNumberFilter
{
    public CollectedCoinsFilter(AppDbContext dbContext) : base(dbContext, "collected_coins") { }
}

public sealed class ChimeraFoundFilter : NbtNumberFilter
{
    public ChimeraFoundFilter(AppDbContext dbContext) : base(dbContext, "chimera_found") { }
}

public sealed class PickonimbusDurabilityFilter : NbtNumberFilter
{
    public PickonimbusDurabilityFilter(AppDbContext dbContext) : base(dbContext, "pickonimbus_durability") { }
}

public sealed class IntelligenceEarnedFilter : NbtNumberFilter
{
    public IntelligenceEarnedFilter(AppDbContext dbContext) : base(dbContext, "intelligence_earned") { }
}

public sealed class RaffleWinCountFilter : NbtNumberFilter
{
    public RaffleWinCountFilter(AppDbContext dbContext) : base(dbContext, "raffle_win") { }
}

public sealed class RaffleYearCountFilter : NbtNumberFilter
{
    public RaffleYearCountFilter(AppDbContext dbContext) : base(dbContext, "raffle_year") { }
}

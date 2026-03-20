using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class ZombieKillsFilter : NbtNumberFilter
{
    public ZombieKillsFilter(AppDbContext dbContext) : base(dbContext, "zombie_kills") { }
}

public sealed class SpiderKillsFilter : NbtNumberFilter
{
    public SpiderKillsFilter(AppDbContext dbContext) : base(dbContext, "spider_kills") { }
}

public sealed class EmanKillsFilter : NbtNumberFilter
{
    public EmanKillsFilter(AppDbContext dbContext) : base(dbContext, "eman_kills") { }
}

public sealed class ExpertiseKillsFilter : NbtNumberFilter
{
    public ExpertiseKillsFilter(AppDbContext dbContext) : base(dbContext, "expertise_kills") { }
}

public sealed class RaiderKillsFilter : NbtNumberFilter
{
    public RaiderKillsFilter(AppDbContext dbContext) : base(dbContext, "raider_kills") { }
}

public sealed class SwordKillsFilter : NbtNumberFilter
{
    public SwordKillsFilter(AppDbContext dbContext) : base(dbContext, "sword_kills") { }
}

public sealed class BloodGodKillsFilter : NbtNumberFilter
{
    public BloodGodKillsFilter(AppDbContext dbContext) : base(dbContext, "blood_god_kills") { }
}

public sealed class BlazeKillsFilter : NbtNumberFilter
{
    public BlazeKillsFilter(AppDbContext dbContext) : base(dbContext, "blaze_kills") { }
}

public sealed class YogsKilledFilter : NbtNumberFilter
{
    public YogsKilledFilter(AppDbContext dbContext) : base(dbContext, "yogsKilled") { }
}

public sealed class BlazeConsumerFilter : NbtNumberFilter
{
    public BlazeConsumerFilter(AppDbContext dbContext) : base(dbContext, "blaze_consumer") { }
}

public sealed class RunicKillsFilter : NbtNumberFilter
{
    public RunicKillsFilter(AppDbContext dbContext) : base(dbContext, "runic_kills") { }
}

public sealed class HandlesFoundFilter : NbtNumberFilter
{
    public HandlesFoundFilter(AppDbContext dbContext) : base(dbContext, "handles_found") { }
}

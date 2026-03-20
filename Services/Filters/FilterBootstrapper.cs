using Microsoft.Extensions.DependencyInjection;
using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public static class FilterBootstrapper
{
    public static void RegisterCoreFilters(IServiceProvider provider)
    {
        var registry = provider.GetRequiredService<FilterRegistry>();
        var dbContext = provider.GetRequiredService<AppDbContext>();

        // Basic filters
        registry.Add(provider.GetRequiredService<StarsFilter>());
        registry.Add(provider.GetRequiredService<RarityFilter>());
        registry.Add(provider.GetRequiredService<ReforgeFilter>());
        registry.Add(provider.GetRequiredService<BinFilter>());
        registry.Add(provider.GetRequiredService<StartingBidFilter>());
        registry.Add(provider.GetRequiredService<HighestBidFilter>());
        registry.Add(provider.GetRequiredService<CountFilter>());
        registry.Add(provider.GetRequiredService<EnchantmentFilter>());
        registry.Add(provider.GetRequiredService<EnchantLvlFilter>());
        registry.Add(provider.GetRequiredService<HotPotatoCountFilter>());
        registry.Add(provider.GetRequiredService<ArtOfTheWarFilter>());
        registry.Add(provider.GetRequiredService<FarmingForDummiesFilter>());
        registry.Add(provider.GetRequiredService<RecombobulatedFilter>());
        registry.Add(provider.GetRequiredService<EthermergeFilter>());
        registry.Add(provider.GetRequiredService<AbilityScrollFilter>());
        registry.Add(provider.GetRequiredService<SkinFilter>());
        registry.Add(provider.GetRequiredService<WinningBidFilter>());
        registry.Add(provider.GetRequiredService<EditionFilter>());
        registry.Add(provider.GetRequiredService<CapturedPlayerFilter>());
        registry.Add(provider.GetRequiredService<EndBeforeFilter>());
        registry.Add(provider.GetRequiredService<EndAfterFilter>());
        registry.Add(provider.GetRequiredService<ItemCreatedBeforeFilter>());
        registry.Add(provider.GetRequiredService<ItemCreatedAfterFilter>());

        // Pet filters
        registry.Add(provider.GetRequiredService<PetLevelFilter>());
        registry.Add(provider.GetRequiredService<PetItemFilter>());
        registry.Add(provider.GetRequiredService<PetSkinFilter>());
        registry.Add(provider.GetRequiredService<PetExpFilter>());

        // Color filters
        registry.Add(provider.GetRequiredService<ColorFilter>());
        registry.Add(provider.GetRequiredService<HexColorListFilter>());
        registry.Add(provider.GetRequiredService<ExoticColorFilter>());
        registry.Add(provider.GetRequiredService<DyeItemFilter>());

        // Slot / attribute / gem filters
        registry.Add(provider.GetRequiredService<UnlockedSlotsFilter>());
        registry.Add(provider.GetRequiredService<UnlockedSlotsMatchFilter>());
        registry.Add(provider.GetRequiredService<HasAttributeFilter>());
        registry.Add(provider.GetRequiredService<PerfectGemsCountFilter>());
        registry.Add(provider.GetRequiredService<FlawlessGemsCountFilter>());

        // Kills / counter filters
        registry.Add(provider.GetRequiredService<ZombieKillsFilter>());
        registry.Add(provider.GetRequiredService<SpiderKillsFilter>());
        registry.Add(provider.GetRequiredService<EmanKillsFilter>());
        registry.Add(provider.GetRequiredService<ExpertiseKillsFilter>());
        registry.Add(provider.GetRequiredService<RaiderKillsFilter>());
        registry.Add(provider.GetRequiredService<SwordKillsFilter>());
        registry.Add(provider.GetRequiredService<BloodGodKillsFilter>());
        registry.Add(provider.GetRequiredService<BlazeKillsFilter>());
        registry.Add(provider.GetRequiredService<YogsKilledFilter>());
        registry.Add(provider.GetRequiredService<BlazeConsumerFilter>());
        registry.Add(provider.GetRequiredService<RunicKillsFilter>());
        registry.Add(provider.GetRequiredService<HandlesFoundFilter>());

        // Stat counter filters
        registry.Add(provider.GetRequiredService<BaseStatBoostFilter>());
        registry.Add(provider.GetRequiredService<ManaDisintegratorFilter>());
        registry.Add(provider.GetRequiredService<FarmedCultivatingFilter>());
        registry.Add(provider.GetRequiredService<MinedCropsFilter>());
        registry.Add(provider.GetRequiredService<BlocksBrokenFilter>());
        registry.Add(provider.GetRequiredService<ThunderChargeFilter>());
        registry.Add(provider.GetRequiredService<CollectedCoinsFilter>());
        registry.Add(provider.GetRequiredService<ChimeraFoundFilter>());
        registry.Add(provider.GetRequiredService<PickonimbusDurabilityFilter>());
        registry.Add(provider.GetRequiredService<IntelligenceEarnedFilter>());
        registry.Add(provider.GetRequiredService<RaffleWinCountFilter>());
        registry.Add(provider.GetRequiredService<RaffleYearCountFilter>());

        // Special filters
        registry.Add(provider.GetRequiredService<IntelligenceBonusFilter>());

        // Gem quality filters (per slot, e.g. ruby0Gem, combat0Gem)
        var gemTypes = new string[]
        {
            "RUBY", "JASPER", "JADE", "TOPAZ", "AMETHYST", "AMBER",
            "SAPPHIRE", "OPAL", "PERIDOT", "AQUAMARINE", "CITRINE"
        };
        var gemGroups = new string[] { "COMBAT", "OFFENSIVE", "DEFENSIVE", "MINING_", "UNIVERSAL", "CHISEL" };

        foreach (var gem in gemTypes.Concat(gemGroups))
        {
            for (int i = 0; i < 2; i++)
            {
                registry.Add(new GemFilter(dbContext, $"{gem}_{i}"));
            }
        }
        // Special case: AMETHYST has a third slot
        registry.Add(new GemFilter(dbContext, "AMETHYST_2"));

        // Gem type filters (per group slot, e.g. combat0GemType)
        foreach (var group in gemGroups)
        {
            for (int i = 0; i < 2; i++)
            {
                registry.Add(new GemTypeFilter(dbContext, $"{group}_{i}"));
            }
        }
    }
}

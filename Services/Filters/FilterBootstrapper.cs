using Microsoft.Extensions.DependencyInjection;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

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

        // Rune filters
        registry.Add(provider.GetRequiredService<MusicRuneFilter>());
        registry.Add(provider.GetRequiredService<EnchantRuneFilter>());
        registry.Add(provider.GetRequiredService<TidalRuneFilter>());
        registry.Add(provider.GetRequiredService<EndRuneFilter>());

        // Item-specific skin filters
        registry.Add(provider.GetRequiredService<DragonArmorSkinFilter>());
        registry.Add(provider.GetRequiredService<ReaperMaskSkinFilter>());
        registry.Add(provider.GetRequiredService<SnowSuiteSkinFilter>());
        registry.Add(provider.GetRequiredService<TarantulaHelmetSkinFilter>());
        registry.Add(provider.GetRequiredService<FrozenBlazeSkinFilter>());
        registry.Add(provider.GetRequiredService<PerfectHelmetSkinFilter>());
        registry.Add(provider.GetRequiredService<DiversMaskSkinFilter>());
        registry.Add(provider.GetRequiredService<ShadowAssassinSkinFilter>());

        // Bool/flag filters
        registry.Add(provider.GetRequiredService<IsShinyFilter>());
        registry.Add(provider.GetRequiredService<ArtOfPeaceFilter>());
        registry.Add(provider.GetRequiredService<WoodSingularityFilter>());
        registry.Add(provider.GetRequiredService<ModelFilter>());
        registry.Add(provider.GetRequiredService<SoldFilter>());
        registry.Add(provider.GetRequiredService<CleanFilter>());

        // Drill/equipment filters
        registry.Add(provider.GetRequiredService<DrillPartEngineFilter>());
        registry.Add(provider.GetRequiredService<DrillPartFuelTankFilter>());
        registry.Add(provider.GetRequiredService<DrillPartUpgradeModuleFilter>());
        registry.Add(provider.GetRequiredService<PowerAbilityScrollFilter>());
        registry.Add(provider.GetRequiredService<TunedTransmissionFilter>());

        // Per-attribute level filters (dynamic loop)
        var attributeKeys = new string[]
        {
            "lifeline", "breeze", "speed", "experience", "mana_pool",
            "life_regeneration", "blazing_resistance", "arachno_resistance",
            "undead_resistance", "blazing_fortune", "fishing_experience",
            "double_hook", "infection", "trophy_hunter", "fisherman", "hunter",
            "fishing_speed", "life_recovery", "ignition", "combo", "attack_speed",
            "midas_touch", "mana_regeneration", "veteran", "mending", "ender_resistance",
            "dominance", "ender", "mana_steal", "blazing", "elite", "arachno", "undead",
            "warrior", "deadeye", "fortitude", "magic_find"
        };
        foreach (var attr in attributeKeys)
        {
            registry.Add(new AttributeLevelFilter(dbContext, attr));
        }
        // "vitality" alias for "mending" (mending is called vitality in-game)
        registry.Add(new AttributeLevelFilter(dbContext, "mending", "vitality"));

        // Misc string filters
        registry.Add(provider.GetRequiredService<SellerFilter>());
        registry.Add(provider.GetRequiredService<CakeOwnerFilter>());
        registry.Add(provider.GetRequiredService<CakeYearFilter>());
        registry.Add(provider.GetRequiredService<PartyHatYearFilter>());
        registry.Add(provider.GetRequiredService<PartyHatColorFilter>());
        registry.Add(provider.GetRequiredService<PartyHatEmojiFilter>());
        registry.Add(provider.GetRequiredService<FairyColorFilter>());
        registry.Add(provider.GetRequiredService<CrystalColorFilter>());

        // Enchant aliases
        registry.Add(new EnchantBaseFilter(dbContext, EnchantmentType.ultimate_duplex, "ultimate_duplex"));
        registry.Add(new EnchantBaseFilter(dbContext, EnchantmentType.ultimate_reiterate, "ultimate_reiterate"));
        registry.Add(new EnchantBaseFilter(dbContext, EnchantmentType.pristine, "pristine"));
        registry.Add(new EnchantBaseFilter(dbContext, EnchantmentType.pristine, "prismatic"));
        registry.Add(new EnchantBaseFilter(dbContext, EnchantmentType.dragon_hunter, "gravity"));
        registry.Add(new EnchantBaseFilter(dbContext, EnchantmentType.syphon, "drain"));
        registry.Add(new EnchantBaseFilter(dbContext, EnchantmentType.experience, "ExperienceEnchant"));
        registry.Add(new EnchantBaseFilter(dbContext, EnchantmentType.mana_steal, "ManaStealEnchant"));

        // Per-enchant type loop
        foreach (var enchant in Enum.GetValues<EnchantmentType>())
        {
            if (enchant == EnchantmentType.unknown) continue;
            registry.Add(new EnchantBaseFilter(dbContext, enchant));
        }

        // Remaining coflnet filters
        registry.Add(provider.GetRequiredService<CandyFilter>());
        registry.Add(provider.GetRequiredService<JalapenoBookFilter>());
        registry.Add(provider.GetRequiredService<BassWeightFilter>());
        registry.Add(provider.GetRequiredService<ItemTierFilter>());
        registry.Add(provider.GetRequiredService<PowderCoatingFilter>());
        registry.Add(provider.GetRequiredService<GrowthStagesFilter>());
        registry.Add(provider.GetRequiredService<UIdFilter>());
        registry.Add(provider.GetRequiredService<CrabHatColorFilter>());
        registry.Add(provider.GetRequiredService<TalismanEnrichmentFilter>());
        registry.Add(provider.GetRequiredService<DungeonSkillReqFilter>());
        registry.Add(provider.GetRequiredService<RodHookFilter>());
        registry.Add(provider.GetRequiredService<RodLineFilter>());
        registry.Add(provider.GetRequiredService<RodSinkerFilter>());
        registry.Add(provider.GetRequiredService<LogsCutFilter>());
        registry.Add(provider.GetRequiredService<AbsorbLogsFilter>());
        registry.Add(provider.GetRequiredService<AxeBoostersFilter>());
        registry.Add(provider.GetRequiredService<PlarvoidBookFilter>());
        registry.Add(provider.GetRequiredService<ItemIdFilter>());
        registry.Add(provider.GetRequiredService<ItemTagFilter>());
        registry.Add(provider.GetRequiredService<EverythingFilter>());
        registry.Add(provider.GetRequiredService<ItemNameContainsFilter>());
        registry.Add(provider.GetRequiredService<JyrreMaxFilter>());
        registry.Add(provider.GetRequiredService<SecondEnchantmentFilter>());
        registry.Add(provider.GetRequiredService<SecondEnchantLvlFilter>());
        registry.Add(provider.GetRequiredService<NoOtherValuableEnchantsFilter>());
        registry.Add(provider.GetRequiredService<PricePerLevelFilter>());
        registry.Add(provider.GetRequiredService<PricePerUnitFilter>());
        registry.Add(provider.GetRequiredService<CostPerExpPlusBaseFilter>());
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SkyFlipperSolo.Models;

/// <summary>
/// Represents an enchantment on an item.
/// </summary>
public class Enchantment
{
    [Key]
    public int Id { get; set; }

    public EnchantmentType Type { get; set; }

    public byte Level { get; set; }

    // Foreign key to Auction
    public int AuctionId { get; set; }

    [ForeignKey("AuctionId")]
    public Auction? Auction { get; set; }

    public Enchantment() { }

    public Enchantment(EnchantmentType type, byte level)
    {
        Type = type;
        Level = level;
    }
}

/// <summary>
/// Enum of known enchantment types.
/// </summary>
public enum EnchantmentType
{
    unknown,
    // Sword
    sharpness,
    smite,
    bane_of_arthropods,
    first_strike,
    giant_killer,
    ender_slayer,
    cubism,
    execute,
    impaling,
    vampirism,
    life_steal,
    looting,
    luck,
    scavenger,
    experience,
    fire_aspect,
    knockback,
    venomous,
    cleave,
    thunderlord,
    telekinesis,
    triple_strike,
    lethality,
    syphon,
    mana_steal,
    prosecute,
    titan_killer,
    thunderbolt,
    swarm,
    dragon_hunter,
    vicious,
    champion,
    divine_gift,
    
    // Bow
    power,
    aiming,
    infinite_quiver,
    flame,
    piercing,
    punch,
    snipe,
    overload,
    soul_eater,
    chance,
    dragon_tracer,
    
    // Armor
    protection,
    growth,
    thorns,
    respiration,
    depth_strider,
    aqua_affinity,
    feather_falling,
    frost_walker,
    rejuvenate,
    sugar_rush,
    true_protection,
    reflection,
    respite,
    smarty_pants,
    counter_strike,
    transylvanian,
    blazing_resistance,
    big_brain,
    ender,
    
    // Tools
    efficiency,
    fortune,
    silk_touch,
    smelting_touch,
    harvesting,
    compact,
    expertise,
    cultivating,
    turbo_cactus,
    turbo_cane,
    turbo_carrot,
    turbo_cocoa,
    turbo_melon,
    turbo_mushrooms,
    turbo_potato,
    turbo_pumpkin,
    turbo_warts,
    turbo_wheat,
    dedication,
    pristine,
    
    // Fishing
    angler,
    blessing,
    caster,
    frail,
    luck_of_the_sea,
    lure,
    magnet,
    spiked_hook,
    
    // Ultimate enchants
    ultimate_bank,
    ultimate_chimera,
    ultimate_combo,
    ultimate_duplex,
    ultimate_fatal_tempo,
    ultimate_flash,
    ultimate_habanero_tactics,
    ultimate_inferno,
    ultimate_jerry,
    ultimate_last_stand,
    ultimate_legion,
    ultimate_no_pain_no_gain,
    ultimate_one_for_all,
    ultimate_reiterate,
    ultimate_rend,
    ultimate_soul_eater,
    ultimate_swarm,
    ultimate_the_one,
    ultimate_wisdom
}

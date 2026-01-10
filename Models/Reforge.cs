namespace SkyFlipperSolo.Models;

/// <summary>
/// Represents a reforge applied to an item.
/// Complete list of all Hypixel Skyblock reforges organized by category.
/// </summary>
public enum Reforge
{
    None,
    
    // ===== SWORD REFORGES (Melee) =====
    // Basic
    Epic,
    Fair,
    Fast,
    Gentle,
    Heroic,
    Legendary,
    Odd,
    Rich,
    Sharp,
    Spicy,
    // Advanced (Reforge Stones)
    Bulky,          // Bulky Stone
    Dirty,          // Dirt Bottle
    Fabled,         // Dragon Claw
    Gilded,         // Midas Jewel (Midas Sword only)
    Suspicious,     // Suspicious Vial
    Warped,         // Warped Stone (AOTE/AOTV only)
    Withered,       // Wither Blood
    
    // ===== BOW REFORGES (Ranged) =====
    // Basic
    Awkward,
    Deadly,
    Fine,
    Grand,
    Hasty,
    Neat,
    Rapid,
    Unreal,
    // Advanced (Reforge Stones)
    Precise,        // Optical Lens
    Spiritual,      // Spirit Stone
    Headstrong,     // Dragon Scale
    
    // ===== ARMOR REFORGES =====
    // Basic
    Clean,
    Fierce,
    Heavy,
    Light,
    Mythic,
    Pure,
    Smart,
    Titanic,
    Wise,
    // Advanced (Reforge Stones)
    Ancient,        // Precursor Gear
    Bustling,       // Skymart Brochure
    Candied,        // Candy Corn
    Festive,        // Frozen Bobbin
    Giant,          // Giant Tooth
    Jaded,          // Jaderald (Mining Armor)
    Loving,         // Red Scarf (Chestplate only)
    Mossy,          // Overgrown Grass
    Necrotic,       // Necromancer's Brooch
    Perfect,        // Diamond Atom
    Renowned,       // Dragon Horn
    Spiked,         // Dragon Scale
    Submerged,      // Deep Sea Orb
    
    // ===== EQUIPMENT REFORGES (Necklace/Cloak/Belt/Gloves) =====
    // Basic
    Astute,
    Blended,
    Brilliant,
    Colossal,
    Hefty,
    Honored,
    Menacing,
    Soft,
    Stained,
    // Advanced (Reforge Stones)
    Blooming,       // Flowering Bouquet
    Erratic,        // Gallon of Red Paint (Great Spook)
    Fortified,      // Meteor Shard
    Glistening,     // Shiny Prism
    Pitched,        // Pitchin' Koi
    Rooted,         // Burrowing Spores
    Snowy,          // Terry's Snowglobe
    Strengthened,   // Searing Stone
    Waxed,          // Blaze Wax
    
    // ===== MINING TOOL REFORGES (Pickaxe/Drill/Gauntlet) =====
    // Basic
    Great,
    Magnetic,
    Refined,
    Silky,
    // Advanced (Reforge Stones)
    Auspicious,     // Rock Gemstone
    Fleet,          // Diamonite
    Glacial,        // Frigid Husk
    Heated,         // Scorched Topaz
    Lustrous,       // Onyx
    Stellar,        // Petrified Starfall
    Sunny,          // Sunny Side Omelette
    
    // ===== FARMING TOOL REFORGES (Hoe/Axe) =====
    // Basic
    Robust,
    Strong,
    Zooming,
    // Advanced (Reforge Stones)
    Blessed,        // Blessed Fruit
    Bountiful,      // Golden Ball
    Earthly,        // Large Walnut
    Moil,           // Moil Log (Axes only)
    Toil,           // Toil Log (Axes only)
    
    // ===== FISHING ROD REFORGES =====
    // Advanced (Reforge Stones)
    Pitchin,        // Pitchin' Koi (Note: without apostrophe)
    Salty,          // Salt Cube
    Treacherous,    // Rusty Anchor
    
    // ===== VACUUM REFORGES (Rift/Garden) =====
    // Advanced (Reforge Stones)
    Beady,          // Beady Eyes
    Buzzing,        // Clipped Wings
    
    // ===== LEGACY/MISC =====
    Bizarre,
    Itchy,
    Ominous,
    Pleasant,
    Pretty,
    Simple,
    Strange,
    Vivid,
    Unpleasant,
    Hurtful,
    Demonic,
    Forceful,
    Godly,
    Zealous,
    Bloody,
    Sweet,
    Stiff,
    Lucky,
    Empowered,
    Fruitful,
    Ridiculous,
    Hyper,
    
    // Special handling for possessive forms
    Jerry,          // For "Jerry's" pattern
    Jerries         // Alternative
}

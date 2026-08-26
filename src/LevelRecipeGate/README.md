
# LevelRecipeGate

V Rising server mod originally based on RecipeTweaker, simplified to progression checks for configured items:

**Block equipping configured equippables/jewels and using configured consumables until players reach the required gear level.**

## Credits

* Original progression-gating concept by **@Chimll**
* Source code originally based on **RecipeTweaker**
* Reworked and simplified into a progression-focused recipe gating system

---

## Features

### Item Level Gating

Configure recipe or item prefabs behind a required gear level. Recipe prefabs are used to find their real output items; crafting itself is not blocked.

Examples:

* Merciless Iron -> Level 33
* Reinforced Iron -> Level 50
* Dark Silver -> Level 65
* Sanguine -> Level 74

Players can still craft configured recipes, but they cannot equip gated equippables or use gated consumables until they meet the requirement.

Equippables are checked against the player's current gear level. Consumables are checked against the player's highest recorded gear level.

### Jewel Tier Gating

Configure jewel tiers behind a required current gear level. Tiers are player-facing tiers 1-4.

Example:

* Tier 3 jewels require current gear level 65
* Tier 4 jewels require current gear level 74

When a player tries to equip a gated jewel tier before meeting the requirement, the equip request is blocked before the jewel is equipped.

### Mixed Blood Potion Blocking

Configure blood potions that contain a secondary blood type behind a required max recorded gear level. This checks the potion's runtime `StoredBlood.SecondaryBlood` data, so it catches mixed blood potions even when the item prefab itself is a normal prison potion.

Set `secondary_blood_potion_min_level` to `0` to disable this gate.

---

### Persistent Progression Tracking

The mod tracks the **highest gear level a player has ever reached**.

This means:

* Reach Gear Level 91 once -> permanently recorded.
* Downgrade gear later -> still considered Gear Level 91 for consumable gates.
* Equippable and jewel gates still use the player's current gear level.

Highest player levels are stored in:

```txt
BepInEx/config/BlackVeinMods/player_highest_levels.json
```

Example:

```json
{
  "76561199042557118": {
    "name": "Neen",
    "steam_id": 1234567890123446,
    "highest_gear_level": 91
  }
}
```

---

### Blocking Feedback

When a player attempts to equip or use a locked item or jewel:

* The equip/use action is blocked
* Player receives a chat/system message

Example message:

```txt
You cannot equip this yet. Required gear level: 74.
You cannot equip tier 4 jewels yet. Required gear level: 74.
You cannot drink mixed blood potions yet. Required gear level: 80.
```

---

## Removed From RecipeTweaker

Removed from this clean version:

* Drop blocking
* Servant loot blocking
* Scheduled unlocks
* Global `blocked_recipes.txt`
* Recipe unlock schedules
* Drop restriction systems

---

## Configuration

Generated at:

```txt
BepInEx/config/BlackVeinMods/level_recipe_blocks.json
```

Example:

```json
{
  "enabled": true,
  "message": "Unused for item equip/use blocking; kept for config compatibility.",
  "secondary_blood_potion_min_level": 80,
  "recipe_level_blocks": [
    {
      "min_level": 33,
      "recipes": [
        "Recipe_Weapon_Axe_T05_Iron",
        "Recipe_Weapon_Claws_T05_Iron"
      ]
    },
    {
      "min_level": 50,
      "recipes": [
        "Recipe_Weapon_Axe_T06_Iron_Reinforced"
      ]
    }
  ],
  "jewel_tier_level_blocks": [
    {
      "tier": 3,
      "min_level": 65
    },
    {
      "tier": 4,
      "min_level": 74
    }
  ]
}
```

---

## Commands

```txt
.levelrecipegate reload
.levelrecipegate list
.levelrecipegate add <level> <recipeOrItemPrefab>
.levelrecipegate remove <recipeOrItemPrefab>
.levelrecipegate addjeweltier <tier> <level>
.levelrecipegate removejeweltier <tier>
.levelrecipegate secondarybloodlevel <level>
```

Examples:

```txt
.levelrecipegate add 74 Recipe_Weapon_Sword_T08_Sanguine
.levelrecipegate remove Recipe_Weapon_Sword_T08_Sanguine
.levelrecipegate addjeweltier 4 74
.levelrecipegate removejeweltier 4
.levelrecipegate secondarybloodlevel 80
.levelrecipegate list
.levelrecipegate reload
```

---

### Live Reload

Changes to `level_recipe_blocks.json` are watched and reloaded automatically.
The `.levelrecipegate reload` command is still available for manual reloads.

---

## How Gear Level Is Calculated

Uses the same calculation used by V Rising:

```txt
ArmorLevel + SpellLevel + WeaponLevel
```

The highest value ever reached is saved in `BepInEx\config\BlackVeinMods\player_highest_levels.json` and used for consumable checks. Equippable and jewel checks use the player's current gear level.

---

## Build

Copy to:

```txt
VRising_Server\BepInEx\plugins\
```

---

## Troubleshooting

Check:

```txt
BepInEx/LogOutput.log|

```

---

Dependencies

    BepInEx
    VampireCommandFramework
    ```

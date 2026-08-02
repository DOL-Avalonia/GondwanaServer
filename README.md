# 🏰 GONDWANA SERVER

**DAOC RP/PvP/GvG server, following Amtenael and Avalonia**

[![Build Status](https://github.com/Dawn-of-Light/DOLSharp/actions/workflows/create_release.yml/badge.svg?event=push)](https://github.com/Dawn-of-Light/DOLSharp/actions/workflows/create_release.yml)

---

## 📜 About Dawn of Light (DOL)

**Dawn of Light** is an open-source server emulator for the game *Dark Age of Camelot* (DAOC), built by a passionate community. The DOL Server architecture provides:

* **Network Communication:** Allows DAOC game clients to connect directly to the server.
* **Database Layer:** Bridges the server and MySQL/SQLite to securely store characters, NPCs, items, and world data.
* **Persistent World Framework:** Enables extensive customization of game rulesets, mechanics, and behaviors.

---

## 🛠️ How To Build

1. **Clone the Repository:** Clone the Git repository to your local working directory.
2. **Restore NuGet Packages:** This downloads dependencies from the NuGet repository instead of using embedded binaries. *(Need help? Check the [NuGet FAQ](https://docs.nuget.org/consume/nuget-faq))*
3. **Build the Project:** Compile the solution. 
   > **Note:** Use the `Debug` target if you intend to contribute or write your own scripts.

### 💡 Developer Workflow Tips
* The `debug` folder should be your working directory from now on.
* Focus on populating a database to build your server, and rely on source files to find constant values used in database records.
* **Use an IDE:** Navigating the code to track default behaviors and locating constants is highly recommended. Using an IDE to handle breakpoints with a debugger can be a lifesaver when trying to understand how specific legacy game rules are enforced!

**Latest Release:** [Download here](https://github.com/Dawn-of-Light/DOLSharp/releases/latest)

---

## 📚 Documentation & Community

* 🏠 **Homepage:** [dolserver.net](http://www.dolserver.net)
* 💬 **Getting Started:** [Official Forum](http://www.dolserver.net/index.php)
* 📖 **Coding & Architecture:** [Wiki Home](https://github.com/Dawn-of-Light/DOLSharp/wiki)
* 🎧 **Discord:** [Official Help](https://discord.gg/CXk6zpgwqp)

---

## ⌨️ Custom Server Commands

### General Commands
| Command | Arguments | Description |
|---|---|---|
| `/cmdhelp` | - | Displays the list of all commands available in the game. |
| `/language set` | `<EN \| FR \| DE \| ES \| IT \| NL \| CS \| RO \| VI>` | Sets your account language to translate texts, descriptions, and info. |
| `/autotranslate` *(or `/at`)* | `<on\|off>` | Translates other players' dialogs, quests, and books into your language automatically. |
| `/market` | `<open\|close\|name>` | Deploys, renames, or closes your personal market. |
| `/craftmacro` | `<set\|clear\|show\|buy\|buyto>` | Automates your crafting queue and buys missing recipe ingredients from merchants. |
| `/combine list` | - | Displays successfully discovered alternative craft item combinations. |
| `/vol` *(or `/steal`)* | `<player>` | Steal money or Territory Relics from rival players, or pilfer opposing clan PvP chests. |
| `/facemob` | `<mobname>` | Turns your character toward the specified mob in your region (Useful for quests!). |
| `/askname` | - | Requests your target's name to display it. Required before adding them as a friend. |
| `/genistar` | `<care\|rename\|emote\|visibleweapon\|addweapon\|removeweapon>` | Interact with your Genistar pet. Care for the egg, rename, emote, or manage visible combat gear. |
| `/pvp info` / `/pvp scores` | - | Displays information and current scores for the active PvP session. |
| `/rvr info` | - | Displays information and current scores for the active RvR campaign. |
| `/bet` | `<Team1 \| Team2> <amount>` | Place bets on arena fighters up to {0} Gold coins. Use `/bet list` to check active pools. |

### Guild Commands
| Command | Arguments | Description |
|---|---|---|
| `/gc territories` / `/gc subterritories` | - | View GvG maps and territory statuses. |
| `/gc territoryportal` | - | Opens a portal directly to your claimed lands. |
| `/gc combatzone` | - | Creates a temporary PvP combat zone. |
| `/gc jailrelease` | `<name>` | Pay bail to release a guildmate from prison. |
| `/gc houserent` | `<on\|off>` | Toggles automatic guild house rent payments. |
| `/gc territorybanner` / `/gc buybanner` / `/gc summon`| - | Buy and deploy tactical banners to buff members or territory guards. |

---

## ⚙️ Technical Reference

### Item Utility & Imbue Calculations
*(ID numbers are based off of `eProperty`)*

| ID / Range | Property | Multiplier |
|:---|:---|:---|
| **1-8** | Stats | `* 0.6667` |
| **9** | Power Cap | `* 2` |
| **10** | Max HP | `* 0.25` |
| **11-19** | Resists | `* 2` |
| **20-115** | Skill | `* 5` |
| **59** | Crafting Skill Gain | `* 0.25` |
| **71** | Robbery Resist Chance | `* 2` |
| **116** | Crafting Speed | `* 2` |
| **117** | Secondary Style Spell Chance | `* 2` |
| **118** | Mythical Regeneration | `* 5` |
| **119** | Tension Gain | `* 2` |
| **145** | Max Speed | `* 1` |
| **146** | Spell Reflection Chance | `* 2` |
| **147** | Max Concentration | `* 2` |
| **148** | Armor Factor | `* 1` |
| **149** | Armor Absorption | `* 2` |
| **150-155** | Regeneration / Range | `* 5` |
| **156** | Acuity | `* 0.6667` |
| **163** | All Magic | `* 5` |
| **164** | All Melee | `* 5` |
| **167** | All Dual Wield | `* 5` |
| **168** | All Archery | `* 5` |
| **169-172** | Evade / Parry Chance / Fatigue Consumption | `* 2` |
| **173** | TOA Melee Damage | `* 5` |
| **174** | TOA Ranged Damage | `* 5` |
| **175-186** | TOA Spell Duration Reduction | `* 2` |
| **187** | TOA Hit Point Bonus | `* 0.25` |
| **188** | TOA Archery Speed | `* 5` |
| **189-190** | TOA Arrow Recover / Debuff | `* 2` |
| **191** | TOA Casting Speed | `* 5` |
| **192-195** | TOA Debuff / Fatigue / Healing | `* 2` |
| **196** | TOA Power Pool | `* 2` |
| **197** | TOA Resist Pierce | `* 5` |
| **198** | TOA Spell Damage | `* 5` |
| **199** | TOA Spell Duration | `* 2` |
| **200** | TOA Style Damage | `* 5` |
| **201-209** | TOA Skill Cap | `* 2` |
| **210** | TOA Hit Point Cap | `* 0.25` |
| **211** | TOA Power Pool Cap | `* 2` |
| **212** | Weapon Skills | `* 5` |
| **213** | All Skills | `* 5` |
| **214-217** | Critical Hit / Water Speed | `* 5` |
| **217-220** | TOA Spell Level / Miss Hit / Keep | `* 2` |
| **221-229** | Mythical Resist and Cap | `* 4` |
| **230-231** | TOA DPS / Magic Absorption | `* 2` |
| **232-235** | Critical Heal / Mythical Fall / Coin / Discumbering | `* 5` |
| **236-245** | Mythical Stat and Cap Increase | `* 4` |
| **247-250** | BP / XP / Natural / Extra HP | `* 5` |
| **251-252** | Conversion / Style Absorb | `* 2` |
| **253-255** | RP / Arcane | `* 5` |
| **256-269** | New Special Bonuses | `* 2` |
| **270-309** | New Special Bonuses | `* 5` |

---

### Genistar Pets - Usable Equipment Item Bonuses
*(ID numbers are based off of `eProperty`)*

| ID / eProperty | Trigger Effect / Bonus |
|:---|:---|
| **1-8** | All Stats |
| **11-19** | All Resists |
| **LootChance** | Proc Trigger Chance |
| **SpellRange** | Spell and Archery Range |
| **MythicalDebuffResistChance** | Debuff Resist Chance |
| **OffhandChanceBonus** | Left Hand Swing Chance |
| **StyleAbsorb** | Melee Damage Absorption |
| **CriticalDotHitChance** | DoT Spell Damage Absorption |
| **MythicalCrowdDuration** | Crowd Control Spell Resist Chance |
| **MagicAbsorption** | Spell Damage Absorption |
| **MeleeSpeed** | Weapon Speed |
| **PieceAblative** | Ablative Shield Modifier |

---

### Item Flags & Special Properties

#### General & Base Mechanics
* **Flag 1 (Unsellable):** Marks an item so it cannot be sold to merchants (delves as "Cannot be sold" if the item is otherwise droppable).
* **Flag 2 (Sitting Effect):** Item triggers a special effect or bonus only when the player is sitting.
* **Flag 3 (Stackable Potion):** Characterizes the item as a potion that can be combined/stacked with identical potions to pool their charges.
* **Flag 4 (Spell Parchment/Scroll):** Item acts as a scroll that can be used to cast special magical spells.
* **Flag 5 (Patterned Item):** A status flag indicating the target item currently has a custom pattern applied to its appearance.

#### Dyes
* **Flag 11:** Cloth/Cloak/Barding Dye.
* **Flag 12:** Leather Dye.
* **Flag 13:** Metal/Shield/Saddle Dye (Studded, chain, plate, reinforced, scale armor, shields, instruments, saddles).
* **Flag 14:** Weapon/Basic Item Dye.
* **Flag 15 (Omnidye):** Universal dye that bypasses material restrictions.

#### Patterns (Reskinning Items)
* **Flag 16:** Weapon Pattern (Checks for 2H vs 1H and damage-type compatibility).
* **Flag 17:** Shield Pattern (Ensures shield sizes match).
* **Flag 18:** Cloth/Cloak Pattern (Prevents mixing cloth patterns with cloak items).
* **Flag 19:** Armor Pattern (Leather, studded, chain, plate, etc.).
* **Flag 20:** Mask Pattern (Explicitly for head slot masks).
* **Flag 21 (Pattern Removal Tool):** Scrubs a pattern off an item reverting it to original database stats, or empties a "Filled" pattern back into a "Blank" pattern.
* **Flag 22 (Smart Pattern):** Specialized pattern bypassing standard copy/paste logic; assigns predefined models determined by the `ArmorPatternMgr`.

#### Genistar & Pet Equipment
* **Flags 25 - 28:** Genistar House/Garden Item (Explicitly blocks manual right-clicking/usage from inventory).
* **Flags 30, 31, 33 - 36:** Genistar/Pet Weapons.
* **Flag 32:** Genistar/Pet Shield.
* **Flags 38 - 40:** Genistar/Pet Armor.

#### Cursed, Unequippable & Consuming Items
*These flags read a special PackageID string format (e.g., `MANA|10;COND|5;DEATHCOND|10`) to penalize the equipping player.*
* **Flag 43 (Consuming Item):** When equipped, consumes a percentage of mana, endurance, or health, or drains its own condition over time.
* **Flag 44 (Death-Penalty Unequippable):** Cannot be unequipped. The item strictly loses condition (durability) upon player death. Destroyed at 0 condition if `DESTROY` is flagged in its package.
* **Flag 45 (Cooldown Unequippable):** A hybrid curse. Cannot be unequipped manually, applies an equip-cooldown timer if forcefully removed, and actively consumes mana/endurance/condition while worn.

#### Loan Coupons
* **Flag 46:** Personal Loan coupons (obtained via Banker, 400 to 800 Gold).
* **Flag 47:** House Loan coupons (obtained via Banker, 1500 to 25000 Gold). Can only be used in Housing regions.
GONDWANA SERVER
DAOC RP/PvP/GvG server, following Amtenael and Avalonia
========

Dawn of Light - Dark Age of Camelot Server Emulator (DOL)
----

DOL Server is a server emulator for the game Dark Age of Camelot written by the Dawn of Light community

It does the following:

    Provides the network communication needed to allow a DAOC game client to connect to the server
    Provides a database layer between the server and MySQL~SQLite to allow storage of characters, npcs, items and so on
    Provides a persistent world framework for customisation of game rulesets and behaviours

Auto Builds
----
Build Status for NetFramework and NetCore builds: [![Build Status](https://github.com/Dawn-of-Light/DOLSharp/actions/workflows/create_release.yml/badge.svg?event=push)](https://github.com/Dawn-of-Light/DOLSharp/actions/workflows/create_release.yml)

Latest Release : https://github.com/Dawn-of-Light/DOLSharp/releases/latest

How To Build
----

Clone Git Repository to a working Directory.

Restore Nuget Package : https://docs.nuget.org/consume/nuget-faq

This will download dependencies from nuget repository instead of using embedded binaries.

Then Build the project. (use Debug Target if you intend to contribute or write your own scripts...)

The debug folder should be your working directory from now on, you should focus on populating a database to build your server and rely on source files to find constants values used in database records...

You should use an IDE to track default behaviors and reach code parts where constants are used, handling breakpoint with a debugger can be a life savior in understanding how some game rules are enforced with legacy code.

Documentation
----

 - Homepage: http://www.dolserver.net
 - Getting Started: [Official Forum](http://www.dolserver.net/index.php)
 - Coding: [Wiki Home](https://github.com/Dawn-of-Light/DOLSharp/wiki)
 - Discord: [Official Help](https://discord.gg/CXk6zpgwqp)



NEW and useful SERVER COMMANDS
----

/cmdhelp: Display the list of all commands available in the game.

/language set <EN | FR | DE | ES | IT | NL | CS | RO | VI>: Set your account language so all texts, descriptions, and information will be translated.

/autotranslate <on|off> (or /at): Turn on the autotranslator to automatically translate other players' dialogs, quests, and books into your language.

/market <open|close|name>: Deploy, rename, or close your personal market.

/craftmacro <set|clear|show|buy|buyto>: Automate your crafting queue and easily buy missing recipe ingredients directly from merchants.

/combine list: Display your successfully discovered alternative craft item combinations.

/vol or /steal <player>: Steal money or precious Territory Relics from rival players! It also allows you to pilfer treasures looted from an opposing clan's chests in PvP.

/facemob <mobname>: Turn towards the direction where the specified mob is located in your region. Very useful for quests!

/askname: Ask for your target's name in order to display it. You must do this before you can add them as a friend.

/genistar <care|rename|emote|visibleweapon|addweapon|removeweapon>: Interact with and customize your Genistar pet. Target your egg to care for it, grant your grown companion a custom name, direct its emotes, and arm it for battle by managing its visible combat gear.

/pvp info & /pvp scores: Display information and current scores for the active PvP session.
/rvr info: Display information and current scores for the active RvR campaign.



NEW GUILD COMMANDS
----
/gc territories & /gc subterritories : View GvG maps and statuses.
/gc territoryportal : Open a portal directly to your claimed lands.
/gc combatzone : Create a temporary PvP combat zone.
/gc jailrelease <name> : Pay bail to release a guildmate from prison.
/gc houserent <on|off> : Toggle automatic guild house rent payments.
/gc territorybanner & /gc buybanner & /gc summon : Buy and deploy tactical banners to buff your members or territory guards.



ITEM UTILITY AND IMBUE INFORMATIONS & CALCULATIONS
----

ID numbers are based off of eProperty
1-8 == stats = *.6667
9 == power cap = *2
10 == maxHP =  *.25
11-19 == resists = *2
59 == Crafting skill gain = *.25
71 == Robbery resist chance = *2
20-115 == skill = *5
116 == Crafting speed = *2
117 == Secondary style spell chance = *2
118 == Mythical regeneration = *5
119 == Tension gain = *2
145 == MaxSpeed = *1
146 == SpellReflectionChance = *2
147 == MaxConcentration = *2
148 == ArmorFactor = *1
149 == ArmorAbsorption = *2
150-155 Regeneration/Range = *5
156 == acuity = *.6667
163 == all magic = *5
164 == all melee = *5
167 == all dual weild = *5
168 == all archery = *5
169-172	evade/parry chance/fatigue consumption	*2
173	TOA Melee damage	*5
174	TOA Ranged damage	*5
175-186	TOA spell duration reduction *2
187	TOA Hit point Bonus	*0.25
188	TOA Archery speed	*5
189-190	TOA Arrow recover/Debuff	*2
191	TOA Casting speed	*5
192-195	TOA Debuff/Fatigue/healing	*2
196	TOA Power pool	*2
197	TOA Resist Pierce	*5
198	TOA Spell Damage	*5
199	TOA Spell Duration	*2
200	TOA Style Damage	*5
201-209	TOA Skill Cap	*2
210	TOA Hit point Cap	*0.25
211	TOA Power pool cap	*2
212 == weapon skills = *5
213 == all skills = *5
214-217 Critical Hit/waterspeed = *5
217-220	TOA Spell Level/Miss hit/Keep	*2
221-229	Mythical Resist and Cap	*4
230-231	TOA DPS/Magic Absorption	*2
232-235 Critical Heal/Mythical fall/Coin/Discumbering = *5
236-245	Mythical Stat and Cap Increase	*4
247-250 BP/XP/Natural/Extra HP = *5
251-252 Conversion/Style Absorb = *2
253-255 RP/Arcane = *5
256-269 New Special Bonuses = *2
270-309 New Special Bonuses = *5



NEW GENISTAR PETS USABLE EQUIPMENT ITEM BONUSES
----

ID numbers are based off of eProperty
1-8 == All stats
11-19 == All resists
eProperty.LootChance == Proc Trigger Chance
eProperty.SpellRange == Spell and Archery Range
eProperty.MythicalDebuffResistChance == Debuff Resist Chance
eProperty.OffhandChanceBonus == Left Hand Swing Chance
eProperty.StyleAbsorb == Melee Damage Absorption
eProperty.CriticalDotHitChance == DoT Spell Damage Absorption
eProperty.MythicalCrowdDuration == Crowd Control Spell Resist Chance
eProperty.MagicAbsorption == Spell Damage Absorption
eProperty.MeleeSpeed == Weapon Speed
eProperty.PieceAblative == Ablative Shield Modifier
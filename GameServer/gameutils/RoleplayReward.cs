using DOL.AI.Brain;
using DOL.Events;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using DOL.GS.ServerProperties;
using DOL.Language;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DOL.GS
{
    public static class RoleplayReward
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        private static readonly int MIN_WORDS_REQUIRED = 13;
        private static readonly int MIN_TIME_BETWEEN_TALKS_MS = 8000;
        private static readonly int MAX_CHAIN_TIMEOUT_MS = 3 * 60 * 1000;
        private static readonly int PENALTY_DURATION_MS = 3 * 60 * 1000;
        private static readonly int BADWORD_WARNING_DECAY_MS = 10 * 60 * 1000;

        private static readonly int MAX_WORD_BONUS = 10;
        private static readonly int MAX_CHAIN_BONUS = 10;
        private static readonly ushort REQUIRED_PLAYER_RADIUS = 1800;

        public static readonly HashSet<string> RP_WORDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // ================= FRENCH =================
            // 1. Univers DAoC & Classes
            "paladin", "maître d'armes", "théurge", "cabaliste", "sorcier", "clerc", "moine", "mercenaire", "infiltrateur", "fléau d'arawn", "hérétique",
            "guerrier", "berserker", "chasseur", "skald", "thane", "runiste", "spiritiste", "chaman", "guérisseur", "ombre", "bogdar", "sauvage", "valkyrie",
            "héros", "finelame", "veilleurs", "barde", "eldritch", "enchanteur", "mentaliste", "druide", "protecteur", "empathe", "nightshade", "faucheur",
            "animiste", "valewalker", "vampiir", "sentinelle", "champion", "faucheuse", "relique", "fort", "avant-poste",
            "breton", "avalonien", "sarrazin", "highlander", "inconnu", "demi-ogre", "nordique", "nain", "troll", "kobold", "frostalf", "valkyn",
            "celte", "elfe", "firbolg", "lurikeen", "sylvestre", "shar", "korazh", "graoch", "luridien",
            "camelot", "jordheim", "tir na nog", "salisbury", "snowdonia", "dartmoor", "myrkwood", "raumarike", "malmohus", "lough derg",
            "lyonesse", "glass", "svealand", "mularn", "moher", "sheeroe", "connacht", "shannon", "pennine", "anderson",
            "emain macha", "breifine", "odin", "thor", "freya", "hibernia", "albion", "midgard", "frontier", "passage",
            "golestandt", "cuuldurach", "legion", "siabra", "bwca", "puca", "kelpie", "morrigane", "glimmer", "granit", "sylvain", "draco", "wyrm",

            // 2. Vie médiévale, Rôles & Artisanat
            "messire", "damoiseau", "sire", "dame", "seigneur", "roi", "reine", "prince", "princesse", "monarque", "duc", "baron", "comte",
            "châtelain", "noble", "chevalier", "preux", "écuyer", "sénéchal", "connétable", "intendant", "héraut", "page",
            "bailli", "prévôt", "chancelier", "grand-maître", "templier", "exorciste", "druidesse", "palfrenier", "éclaireur", "vigie", "messager",
            "confrère", "soeur", "frère", "père", "mère", "suzerain", "vassal", "chambellan", "maréchal", "vidame", "trouvère", "troubadour",
            "saltimbanque", "apothicaire", "tanneur", "tisserand", "forgeron", "herboriste", "alchimiste", "archimage", "prêtre",
            "brigand", "voleur", "assassin", "garde", "capitaine", "nécromancien", "archer", "gueux", "maraud", "manant", "félon", "misérable", "vil",
            "taverne", "auberge", "festin", "hydromel", "cervoise", "hypocras", "venaison", "brouet", "gruau", "miche", "saugrenée", "potée",
            "teinturier", "cordonnier", "charpentier", "maçon", "tailleur", "meunier", "boulanger", "boucher", "tavernier", "aubergiste",
            "maréchal-ferrant", "armurier", "heaumier", "arbalétrier", "orfèvre", "changeur", "mercier", "drapier", "pelletier", "parcheminier",
            "enlumineur", "copiste", "relieur", "barbier", "chirurgien", "mire", "astrologue", "ménestrel", "jongleur", "acrobate", "bouffon",
            "crieur", "sergent", "guetteur", "veilleur", "échanson", "panetier", "sommelier", "cuisinier", "marmiton", "lavandière", "lingère",
            "fileuse", "tisserande", "brodeuse", "dentellière", "nourrice", "gouvernante", "sage-femme", "nonne", "abbé", "abbesse", "évêque",
            "cardinal", "pape", "ermite", "pèlerin", "croisé", "serf", "vilain", "paysan", "laboureur", "vigneron", "berger", "porcher", 
            "bûcheron", "charbonnier", "braconnier", "pêcheur", "batelier", "marin", "corsaire", "pirate", "mendiant", "vagabond", "lépreux", "bourreau", "geôlier",

            // 3. Architecture & Fortifications
            "royaume", "empire", "fief", "bourg", "hameau", "village", "cité", "forteresse", "château", "tour", "donjon", "rempart",
            "herse", "douve", "meurtrière", "lice", "poterne", "chemin de ronde", "castrum", "abbaye", "ermitage", "cairn", "dolmen",
            "menhir", "bosquet", "marais", "estuaire", "archipel", "vallon", "causse", "plateau", "ravin", "gouffre", "crypte", "abysse",
            "sanctuaire", "autel", "frontière", "contrée", "territoire", "montagne", "val", "vallée", "rivière", "fleuve", "océan", "mer",
            "caverne", "tombeau", "assommoir", "barbacane", "bastille", "bretèche", "caponnière", "châtelet", "courtine", "cunette", "créneau",
            "échauguette", "embrasure", "mâchicoulis", "merlon", "motte castrale", "pont-dormant", "oubliette", "hourd", "bassefosse", "ferté",

            // 4. Équipement, Armes, Vêtements & Parures
            "épée", "bouclier", "armure", "heaume", "hache", "lance", "arc", "grimoire", "rune", "talisman", "relique", "parchemin", "potion",
            "cotte", "mailles", "gantelet", "dague", "poignard", "masse", "fléau", "hallebarde", "javelot", "flèche", "carquois", "baguette",
            "sceptre", "couronne", "cape", "besace", "bourse", "amulette", "anneau", "joyau", "gemme", "cristal", "pierre", "acier", "fer",
            "mithril", "or", "argent", "gladius", "spatha", "claymore", "francisque", "framée", "scramasaxe", "broigne", "gambison", "haubert",
            "bassinet", "salade", "gorgerin", "éperon", "destrier", "palefroi", "roncin", "cuirasse", "boucle", "fourreau", "miséricorde",
            "espadon", "vouge", "pertuisane", "fauchart", "arbalète", "brigandine", "camail", "plastron", "brassard", "grève", "soleret",
            "cuissard", "spallière", "armet", "pavois", "targe", "dondaine", "espringale", "guisarme", "haubergeon", "heaumier", "mézail", "bouterolle",
            "tunique", "chemise", "braies", "chausses", "pourpoint", "surcot", "manteau", "pèlerine", "chaperon", "bonnet", "calotte", "toque",
            "chapeau", "feutre", "voile", "guimpe", "coiffe", "diadème", "tiare", "mitre", "chasuble", "aube", "étole", "manipule", "dalmatique",
            "surplis", "soutane", "froc", "coule", "bure", "habit", "livrée", "uniforme", "tabard", "jaque", "hoqueton", "dossière", "épaulière",
            "botte", "bottine", "soulier", "sandale", "socque", "patin", "ceinture", "ceinturon", "baudrier", "agrafe", "bouton", "lacet",
            "ruban", "galon", "broderie", "dentelle", "fourrure", "hermine", "vair", "zibeline", "martre", "renard", "mouton", "agneau", "laine",
            "soie", "velours", "satin", "damas", "brocart", "taffetas", "lin", "chanvre", "coton", "peau", "velin", "bague", "alliance", "chevalière",
            "bracelet", "collier", "torque", "pendentif", "médaille", "broche", "fibule", "boucle d'oreille", "globe",

            // 5. Magie, Religion & Alchimie
            "magie", "sortilège", "malédiction", "rituel", "incantation", "prophétie", "oracle", "ombre", "flamme", "mana", "bénédiction",
            "pacte", "démoniaque", "divin", "sacré", "profane", "esprit", "âme", "dieu", "dieux", "déesse", "foi", "prière", "miracle",
            "ténèbres", "lumière", "enchantement", "illusion", "invocation", "dogme", "hérésie", "pénitence", "absolution", "ferveur",
            "dévotion", "ascèse", "mystique", "augure", "athanor", "cornue", "aludel", "creuset", "alambic", "transmutation", "hermétique",
            "ésotérisme", "occultisme", "cosmogonie", "cosmologie", "eschatologie", "sotériologie", "hiérogamie", "analogie", "décoction",
            "excommunication", "phylactère", "abjurateur", "ectoplasme", "khumeia", "alchimia",

            // 6. Thèmes, Actions & Émotions
            "honneur", "gloire", "sang", "bataille", "guerre", "quête", "destin", "serment", "alliance", "vengeance", "courage", "héroïsme",
            "siège", "victoire", "défaite", "trahison", "bravoure", "lâcheté", "mort", "vie", "croisade", "périple", "voyage", "combat",
            "lutte", "triomphe", "sacrifice", "péril", "danger", "embuscade", "escarmouche", "trêve", "paix", "justice", "châtiment",
            "pardon", "renommée", "mandat", "quérir", "oyez", "festoyer", "guerroyer", "jouter", "pourfendre", "occire", "truander",
            "haranguer", "vaillant", "belliqueux", "infâme", "loyal", "fourbe", "austère", "magnanime", "intrépide", "outrecuidant",
            "bouter", "férir", "choir", "ouïr", "mandier", "mander", "bailler", "vergogner", "conchier", "mugueter", "pandiculer",
            "adouber", "ensaisiner", "ordalie", "plaid", "wergeld", "niquedouille", "baliverne", "bobance", "bramer",
            "courroux", "allégresse", "mélancolie", "tourment", "ravissement", "épouvante", "dédain", "fierté", 
            "orgueil", "humilité", "compassion", "rancoeur", "fureur", "tourmente", "destinée", "karma", 
            "équilibre", "chaos", "ordre", "dualité", "infini", "éphémère", "éternité", "auguste", "solennel",
            "vérité", "mensonge", "parjure", "loyauté", "vertu", "vice", "légende", "chroniques", "épopée",

            // 7. Créatures & Bestiaire
            "dragon", "troll", "elfe", "nain", "orc", "gobelin", "géant", "vampire", "spectre", "griffon", "licorne", "hydre",
            "fée", "loup", "démon", "fantôme", "goule", "squelette", "mort-vivant", "monstre", "bête", "créature", "minotaure",
            "centaure", "gargouille", "harpie", "golem", "satyre", "titan", "hippocampe", "basilic", "chimère", "banshee",
            "farfadet", "gnome", "dryade", "nymphe", "sylphide", "ondine", "kraken", "léviathan", "manticore", "wyverne", "pégase",
            "aspi", "cocatrix", "échéneis", "lantichare", "leucota", "monocéros", "vouivre", "amphisbène", "loup-garou",

            // 8. Héraldique (Blasons & Symboles)
            "azur", "gueules", "sinople", "sable", "blason", "armoiries", "écu", "chevron", "fasce", "pal", "sautoir",
            "lambel", "billette", "besant", "bezant", "alérion", "merlette", "fleur de lys", "quintefeuille", "rampant", "passant", "volant",
            "issant", "essorant", "contourné", "écartelé", "échiqueté", "émail", "métaux", "brisure", "lambrequin", "cimier", "bourdon",
            "pairle", "vivré", "saltire", "orle", "macle", "fusée", "frette", "écusson", "cabochon", "senestre", "dextre",

            // 9. Flore & Herboristerie
            "belladone", "mandragore", "jusquiame", "datura", "ciguë", "digitale", "vératre", "livèche", "aurone", "hysope", "verveine",
            "armoise", "millepertuis", "sauge", "romarin", "thym", "sarriette", "marjolaine", "angélique", "bénédicte", "souci", "mauve",
            "guimauve", "bouillon blanc", "coquelicot", "bleuet", "bourrache", "consoude", "ortie", "pissenlit", "plantain", "achillée",
            "tanaisie", "absinthe", "rue", "sétaire", "camomille", "valériane", "mélisse", "fenouil", "aneth", "coriandre", "cumin", "carvi",

            // 10. Objets, Mobilier & Quotidien
            "coffre", "huche", "dressoir", "bahut", "escabeau", "banc", "tabouret", "chaise", "fauteuil", "lit", "paillasse", "traversin",
            "oreiller", "courtepointe", "couverture", "drap", "rideau", "tapisserie", "tapis", "natte", "chandelier", "bougeoir", "torche",
            "lanterne", "lampe à huile", "brasero", "cheminée", "chenet", "soufflet", "tisonnier", "crémaillère", "chaudron", "marmite",
            "poêle", "faitout", "louche", "cuillère", "couteau", "hanap", "coupe", "verre", "pichet", "cruche", "carafe", "bouteille",
            "flacon", "baril", "tonneau", "cuve", "baquet", "seau", "bassine", "jarre", "amphore", "sac", "gibecière",
            "escarcelle", "panier", "corbeille", "malle", "coffret", "écrin", "miroir", "peigne", "brosse", "rasoir", "éponge",
            "savon", "serviette", "nappe", "assiette", "plat", "écuelle", "tranchoir", "mortier", "pilon", "balance", "poids", "mesure",
            "clef", "serrure", "verrou", "gond", "charnière", "clou", "vis", "marteau", "tenailles", "scie", "rabot", "ciseau",
            "tarière", "enclume", "soufflet de forge", "éprouvette", "plume",
            "encre", "encrier", "sceau", "cire", "cachet", "boussole", "sextant", "astrolabe", "sablier", "cadran solaire", "horloge",

            // 11. Exclamations & Météo DAoC
            "parbleu", "fichtre", "diantre", "pardi", "guilde", "certes", "nonobstant", "hélas", "mortecouille", "morbleu", "ventrebleu",
            "sacrebleu", "nonpoint", "icelle", "icelui", "que trépas me prenne", "par les dieux", "par ma barbe", "malepeste", "faquin",
            "pendart", "jarnidieu", "ventre saint-gris", "bigre", "hurluberlu", "iconoclasse", "lacrimable", "accointe",
            "bienvenue", "adieu", "salutations", "printemps", "été", "automne", "hiver", "givre", "rosée", "brise", "orage", "tempête", 
            "météore", "comète", "éclipse", "foudre", "tonnerre", "zénith", "aurore", "crépuscule", "autan", "borée", 
            "zéphyr", "frimas", "nimbes", "éther", "firmament", "constellation", "astral",


            // ================= ENGLISH =================
            // 1. DAoC Universe & Classes
            "paladin", "armsman", "theurgist", "cabalist", "sorcerer", "cleric", "friar", "mercenary", "infiltrator", "reaver", "heretic",
            "warrior", "berserker", "hunter", "skald", "thane", "runemaster", "spiritmaster", "shaman", "healer", "shadowblade", "bonedancer", "savage", "valkyrie",
            "hero", "blademaster", "warden", "bard", "eldritch", "enchanter", "mentalist", "druid", "animist", "valewalker", "vampiir", "champion", "nightshade", "ranger",
            "scout", "minstrel", "celt", "firbolg", "sylvan", "elf", "briton", "avalonian", "saracen", "highlander", "inconnu", "norseman", "dwarf", "troll", "kobold", "frostalf", "valkyn",
            "camelot", "jordheim", "tir na nog", "salisbury", "snowdonia", "dartmoor", "myrkwood", "raumarike", "malmohus", "lough derg",
            "lyonesse", "emain macha", "odin", "thor", "freya", "hibernia", "albion", "midgard", "frontiers", "relic", "outpost",
            "golestandt", "cuuldurach", "legion", "siabra", "morrigan", "glimmer", "wyrm",

            // 2. Medieval Life, Roles & Crafting
            "lord", "lady", "madam", "sire", "king", "queen", "prince", "princess", "monarch", "majesty", "highness", "regent",
            "sovereign", "duke", "baron", "count", "noble", "knight", "squire", "seneschal", "constable", "herald", "page",
            "bailiff", "provost", "chancellor", "grandmaster", "templar", "exorcist", "messenger", "brother", "sister", "father", "mother",
            "suzerain", "vassal", "chamberlain", "marshal", "apothecary", "tanner", "weaver", "blacksmith", "herbalist", "alchemist",
            "archmage", "priest", "monk", "bandit", "thief", "assassin", "guard", "sentinel", "captain", "necromancer", "peasant",
            "beggar", "scoundrel", "villain", "tavern", "inn", "feast", "mead", "ale", "venison", "broth", "gruel", "loaf", "stew",
            "carpenter", "mason", "tailor", "miller", "baker", "butcher", "innkeeper", "armorer", "fletcher", "goldsmith",
            "merchant", "scribe", "barber", "surgeon", "astrologer", "minstrel", "juggler", "acrobat", "jester", "crier",
            "sergeant", "watchman", "cook", "maid", "nurse", "midwife", "nun", "abbot", "abbess", "bishop", "cardinal", "pope",
            "hermit", "pilgrim", "crusader", "serf", "farmer", "shepherd", "woodcutter", "poacher", "fisherman", "sailor", "pirate", "executioner",

            // 3. Architecture & Fortifications
            "realm", "empire", "fiefdom", "county", "duchy", "hamlet", "town", "city", "fortress", "castle", "tower", "dungeon", "rampart",
            "portcullis", "moat", "loophole", "postern", "abbey", "hermitage", "cairn", "dolmen", "menhir", "grove", "swamp", "estuary",
            "archipelago", "valley", "plateau", "ravine", "chasm", "crypt", "abyss", "sanctuary", "altar", "border", "territory", "mountain",
            "river", "ocean", "sea", "cavern", "tomb", "barbican", "bastion", "battlement", "keep", "stronghold", "oubliette",

            // 4. Equipment, Weapons, Clothes & Jewelry
            "sword", "shield", "armor", "helm", "axe", "spear", "bow", "grimoire", "rune", "talisman", "parchment", "potion",
            "mail", "gauntlet", "dagger", "mace", "flail", "halberd", "javelin", "arrow", "quiver", "wand", "scepter", "crown",
            "cloak", "pouch", "purse", "amulet", "ring", "jewel", "gem", "crystal", "stone", "steel", "iron", "mithril", "gold", "silver",
            "claymore", "gambeson", "hauberk", "spur", "steed", "palfrey", "cuirass", "scabbard", "greatsword", "crossbow", "brigandine",
            "breastplate", "bracer", "greave", "tunic", "shirt", "breeches", "hose", "doublet", "surcoat", "mantle", "hood", "cap", "hat",
            "veil", "coif", "diadem", "tiara", "mitre", "chasuble", "cassock", "habit", "livery", "uniform", "tabard", "pauldron",
            "boot", "shoe", "sandal", "belt", "baldric", "buckle", "clasp", "button", "lace", "ribbon", "embroidery", "fur", "ermine",
            "wool", "silk", "velvet", "satin", "linen", "cotton", "vellum", "bracelet", "necklace", "pendant", "brooch", "earring", "orb",

            // 5. Magic, Religion & Alchemy
            "magic", "spell", "curse", "ritual", "incantation", "prophecy", "oracle", "shadow", "flame", "mana", "blessing", "pact",
            "demonic", "divine", "sacred", "profane", "spirit", "soul", "god", "goddess", "faith", "prayer", "miracle", "darkness", "light",
            "enchantment", "illusion", "invocation", "dogma", "heresy", "penance", "absolution", "devotion", "mystic", "omen", "crucible",
            "alchemy", "occult", "excommunication", "phylactery", "ectoplasm", "transmutation", "esotericism",

            // 6. Themes, Actions & Emotions
            "honor", "glory", "blood", "battle", "war", "quest", "destiny", "oath", "alliance", "vengeance", "courage", "heroism",
            "siege", "victory", "defeat", "treason", "bravery", "cowardice", "death", "life", "crusade", "journey", "voyage", "combat",
            "struggle", "triumph", "sacrifice", "peril", "danger", "ambush", "skirmish", "truce", "peace", "justice", "punishment",
            "forgiveness", "renown", "valiant", "loyal", "deceitful", "austere", "magnanimous", "intrepid", "wrath", "joy", "melancholy",
            "torment", "dread", "disdain", "pride", "humility", "compassion", "fury", "karma", "chaos", "order", "duality", "infinity",
            "eternity", "truth", "lie", "perjury", "virtue", "vice", "legend", "chronicle", "epic", "decree", "edict", "exploit", "deed",

            // 7. Creatures & Bestiary
            "dragon", "troll", "elf", "dwarf", "orc", "goblin", "giant", "vampire", "specter", "griffon", "unicorn", "hydra",
            "fairy", "wolf", "demon", "ghost", "ghoul", "skeleton", "undead", "monster", "beast", "creature", "minotaur", "centaur",
            "gargoyle", "harpy", "golem", "satyr", "titan", "hippocampus", "basilisk", "chimera", "banshee", "sprite", "gnome", "dryad",
            "nymph", "sylph", "undine", "kraken", "leviathan", "manticore", "wyvern", "pegasus", "werewolf", "fiend", "abomination",

            // 8. Heraldry
            "azure", "gules", "vert", "sable", "ermine", "vair", "coat-of-arms", "crest", "chevron", "fess", "pale", "saltire",
            "label", "bezant", "rampant", "passant", "quartered", "sigil", "standard", "banner",

            // 9. Flora & Herbalism
            "belladonna", "mandrake", "henbane", "hemlock", "foxglove", "lovage", "hyssop", "vervain", "mugwort", "sage", "rosemary",
            "thyme", "savory", "marjoram", "angelica", "marigold", "mallow", "marshmallow", "poppy", "cornflower", "borage", "comfrey",
            "nettle", "dandelion", "plantain", "yarrow", "tansy", "absinthe", "rue", "chamomile", "valerian", "fennel", "dill", "coriander", "cumin",

            // 10. Objects, Furniture & Daily Life
            "chest", "dresser", "stool", "bench", "chair", "armchair", "bed", "mattress", "bolster", "pillow", "blanket", "sheet",
            "curtain", "tapestry", "rug", "mat", "chandelier", "candlestick", "torch", "lantern", "brazier", "fireplace", "cauldron",
            "pot", "pan", "ladle", "spoon", "knife", "chalice", "cup", "glass", "pitcher", "jug", "carafe", "bottle", "flask", "barrel",
            "cask", "tub", "bucket", "basin", "jar", "amphora", "bag", "satchel", "basket", "trunk", "mirror", "comb", "brush", "razor",
            "sponge", "soap", "towel", "tablecloth", "plate", "dish", "bowl", "trencher", "mortar", "pestle", "scale", "weight", "key",
            "lock", "bolt", "hinge", "nail", "screw", "hammer", "pincers", "saw", "plane", "chisel", "anvil", "bellows", "alembic",
            "quill", "ink", "inkwell", "seal", "wax", "compass", "sextant", "astrolabe", "hourglass", "sundial", "clock",

            // 11. Exclamations & Weather
            "forsooth", "verily", "hark", "alas", "huzzah", "greetings", "farewell", "welcome", "aye", "nay", "yonder", "thou", "thee", "thy", "thine",
            "spring", "summer", "autumn", "winter", "frost", "dew", "breeze", "storm", "tempest", "meteor", "comet", "eclipse",
            "lightning", "thunder", "zenith", "dawn", "dusk", "zephyr", "aether", "firmament", "constellation", "astral",


            // ================= GERMAN =================
            // 1. DAoC Universe & Classes
            "paladin", "waffenmeister", "theurg", "kabbalist", "hexenmeister", "kleriker", "ordensbruder", "söldner", "infiltrator", "ketzer",
            "krieger", "berserker", "jäger", "skalde", "thane", "runenmeister", "geisterbeschwörer", "schamane", "heiler", "schattenklinge", "knochentänzer", "wilder", "walküre",
            "held", "schwertmeister", "barde", "eldritch", "beschwörer", "mentalist", "druide", "animist", "champion", "nachtschatten", "waldläufer",
            "kundschafter", "minnesänger", "kelte", "firbolg", "elf", "bretone", "avalonier", "sarazene", "inconnu", "nordmann", "zwerg", "troll", "kobold", "frostalf", "valkyn",
            "camelot", "jordheim", "tir na nog", "salisbury", "snowdonia", "dartmoor", "myrkwood", "raumarike", "malmohus", "lough derg",
            "lyonesse", "emain macha", "odin", "thor", "freya", "hibernia", "albion", "midgard", "grenzgebiete", "relikt", "außenposten",
            "golestandt", "cuuldurach", "legion", "siabra", "morrigan", "glimmer",

            // 2. Medieval Life, Roles & Crafting
            "herr", "dame", "könig", "königin", "prinz", "prinzessin", "monarch", "majestät", "hoheit", "regent",
            "herrscher", "herzog", "graf", "adel", "ritter", "knappe", "herold", "page", "kanzler", "großmeister", "templer", "exorzist",
            "bote", "bruder", "schwester", "vater", "mutter", "lehnsherr", "vasall", "marschall", "apotheker", "gerber", "weber", "schmied",
            "alchemist", "erzmagier", "priester", "mönch", "bandit", "dieb", "assassine", "wache", "wächter", "hauptmann", "nekromant",
            "bauer", "bettler", "schurke", "bösewicht", "taverne", "gasthaus", "fest", "met", "bier", "wildbret", "brühe", "eintopf",
            "zimmermann", "maurer", "schneider", "müller", "bäcker", "fleischer", "wirt", "rüstschmied", "goldschmied",
            "kaufmann", "schreiber", "barbier", "chirurg", "astrologe", "gaukler", "akrobat", "narr", "ausrufer",
            "koch", "magd", "amme", "hebamme", "nonne", "abt", "äbtissin", "bischof", "kardinal", "papst",
            "eremit", "pilger", "kreuzritter", "leibeigener", "hirte", "holzfäller", "wilderer", "fischer", "seemann", "pirat", "henker",

            // 3. Architecture & Fortifications
            "reich", "imperium", "lehen", "grafschaft", "herzogtum", "dorf", "weiler", "stadt", "festung", "burg", "schloss", "turm", "verlies",
            "wall", "fallgatter", "graben", "schießscharte", "abtei", "einsiedelei", "dolmen", "menhir", "hain", "sumpf", "mündung",
            "archipel", "tal", "plateau", "schlucht", "abgrund", "krypta", "heiligtum", "altar", "grenze", "territorium", "berg",
            "fluss", "ozean", "meer", "höhle", "grab", "barbakane", "bastion", "zinnen", "bollwerk",

            // 4. Equipment, Weapons, Clothes & Jewelry
            "schwert", "schild", "rüstung", "helm", "axt", "speer", "bogen", "grimoire", "rune", "talisman", "pergament", "trank",
            "kettenhemd", "fehdehandschuh", "dolch", "streitkolben", "flegel", "hellebarde", "wurfspeer", "pfeil", "köcher", "zauberstab", "zepter",
            "krone", "umhang", "beutel", "amulett", "ring", "juwel", "edelstein", "kristall", "stein", "stahl", "eisen", "mithril", "gold", "silber",
            "claymore", "wams", "sporn", "ross", "kürass", "scheide", "großschwert", "armbrust", "brigantine",
            "brustpanzer", "armschiene", "beinschiene", "tunika", "hemd", "hose", "mantel", "kapuze", "kappe", "hut",
            "schleier", "diadem", "tiara", "mitra", "gewand", "uniform", "wappenrock", "stiefel", "schuh", "sandale", "gürtel",
            "schnalle", "knopf", "spitze", "band", "stickerei", "pelz", "hermelin", "wolle", "seide", "samt", "leinen", "baumwolle",
            "pergamentpapier", "armband", "halskette", "anhänger", "brosche", "ohrring", "reichsapfel",

            // 5. Magic, Religion & Alchemy
            "magie", "zauber", "fluch", "ritual", "beschwörung", "prophezeiung", "orakel", "schatten", "flamme", "mana", "segen", "pakt",
            "dämonisch", "göttlich", "heilig", "profan", "geist", "seele", "gott", "göttin", "glaube", "gebet", "wunder", "dunkelheit", "licht",
            "verzauberung", "illusion", "dogma", "häresie", "buße", "absolution", "hingabe", "mystiker", "omen", "tiegel",
            "alchemie", "okkult", "exkommunikation", "phylakterion", "ektoplasma", "transmutation", "esoterik",

            // 6. Themes, Actions & Emotions
            "ehre", "ruhm", "blut", "schlacht", "krieg", "queste", "schicksal", "eid", "allianz", "rache", "mut", "heldentum",
            "belagerung", "sieg", "niederlage", "verrat", "tapferkeit", "feigheit", "tod", "leben", "kreuzzug", "reise", "kampf",
            "ringen", "triumph", "opfer", "gefahr", "hinterhalt", "scharmützel", "waffenstillstand", "frieden", "gerechtigkeit", "strafe",
            "vergebung", "tapfer", "loyal", "unerschrocken", "zorn", "freude", "melancholie",
            "qual", "furcht", "verachtung", "stolz", "demut", "mitgefühl", "wut", "karma", "chaos", "ordnung", "dualität", "unendlichkeit",
            "ewigkeit", "wahrheit", "lüge", "meineid", "tugend", "laster", "legende", "chronik", "epos", "dekret", "erlass", "heldentat",

            // 7. Creatures & Bestiary
            "drache", "troll", "elf", "zwerg", "ork", "goblin", "riese", "vampir", "gespenst", "greif", "einhorn", "hydra",
            "fee", "wolf", "dämon", "geist", "ghul", "skelett", "untoter", "monster", "bestie", "kreatur", "minotaurus", "zentaur",
            "wasserspeier", "harpyie", "golem", "satyr", "titan", "hippokamp", "basilisk", "chimäre", "banshee", "kobold", "gnom", "dryade",
            "nymphe", "sylphe", "undine", "krake", "leviathan", "mantikor", "wyvern", "pegasus", "werwolf", "abscheulichkeit",

            // 8. Heraldry
            "azur", "kehl", "sinopel", "sable", "hermelin", "feh", "wappen", "siegel", "sparren", "balken", "pfahl", "andreaskreuz",
            "turnierkragen", "bezant", "steigend", "schreitend", "geviert", "standarte", "banner",

            // 9. Flora & Herbalism
            "tollkirsche", "alraune", "bilsenkraut", "schierling", "fingerhut", "liebstöckel", "ysop", "eisenkraut", "beifuß", "salbei", "rosmarin",
            "thymian", "bohnenkraut", "majoran", "engelwurz", "ringelblume", "malve", "eibisch", "mohn", "kornblume", "borretsch", "beinwell",
            "brennnessel", "löwenzahn", "wegerich", "schafgarbe", "reinfarn", "absinth", "raute", "kamille", "baldrian", "fenchel", "dill", "koriander", "kümmel",

            // 10. Objects, Furniture & Daily Life
            "truhe", "kommode", "hocker", "bank", "stuhl", "sessel", "bett", "matratze", "kissen", "decke", "laken",
            "vorhang", "wandteppich", "teppich", "matte", "kronleuchter", "kerzenständer", "fackel", "laterne", "feuerschale", "kamin", "kessel",
            "topf", "pfanne", "kelle", "löffel", "messer", "kelch", "tasse", "glas", "krug", "karaffe", "flasche", "flachmann", "fass",
            "wanne", "eimer", "becken", "krug", "amphore", "tasche", "beutel", "korb", "koffer", "spiegel", "kamm", "bürste", "rasiermesser",
            "schwamm", "seife", "handtuch", "tischdecke", "teller", "schüssel", "mörser", "stößel", "waage", "gewicht", "schlüssel",
            "schloss", "riegel", "scharnier", "nagel", "schraube", "hammer", "zange", "säge", "hobel", "meißel", "amboss", "blasebalg", "destillierkolben",
            "federkiel", "tinte", "tintenfass", "siegel", "wachs", "kompass", "sextant", "astrolabium", "sanduhr", "sonnenuhr", "uhr",

            // 11. Exclamations & Weather
            "fürwahr", "wahrlich", "horcht", "dort", "du", "dir", "dein", "deine", "willkommen", "ja", "nein", "obgleich", "fortan", "sogleich",
            "frühling", "sommer", "herbst", "winter", "frost", "tau", "brise", "sturm", "unwetter", "meteor", "komet", "finsternis",
            "blitz", "donner", "zenit", "morgengrauen", "dämmerung", "zephir", "äther", "firmament", "sternbild", "astral"
        };

        [ScriptLoadedEvent]
        public static void OnScriptCompiled(DOLEvent e, object sender, EventArgs args)
        {
            log.Info("RoleplayReward script loading...");
            GameEventMgr.AddHandler(GameLivingEvent.Say, OnPlayerTalk);
            GameEventMgr.AddHandler(GameLivingEvent.Yell, OnPlayerTalk);
            GameEventMgr.AddHandler(GameLivingEvent.Whisper, OnPlayerTalk);
        }

        [ScriptUnloadedEvent]
        public static void OnScriptUnloaded(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.RemoveHandler(GameLivingEvent.Say, OnPlayerTalk);
            GameEventMgr.RemoveHandler(GameLivingEvent.Yell, OnPlayerTalk);
            GameEventMgr.RemoveHandler(GameLivingEvent.Whisper, OnPlayerTalk);
        }

        private static void OnPlayerTalk(DOLEvent e, object sender, EventArgs args)
        {
            if (!Properties.ROLEPLAYREWARD_ENABLED)
                return;

            if (!(sender is GamePlayer player)) return;

            if (player.IsInPvP || player.IsInRvR || player.TempProperties.getProperty<bool>("ArenaParticipant", false))
                return;

            string text = "";
            if (args is SayEventArgs sayArgs) text = sayArgs.Text;
            else if (args is YellEventArgs yellArgs) text = yellArgs.Text;
            else if (args is WhisperEventArgs whisperArgs) text = whisperArgs.Text;

            if (string.IsNullOrWhiteSpace(text) || text.StartsWith("/")) return;

            long now = player.CurrentRegion.Time;

            // Bad words usage breaks the chain, cancels rewards, sets penalty, and triggers Guards
            if (BookUtils.ContainsProhibitedTerms(text, out string badWord))
            {
                HandleBadWord(player, now);
                return;
            }

            if (player.Level < 25 && !player.IsRenaissance)
                return;

            // Check if player is currently under an RP reward penalty
            long penaltyEnd = player.TempProperties.getProperty<long>("RP_PenaltyEnd", 0);
            if (now < penaltyEnd)
                return;

            // Anti-Macro Spam Check: Did they just paste the exact same text as last time?
            string lastText = player.TempProperties.getProperty<string>("RP_LastMessageText", "");
            if (text.Equals(lastText, StringComparison.OrdinalIgnoreCase))
                return;

            // Gibberish won't create a penalty but skips the RP reward.
            if (BookUtils.LooksLikeGibberish(text))
                return;

            // Minimum words requirement
            int wordCount = BookUtils.CountWords(text);
            if (wordCount < MIN_WORDS_REQUIRED)
                return;

            // Talking alone requirement: Ensure at least one other player is within 1800 units
            bool playerAround = false;
            foreach (GamePlayer p in player.GetPlayersInRadius(REQUIRED_PLAYER_RADIUS))
            {
                if (p != null && p != player && p.IsAlive)
                {
                    playerAround = true;
                    break;
                }
            }

            if (!playerAround) return;

            // Chain Cooldown Spam Check (Prevents Macro abuse)
            long lastTalk = player.TempProperties.getProperty<long>("RP_LastTalk", 0);
            if (lastTalk > 0 && now - lastTalk < MIN_TIME_BETWEEN_TALKS_MS)
                return;

            CalculateAndGiveReward(player, text, wordCount, now, lastTalk);
        }

        public static void ResetRPChain(GamePlayer player)
        {
            if (player == null) return;
            long now = player.CurrentRegion != null ? player.CurrentRegion.Time : 0;

            player.TempProperties.setProperty("RP_Chain", 0);
            player.TempProperties.setProperty("RP_LastTalk", now);
        }

        private static void HandleBadWord(GamePlayer player, long now)
        {
            player.TempProperties.setProperty("RP_PenaltyEnd", now + PENALTY_DURATION_MS);
            player.TempProperties.setProperty("RP_Chain", 0);

            long lastWarningTime = player.TempProperties.getProperty<long>("RP_LastBadWordTime", 0);
            int warnings = player.TempProperties.getProperty<int>("RP_BadWord_Warnings", 0);

            if (lastWarningTime > 0 && now - lastWarningTime > BADWORD_WARNING_DECAY_MS)
                warnings = 0;

            warnings++;
            player.TempProperties.setProperty("RP_BadWord_Warnings", warnings);
            player.TempProperties.setProperty("RP_LastBadWordTime", now);

            var guards = player.GetNPCsInRadius((ushort)WorldMgr.VISIBILITY_DISTANCE).OfType<GameNPC>().Where(n => n is IGuardNPC).ToList();

            if (guards.Count > 0)
            {
                string lang = player.Client.Account.Language;

                if (warnings >= 3)
                {
                    foreach (var guard in guards)
                    {
                        guard.TurnTo(player.Coordinate);

                        string attackKey = "RoleplayReward.GuardAttack." + Util.Random(0, 2);
                        guard.Say(LanguageMgr.GetTranslation(lang, attackKey, player.Name));

                        if (guard.Brain is StandardMobBrain brain)
                        {
                            brain.AddToAggroList(player, 500);
                        }
                        guard.StartAttack(player);
                    }
                    player.TempProperties.setProperty("RP_BadWord_Warnings", 0);
                }
                else
                {
                    var closestGuard = guards.OrderBy(g => g.GetDistanceTo(player)).First();
                    closestGuard.TurnTo(player.Coordinate);

                    string warningKey = "RoleplayReward.GuardWarning." + Util.Random(0, 2);
                    closestGuard.Say(LanguageMgr.GetTranslation(lang, warningKey, player.Name));
                }
            }
        }

        private static void CalculateAndGiveReward(GamePlayer player, string text, int wordCount, long now, long lastTalk)
        {
            int chain = player.TempProperties.getProperty<int>("RP_Chain", 0);

            // Chain breaks if too much time has passed (End of talking cooldown)
            if (lastTalk > 0 && now - lastTalk > MAX_CHAIN_TIMEOUT_MS)
                chain = 0;

            // Base Reward
            int baseRP = (player.Level < 25) ? 1 : 2;

            // Apply Exponential Erudition RP Bonus to Base (e.g. Math.Pow(1.2, Level))
            double eruditionMultiplier = Math.Pow(1.0 + (Properties.ERUDITION_RP_BONUS_PERCENT / 100.0), player.EruditionLevel);
            int rpReward = (int)Math.Round(baseRP * eruditionMultiplier);

            // Hidden score system / high-quality RP words multiplier 
            var wordsInText = Regex.Matches(text, @"\b[\p{L}]+\b").Cast<Match>().Select(m => m.Value).ToList();
            var usedRPWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string w in wordsInText)
            {
                if (RP_WORDS.Contains(w) && !usedRPWords.Contains(w))
                {
                    usedRPWords.Add(w);
                }
            }

            int wordBonus = usedRPWords.Count;
            if (wordBonus > MAX_WORD_BONUS)
                wordBonus = MAX_WORD_BONUS;

            rpReward += wordBonus;

            // Chain bonus - each success adds to stack 
            int chainBonus = chain;
            if (chainBonus > MAX_CHAIN_BONUS)
                chainBonus = MAX_CHAIN_BONUS;

            rpReward += chainBonus;

            // Erudition Points (1 point per word, +2 points per 'good' vocabulary word)
            int eruditionPoints = wordCount + (usedRPWords.Count * 2);

            player.TempProperties.setProperty("RP_Chain", chain + 1);
            player.TempProperties.setProperty("RP_LastTalk", now);
            player.TempProperties.setProperty("RP_LastMessageText", text);
            player.GainEruditionPoints(eruditionPoints, false);
            player.GainRealmPoints(rpReward, false, false, true);

            string msgKey = "RoleplayReward.RewardMsg." + Util.Random(0, 7);
            string rewardMessage = LanguageMgr.GetTranslation(player.Client.Account.Language, msgKey, rpReward);

            player.Out.SendMessage(rewardMessage, eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
            player.Out.SendMessage(rewardMessage + $" (Erudition: +{eruditionPoints})", eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
        }
    }
}
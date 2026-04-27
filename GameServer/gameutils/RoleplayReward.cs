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

        private static readonly HashSet<string> RP_WORDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // ================= FRENCH =================
            // Titres, salutations et rôles
            "messire", "damoiseau", "sire", "dame", "seigneur", "roi", "reine", "prince", "princesse", "monarque",
            "duc", "baron", "comte", "châtelain", "noble", "paladin", "chevalier", "preux", "héros", "champion",
            "mercenaire", "paysan", "tavernier", "barde", "ménestrel", "forgeron", "herboriste", "alchimiste",
            "archimage", "prêtre", "clerc", "moine", "brigand", "voleur", "assassin", "garde", "sentinelle",
            "capitaine", "nécromancien", "druide", "sorcier", "chaman", "chasseur", "archer", "guerrier",
            "gueux", "maraud", "manant", "félon", "misérable", "vil", "salutations", "adieu", "bienvenue",
            "écuyer", "sénéchal", "connétable", "intendant", "héraut", "page", "bailli", "prévôt", "chancelier",
            "grand-maître", "templier", "exorciste", "druidesse", "palfrenier", "éclaireur", "vigie", "messager",
            "confrère", "soeur", "frère", "père", "mère", "daim", "cerf", "lignée", "ancêtre", "suzerain", "vassal",
            "majesté", "altesse", "cour", "trône", "régence", "souverain", "souveraineté", "éminence", "noblesse",
            
            // Lieux et géographie
            "royaume", "empire", "fief", "comté", "duché", "village", "hameau", "bourg", "auberge", "taverne",
            "crypte", "donjon", "abysse", "sanctuaire", "autel", "forteresse", "tour", "cité", "forêt", "temple",
            "grotte", "ruine", "nécropole", "château", "rempart", "frontière", "contrée", "territoire", "montagne",
            "val", "vallée", "rivière", "fleuve", "océan", "mer", "caverne", "tombeau", "herse", "douve", "meurtrière",
            "lice", "poterne", "chemin de ronde", "castrum", "abbaye", "ermitage", "cairn", "dolmen", "menhir",
            "bosquet", "marais", "estuaire", "archipel", "vallon", "causse", "plateau", "ravin", "gouffre",
            
            // Équipement, objets, et héraldique
            "épée", "bouclier", "armure", "heaume", "hache", "lance", "arc", "grimoire", "rune", "talisman", "relique",
            "parchemin", "potion", "hydromel", "cotte", "mailles", "gantelet", "dague", "poignard", "masse", "fléau",
            "hallebarde", "javelot", "flèche", "carquois", "baguette", "sceptre", "couronne", "cape", "besace", "bourse",
            "amulette", "anneau", "joyau", "gemme", "cristal", "pierre", "acier", "fer", "mithril", "or", "argent",
            "gladius", "spatha", "claymore", "francisque", "framée", "scramasaxe", "broigne", "gambison", "haubert",
            "bassinet", "salade", "gorgerin", "éperon", "destrier", "palefroi", "roncin", "cuirasse", "boucle", "fourreau",
            "diadème", "blason", "armoiries", "étendard", "bannière", "reliquaire",
            
            // Magie, religion, mythes et concepts
            "magie", "sortilège", "malédiction", "rituel", "incantation", "prophétie", "oracle", "ombre", "flamme",
            "mana", "bénédiction", "pacte", "démoniaque", "divin", "sacré", "profane", "esprit", "âme", "dieu", "dieux",
            "déesse", "foi", "prière", "miracle", "ténèbres", "lumière", "enchantement", "illusion", "invocation",
            "dogme", "hérésie", "pénitence", "absolution", "ferveur", "dévotion", "ascèse", "mystique", "augure",
            "divinité", "panthéon", "céleste", "saint", "dévot", "béatitude", "grâce", "chérubin", "archange", "idole",
            "pèlerin", "pèlerinage", "trinité", "prophète", "sacrilège", "blasphème",
            
            // Contes, légendes et musique
            "conte", "légende", "mythe", "fable", "chant", "symphonie", "lyre", "luth", "harpe", "hymne", "mélodie",
            "épopée", "saga",
            
            // Thèmes, actions, quêtes et émotions
            "honneur", "gloire", "sang", "bataille", "guerre", "quête", "destin", "serment", "alliance", "vengeance",
            "courage", "héroïsme", "siège", "victoire", "défaite", "trahison", "bravoure", "lâcheté", "mort", "vie",
            "croisade", "périple", "voyage", "festin", "combat", "lutte", "triomphe", "sacrifice", "péril", "danger",
            "embuscade", "escarmouche", "trêve", "paix", "justice", "châtiment", "pardon", "renommée",
            "mandat", "quérir", "oyez", "festoyer", "guerroyer", "jouter", "pourfendre", "occire", "truander", "haranguer",
            "vaillant", "belliqueux", "infâme", "loyal", "fourbe", "austère", "magnanime", "intrépide", "outrecuidant",
            "décret", "édit", "exploit", "haut-fait", "prouesse", "vaillance", "odyssée", "entreprise", "conquête",
            "expédition", "miséricorde", "clémence",
            
            // Créatures, bestiaire et ennemis
            "dragon", "troll", "elfe", "nain", "orc", "gobelin", "géant", "vampire", "spectre", "griffon", "licorne",
            "hydre", "wyrm", "fée", "celte", "breton", "nordique", "loup", "démon", "fantôme", "goule", "squelette",
            "mort-vivant", "monstre", "bête", "créature", "minotaure", "centaure", "gargouille", "harpie", "golem",
            "luridien", "firbolg", "sylvestre", "sarrazin", "highlander", "kobold", "valkyrie", "berserker", "skald",
            "thane", "runiste", "barde", "eldritch", "enchanteur", "mentaliste", "animiste", "faucheur",
            "adversaire", "ennemi", "némésis", "antagoniste", "abomination", "tyran", "despote", "usurpateur",
            "oppresseur", "envahisseur", "horde", "vermine", "racaille",
            
            // Exclamations et vieux français
            "parbleu", "fichtre", "diantre", "pardi", "guilde", "albion", "midgard",
            "hibernia", "certes", "nonobstant", "hélas", "mortecouille", "morbleu", "ventrebleu", "sacrebleu",
            "nonpoint", "icelle", "icelui", "que trépas me prenne", "par ma barbe",
            
            // ================= ENGLISH =================
            // Titles, Greetings, and Roles
            "lord", "lady", "madam", "king", "queen", "princess", "monarch", "majesty", "highness", "regent",
            "sovereign", "sovereignty", "eminence", "nobility", "duke", "count", "knight", "hero", "mercenary",
            "peasant", "tavern", "minstrel", "blacksmith", "herbalist", "alchemist", "archmage", "priest", "cleric",
            "monk", "bandit", "thief", "assassin", "guard", "sentinel", "captain", "necromancer", "druid", "sorcerer",
            "shaman", "hunter", "warrior", "greetings", "farewell", "welcome", "squire", "seneschal", "constable",
            "herald", "page", "bailiff", "provost", "chancellor", "grandmaster", "templar", "exorcist", "scout",
            "messenger", "brother", "sister", "father", "mother", "ancestor", "suzerain", "vassal",
            
            // Places and Geography
            "realm", "fiefdom", "county", "duchy", "hamlet", "town", "inn", "crypt", "dungeon", "abyss", "sanctuary",
            "altar", "fortress", "tower", "city", "forest", "cave", "ruin", "necropolis", "castle", "rampart",
            "border", "territory", "mountain", "valley", "river", "ocean", "sea", "cavern", "tomb", "moat", "abbey",
            "grove", "swamp", "estuary", "archipelago", "plateau", "ravine", "chasm", "court", "throne",
            
            // Equipment, Items, and Heraldry
            "sword", "shield", "armor", "helm", "axe", "spear", "bow", "grimoire", "talisman", "relic", "reliquary",
            "parchment", "mead", "gauntlet", "dagger", "mace", "flail", "halberd", "javelin", "arrow", "quiver",
            "wand", "scepter", "crown", "cloak", "amulet", "ring", "jewel", "gem", "crystal", "stone", "steel",
            "iron", "gladius", "gambeson", "hauberk", "spur", "steed", "scabbard", "diadem", "coat-of-arms",
            "standard", "banner", "crest", "sigil",
            
            // Magic, Religion, Myths, and Concepts
            "magic", "spell", "curse", "ritual", "incantation", "prophecy", "oracle", "shadow", "flame", "blessing",
            "pact", "demonic", "divine", "sacred", "profane", "spirit", "soul", "god", "goddess", "faith", "prayer",
            "miracle", "darkness", "light", "enchantment", "illusion", "invocation", "dogma", "heresy", "penance",
            "absolution", "devotion", "mystic", "omen", "deity", "pantheon", "celestial", "saint", "devout", "cherub",
            "archangel", "idol", "pilgrim", "pilgrimage", "trinity", "prophet", "holy", "hallowed", "consecrate",
            "anoint", "sacrilege", "blasphemy",
            
            // Tales, Legends, and Music
            "tale", "legend", "myth", "fable", "epic", "saga", "song", "symphony", "lyre", "lute", "harp", "anthem",
            "melody", "ballad", "folklore",
            
            // Themes, Actions, Quests, and Emotions
            "honor", "glory", "blood", "battle", "war", "quest", "destiny", "oath", "alliance", "vengeance", "courage",
            "heroism", "siege", "victory", "defeat", "treason", "bravery", "cowardice", "death", "life", "crusade",
            "journey", "voyage", "feast", "combat", "struggle", "triumph", "sacrifice", "peril", "danger", "ambush",
            "skirmish", "truce", "peace", "justice", "punishment", "forgiveness", "renown", "valiant", "loyal",
            "intrepid", "decree", "edict", "exploit", "deed", "prowess", "valor", "odyssey", "undertaking", "conquest",
            "expedition", "vow", "pledge", "damned", "mercy", "clemency", "infamy",
            
            // Creatures, Bestiary, and Foes
            "elf", "dwarf", "goblin", "giant", "specter", "unicorn", "fairy", "celt", "briton", "norse", "wolf",
            "demon", "ghost", "ghoul", "skeleton", "undead", "monster", "beast", "creature", "minotaur", "centaur",
            "gargoyle", "harpy", "sylvan", "saracen", "runemaster", "reaper", "adversary", "enemy", "nemesis",
            "antagonist", "scourge", "abomination", "tyrant", "despot", "usurper", "oppressor", "invader", "horde",
            "vermin", "scum", "fiend", "villain",
            
            // Old English Exclamations and Terminology
            "hail", "forsooth", "verily", "aye", "nay", "hark", "yonder", "thou", "thee", "thy", "thine", "albeit",
            "whence", "whither", "henceforth", "forthwith", "prithee",

            // ================= GERMAN =================
            // Titles, Greetings, and Roles
            "herr", "dame", "könig", "königin", "prinz", "prinzessin", "monarch", "majestät", "hoheit", "regent",
            "herrscher", "herrschaft", "adel", "herzog", "graf", "ritter", "held", "söldner", "bauer", "wirt",
            "barde", "minnesänger", "schmied", "kräuterkundiger", "alchemist", "erzmagier", "priester", "kleriker",
            "mönch", "bandit", "dieb", "assassine", "wache", "wächter", "hauptmann", "nekromant", "druide", "zauberer",
            "schamane", "jäger", "krieger", "knappe", "herold", "page", "kanzler", "großmeister", "templer", "exorzist",
            "späher", "bote", "bruder", "schwester", "vater", "mutter", "ahne", "lehnsherr", "vasall",
            
            // Places and Geography
            "reich", "lehen", "grafschaft", "herzogtum", "dorf", "weiler", "stadt", "gasthaus", "taverne", "krypta",
            "verlies", "abgrund", "heiligtum", "altar", "festung", "turm", "wald", "höhle", "ruine", "nekropole",
            "schloss", "burg", "wall", "grenze", "territorium", "berg", "tal", "fluss", "ozean", "meer", "grab",
            "graben", "abtei", "hain", "sumpf", "archipel", "plateau", "schlucht", "hof", "thron",
            
            // Equipment, Items, and Heraldry
            "schwert", "schild", "rüstung", "helm", "axt", "speer", "bogen", "grimoire", "talisman", "relikt",
            "pergament", "met", "fehdehandschuh", "dolch", "streitkolben", "flegel", "hellebarde", "pfeil", "köcher",
            "zauberstab", "zepter", "krone", "umhang", "amulett", "ring", "juwel", "edelstein", "kristall", "stein",
            "stahl", "eisen", "wappen", "standarte", "banner", "siegel",
            
            // Magic, Religion, Myths, and Concepts
            "magie", "zauber", "fluch", "ritual", "beschwörung", "prophezeiung", "orakel", "schatten", "flamme", "segen",
            "pakt", "dämonisch", "göttlich", "heilig", "profan", "geist", "seele", "gott", "göttin", "glaube", "gebet",
            "wunder", "dunkelheit", "licht", "verzauberung", "illusion", "dogma", "häresie", "buße", "absolution",
            "hingabe", "mystiker", "omen", "gottheit", "pantheon", "himmlisch", "engel", "erzengel", "pilger",
            "pilgerfahrt", "dreifaltigkeit", "prophet", "sakrileg", "blasphemie",
            
            // Tales, Legends, and Music
            "märchen", "legende", "mythos", "fabel", "epos", "saga", "lied", "symphonie", "lyra", "laute", "harfe",
            "hymne", "melodie", "ballade", "folklore",
            
            // Themes, Actions, Quests, and Emotions
            "ehre", "ruhm", "blut", "schlacht", "krieg", "queste", "schicksal", "eid", "allianz", "rache", "mut",
            "heldentum", "belagerung", "sieg", "niederlage", "verrat", "tapferkeit", "feigheit", "tod", "leben",
            "kreuzzug", "reise", "fest", "kampf", "ringen", "triumph", "opfer", "gefahr", "hinterhalt", "scharmützel",
            "waffenstillstand", "frieden", "gerechtigkeit", "strafe", "vergebung", "tapfer", "loyal", "unerschrocken",
            "dekret", "erlass", "heldentat", "eroberung", "expedition", "schwur", "verdammt", "gnade", "nachsicht",
            "schande",
            
            // Creatures, Bestiary, and Foes
            "elf", "zwerg", "goblin", "riese", "gespenst", "einhorn", "fee", "kelte", "brite", "nordmann", "wolf",
            "dämon", "ghul", "skelett", "untoter", "monster", "bestie", "kreatur", "minotaurus", "zentaur",
            "wasserspeier", "harpyie", "waldwesen", "sarazene", "runenmeister", "schnitter", "gegner", "feind",
            "nemesis", "antagonist", "geißel", "abscheulichkeit", "tyrann", "despot", "usurpator", "unterdrücker",
            "eindringling", "horde", "ungeziefer", "abschaum", "schurke",
            
            // Exclamations and Old German
            "willkommen", "fürwahr", "wahrlich", "ja", "nein", "horcht", "dort", "du", "dir", "dein", "deine",
            "obgleich", "fortan", "sogleich", "albion", "midgard", "hibernia"
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

            CalculateAndGiveReward(player, text, now, lastTalk);
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

        private static void CalculateAndGiveReward(GamePlayer player, string text, long now, long lastTalk)
        {
            int chain = player.TempProperties.getProperty<int>("RP_Chain", 0);

            // Chain breaks if too much time has passed (End of talking cooldown)
            if (lastTalk > 0 && now - lastTalk > MAX_CHAIN_TIMEOUT_MS)
                chain = 0;

            // Base Reward
            int rpReward = (player.Level < 25) ? 1 : 2;

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

            player.TempProperties.setProperty("RP_Chain", chain + 1);
            player.TempProperties.setProperty("RP_LastTalk", now);
            player.TempProperties.setProperty("RP_LastMessageText", text);
            player.GainRealmPoints(rpReward, false, false, true);

            string msgKey = "RoleplayReward.RewardMsg." + Util.Random(0, 7);
            string rewardMessage = LanguageMgr.GetTranslation(player.Client.Account.Language, msgKey, rpReward);

            player.Out.SendMessage(rewardMessage, eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
        }
    }
}
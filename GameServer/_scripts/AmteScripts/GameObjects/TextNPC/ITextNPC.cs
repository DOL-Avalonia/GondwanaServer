/**
 * Created by Virant "Dre" Jérémy for Amtenael
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Threading;
using DOL.Database;
using DOL.Events;
using DOL.GameEvents;
using DOL.GS.Finance;
using DOL.GS.PacketHandler;
using DOL.GS.Quests;
using log4net;
using static DOL.Database.ArtifactBonus;
using static DOL.GS.GameNPC;
using static DOL.GS.Quests.DataQuestJsonGoal;
using DOL.Territories;
using DOL.Language;
using System.Text.RegularExpressions;
using System.Numerics;

namespace DOL.GS.Scripts
{
    public interface ITextNPC
    {
        TextNPCPolicy GetTextNPCPolicy(GameLiving target = null);

        TextNPCPolicy GetOrCreateTextNPCPolicy(GameLiving target = null);

        void SayRandomPhrase();

        bool CheckQuestAvailable(GameLiving target, string Name, int goalId = 0)
        {
            return GetTextNPCPolicy(target)?.CheckQuestAvailable(Name, goalId) ?? false;
        }
    }

    public sealed class TextNPCPolicy
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        private long _lastPhrase;
        private readonly GameNPC _body;
        private readonly Dictionary<string, Dictionary<string, string>> _playerResponseKeyMappings = new Dictionary<string, Dictionary<string, string>>();

        public readonly Dictionary<string, DBEchangeur> EchangeurDB = new Dictionary<string, DBEchangeur>();
        public Dictionary<string, string> QuestTexts { get; private set; }
        public Dictionary<string, string> Reponses { get; private set; }
        public Dictionary<string, eEmote> EmoteReponses { get; private set; }
        public Dictionary<string, ushort> SpellReponses { get; private set; }
        public HashSet<string> ResponseTrigger { get; set; }
        public Dictionary<string, bool> SpellReponsesCast { get; private set; }
        public Dictionary<string, string> QuestReponses { get; private set; }
        public Dictionary<string, string> GiveItem { get; private set; }
        public Dictionary<string, Tuple<string, int>> QuestReponsesValues { get; private set; }
        public Dictionary<string, eEmote> RandomPhrases { get; private set; }
        public Dictionary<string, string> StartEventResponses { get; private set; }
        public Dictionary<string, string> StopEventResponses { get; private set; }
        public string QuestReponseKey { get; set; }
        public string Interact_Text { get; set; }
        public int PhraseInterval { get; set; }
        public TextNPCCondition Condition { get; private set; }
        public DBTextNPC TextDB { get; set; }
        public bool? IsOutlawFriendly { get; set; }
        public bool? IsTerritoryLinked { get; set; }
        
        public int RequiredModel { get; set; }

        public Dictionary<string, EchangeurInfo> PlayerReferences;

        public TextNPCPolicy(GameNPC body)
        {
            Condition = new TextNPCCondition("");
            QuestTexts = new Dictionary<string, string>();
            Reponses = new Dictionary<string, string>();
            EmoteReponses = new Dictionary<string, eEmote>();
            SpellReponses = new Dictionary<string, ushort>();
            ResponseTrigger = new HashSet<string>();
            SpellReponsesCast = new Dictionary<string, bool>();
            QuestReponses = new Dictionary<string, string>();
            QuestReponsesValues = new Dictionary<string, Tuple<string, int>>();
            StartEventResponses = new Dictionary<string, string>();
            StopEventResponses = new Dictionary<string, string>();
            _body = body;
            _lastPhrase = 0;
            Interact_Text = "";
            PhraseInterval = 0;
            PlayerReferences = new Dictionary<string, EchangeurInfo>();
        }

        public TextNPCPolicy(GameNPC body, TextNPCPolicy policy)
        {
            Condition = policy.Condition;
            QuestTexts = new Dictionary<string, string>(policy.QuestTexts);
            Reponses = new Dictionary<string, string>(policy.Reponses);
            EmoteReponses = new Dictionary<string, eEmote>(policy.EmoteReponses);
            SpellReponses = new Dictionary<string, ushort>(policy.SpellReponses);
            ResponseTrigger = new HashSet<string>(policy.ResponseTrigger);
            SpellReponsesCast = new Dictionary<string, bool>(policy.SpellReponsesCast);
            QuestReponses = new Dictionary<string, string>(policy.QuestReponses);
            QuestReponsesValues = new Dictionary<string, Tuple<string, int>>(policy.QuestReponsesValues);
            StartEventResponses = new Dictionary<string, string>(policy.StartEventResponses);
            StopEventResponses = new Dictionary<string, string>(policy.StopEventResponses);
            _body = body;
            _lastPhrase = 0;
            Interact_Text = policy.Interact_Text;
            PhraseInterval = policy.PhraseInterval;
            PlayerReferences = new Dictionary<string, EchangeurInfo>();
            SaveIntoDatabase();
        }

        private string TranslateNpcText(GamePlayer player, string originalText)
        {
            if (player == null || string.IsNullOrWhiteSpace(originalText))
                return originalText;

            if (!player.AutoTranslateEnabled || !GS.ServerProperties.Properties.AUTOTRANSLATE_ENABLE)
                return originalText;

            string serverLang = LanguageMgr.DefaultLanguage; // FR on your server
            string playerLang = player.Client?.Account?.Language ?? serverLang;

            if (string.Equals(serverLang, playerLang, StringComparison.OrdinalIgnoreCase))
                return originalText;

            // Prepare / reset mapping for this player.
            if (!_playerResponseKeyMappings.TryGetValue(player.InternalID, out var keyMap))
            {
                keyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _playerResponseKeyMappings[player.InternalID] = keyMap;
            }

            var regex = new Regex(@"\[(.+?)\]", RegexOptions.Compiled);
            int index = 0;

            // 1) Replace [key] with placeholders and build mapping originalKey -> translatedKey.
            const string placeholderPrefix = "§§";
            const string placeholderSuffix = "§§";

            var placeholderToBracket = new Dictionary<string, string>();

            string placeholderText = regex.Replace(originalText, match =>
            {
                string originalKey = match.Groups[1].Value.Trim();

                // §§0§§, §§1§§, etc.
                string placeholder = $"{placeholderPrefix}{index++}{placeholderSuffix}";

                bool isResponseKey =
                    (Reponses != null && Reponses.ContainsKey(originalKey)) ||
                    (QuestReponses != null && QuestReponses.ContainsKey(originalKey)) ||
                    (StartEventResponses != null && StartEventResponses.ContainsKey(originalKey)) ||
                    (StopEventResponses != null && StopEventResponses.ContainsKey(originalKey));

                string translatedKey = originalKey;

                if (isResponseKey)
                {
                    try
                    {
                        var singleTranslated = string.Empty;//;ExternalTranslator.Translate(originalKey, serverLang, playerLang);
                        if (!string.IsNullOrWhiteSpace(singleTranslated))
                            translatedKey = singleTranslated;
                    }
                    catch
                    {
                        translatedKey = originalKey;
                    }

                    // Save mapping translatedKey -> originalKey so whispers work.
                    keyMap[translatedKey] = originalKey;
                }

                placeholderToBracket[placeholder] = "[" + translatedKey + "]";
                return placeholder;
            });

            // 2) Translate the full text with placeholders so Google sees full context
            string translatedFull;
            try
            {
                translatedFull =  string.Empty; // ExternalTranslator.Translate(placeholderText, serverLang, playerLang);
            }
            catch
            {
                return originalText;
            }

            if (string.IsNullOrWhiteSpace(translatedFull))
                return originalText;

            // 3) Replace placeholders with the final [translatedKey] texts
            foreach (var kvp in placeholderToBracket)
            {
                translatedFull = translatedFull.Replace(kvp.Key, kvp.Value);
            }

            if (translatedFull.Contains(placeholderPrefix))
            {
                return originalText;
            }

            return translatedFull;
        }

        private string ResolveResponseKey(GamePlayer player, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return key;

            if ((Reponses != null && Reponses.ContainsKey(key)) ||
                (QuestReponses != null && QuestReponses.ContainsKey(key)) ||
                (StartEventResponses != null && StartEventResponses.ContainsKey(key)) ||
                (StopEventResponses != null && StopEventResponses.ContainsKey(key)))
            {
                return key;
            }

            if (player == null)
                return key;

            if (_playerResponseKeyMappings.TryGetValue(player.InternalID, out var keyMap))
            {
                if (keyMap.TryGetValue(key, out var originalKey))
                    return originalKey;
            }

            return key;
        }

        public void TurnTo(GameLiving living)
        {
            _body.TurnTo(living, 10000);
        }

        public bool Interact(GamePlayer player)
        {
            if (!CanInteractWith(player))
                return false;

            TurnTo(player);

            if (!WillTalkTo(player))
                return false;

            //Message
            if (CheckQuestDialog(player) && QuestReponseKey != null)
            {
                string text = QuestTexts.ContainsKey(QuestReponses[QuestReponseKey])
                    ? QuestTexts[QuestReponses[QuestReponseKey]]
                    : "";

                text = string.Format(text, player.Name, player.LastName, player.GuildName, player.CharacterClass.Name, player.RaceName);

                if (!string.IsNullOrEmpty(text))
                {
                    text = TranslateNpcText(player, text);
                    player.Out.SendMessage(text, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }
            }
            else if (!string.IsNullOrEmpty(Interact_Text))
            {
                string text = string.Format(Interact_Text, player.Name, player.LastName, player.GuildName, player.CharacterClass.Name, player.RaceName);

                if (!string.IsNullOrEmpty(text))
                {
                    text = TranslateNpcText(player, text);
                    player.Out.SendMessage(text, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }
            }

            //Spell
            if (SpellReponses != null && SpellReponses.ContainsKey("INTERACT"))
                foreach (GamePlayer plr in _body.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    plr.Out.SendSpellEffectAnimation(_body, player, SpellReponses["INTERACT"], 0, false, 1);

            //Quest
            if (QuestReponses != null && QuestReponses.ContainsKey("INTERACT"))
                HandleQuestInteraction(player, "INTERACT");

            //Emote
            if (EmoteReponses != null && EmoteReponses.ContainsKey("INTERACT"))
                foreach (GamePlayer plr in _body.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    plr.Out.SendEmoteAnimation(_body, EmoteReponses["INTERACT"]);

            return true;
        }

        public bool WhisperReceive(GameLiving source, string str)
        {
            if (source is not GamePlayer player)
                return false;
            
            if (!CanInteractWith(player))
                return false;

            TurnTo(player);

            if (!WillTalkTo(player))
                return false;

            string normalizedKey = ResolveResponseKey(player, str);

            //Message
            if (Reponses != null && Reponses.ContainsKey(normalizedKey))
            {
                string text = string.Format(Reponses[normalizedKey], player.Name, player.LastName, player.GuildName, player.CharacterClass.Name, player.RaceName);
                if (!string.IsNullOrEmpty(text))
                {
                    text = TranslateNpcText(player, text);
                    player.Out.SendMessage(text, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }
            }

            //Spell
            if (SpellReponses?.TryGetValue(normalizedKey, out var spellId) == true)
            {
                if (SpellReponsesCast.TryGetValue(normalizedKey, out bool castSpell) && castSpell)
                {
                    //cast spell on player
                    SpellLine spellLine = SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells);
                    Spell spell = SkillBase.GetSpellByID(spellId);
                    if (spellLine != null && spell != null)
                    {
                        _body.ApplyAttackRules = false;
                        _body.TargetObject = player;
                        _body.CastSpell(spell, spellLine);
                    }
                }
                else
                {
                    foreach (GamePlayer plr in _body.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                        plr.Out.SendSpellEffectAnimation(_body, player, SpellReponses[normalizedKey], 0, false, 1);
                }
            }

            //Quest
            if (QuestReponses != null && QuestReponseKey != null && QuestReponseKey.LastIndexOf('-') != -1)
            {
                string questStr = QuestReponseKey.Remove(QuestReponseKey.LastIndexOf('-'));
                string compositeKey = questStr + "-" + normalizedKey;

                if (QuestReponses.ContainsKey(compositeKey))
                {
                    HandleQuestInteraction(player, compositeKey);
                    //Trigger
                    if (ResponseTrigger != null && ResponseTrigger.Contains(compositeKey))
                        _body.FireAllResponseTriggers(eAmbientTrigger.interact, player, compositeKey);
                }
            }
            else if (QuestReponses != null && QuestReponses.ContainsKey(normalizedKey))
            {
                HandleQuestInteraction(player, normalizedKey);
                //Trigger
                if (ResponseTrigger != null && ResponseTrigger.Contains(QuestReponses[normalizedKey] + "-" + normalizedKey))
                    _body.FireAllResponseTriggers(eAmbientTrigger.interact, player, QuestReponses[normalizedKey] + "-" + normalizedKey);
            }

            //Emote
            if (EmoteReponses != null && EmoteReponses.ContainsKey(normalizedKey))
                foreach (GamePlayer plr in _body.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    plr.Out.SendEmoteAnimation(_body, EmoteReponses[normalizedKey]);

            //Trigger
            if (ResponseTrigger != null && ResponseTrigger.Contains(normalizedKey))
                _body.FireAllResponseTriggers(eAmbientTrigger.interact, player, normalizedKey);

            //Give Item
            if (GiveItem != null && GiveItem.ContainsKey(normalizedKey))
            {
                //Get item from db
                ItemTemplate item = GameServer.Database.FindObjectByKey<ItemTemplate>(GiveItem[normalizedKey]);
                if (item != null)
                {
                    InventoryItem playerItem = player.Inventory.GetFirstItemByName(item.Name, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                    if (playerItem == null)
                    {
                        InventoryItem itemToGive = GameInventoryItem.Create(item);
                        player.Inventory.AddTemplate(itemToGive, 1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);
                    }
                }
            }

            string eventId;
            //Stop Event
            if (StopEventResponses != null && StopEventResponses.TryGetValue(normalizedKey, out eventId))
            {
                var ev = GameEventManager.Instance.GetEventByID(eventId);

                if (ev != null)
                {
                    lock (ev)
                    {
                        if (ev.StartedTime.HasValue)
                        {
                            GameEventManager.Instance.StopEvent(ev, EndingConditionType.TextNPC, source as GamePlayer);
                        }
                    }
                }
            }
            //Start Event
            if (StartEventResponses != null && StartEventResponses.TryGetValue(normalizedKey, out eventId))
            {
                var ev = GameEventManager.Instance.GetEventByID(eventId);

                if (ev != null)
                {
                    lock (ev)
                    {
                        if (!ev.StartedTime.HasValue)
                        {
                            GameEventManager.Instance.StartEvent(ev, source as GamePlayer);
                        }
                    }
                }
            }

            return true;
        }
        
        void HandleQuestInteraction(GamePlayer player, string response)
        {
            var possibleQuests = _body.QuestIdListToGive;
            var questItem = QuestReponsesValues[response].Item2;
            if (questItem == 0 && !player.QuestList.Any(q => possibleQuests.Contains(q.QuestId)))
            {
                // Quest not in progress
                var questName = QuestReponsesValues[response].Item1;

                foreach (var questId in possibleQuests)
                {
                    if (DataQuestJsonMgr.Quests.TryGetValue(questId, out var quest) && quest?.CheckQuestQualification(player) == true)
                    {
                        player.Out.SendQuestOfferWindow(quest.Npc, player, PlayerQuest.CreateQuestPreview(quest, player));
                        return;
                    }
                }
            }
            else if (questItem != 0)
            {
                // Quest in progress
                var goalId = questItem;
                var currentQuest = player.QuestList.FirstOrDefault(q => q.Quest.Name == QuestReponsesValues[response].Item1 && q.VisibleGoals.Any(g => g is GenericDataQuestGoal jgoal && jgoal.Goal.GoalId == goalId));
                if (currentQuest != null)
                {
                    var currentGoal = currentQuest.VisibleGoals.FirstOrDefault(g => g is GenericDataQuestGoal jgoal && jgoal.Goal.GoalId == goalId);
                    if (currentGoal != null && currentGoal.Status == eQuestGoalStatus.Active)
                    {
                        // finish visible goal
                        if (currentQuest.CanFinish())
                            player.Out.SendQuestRewardWindow(_body, player, currentQuest);
                        else
                        {
                            var jGoal = currentGoal as GenericDataQuestGoal;
                            var goalState = currentQuest.GoalStates.Find(gs => gs.GoalId == goalId);
                            jGoal!.Goal.AdvanceGoal(currentQuest, goalState);
                        }
                    }
                }
                else
                {
                    currentQuest = player.QuestList.FirstOrDefault(q => q.Quest.Name == QuestReponsesValues[response].Item1 && q.Goals.Any(g => g is GenericDataQuestGoal jgoal && jgoal.Goal.GoalId == goalId));
                    if (currentQuest != null)
                    {
                        // start another goal
                        var currentGoal = currentQuest.Goals.FirstOrDefault(g => g is GenericDataQuestGoal jgoal && jgoal.Goal.GoalId == goalId && jgoal.Goal.IsFinished(currentQuest) == false);
                        if (currentGoal != null)
                        {
                            var jGoal = currentGoal as GenericDataQuestGoal;
                            jGoal!.Goal.ForceStartGoal(currentQuest);
                        }
                    }
                }
            }
        }

        public bool CheckQuestDialog(GamePlayer player)
        {
            QuestReponseKey = null;
            if (QuestReponsesValues.Count == 0)
                return false;

            var possibleQuests = _body.QuestIdListToGive;

            foreach (var questReponse in QuestReponsesValues)
            {
                if (questReponse.Value.Item2 == 0)
                {
                    var quest = DataQuestJsonMgr.GetQuest(questReponse.Value.Item1);
                    if (quest != null && quest.CheckQuestQualification(player))
                    {
                        QuestReponseKey = questReponse.Key;
                        return true;
                    }
                }
                else
                {
                    // Quest in progress
                    var goalId = questReponse.Value.Item2;
                    var currentQuest = player.QuestList.FirstOrDefault(q => q.Quest.Name == questReponse.Value.Item1 && q.VisibleGoals.Any(g => g is GenericDataQuestGoal jgoal && jgoal.Goal.GoalId == goalId));
                    if (currentQuest != null)
                    {
                        var currentGoal = currentQuest.VisibleGoals.FirstOrDefault(g => g is GenericDataQuestGoal jgoal && jgoal.Goal.GoalId == goalId);
                        if (currentGoal != null)
                        {
                            QuestReponseKey = questReponse.Key;
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        
        public bool CheckQuestAvailable(string Name, int goalId = 0)
        {
            foreach (var kvp in QuestReponsesValues)
            {
                if (kvp.Value.Item1 == Name)
                {
                    if (goalId == 0 && kvp.Value.Item2 != goalId)
                        return false;
                    if (goalId == 0 || kvp.Value.Item2 == goalId)
                        return true;
                }
            }
            return false;
        }

        public bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            if (source is not GamePlayer player || item == null)
                return false;

            if (!CanInteractWith(player))
                return false;

            TurnTo(player);

            if (!WillTalkTo(player))
                return false;
            
            _body.Notify(GameObjectEvent.ReceiveItem, _body, new ReceiveItemEventArgs(source, _body, item));

            if (!EchangeurDB.TryGetValue(item.Id_nb, out var EchItem))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameLiving.ReceiveItem", _body.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (EchItem.ItemRecvCount > item.Count)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TextNPC.YouMustGive", EchItem.ItemRecvCount, item.Name), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }

            if (EchItem.MoneyPrice > 0 && player.CopperBalance < EchItem.MoneyPrice)
            {
                player.Out.SendMessage(string.Format(LanguageMgr.GetTranslation(player.Client.Account.Language, "TextNPC.YouNeedToExchange", Money.GetString(EchItem.MoneyPrice))), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }

            if (string.IsNullOrEmpty(EchangeurDB[item.Id_nb].QuestName) == false)
            {
                var currentQuest = player.QuestList.FirstOrDefault(q => q.Quest.Name == EchangeurDB[item.Id_nb].QuestName
                    && q.VisibleGoals.Any(g => g is GenericDataQuestGoal jgoal && jgoal.Goal.GoalId == EchangeurDB[item.Id_nb].Step));
                if (currentQuest == null)
                {
                    return false;
                }
            }

            var requireditems = this.GetRequireItems(EchItem);

            if (requireditems.Any())
            {
                var playerItems = this.GetPlayerRequiredItems(player, requireditems, item.Id_nb);
                if (playerItems.HasAllRequiredItems)
                {
                    player.Client.Out.SendCustomDialog(string.Format("Afin de procéder à l'échange, il va falloir payer {0} et me donner en plus {1}",
                       Money.GetString(EchItem.MoneyPrice),
                       string.Join(", ", requireditems.Select(r => string.Format("{0} {1}", r.Count, r.Name)))
                        ), this.HandleClientResponse);
                }
                else
                {
                    player.Client.Out.SendMessage(string.Format("Il va te manquer \n{0}\n pour procéder à l'échange.", string.Join("\n", playerItems.Items.Select(i => string.Format("{0} {1}", i.Value, i.Key)))), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }

                //Handle references for callback
                if (PlayerReferences.ContainsKey(player.InternalID))
                {
                    this.PlayerReferences[player.InternalID] = new EchangeurInfo() { requireInfos = requireditems, GiveItem = item as GameInventoryItem };
                }
                else
                {
                    this.PlayerReferences.Add(player.InternalID, new EchangeurInfo() { requireInfos = requireditems, GiveItem = item as GameInventoryItem });
                }

                return false;
            }
            else
            {
                if (EchItem.MoneyPrice > 0)
                {
                    //Handle references for callback
                    if (PlayerReferences.ContainsKey(player.InternalID))
                    {
                        this.PlayerReferences[player.InternalID] = new EchangeurInfo() { requireInfos = requireditems, GiveItem = item as GameInventoryItem };
                    }
                    else
                    {
                        this.PlayerReferences.Add(player.InternalID, new EchangeurInfo() { requireInfos = requireditems, GiveItem = item as GameInventoryItem });
                    }

                    player.Client.Out.SendCustomDialog(string.Format("J'aurais besoin de {0} pour échanger ça. Valider l'échange ?", Money.GetString(EchItem.MoneyPrice)), this.HandleClientResponse);
                    return false;
                }
                else
                {
                    return ProcessExchange(item, player, EchItem, requireditems);
                }
            }
        }

        public void HandleClientResponse(GamePlayer player, byte response)
        {
            if (response == 1)
            {
                if (this.PlayerReferences.ContainsKey(player.InternalID) && EchangeurDB.ContainsKey(this.PlayerReferences[player.InternalID].GiveItem.Id_nb))
                {
                    var echangeur = EchangeurDB[this.PlayerReferences[player.InternalID].GiveItem.Id_nb];

                    this.ProcessExchange(this.PlayerReferences[player.InternalID].GiveItem, player, echangeur, this.PlayerReferences[player.InternalID].requireInfos);
                }
                else
                {
                    log.Error("Impossible to get player reference from callback in Echangeur from player id" + player.InternalID);
                }
            }

            this.PlayerReferences.Remove(player.InternalID);
        }

        private IEnumerable<RequireItemInfo> GetRequireItems(DBEchangeur ech)
        {
            List<RequireItemInfo> items = new List<RequireItemInfo>();

            var val1 = this.ParseItem(ech.PriceRessource1);

            if (val1 != null)
            {
                items.Add(val1);
            }

            var val2 = this.ParseItem(ech.PriceRessource2);

            if (val2 != null)
            {
                items.Add(val2);
            }

            var val3 = this.ParseItem(ech.PriceRessource3);

            if (val3 != null)
            {
                items.Add(val3);
            }

            return items;
        }

        private RequireItemInfo ParseItem(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            int count = 0;
            var item = raw.Split(new char[] { '|' });

            if (item.Length == 2 && int.TryParse(item[1], out count))
            {
                var itemTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>(item[0]);
                string name = string.Empty;

                if (itemTemplate != null)
                {
                    name = itemTemplate.Name;
                }

                return new RequireItemInfo()
                {
                    ItemId = item[0],
                    Count = count,
                    Name = name
                };
            }

            return null;
        }


        private void RemoveItemsFromPlayer(GamePlayer player, IEnumerable<RequireItemInfo> requireItems, InventoryItem gaveItem)
        {
            List<GameInventoryItem> items = new List<GameInventoryItem>();
            var playerItems = new Dictionary<string, int>();

            foreach (var val in requireItems)
            {
                if (!playerItems.ContainsKey(val.ItemId))
                    playerItems.Add(val.ItemId, 0);
            }

            foreach (GameInventoryItem item in player.Inventory.GetItemRange(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
            {
                var requireItem = requireItems.FirstOrDefault(i => i.ItemId.Equals(item.Id_nb));

                if (requireItem != null)
                {
                    if (item.Id_nb.Equals(gaveItem))
                    {
                        continue;
                    }

                    if (item.Count >= requireItem.Count)
                    {
                        player.Inventory.RemoveCountFromStack(item, requireItem.Count);
                    }
                    else
                    {
                        items.Add(item);
                    }
                    requireItem.Name = item.Name;
                }
            }

            foreach (var item in items)
            {
                if (item.OwnerID == null)
                    item.OwnerID = player.InternalID;

                player.Inventory.RemoveItem(item);
            }
        }

        private EchangeurPlayerItemsCount GetPlayerRequiredItems(GamePlayer player, IEnumerable<RequireItemInfo> requireItems, string gaveItem)
        {
            var playerItems = new Dictionary<string, int>();
            var playerItemsCount = new Dictionary<string, int>();
            bool hasAllRequiredItems = true;

            foreach (var val in requireItems)
            {
                if (!playerItems.ContainsKey(val.ItemId))
                    playerItems.Add(val.ItemId, 0);
            }

            bool hasRemovedgaveItem = false;

            foreach (var item in player.Inventory.GetItemRange(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
            {
                var requireItem = requireItems.FirstOrDefault(i => i.ItemId.Equals(item.Id_nb));

                if (requireItem != null)
                {
                    if (item.Id_nb.Equals(gaveItem) && !hasRemovedgaveItem)
                    {
                        hasRemovedgaveItem = true;
                        continue;
                    }

                    if (item.Count >= requireItem.Count)
                    {
                        playerItems[item.Id_nb] = item.Count;
                    }
                    else
                    {
                        playerItems[item.Id_nb] += item.Count;
                    }

                    requireItem.Name = item.Name;
                }
            }


            foreach (var reqItem in requireItems)
            {
                int missingCount = reqItem.Count - playerItems[reqItem.ItemId];
                if (missingCount > 0)
                {
                    hasAllRequiredItems = false;
                    playerItemsCount.Add(reqItem.Name, missingCount);
                }
            }

            return new EchangeurPlayerItemsCount() { HasAllRequiredItems = hasAllRequiredItems, Items = playerItemsCount };
        }


        private bool ProcessExchange(InventoryItem item, GamePlayer player, DBEchangeur echItem, IEnumerable<RequireItemInfo> requireItems)
        {
            if (!player.Inventory.RemoveCountFromStack(item, echItem.ItemRecvCount))
                return false;
            InventoryLogging.LogInventoryAction(player, _body, eInventoryActionType.Quest, item, echItem.ItemRecvCount);

            if (echItem.GiveTemplate != null)
                if (!player.Inventory.AddTemplate(GameInventoryItem.Create(echItem.GiveTemplate), echItem.ItemGiveCount, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TextNPC.InventoryFullItemGround"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    var invItem = GameInventoryItem.Create(echItem.GiveTemplate);
                    invItem.Count = echItem.ItemGiveCount;
                    player.CreateItemOnTheGround(invItem);
                    InventoryLogging.LogInventoryAction(_body, "", $"(ground;{player.InternalID})", eInventoryActionType.Quest, invItem, echItem.ItemGiveCount);
                }
                else
                    InventoryLogging.LogInventoryAction(_body, player, eInventoryActionType.Quest, echItem.GiveTemplate, echItem.ItemGiveCount);

            if (echItem.GainMoney > 0)
            {
                player.AddMoney(Currency.Copper.Mint(echItem.GainMoney));
                InventoryLogging.LogInventoryAction(_body, player, eInventoryActionType.Quest, echItem.GainMoney);
            }
            if (echItem.GainXP > 0)
                player.GainExperience(GameLiving.eXPSource.Quest, echItem.GainXP);
            else if (echItem.GainXP < 0)
            {
                long xp = (player.ExperienceForNextLevel - player.ExperienceForCurrentLevel) * echItem.GainXP / -1000;
                player.GainExperience(GameLiving.eXPSource.Quest, xp);
            }

            if (echItem.MoneyPrice > 0)
                player.RemoveMoney(Currency.Copper.Mint(echItem.MoneyPrice));
            player.SendSystemMessage(string.Format("Vous avez payé {0}.", Money.GetString(echItem.MoneyPrice)));

            if (requireItems.Any())
                this.RemoveItemsFromPlayer(player, requireItems, item);


            echItem.ChangedItemCount++;
            GameServer.Database.SaveObject(echItem);

            if (Reponses != null && Reponses.ContainsKey(echItem.ItemRecvID))
            {
                string text = string.Format(Reponses[echItem.ItemRecvID], player.Name, player.LastName, player.GuildName, player.CharacterClass.Name, player.RaceName);
                if (text != "")
                    player.Out.SendMessage(text, eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }
            return true;
        }

        /// <summary>
        /// Checks whether the NPC can even be interacted with, that is, whether right clicking will trigger dialogue
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public bool CanInteractWith(GamePlayer player)
        {
            return Condition.CheckAccess(player);
        }

        /// <summary>
        /// Checks whether the NPC will talk to a player if true, or respond with some variance of "I hate you!" to the player if false
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public bool WillTalkTo(GamePlayer player, bool silent = false)
        {
            if (IsTerritoryLinked == true)
            {
                switch (_body.CurrentTerritory?.IsOwnedBy(player))
                {
                    case true:
                        break;
                    
                    case null:
                        log.Warn($"TextNPC {_body.Name} (${_body.InternalID}) has `IsTerritoryLinked = true`, but is not in a territory");
                        goto case false;
                        
                    case false:
                        if (!silent)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TextNPC.NotInOwnedTerritory"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        }
                        return false;
                }
            }

            if (RequiredModel != 0 && player.Model != RequiredModel)
            {
                if (!silent)
                {
                    player.Out.SendMessage("...", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }
                return false;
            }

            if (this.IsOutlawFriendly.HasValue)
            {
                if (this.IsOutlawFriendly.Value)
                {
                    if (player.Reputation >= 0 && player.Client.Account.PrivLevel == 1)
                    {
                        if (!silent)
                        {
                            player.Out.SendMessage("...", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        }
                        return false;
                    }
                }
                else
                {
                    if (player.Reputation < 0 && player.Client.Account.PrivLevel == 1)
                    {
                        if (!silent)
                        {
                            player.Out.SendMessage("...", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        }
                        return false;
                    }
                }
            }

            return true;
        }

        public void SayRandomPhrase()
        {
            if (RandomPhrases == null || RandomPhrases.Count < 1)
                return;
            if (_lastPhrase > DateTime.Now.Ticks / 10000 - PhraseInterval * 1000)
                return;
            _lastPhrase = DateTime.Now.Ticks / 10000;

            if (!Condition.IsActiveAtTick(WorldMgr.GetCurrentGameTime()))
            {
                return;
            }

            int phrase = Util.Random(0, RandomPhrases.Count - 1);
            int i = 0;
            string text = "";
            eEmote emote = 0;
            foreach (var de in RandomPhrases)
            {
                if (i == phrase)
                {
                    text = de.Key;
                    emote = de.Value;
                    break;
                }
                i++;
            }

            //Phrase
            if (text.StartsWith("say:"))
                _body.Say(text.Substring(4));
            else if (text.StartsWith("yell:"))
                _body.Yell(text.Substring(5));
            else if (text.StartsWith("em:"))
            {
                string baseMsg = _body.Name + " " + text.Substring(3);
                foreach (GamePlayer plr in _body.GetPlayersInRadius(WorldMgr.YELL_DISTANCE))
                {
                    string localized = string.Empty; // AutoTranslateManager.MaybeTranslateServerText(plr, baseMsg);
                    plr.Out.SendMessage(localized, eChatType.CT_Emote, eChatLoc.CL_ChatWindow);
                }
            }

            //Emote
            if (emote != 0)
                foreach (GamePlayer plr in _body.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    plr.Out.SendEmoteAnimation(_body, emote);

        }

        public void LoadFromDatabase(DBTextNPC data)
        {
            var objs = GameServer.Database.SelectObjects<DBEchangeur>(e => e.NpcID == data.MobID);
            foreach (DBEchangeur echangeur in objs)
                if (!EchangeurDB.ContainsKey(echangeur.ItemRecvID))
                    EchangeurDB.Add(echangeur.ItemRecvID, echangeur);

            //Chargement des textes
            TextDB = data;
            Interact_Text = TextDB.Text;

            //Set this value only when OR Exclusive
            if (data.IsOutlawFriendly ^ data.IsRegularFriendly)
            {
                if (data.IsRegularFriendly)
                {
                    IsOutlawFriendly = false;
                }

                if (data.IsOutlawFriendly)
                {
                    IsOutlawFriendly = true;
                }
            }
            else if (data.IsOutlawFriendly && data.IsRegularFriendly)
            {
                log.Error("Cannot load IsOutlawFriendly Status because both values are set. Update database (DbTextNPC) for id: " + data.ObjectId + " npc: " + data.MobName);
            }

            IsTerritoryLinked = data.IsTerritoryLinked;

            QuestTexts = new Dictionary<string, string>();
            if (TextDB.QuestTexts != null && TextDB.QuestTexts != "")
            {
                foreach (string item in TextDB.QuestTexts.Split(';'))
                {
                    string[] items = item.Split('|');
                    if (items.Length != 2)
                        continue;
                    QuestTexts.Add(items[0], items[1]);
                }
            }

            var table = new Dictionary<string, string>();
            if (TextDB.Reponse != "")
            {
                foreach (string item in TextDB.Reponse.Split(';'))
                {
                    string[] items = item.Split('|');
                    if (items.Length != 2)
                        continue;
                    table.Add(items[0], items[1]);
                }
            }
            Reponses = table;

            GiveItem = new Dictionary<string, string>();
            if (TextDB.GiveItem != null && TextDB.GiveItem != "")
            {
                TextDB.GiveItem.Replace("\r", "\n");
                foreach (string item in TextDB.GiveItem.Split('\n'))
                {
                    string[] items = item.Split('|');
                    if (items.Length != 2)
                        continue;
                    GiveItem.Add(items[0], items[1]);
                }
            }

            StartEventResponses = new Dictionary<string, string>();
            if (TextDB.ResponseStartEvent != null && TextDB.ResponseStartEvent != "")
            {
                TextDB.ResponseStartEvent.Replace("\r", "\n");
                foreach (string eventResponse in TextDB.ResponseStartEvent.Split('\n'))
                {
                    string[] ev = eventResponse.Split('|');
                    if (ev.Length != 2)
                        continue;
                    StartEventResponses.Add(ev[0], ev[1]);
                }
            }

            StopEventResponses = new Dictionary<string, string>();
            if (TextDB.ResponseStopEvent != null && TextDB.ResponseStopEvent != "")
            {
                TextDB.ResponseStopEvent.Replace("\r", "\n");
                foreach (string eventResponse in TextDB.ResponseStopEvent.Split('\n'))
                {
                    string[] ev = eventResponse.Split('|');
                    if (ev.Length != 2)
                        continue;
                    StopEventResponses.Add(ev[0], ev[1]);
                }
            }

            QuestReponses = new Dictionary<string, string>();
            QuestReponsesValues = new Dictionary<string, Tuple<string, int>>();
            if (TextDB.ReponseQuest != null && TextDB.ReponseQuest != "")
            {
                TextDB.ReponseQuest.Replace("\r", "\n");
                foreach (string item in TextDB.ReponseQuest.Split('\n'))
                {
                    string[] items = item.Split('|');
                    if (items.Length != 2)
                        continue;
                    QuestReponses.Add(items[0], items[1]);
                    var values = items[1].Split('-');
                    if (values.Length != 2)
                        QuestReponsesValues.Add(items[0], new Tuple<string, int>(values[0], 0));
                    else
                        QuestReponsesValues.Add(items[0], new Tuple<string, int>(values[0], int.Parse(values[1])));
                }
            }

            //Chargement des spells réponses
            var table2 = new Dictionary<string, ushort>();
            if (TextDB.ReponseSpell != "")
            {
                TextDB.ReponseSpell.Replace("\r", "\n");
                foreach (string item in TextDB.ReponseSpell.Split('\n'))
                {
                    string[] items = item.Split('|');
                    if (items.Length != 2 && items.Length != 3)
                        continue;
                    try
                    {
                        table2.Add(items[0], ushort.Parse(items[1]));
                        if (items.Length == 3)
                            SpellReponsesCast.Add(items[0], bool.Parse(items[2]));
                    }
                    catch { }
                }
            }
            SpellReponses = table2;

            //Chargement des trigger réponses
            ResponseTrigger = new HashSet<string>();
            var ambientTexts = GameServer.Instance.NpcManager.AmbientBehaviour[_body.Name];
            if (ambientTexts != null)
            {
                foreach (string responseTrigger in
                         ambientTexts.Where(i => !string.IsNullOrEmpty(i.ResponseTrigger)).Select(i => i.ResponseTrigger))
                {
                    ResponseTrigger.Add(responseTrigger);
                }
            }

            //Chargement des emotes réponses
            var table3 = new Dictionary<string, eEmote>();
            if (TextDB.ReponseEmote != "")
            {
                TextDB.ReponseEmote.Replace("\r", "\n");
                foreach (string item in TextDB.ReponseEmote.Split('\n'))
                {
                    string[] items = item.Split('|');
                    if (items.Length != 2)
                        continue;
                    try
                    {
                        table3.Add(items[0], (eEmote)Enum.Parse(typeof(eEmote), items[1], false));
                    }
                    catch { }
                }
            }
            EmoteReponses = table3;

            //phrase/emote aléatoire
            var table4 = new Dictionary<string, eEmote>();
            if (TextDB.RandomPhraseEmote != "")
            {
                TextDB.RandomPhraseEmote.Replace("\r", "\n");
                foreach (string item in TextDB.RandomPhraseEmote.Split('\n'))
                {
                    string[] items = item.Split('|');
                    if (items.Length != 2)
                        continue;
                    try
                    {
                        if (items[1] == "0")
                            table4.Add(items[0], 0);
                        else
                            table4.Add(items[0], (eEmote)Enum.Parse(typeof(eEmote), items[1], false));
                    }
                    catch (Exception e)
                    {
                        log.Error("ERROR :", e);
                    }
                }
            }
            RandomPhrases = table4;
            PhraseInterval = TextDB.PhraseInterval;

            RequiredModel = TextDB.RequiredModel;


            //Chargement des conditions
            Condition = new TextNPCCondition(TextDB.Condition);
        }

        public void SaveIntoDatabase()
        {
            if (TextDB == null)
                TextDB = new DBTextNPC();
            TextDB.MobID = _body.InternalID;
            TextDB.MobName = _body.Name;
            TextDB.MobRealm = (byte)_body.Realm;
            TextDB.Text = Interact_Text;

            if (IsOutlawFriendly != null)
            {
                TextDB.IsOutlawFriendly = (bool)IsOutlawFriendly;
                TextDB.IsRegularFriendly = !(bool)IsOutlawFriendly;
            }
            else
            {
                TextDB.IsOutlawFriendly = false;
                TextDB.IsRegularFriendly = false;
            }

            TextDB.IsTerritoryLinked = IsTerritoryLinked ?? false;

            //Sauve quest texts
            string questTexts = "";
            if (QuestTexts != null && QuestTexts.Count > 0)
            {
                foreach (var de in QuestTexts)
                {
                    if (questTexts.Length > 1)
                        questTexts += ";";
                    questTexts += de.Key.Trim('|', ';') + "|" + de.Value.Trim('|', ';');
                }
            }
            TextDB.QuestTexts = questTexts;

            //Sauve les réponses
            string reponse = "";
            if (Reponses != null && Reponses.Count > 0)
            {
                foreach (var de in Reponses)
                {
                    if (reponse.Length > 1)
                        reponse += ";";
                    reponse += de.Key.Trim('|', ';') + "|" + de.Value.Trim('|', ';');
                }
            }
            TextDB.Reponse = reponse;

            //Sauve les quest réponses
            reponse = "";
            if (QuestReponses != null && QuestReponses.Count > 0)
            {
                foreach (var de in QuestReponses)
                {
                    if (reponse.Length > 1)
                        reponse += "\n";
                    reponse += de.Key.Trim('|', ';') + "|" + de.Value;
                }
            }
            TextDB.ReponseQuest = reponse;

            //Sauve les spell réponses
            reponse = "";
            if (SpellReponses != null && SpellReponses.Count > 0)
            {
                foreach (var de in SpellReponses)
                {
                    if (reponse.Length > 1)
                        reponse += "\n";
                    reponse += de.Key.Trim('|', ';') + "|" + de.Value;

                    if (SpellReponsesCast.ContainsKey(de.Key))
                        reponse += "|" + SpellReponsesCast[de.Key];
                }
            }
            TextDB.ReponseSpell = reponse;

            //Sauve les emote réponses
            reponse = "";
            if (EmoteReponses != null && EmoteReponses.Count > 0)
            {
                foreach (var de in EmoteReponses)
                {
                    if (reponse.Length > 1)
                        reponse += "\n";
                    reponse += de.Key.Trim('|', ';') + "|" + de.Value;
                }
            }
            TextDB.ReponseEmote = reponse;

            //Sauve les phrase/emote aléatoire
            reponse = "";
            if (RandomPhrases != null && RandomPhrases.Count > 0)
            {
                foreach (var de in RandomPhrases)
                {
                    if (reponse.Length > 1)
                        reponse += "\n";
                    reponse += de.Key.Trim('|', '\n') + "|" + de.Value;
                }
            }
            TextDB.RandomPhraseEmote = reponse;
            TextDB.PhraseInterval = PhraseInterval;

            reponse = "";
            if (GiveItem != null)
            {
                foreach (KeyValuePair<string, string> entry in GiveItem)
                {
                    if (reponse.Length > 1)
                        reponse += "\n";
                    reponse += entry.Key + "|" + entry.Value;
                }
            }
            TextDB.GiveItem = reponse;

            reponse = "";
            if (StartEventResponses != null)
            {
                foreach (KeyValuePair<string, string> entry in StartEventResponses)
                {
                    if (reponse.Length > 1)
                        reponse += "\n";
                    reponse += entry.Key + "|" + entry.Value;
                }
            }
            TextDB.ResponseStartEvent = reponse;

            reponse = "";
            if (StopEventResponses != null)
            {
                foreach (KeyValuePair<string, string> entry in StopEventResponses)
                {
                    if (reponse.Length > 1)
                        reponse += "\n";
                    reponse += entry.Key + "|" + entry.Value;
                }
            }
            TextDB.ResponseStopEvent = reponse;

            TextDB.RequiredModel = RequiredModel;

            //Sauve les conditions
            if (Condition != null)
                TextDB.Condition = Condition.GetConditionString();

            var data = GameServer.Database.FindObjectByKey<DBTextNPC>(TextDB.ObjectId);
            if (data == null)
            {
                GameServer.Database.AddObject(TextDB);
            }
            else
            {
                TextDB.ObjectId = data.ObjectId;
                GameServer.Database.SaveObject(TextDB);
            }

            foreach (KeyValuePair<string, DBEchangeur> pair in EchangeurDB)
            {
                pair.Value.NpcID = _body.InternalID;
                if (pair.Value.IsPersisted)
                    GameServer.Database.SaveObject(pair.Value);
                else
                    GameServer.Database.AddObject(pair.Value);
            }
        }

        public void DeleteFromDatabase()
        {
            if (TextDB != null && TextDB.IsPersisted)
                GameServer.Database.DeleteObject(TextDB);
            foreach (KeyValuePair<string, DBEchangeur> pair in EchangeurDB)
                if (pair.Value.IsPersisted)
                    GameServer.Database.DeleteObject(pair.Value);
        }

        public IList<string> DelveInfo()
        {
            List<string> text = new List<string>
                {
                    " + OID: " + _body.ObjectID,
                    " + Class: " + _body.GetType(),
                    " + Position: " + _body.Position + " Heading=" + _body.Heading,
                    " + Realm: " + _body.Realm,
                    " + Model: " + _body.Model,
                    "",
                    "-- Echangeur (Items) --"
                };
            foreach (KeyValuePair<string, DBEchangeur> pair in EchangeurDB)
            {
                text.Add(" - " + pair.Value.ItemRecvID + " (" + pair.Value.ItemRecvCount + "):");
                if (pair.Value.ItemGiveCount > 0)
                    text.Add(" . " + pair.Value.ItemGiveCount + " " + pair.Value.ItemGiveID);
                if (pair.Value.GainMoney > 0)
                    text.Add(" . " + Money.GetString(pair.Value.GainMoney));
                if (pair.Value.GainXP > 0)
                    text.Add(" . " + pair.Value.GainXP + "xp");
                if (pair.Value.GainXP < 0)
                    text.Add(" . " + (-pair.Value.GainXP) + "/1000 du niveau en cours");
                if (Reponses.ContainsKey(pair.Value.ItemRecvID))
                    text.Add(" . Réponse: " + Reponses[pair.Value.ItemRecvID]);
                text.Add(" . " + pair.Value.ChangedItemCount + " Items échangés");
            }
            return text;
        }
    }
}

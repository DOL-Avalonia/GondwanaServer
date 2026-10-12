using DOL.Database;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using DOL.Language;
using System.Linq;
using System;
using System.Collections.Generic;
using static AmteScripts.Managers.PvpManager;

namespace AmteScripts.PvP.Modes
{
    public sealed class BiohazardMode : PvPGameModeBase
    {
        public override eSessionTypes Type => eSessionTypes.Biohazard;
        public override int KillBasePoints => 5;
        public override bool KillHasRr5Bonus => true;
        private const int INTERVAL = 4000;
        private readonly List<GameNPC> _spawns = new();
        private readonly List<BiohazardChest> _chests = new();
        private readonly List<InventoryItem> _updated = new(4);
        private int _ticks; private long _nextAt;
        private Spell _disease; private SpellLine _mobLine;

        protected override void OnOpen()
        {
            _spawns.AddRange(Ctx.FindNpcs(static n => n.Realm == eRealm.None && n.GuildName == "PVP" && n.Name.IndexOf("SAMPLE_SPAWN", StringComparison.OrdinalIgnoreCase) >= 0));
            if (_spawns.Count == 0) log.Warn("[Biohazard] No 'SAMPLE_SPAWN' NPCs found in the session's zones!");
            _disease = SkillBase.GetSpellByID(25320); _mobLine = SkillBase.GetSpellLine(GlobalSpellsLines.Mob_Spells);
            RotateChests();
            _nextAt = GameLoop.GameLoopTime + INTERVAL;
        }

        protected override void OnClose() { foreach (BiohazardChest c in _chests) c.Delete(); _chests.Clear(); _spawns.Clear(); }

        public override int Tick(long now)
        {
            if (now < _nextAt) return Until(_nextAt, now);
            _nextAt = now + INTERVAL;
            _ticks++;
            bool applyDisease = _ticks % 2 == 0;
            if (_ticks % 45 == 0) RotateChests();
            foreach (GamePlayer p in Ctx.Participants.Players)
            {
                if (!p.IsAlive || p.CurrentRegion == null) continue;
                if (p.IsDamned && !p.IsRadioactiveAreaActive)
                {
                    p.TakeDamage(null, eDamageType.Natural, (int)(p.MaxHealth * 0.10), 0);
                    if (p.IsAlive) { p.Out.SendMessage(LanguageMgr.GetTranslation(p.Language, "PvPManager.Biohazard.FleshRots"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow); p.Out.SendSpellEffectAnimation(p, p, 13153, 0, false, 1); }
                }
                _updated.Clear(); bool hasSample = false;
                for (eInventorySlot slot = eInventorySlot.FirstBackpack; slot <= eInventorySlot.LastBackpack; slot++)
                {
                    if (p.Inventory.GetItem(slot) is not PvPTreasure t || string.IsNullOrEmpty(t.Id_nb) || t.Id_nb.IndexOf("toxic_sample", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    hasSample = true;
                    if (t.Condition > 80) { t.Condition -= 1; t.Dirty = true; _updated.Add(t); }
                }
                if (_updated.Count > 0) p.Out.SendInventoryItemsUpdate(_updated);
                if (!hasSample) continue;
                p.Out.SendSpellEffectAnimation(p, p, 14326, 0, false, 1);
                if (applyDisease && _disease != null)
                {
                    ScriptMgr.CreateSpellHandler(p, _disease, _mobLine)?.StartSpell(p);
                    p.Out.SendMessage(LanguageMgr.GetTranslation(p.Language, "PvPManager.Biohazard.SampleLeaks"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                }
            }
            return INTERVAL;
        }

        private void PruneChests() => _chests.RemoveAll(c => c.ObjectState != GameObject.eObjectState.Active);

        private bool IsOccupied(GameNPC spawn)
        {
            foreach (BiohazardChest c in _chests) if (c.CurrentRegionID == spawn.CurrentRegionID && c.Coordinate.DistanceTo(spawn.Coordinate) < 150) return true;
            return false;
        }

        private void SpawnChestAt(GameNPC spawn)
        {
            bool pure = Util.Chance(30);
            BiohazardChest chest = new() { Name = "Toxic Sample", Model = pure ? (ushort)4170 : (ushort)4171, CurrentRegionID = spawn.CurrentRegionID, Position = spawn.Position, ItemChance = 100, CoffreOpeningInterval = 0, IsOpenableOnce = false, RespawnInterval = 0, HasPickableAnim = true };
            chest.Items.Add(new GameCoffre.CoffreItem(pure ? "pvp_pure_toxic_sample" : "pvp_raw_toxic_sample", 100));
            if (chest.AddToWorld()) _chests.Add(chest);
        }

        public void RespawnSingleChest(BiohazardChest old)
        {
            if (!IsActive) return;
            _chests.Remove(old); PruneChests();
            List<GameNPC> avail = _spawns.Where(s => !IsOccupied(s) && s.Position.Coordinate.DistanceTo(old.Position) > 150).ToList();
            if (avail.Count == 0) avail = _spawns.Where(s => !IsOccupied(s)).ToList();
            if (avail.Count == 0) { log.Warn("[Biohazard] No empty spawn points available for respawn!"); return; }
            SpawnChestAt(avail[Util.Random(avail.Count - 1)]);
        }

        private void RotateChests()
        {
            foreach (BiohazardChest c in _chests) c.Delete();
            _chests.Clear();
            if (_spawns.Count == 0) return;
            List<GameNPC> shuffled = _spawns.OrderBy(_ => Util.Random(1000)).ToList();
            int count = Math.Clamp((int)Math.Ceiling(shuffled.Count / 3.0), 1, shuffled.Count);
            for (int i = 0; i < count; i++) SpawnChestAt(shuffled[i]);
            Ctx.Broadcast("PvPManager.Biohazard.NewSamples", eChatType.CT_Important);
        }
    }
}
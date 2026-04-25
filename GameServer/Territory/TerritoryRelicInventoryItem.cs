using DOL.Database;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.Language;
using System;

namespace AmteScripts.Managers
{
    public class TerritoryRelicInventoryItem : GameInventoryItem
    {
        public TerritoryRelicStatic RelicReference { get; set; }
        public bool IsRemovalExpected { get; set; } = false;
        private bool _isSilencing = false;
        private RegionTimer _stateCheckTimer;
        private GameSpellEffect _relicEffect;

        public bool IsSilencing
        {
            get => _isSilencing;
            set
            {
                if (!value && _isSilencing && m_owner != null)
                {
                    m_owner.IsSilenced = false;
                    m_owner.IsDisarmed = false;
                }
                else if (value && !_isSilencing && m_owner != null)
                {
                    m_owner.IsSilenced = true;
                    m_owner.IsDisarmed = true;
                }
                _isSilencing = value;
            }
        }

        public TerritoryRelicInventoryItem() : base() { }
        public TerritoryRelicInventoryItem(ItemTemplate template) : base(template) { }
        public TerritoryRelicInventoryItem(TerritoryRelicStatic relic) : base(CreateTemplate(relic))
        {
            RelicReference = relic;
        }

        private static ItemTemplate CreateTemplate(TerritoryRelicStatic relic)
        {
            return new ItemTemplate
            {
                Name = relic.Name,
                Id_nb = "TerritoryRelic_" + relic.DbRecord.RelicID,
                Level = 50,
                Model = relic.Model,
                IsDropable = false,
                IsPickable = true,
                IsTradable = false,
                Weight = 100,
                Object_Type = (int)eObjectType.GenericItem,
                ClassType = typeof(TerritoryRelicInventoryItem).FullName
            };
        }

        public override bool CanPersist => false;

        public override void OnReceive(GamePlayer player)
        {
            base.OnReceive(player);
            IsSilencing = true;

            if (RelicReference != null && RelicReference.DbRecord.relicSpell > 0)
            {
                var spell = SkillBase.GetSpellByID(RelicReference.DbRecord.relicSpell);
                if (spell != null)
                {
                    var handler = ScriptMgr.CreateSpellHandler(player, spell, SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells));
                    if (handler != null)
                    {
                        _relicEffect = new GameSpellEffect(handler, spell.Duration, 0);
                        _relicEffect.Start(player);
                    }
                }
            }

            if (RelicReference != null && RelicReference.DbRecord.Effect > 0)
            {
                player.Out.SendMinotaurRelicWindow(player, RelicReference.DbRecord.Effect, true);
            }

            _stateCheckTimer = new RegionTimer(player, new RegionTimerCallback(StateCheckTimerCallback));
            _stateCheckTimer.Start(800);
        }

        private int StateCheckTimerCallback(RegionTimer timer)
        {
            var player = m_owner as GamePlayer;
            if (player == null || !player.IsAlive) return 0;

            var wsd = SpellHandler.FindEffectOnTarget(player, "WarlockSpeedDecrease");
            if (player.IsDamned || SpellHandler.FindEffectOnTarget(player, "Petrify") != null || (wsd != null && wsd.Spell?.AmnesiaChance == 1))
            {
                player.DropTerritoryRelicsOnDeath(null);
                return 0; // stop timer
            }
            return 800;
        }

        public override void OnLose(GamePlayer player)
        {
            IsSilencing = false;

            if (_relicEffect != null)
            {
                _relicEffect.Cancel(false);
                _relicEffect = null;
            }

            player.Out.SendMinotaurRelicWindow(player, 0, false);

            base.OnLose(player);

            if (!IsRemovalExpected && RelicReference != null)
            {
                // Player died, quit, or teleported incorrectly. Drop on ground!
                RelicReference.DropOnGround(player.Position.X, player.Position.Y, player.Position.Z, player.Heading, player.CurrentRegion);
            }
            IsRemovalExpected = false;
        }

        public override bool Use(GamePlayer player) => false;
    }
}
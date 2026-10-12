using System.Reflection;
using DOL.GS.PacketHandler;
using DOL.Language;
using DOL.Numbers;
using log4net;

namespace DOL.GS;

public class CraftAction : ECSGameTimerWrapperBase
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

    public static readonly int TICK_RATE = 50;

    private readonly GamePlayer m_player;
    private readonly Recipe m_recipe;
    private int m_craftingTime;
    private bool m_finished;
    private bool m_cleanedUp;

    public Recipe CraftedRecipe => m_recipe;
    public int RecipeTime { get; }
    public AbstractCraftingSkill CraftingSkill { get; }

    public CraftAction(GamePlayer player, AbstractCraftingSkill skill, Recipe craftedRecipe, int craftingTime) : base(player)
    {
        m_player = player;
        m_recipe = craftedRecipe;
        RecipeTime = craftingTime * 1000;
        CraftingSkill = skill;
        Start(TICK_RATE);
    }

    /// <summary>
    /// External cancel (GamePlayer.StopCrafting, interrupts).
    /// Needs ECSGameTimer.Stop() virtual.
    /// </summary>
    public override void Stop()
    {
        Cleanup();
        base.Stop();
    }

    private void Cleanup()
    {
        if (m_cleanedUp) return;
        m_cleanedUp = true;
        m_player.Out.SendCloseTimerWindow();
        m_player.TempProperties.removeProperty("CraftQueueRemaining");
    }

    private int Finish()
    {
        Cleanup();
        return 0;
    }

    protected override int OnTick(ECSGameTimer timer)
    {
        if (m_player.ObjectState != GameObject.eObjectState.Active || m_player.IsMezzed || m_player.IsStunned)
            return Finish();

        if (m_player.CurrentSpellHandler?.Spell.Uninterruptible == false)
            return Finish();

        if (m_player.IsMoving)
        {
            m_player.Out.SendMessage(LanguageMgr.GetTranslation(m_player.Client.Account.Language, "AbstractCraftingSkill.CraftItem.MoveAndInterrupt"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return Finish();
        }

        m_craftingTime += TICK_RATE;
        if (m_craftingTime > RecipeTime)
        {
            MakeItem();
            if (m_finished) return Finish();
        }
        return TICK_RATE;
    }

    protected virtual void MakeItem()
    {
        GamePlayer player = m_player;
        if (m_recipe == null)
        {
            player.Out.SendMessage("Could not find recipe or item to craft!", eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            log.Error("Crafting.MakeItem: Could not retrieve recipe from CraftAction.");
            m_finished = true;
            return;
        }

        int queue = player.TempProperties.getProperty<int>("CraftQueueLength");
        int remainingToCraft = player.TempProperties.getProperty<int>("CraftQueueRemaining");
        if (queue > 1 && remainingToCraft == 0) remainingToCraft = queue;

        if (Util.Chance(CraftingSkill.CalculateChanceToMakeItem(player, m_recipe.Level)))
        {
            if (!CraftingSkill.RemoveUsedMaterials(player, m_recipe))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AbstractCraftingSkill.MakeItem.NotAllMaterials"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                if (player.Client.Account.PrivLevel == 1) { m_finished = true; return; }
            }
            CraftingSkill.BuildCraftedItem(player, m_recipe);
            CraftingSkill.GainCraftingSkillPoints(player, m_recipe);
        }
        else
        {
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "AbstractCraftingSkill.MakeItem.LoseNoMaterials", m_recipe.Product.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            player.Out.SendPlaySound(eSoundType.Craft, 0x02);
        }

        if (remainingToCraft > 1 && CraftingSkill.CheckRawMaterials(player, m_recipe))
        {
            player.TempProperties.setProperty("CraftQueueRemaining", remainingToCraft - 1);
            m_craftingTime = 0;
            player.Out.SendTimerWindow(LanguageMgr.GetTranslation(player.Client.Account.Language, "AbstractCraftingSkill.CraftItem.CurrentlyMaking", m_recipe.Product.Name), CraftingSkill.GetCraftingTime(player, m_recipe));
            m_finished = false;
        }
        else
        {
            player.TempProperties.removeProperty("CraftQueueRemaining");
            m_finished = true;
        }
    }
}
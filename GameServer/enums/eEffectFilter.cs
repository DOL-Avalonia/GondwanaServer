
namespace DOL.GS.PacketHandler
{
    /// <summary>
    /// Spell-effect display setting sent by the client with the chat filter packet.
    /// Only consumed by SendSpellEffectAnimation for now; ECS-Effects will reuse it later.
    /// </summary>
    public enum eEffectFilter : byte
    {
        All = 0,
        Self = 1,
        Group = 2,
        Others = 3,
        None = 4,
    }
}
using MoreMountains.Tools;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Fired by EnemyXPReward when an enemy dies, picked up by PlayerXP on the player.
    /// Follows the same pattern as TopDownEnginePointEvent.
    /// </summary>
    public struct XPGainEvent
    {
        public int XPAmount;

        static XPGainEvent e;

        public static void Trigger(int xpAmount)
        {
            e.XPAmount = xpAmount;
            MMEventManager.TriggerEvent(e);
        }
    }
}

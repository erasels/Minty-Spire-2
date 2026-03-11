namespace MintySpire2.MintySpire2.src;

public static class IncomingDamageDisplayPolicy
{
    public static T ResolveLabelHost<T>(T? preferredHost, T fallbackHost) where T : class
    {
        return preferredHost ?? fallbackHost;
    }

    public static bool ShouldHideLabel(
        bool barVisible,
        bool hasCombatManager,
        bool isEnemyTurnStarted,
        bool isPlayerOwnedBar,
        bool hasCombatState,
        bool hasHittableEnemies)
    {
        if (!barVisible || !hasCombatManager || isEnemyTurnStarted)
            return true;

        if (!isPlayerOwnedBar || !hasCombatState || !hasHittableEnemies)
            return true;

        return false;
    }
}

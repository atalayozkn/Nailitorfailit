using ItemScript;

namespace Interactions
{
    // What the look-at panel shows for an interactable. A default value means "show nothing".
    public readonly struct LookAtInfo
    {
        public readonly string Name;
        public readonly string Action;
        public readonly bool IsBlocked;

        private LookAtInfo(string name, string action, bool isBlocked)
        {
            Name = name;
            Action = action;
            IsBlocked = isBlocked;
        }

        public static LookAtInfo Ready(string name, string action) => new LookAtInfo(name, action, false);
        public static LookAtInfo Blocked(string name, string reason) => new LookAtInfo(name, reason, true);
    }

    public interface ILookAtInfo
    {
        // Called repeatedly while targeted, so it must not trigger warnings, events or effects.
        // carried is what the player would hold when the interaction runs, null if empty-handed.
        LookAtInfo GetLookAtInfo(CarriableObject_SP carried);
    }
}

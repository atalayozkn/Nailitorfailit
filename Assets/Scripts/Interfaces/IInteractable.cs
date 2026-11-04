namespace Interactions
{
    public enum InteractableType
    {
        Grabbable,
        Constructor,
        Station,
        Shop,
    }

    public interface IInteractable
    {
        InteractableType InteractableType { get; }

        // Returns true when the interaction actually did something. The player only plays the interact animation then.
        bool OnInteract();

        void OnHoverOn();

        void OnHoverOff();
    }
}
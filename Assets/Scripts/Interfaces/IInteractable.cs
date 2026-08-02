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

        void OnInteract();

        void OnHoverOn();

        void OnHoverOff();
    }
}
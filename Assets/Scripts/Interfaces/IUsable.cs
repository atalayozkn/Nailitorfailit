namespace Interactions
{
    public interface IUsable
    {
        void OnUse();
        UseType UseType { get; }
    }
}
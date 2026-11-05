// What BlueprintVisual reads from a placed tile to reveal it as it gets nailed down.
public interface IBuildableTile
{
    float BuildProgress { get; }
    bool IsBuildComplete { get; }
}

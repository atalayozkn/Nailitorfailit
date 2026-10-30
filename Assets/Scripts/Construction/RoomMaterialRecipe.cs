using ItemScript;
using UnityEngine;

// Decides which material every floor tile of a room needs. Lets one room prefab be reused with
// different difficulty per level (e.g. L1 bathroom Checker Wood/Brick, L3 bathroom Border Glass).
public class RoomMaterialRecipe : MonoBehaviour
{
    public enum Pattern
    {
        AllPrimary,
        Checker,        // alternating primary / secondary
        HalfSplit,      // first half of the rows primary, rest secondary
        Border,         // outer ring secondary, inside primary
        Random          // secondary chance = secondaryRatio, stable per seed
    }

    [Header("Recipe")]
    [SerializeField] private Pattern pattern = Pattern.AllPrimary;
    [SerializeField] private CarriableType primary = CarriableType.Wood;
    [SerializeField] private CarriableType secondary = CarriableType.Brick;
    [Range(0f, 1f)][SerializeField] private float secondaryRatio = 0.3f;
    [SerializeField] private int seed = 0;

    [Header("Room Size (tiles)")]
    [SerializeField] private int width = 3;
    [SerializeField] private int depth = 3;

    private void Awake()
    {
        Apply();
    }

    public void Apply()
    {
        foreach (var constructor in GetComponentsInChildren<Constructor>(true))
        {
            constructor.SetAcceptedType(GetTypeFor(constructor.transform));
        }
    }

    public void Setup(Pattern pattern, CarriableType primary, CarriableType secondary, float secondaryRatio = 0.3f, int seed = 0)
    {
        this.pattern = pattern;
        this.primary = primary;
        this.secondary = secondary;
        this.secondaryRatio = secondaryRatio;
        this.seed = seed;
    }

    public void SetSize(int width, int depth)
    {
        this.width = width;
        this.depth = depth;
    }

    private CarriableType GetTypeFor(Transform tile)
    {
        // Tiles are laid out on a 1m grid centered on the room root.
        Vector3 local = transform.InverseTransformPoint(tile.position);
        int col = Mathf.Clamp(Mathf.RoundToInt(local.x + (width - 1) * 0.5f), 0, width - 1);
        int row = Mathf.Clamp(Mathf.RoundToInt(local.z + (depth - 1) * 0.5f), 0, depth - 1);

        bool useSecondary = false;
        switch (pattern)
        {
            case Pattern.Checker:
                useSecondary = (col + row) % 2 == 1;
                break;
            case Pattern.HalfSplit:
                useSecondary = row >= depth / 2;
                break;
            case Pattern.Border:
                useSecondary = col == 0 || row == 0 || col == width - 1 || row == depth - 1;
                break;
            case Pattern.Random:
                var rng = new System.Random(seed * 7919 + row * 131 + col);
                useSecondary = rng.NextDouble() < secondaryRatio;
                break;
        }
        return useSecondary ? secondary : primary;
    }

    private void OnDrawGizmos()
    {
        // Preview of the recipe in the Scene view (brown wood, red brick, cyan glass).
        foreach (var constructor in GetComponentsInChildren<Constructor>(true))
        {
            switch (GetTypeFor(constructor.transform))
            {
                case CarriableType.Wood: Gizmos.color = new Color(0.6f, 0.4f, 0.2f, 0.8f); break;
                case CarriableType.Brick: Gizmos.color = new Color(0.8f, 0.25f, 0.2f, 0.8f); break;
                case CarriableType.Glass: Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f); break;
                default: Gizmos.color = Color.magenta; break;
            }
            Gizmos.DrawCube(constructor.transform.position + Vector3.up * 0.15f, new Vector3(0.8f, 0.05f, 0.8f));
        }
    }
}

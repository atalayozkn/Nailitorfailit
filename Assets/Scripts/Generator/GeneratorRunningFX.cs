using UnityEngine;

// Generator shakes and puffs smoke while it has power, stops when it runs dry.
// Works with either generator: Generator_SP (standalone prefab) or Generator_Prototype (inside the stations).
public class GeneratorRunningFX : MonoBehaviour
{
    [SerializeField] private Generator_SP generator;
    [SerializeField] private Generator_Prototype prototypeGenerator;
    [SerializeField] private VisualShaker shaker;
    [SerializeField] private ParticleSystem smoke;

    private void Awake()
    {
        if (generator == null) generator = GetComponentInParent<Generator_SP>();
        if (generator == null && prototypeGenerator == null)
            prototypeGenerator = GetComponentInParent<Generator_Prototype>();
    }

    private void OnEnable()
    {
        if (generator != null) generator.OnPowerStateChanged += Apply;
        else if (prototypeGenerator != null) prototypeGenerator.OnPowerStateChanged += Apply;
    }

    private void OnDisable()
    {
        if (generator != null) generator.OnPowerStateChanged -= Apply;
        else if (prototypeGenerator != null) prototypeGenerator.OnPowerStateChanged -= Apply;
    }

    // Energy is set in the generator's Awake, so the first state is read here.
    private void Start()
    {
        Apply(HasPower());
    }

    private bool HasPower()
    {
        if (generator != null) return generator.HasPower;
        return prototypeGenerator != null && prototypeGenerator.HasPower;
    }

    private void Apply(bool hasPower)
    {
        if (shaker != null) shaker.SetShaking(hasPower);

        if (smoke == null) return;

        if (hasPower)
        {
            if (!smoke.isPlaying) smoke.Play(true);
        }
        else
        {
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}

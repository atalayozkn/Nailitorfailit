using UnityEngine;

public class GeneratorLightEffect_SP : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Generator_SP generator;
    [SerializeField] private Light generatorLight;

    [Header("Light Settings")]
    [SerializeField, Min(0f)] private float poweredIntensity = 1.5f;

    // Obje oluþturulduðunda çalýþýr.
    // Generator ýþýðýnýn açýkken kullanacaðý intensity deðerini baþlangýçta ayarlar.
    private void Awake()
    {
        if (generatorLight != null)
        {
            generatorLight.intensity = poweredIntensity;
        }
    }

    // Script aktif olduðunda çalýþýr.
    // Generator'ýn OnPowerStateChanged eventine abone olur ve mevcut güç durumuna göre ýþýðý günceller.
    private void OnEnable()
    {
        if (generator == null) return;

        generator.OnPowerStateChanged += HandlePowerStateChanged;
        SetLightState(generator.HasPower);
    }

    // Script devre dýþý býrakýldýðýnda çalýþýr.
    // Generator'ýn OnPowerStateChanged event aboneliðini kaldýrýr.
    private void OnDisable()
    {
        if (generator == null) return;

        generator.OnPowerStateChanged -= HandlePowerStateChanged;
    }

    // Generator'ýn güç durumu deðiþtiðinde event tarafýndan çaðrýlýr.
    // SetLightState() fonksiyonuna yeni güç durumunu gönderir.
    private void HandlePowerStateChanged(bool hasPower)
    {
        SetLightState(hasPower);
    }

    // Generator ýþýðýný verilen güç durumuna göre açar veya kapatýr.
    // Iþýk zaten istenen durumdaysa gereksiz assignment yapmadan çýkar.
    private void SetLightState(bool hasPower)
    {
        if (generatorLight == null) return;
        if (generatorLight.enabled == hasPower) return;

        generatorLight.enabled = hasPower;
    }
}
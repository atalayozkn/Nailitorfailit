using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// Draws the VolumetricFog volume component. Marches at half resolution into a fog buffer
// (rgb = scattered light, a = transmittance), then blends it over the opaque image with a depth-aware upsample.
// Runs before transparents, so particles and glass draw on top of the fog instead of being fogged as if
// they were the ground behind them.
public class VolumetricFogRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;
    [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;
    [SerializeField] private bool halfResolution = true;

    private Material material;
    private Texture3D noise;
    private FogPass pass;

    public override void Create()
    {
        pass = new FogPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        CameraType type = renderingData.cameraData.cameraType;
        if (type != CameraType.Game && type != CameraType.SceneView) return;
        if (renderingData.cameraData.renderType == CameraRenderType.Overlay) return;

        VolumetricFog fog = VolumeManager.instance.stack.GetComponent<VolumetricFog>();
        if (fog == null || !fog.IsActive()) return;

        if (shader == null) shader = Shader.Find("Hidden/NailIt/VolumetricFog");
        if (shader == null) return;
        if (material == null) material = CoreUtils.CreateEngineMaterial(shader);
        if (noise == null) noise = BuildNoise(32);

        pass.renderPassEvent = injectionPoint;
        pass.Setup(material, noise, fog, halfResolution);
        pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        CoreUtils.Destroy(noise);
        material = null;
        noise = null;
    }

    // Tileable two-octave value noise, built once at load; 32 KB.
    private static Texture3D BuildNoise(int size)
    {
        var rng = new System.Random(1337);
        float[] Lattice(int n)
        {
            var l = new float[n * n * n];
            for (int i = 0; i < l.Length; i++) l[i] = (float)rng.NextDouble();
            return l;
        }
        float Sample(float[] l, int n, float x, float y, float z)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y), z0 = Mathf.FloorToInt(z);
            float fx = x - x0, fy = y - y0, fz = z - z0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy); fz = fz * fz * (3 - 2 * fz);
            float V(int a, int b, int c) => l[((a % n + n) % n) + ((b % n + n) % n) * n + ((c % n + n) % n) * n * n];
            float x00 = Mathf.Lerp(V(x0, y0, z0), V(x0 + 1, y0, z0), fx);
            float x10 = Mathf.Lerp(V(x0, y0 + 1, z0), V(x0 + 1, y0 + 1, z0), fx);
            float x01 = Mathf.Lerp(V(x0, y0, z0 + 1), V(x0 + 1, y0, z0 + 1), fx);
            float x11 = Mathf.Lerp(V(x0, y0 + 1, z0 + 1), V(x0 + 1, y0 + 1, z0 + 1), fx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, fy), Mathf.Lerp(x01, x11, fy), fz);
        }

        const int coarse = 4, fine = 8;
        float[] a = Lattice(coarse), b = Lattice(fine);
        var pixels = new byte[size * size * size];
        for (int z = 0; z < size; z++)
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (float)x / size, v = (float)y / size, w = (float)z / size;
            float n = Sample(a, coarse, u * coarse, v * coarse, w * coarse) * 0.65f
                    + Sample(b, fine, u * fine, v * fine, w * fine) * 0.35f;
            pixels[x + y * size + z * size * size] = (byte)Mathf.Clamp(Mathf.RoundToInt(n * 255f), 0, 255);
        }

        var tex = new Texture3D(size, size, size, GraphicsFormat.R8_UNorm, TextureCreationFlags.None)
        {
            name = "VolumetricFogNoise",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        tex.SetPixelData(pixels, 0);
        tex.Apply(false, true);
        return tex;
    }

    private class FogPass : ScriptableRenderPass
    {
        private static readonly int DensityParamsId = Shader.PropertyToID("_FogDensityParams");
        private static readonly int ShapeParamsId = Shader.PropertyToID("_FogShapeParams");
        private static readonly int AlbedoId = Shader.PropertyToID("_FogAlbedo");
        private static readonly int AmbientId = Shader.PropertyToID("_FogAmbient");
        private static readonly int WindId = Shader.PropertyToID("_FogWind");
        private static readonly int NoiseId = Shader.PropertyToID("_FogNoise");
        private static readonly int LowResSizeId = Shader.PropertyToID("_FogLowResSize");

        private Material material;
        private bool halfRes;

        private class PassData
        {
            public Material material;
            public TextureHandle fog;
        }

        public void Setup(Material mat, Texture3D noise, VolumetricFog fog, bool half)
        {
            material = mat;
            halfRes = half;
            mat.SetVector(DensityParamsId, new Vector4(fog.density.value, fog.baseHeight.value, fog.heightFalloff.value, fog.maxDistance.value));
            mat.SetVector(ShapeParamsId, new Vector4(fog.sunScattering.value, fog.anisotropy.value, fog.noiseStrength.value, fog.noiseScale.value));
            mat.SetColor(AlbedoId, fog.albedo.value);
            mat.SetColor(AmbientId, fog.ambient.value);
            Vector3 w = fog.wind.value;
            mat.SetVector(WindId, new Vector4(w.x, w.y, w.z, fog.steps.value));
            mat.SetTexture(NoiseId, noise);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer || !resources.cameraDepthTexture.IsValid()) return;

            TextureDesc colorDesc = renderGraph.GetTextureDesc(resources.activeColorTexture);
            int div = halfRes ? 2 : 1;
            var desc = new TextureDesc(Mathf.Max(1, colorDesc.width / div), Mathf.Max(1, colorDesc.height / div))
            {
                colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
                name = "_VolumetricFogBuffer",
                clearBuffer = false,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            material.SetVector(LowResSizeId, new Vector4(desc.width, desc.height, 1f / desc.width, 1f / desc.height));
            TextureHandle fogBuffer = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Volumetric Fog (march)", out var data))
            {
                data.material = material;
                builder.UseTexture(resources.cameraDepthTexture);
                if (resources.mainShadowsTexture.IsValid()) builder.UseTexture(resources.mainShadowsTexture);
                builder.UseAllGlobalTextures(true);
                builder.SetRenderAttachment(fogBuffer, 0, AccessFlags.WriteAll);
                builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                    Blitter.BlitTexture(ctx.cmd, new Vector4(1, 1, 0, 0), d.material, 0));
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Volumetric Fog (composite)", out var data))
            {
                data.material = material;
                data.fog = fogBuffer;
                builder.UseTexture(fogBuffer);
                builder.UseTexture(resources.cameraDepthTexture);
                builder.UseAllGlobalTextures(true);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                    Blitter.BlitTexture(ctx.cmd, d.fog, new Vector4(1, 1, 0, 0), d.material, 1));
            }
        }
    }
}

using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace Bernael_Xenotype
{
    // Shared shader resources for Malicious Seal and Dire Orb, loaded once on the main thread.
    [StaticConstructorOnStartup]
    public static class DarkMagicAssets
    {
        private static bool attemptedLoad;
        public static Material Seal { get; private set; }
        public static Material Orb { get; private set; }
        public static Mesh Quad { get; private set; }

        public static bool Ready()
        {
            if (Seal != null) return true;
            if (attemptedLoad) return false;
            attemptedLoad = true;
            Shader seal = null, orb = null;
#if RIMWORLD_1_7
            seal = DefDatabase<ShaderDef>.GetNamed("BX_MaliciousSeal").Shader;
            orb = DefDatabase<ShaderDef>.GetNamed("BX_DireOrb").Shader;
#else
            ModContentPack mod = LoadedModManager.RunningModsListForReading.FirstOrDefault(pack =>
                pack.assemblies.loadedAssemblies.Contains(typeof(DarkMagicAssets).Assembly));
            if (mod != null)
                foreach (AssetBundle bundle in mod.assetBundles.loadedAssetBundles)
                {
                    if (seal == null) seal = bundle.LoadAsset<Shader>("Assets/Shaders/MaliciousSeal.shader");
                    if (orb == null) orb = bundle.LoadAsset<Shader>("Assets/Shaders/DireOrb.shader");
                }
#endif
            if (seal == null || !seal.isSupported || orb == null || !orb.isSupported)
            {
                Log.Error("[Bernael] Malicious Seal / Dire Orb shaders unavailable. Check the mod's version-specific shader assets and restart RimWorld.");
                return false;
            }
            Seal = new Material(seal) { name = "Bernael Malicious Seal (shared)" };
            Orb = new Material(orb) { name = "Bernael Dire Orb (shared)" };
            Quad = new Mesh { name = "Dark magic UV plane" };
            Quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f),
                new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            Quad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            Quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            Quad.RecalculateBounds();
            Quad.UploadMeshData(true);
            return true;
        }

        // Rotates the quad so uv.x runs along delta; both shaders put their strip start at uv.x = 0.
        public static Quaternion Along(Vector3 delta) => Quaternion.LookRotation(new Vector3(-delta.z, 0, delta.x));

        public static void Draw(Material material, Vector3 position, Quaternion rotation, Vector3 scale,
            MaterialPropertyBlock properties)
        {
            Graphics.DrawMesh(Quad, Matrix4x4.TRS(position, rotation, scale), material, 0, null, 0,
                properties, ShadowCastingMode.Off, false);
        }
    }
}

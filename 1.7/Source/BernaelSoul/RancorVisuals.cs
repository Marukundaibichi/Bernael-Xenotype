using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace Bernael_Xenotype
{
    [StaticConstructorOnStartup]
    public static class RancorVisuals
    {
        // A soul's quad; its face sits at 0.78 of the length (BX_Rancor.frag.glsl ghost()).
        public const float SoulLength = 1.45f;
        public const float SoulWidth = 0.72f;
        private const float FaceOffset = 0.28f * SoulLength;
        private const float TrailWidth = 0.6f;
        private const float BurstSize = 12f;
        // The warmup's rupture and whirl stay within two cells of the caster (BX_Rancor.frag.glsl charge).
        private const float GatherSize = 4.6f;
        private const float ImpactSize = 2.4f;
        // Extra soul masks, when the mod ships them: Effects/Souls/Soul_1 to Soul_6, drawn like Piloto's SoulGhost
        // (white in alpha, face toward +x at 0.78 of the width, tail toward -x). Each soul keeps one by its seed.
        private const int MaxSoulVariants = 6;
        private static Material material;
        private static Material[] soulMaterials;
        private static Mesh quad;
        private static bool attempted;
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

        public static bool Ready()
        {
            if (material != null) return true;
            if (attempted) return false;
            attempted = true;
            Shader shader = DefDatabase<ShaderDef>.GetNamed("BX_Rancor").Shader;
            if (shader == null || !shader.isSupported)
            {
                Log.Warning("[Bernael] Rancor shader unavailable; using projectile graphics.");
                return false;
            }
            material = new Material(shader) { name = "Bernael Rancor souls" };
            // Piloto Studio masks; see Common/Textures/Effects/Piloto/SOURCES.json.
            material.SetTexture("_Ghost", ContentFinder<Texture2D>.Get("Effects/Piloto/SoulGhost"));
            material.SetTexture("_Wisp", ContentFinder<Texture2D>.Get("Effects/Piloto/SoulWisp"));
            material.SetTexture("_Ring", ContentFinder<Texture2D>.Get("Effects/Piloto/ShockRing"));
            material.SetTexture("_Smoke", ContentFinder<Texture2D>.Get("Effects/Piloto/SmokeRing"));
            material.SetTexture("_Rays", ContentFinder<Texture2D>.Get("Effects/Piloto/LightRays"));
            var variants = new List<Material> { material };
            for (int i = 1; i <= MaxSoulVariants; i++)
            {
                Texture2D mask = ContentFinder<Texture2D>.Get("Effects/Souls/Soul_" + i, false);
                if (mask == null) continue;
                var variant = new Material(material) { name = "Bernael Rancor soul " + i };
                variant.SetTexture("_Ghost", mask);
                variant.SetFloat("_AuthoredFace", 1f);
                variants.Add(variant);
            }
            // With drawn variants available, they replace the Piloto mask for the souls themselves.
            soulMaterials = (variants.Count > 1 ? variants.GetRange(1, variants.Count - 1) : variants).ToArray();
            quad = new Mesh { name = "Rancor UV plane" };
            quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f),
                new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.RecalculateBounds();
            quad.UploadMeshData(true);
            return true;
        }

        // The volley's last contraction (seconds below zero) and its release burst. The contraction continues the
        // warmup's charge, so it takes the caster's seed and the warmup's length.
        public static void DrawBurst(Vector3 center, float seconds, float angle, int souls, int seed, float warmupSeconds)
        {
            properties.Clear();
            properties.SetFloat("_Mode", 0);
            properties.SetFloat("_Phase", seconds);
            properties.SetFloat("_Angle", angle * Mathf.Deg2Rad);
            properties.SetFloat("_Souls", souls);
            properties.SetFloat("_Seed", GatherSeed(seed));
            properties.SetFloat("_Gather", warmupSeconds);
            properties.SetFloat("_Opacity", 1);
            properties.SetFloat("_Size", BurstSize);
            Draw(center, Quaternion.identity, new Vector3(BurstSize, 1, BurstSize));
        }

        private static float GatherSeed(int seed) => seed % 23 * 0.37f;

        // The cast warmup: the reservoir stirring inside the caster. Nothing flies in; the souls are already stored.
        // The number of souls about to be released sets how many strands of the vortex turn, as in the contraction.
        public static void DrawGather(Vector3 center, float seconds, float progress, int seed, int souls)
        {
            if (!Ready()) return;
            properties.Clear();
            properties.SetFloat("_Mode", 4);
            properties.SetFloat("_Phase", seconds);
            properties.SetFloat("_Progress", progress);
            properties.SetFloat("_Souls", souls);
            properties.SetFloat("_Seed", GatherSeed(seed));
            properties.SetFloat("_Opacity", 1);
            properties.SetFloat("_Size", GatherSize);
            Draw(center, Quaternion.identity, new Vector3(GatherSize, 1, GatherSize));
        }

        public static void DrawImpact(Vector3 at, float seconds, float angle)
        {
            if (!Ready()) return;
            properties.Clear();
            properties.SetFloat("_Mode", 3);
            properties.SetFloat("_Phase", seconds);
            properties.SetFloat("_Angle", angle);
            properties.SetFloat("_Opacity", 1);
            properties.SetFloat("_Size", ImpactSize);
            Draw(at, Quaternion.identity, new Vector3(ImpactSize, 1, ImpactSize));
        }

        // A soul with its trail. The trail is a ribbon of quads along the recorded path, its texture running
        // continuously from the soul (0) to the trail's end (1).
        public static bool DrawSoul(Vector3 at, Vector3 direction, List<Vector3> trail, float phase, int seed)
        {
            if (!Ready()) return false;
            direction.y = 0;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.right;
            direction.Normalize();
            // Souls grow out of the release flash; full-size overlapping faces otherwise form a dense rosette.
            float emergeSeconds = Mathf.Lerp(0.15f, 0.28f, RancorVariation.Value(seed, 5));
            float emergence = Mathf.SmoothStep(0.35f, 1f, Mathf.Clamp01(phase / emergeSeconds));
            float total = 0;
            Vector3 previous = at;
            for (int i = 0; i < trail.Count; i++)
            {
                total += (previous - trail[i]).Yto0().magnitude;
                previous = trail[i];
            }
            if (total > 0.05f)
            {
                properties.Clear();
                properties.SetFloat("_Mode", 2);
                properties.SetFloat("_Phase", phase);
                properties.SetFloat("_Seed", seed % 17 * 0.29f);
                properties.SetFloat("_Length", total);
                properties.SetFloat("_Opacity", 1);
                float covered = 0;
                previous = at;
                for (int i = 0; i < trail.Count; i++)
                {
                    Vector3 delta = (previous - trail[i]).Yto0();
                    float length = delta.magnitude;
                    if (length > 0.002f)
                    {
                        float u0 = covered / total, u1 = (covered + length) / total;
                        properties.SetFloat("_U0", u0);
                        properties.SetFloat("_U1", u1);
                        float width = (TrailWidth * Mathf.Pow(1 - u0, 0.6f) + 0.02f) * emergence;
                        // Segments meet exactly end to end; overlapping, each joint showed as a darker bar.
                        Draw((previous + trail[i]) * 0.5f, Along(delta), new Vector3(length, 1, width));
                    }
                    covered += length;
                    previous = trail[i];
                }
            }
            properties.Clear();
            properties.SetFloat("_Mode", 1);
            properties.SetFloat("_Phase", phase);
            // Varies the soul's shape, rhythm and tint (BX_Rancor.frag.glsl ghost).
            properties.SetFloat("_Seed", seed % 29 * 0.71f);
            // One of the shader's five eye shapes, fixed for the soul's whole flight.
            properties.SetFloat("_FaceVariant", (seed & 0x7fffffff) % 5);
            properties.SetFloat("_Opacity", 1);
            // A soul a little larger or smaller than its neighbours.
            float size = emergence * Mathf.Lerp(0.78f, 1.18f, RancorVariation.Value(seed, 6));
            Draw(at - direction * (FaceOffset * size), Along(direction),
                new Vector3(SoulLength * size, 1, SoulWidth * size), soulMaterials[(seed & 0x7fffffff) % soulMaterials.Length]);
            return true;
        }

        private static Quaternion Along(Vector3 delta) => Quaternion.LookRotation(new Vector3(-delta.z, 0, delta.x));

        private static void Draw(Vector3 at, Quaternion rotation, Vector3 scale, Material with = null)
        {
            at.y = AltitudeLayer.VisEffects.AltitudeFor();
            Graphics.DrawMesh(quad, Matrix4x4.TRS(at, rotation, scale), with ?? material, 0, null, 0,
                properties, ShadowCastingMode.Off, false);
        }
    }
}

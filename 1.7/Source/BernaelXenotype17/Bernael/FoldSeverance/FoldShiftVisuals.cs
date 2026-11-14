using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace Bernael_Xenotype
{
    // The Fold Shift shadow, drawn with BX_FoldShift from a capture of the pawn per facing. Like Dark Mirage, each
    // capture gets a silhouette distance field (worked out on a worker thread) and eye anchors.
    [StaticConstructorOnStartup]
    public sealed class FoldShiftVisuals : IDisposable
    {
        [ThreadStatic] public static bool Capturing;
        private static readonly AccessTools.FieldRef<PawnRenderTree, PawnDrawParms> CachedDrawParms =
            AccessTools.FieldRefAccess<PawnRenderTree, PawnDrawParms>("oldParms");
        private static readonly AccessTools.FieldRef<PawnRenderTree, List<PawnGraphicDrawRequest>> CachedDrawRequests =
            AccessTools.FieldRefAccess<PawnRenderTree, List<PawnGraphicDrawRequest>>("drawRequests");
        private static Shader nativeShader;
        public static Shader Shader
        {
            get
            {
                if (nativeShader == null) nativeShader = DefDatabase<ShaderDef>.GetNamed("BX_FoldShift").Shader;
                return nativeShader;
            }
        }
        public const int CaptureSize = 512;
        // Dark Mirage's framing: BX_FoldShift's outline, glow and eyes are measured in this capture's UVs.
        public const float WorldSize = 3f;
        // Seconds for the shadow to rise over the pawn; until then the pawn itself still shows above it.
        public const float EntrySeconds = 0.58f;
        // The halo's height offset and render layer reach 0.304 above the pawn when facing north.
        // Cover it during entry and exit too, so hiding or restoring the ordinary pawn cannot change its colour.
        public static float ShadowAltitudeAt(Vector3 position) => position.y + 0.33f;

        private sealed class Facing
        {
            public Texture2D snapshot, field;
            public Material material;
            public Task<MirageDistanceField.Data> pending;
            public Vector4 canvas;
            public int appearance;
            // The first frame the finished field may be drawn in; see Finish.
            public int usableFrame;
        }

        // A capture that covers the whole canvas (or nothing at all) was not rendered properly; it is retaken this
        // many times before being used anyway.
        private const int CaptureRetries = 2;
        private readonly int[] retries = new int[4];

        private readonly Facing[] facings = new Facing[4];
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private Facing lastDrawn;
        private float lastPhase;
        private int lastDrawFrame = -2;

        public bool WasDrawnRecently => Time.frameCount <= lastDrawFrame + 1;

        // The capture behind one facing, or null before that facing has been drawn. For diagnostics.
        public Texture2D SnapshotFor(Rot4 facing) => facings[facing.AsInt]?.snapshot;

        // Draws the shadow; false while no capture of this pawn is ready yet, so the game draws the pawn itself.
        public bool Draw(Pawn pawn, Vector3 position, Rot4 facing, float seconds, float opacity, float pulseOpacity)
        {
            Shader shader = Shader;
            if (shader == null || !shader.isSupported) return false;
            Facing entry = Prepare(pawn, facing, shader);
            if (!Finish(entry, facing.AsInt))
            {
                // While a new facing's field is computed, keep showing a facing that is ready.
                entry = lastDrawn;
                if (!IsReady(entry)) return false;
            }
            lastDrawn = entry;
            lastPhase = seconds;
            DrawShadow(entry, pawn, position, seconds, seconds, 0f, opacity);
            if (seconds < 0.4f && pulseOpacity > 0f)
            {
                properties.SetFloat("_Mode", 1f);
                properties.SetFloat("_Opacity", pulseOpacity * opacity);
                position.y = AltitudeLayer.Pawn.AltitudeFor() - 0.02f;
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(position, Quaternion.identity, new Vector3(2.8f, 1, 2.8f)),
                    entry.material, 0, null, 0, properties, UnityEngine.Rendering.ShadowCastingMode.Off, false);
            }
            return true;
        }

        public void DrawExit(Pawn pawn, float elapsed, float fade)
        {
            // Reuse a submitted pose. Preparing a new capture here can make the shadow reappear as it fades.
            Facing entry = facings[pawn.Rotation.AsInt];
            if (!IsReady(entry) || entry.appearance != Appearance(pawn)) entry = lastDrawn;
            if (!IsReady(entry)) return;
            DrawShadow(entry, pawn, pawn.DrawPos, lastPhase + elapsed, lastPhase, fade, 1f);
        }

        private void DrawShadow(Facing entry, Pawn pawn, Vector3 position, float phase, float age, float fade, float opacity)
        {
            Vector3 at = position;
            at.y = ShadowAltitudeAt(position);
            at.x += (entry.canvas.x + entry.canvas.z * 0.5f - 0.5f) * WorldSize;
            at.z += (entry.canvas.y + entry.canvas.w * 0.5f - 0.5f) * WorldSize;
            properties.Clear();
            properties.SetFloat("_Mode", 0f);
            properties.SetFloat("_Phase", phase);
            properties.SetFloat("_SpawnAge", Mathf.Clamp01(age / EntrySeconds));
            properties.SetFloat("_Fade", fade);
            properties.SetFloat("_Opacity", opacity);
            properties.SetFloat("_Seed", pawn.thingIDNumber % 997 * 0.13f);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(at, Quaternion.identity,
                    new Vector3(WorldSize * entry.canvas.z, 1f, WorldSize * entry.canvas.w)),
                entry.material, 0, null, 0, properties, UnityEngine.Rendering.ShadowCastingMode.Off, false);
            lastDrawFrame = Time.frameCount;
        }

        private static bool IsReady(Facing entry) => entry?.field != null && Time.frameCount >= entry.usableFrame;

        private static int Appearance(Pawn pawn)
        {
            int appearance = pawn.equipment?.Primary?.thingIDNumber ?? 0;
            if (pawn.apparel != null)
                foreach (Apparel apparel in pawn.apparel.WornApparel)
                    appearance = unchecked(appearance * 397 ^ apparel.thingIDNumber);
            return appearance;
        }

        private Facing Prepare(Pawn pawn, Rot4 facing, Shader shader)
        {
            int appearance = Appearance(pawn);
            Facing entry = facings[facing.AsInt];
            if (entry != null && entry.appearance == appearance) return entry;
            Release(entry);
            entry = new Facing { appearance = appearance };
            facings[facing.AsInt] = entry;
            Vector4 eyeA, eyeB;
            entry.snapshot = Capture(pawn, facing, out eyeA, out eyeB);
            entry.snapshot.name = "FoldShift snapshot " + pawn.ThingID;
            entry.snapshot.wrapMode = TextureWrapMode.Clamp;
            entry.snapshot.filterMode = FilterMode.Bilinear;
            Color32[] pixels = entry.snapshot.GetPixels32();
            int width = entry.snapshot.width, height = entry.snapshot.height;
            entry.pending = Task.Run(() => MirageDistanceField.Compute(pixels, width, height));
            entry.material = new Material(shader) { name = "FoldShift " + pawn.ThingID, mainTexture = entry.snapshot };
            entry.material.SetVector("_EyeA", eyeA);
            entry.material.SetVector("_EyeB", eyeB);
            return entry;
        }

        private bool Finish(Facing entry, int index)
        {
            if (entry == null || entry.material == null) return false;
            if (entry.field != null) return IsReady(entry);
            if (entry.pending == null || !entry.pending.IsCompleted) return false;
            Task<MirageDistanceField.Data> task = entry.pending;
            entry.pending = null;
            try
            {
                MirageDistanceField.Data data = task.GetAwaiter().GetResult();
                Vector4 bounds = data.RevealBounds;
                if (bounds.y - bounds.x > 0.98f && bounds.w - bounds.z > 0.98f && retries[index] < CaptureRetries)
                {
                    // Empty, or filled edge to edge: drawn, it would hide the pawn or show a filled square.
                    retries[index]++;
                    Release(entry);
                    facings[index] = null;
                    return false;
                }
                entry.field = MirageDistanceField.CreateTexture(data);
                entry.field.name = "FoldShift silhouette distance";
                entry.material.SetTexture("_DistanceTex", entry.field);
                entry.material.SetVector("_RevealBounds", data.RevealBounds);
                entry.material.SetVector("_CanvasRect", data.CanvasRect);
                entry.canvas = data.CanvasRect;
                // Give the renderer one frame to bind the uploaded texture before sampling the field.
                entry.usableFrame = Time.frameCount + 1;
                return false;
            }
            catch (Exception error)
            {
                Log.ErrorOnce("[Bernael] Fold Shift distance field failed: " + error, 0x5F01D);
                return false;
            }
        }

        // A WorldSize-cell square capture of the pawn's render tree (body, clothes and gear, not the held weapon),
        // centred on its draw position. Main thread only; the caller owns the texture.
        public static Texture2D CaptureSnapshot(Pawn pawn, Rot4 facing) => Capture(pawn, facing, out _, out _);

        private static Texture2D Capture(Pawn pawn, Rot4 facing, out Vector4 eyeA, out Vector4 eyeB)
        {
            bool wasCapturing = Capturing;
            pawn.Drawer.renderer.EnsureGraphicsInitialized();
            PawnRenderTree renderTree = pawn.Drawer.renderer.renderTree;
            PawnDrawParms previousDrawParms = CachedDrawParms(renderTree);
            PawnGraphicDrawRequest[] previousDrawRequests = CachedDrawRequests(renderTree).ToArray();
            Camera camera = Find.PawnCacheCamera;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            Color previousBackground = camera.backgroundColor;
            CameraClearFlags previousClear = camera.clearFlags;
            Vector3 previousPosition = camera.transform.position;
            float previousSize = camera.orthographicSize;
            Rect previousRect = camera.rect;
            var capture = new RenderTexture(CaptureSize, CaptureSize, 24, RenderTextureFormat.ARGB32)
            {
                name = "FoldShift temporary capture", antiAliasing = 1,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            try
            {
                Capturing = true;
                capture.Create();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                Find.PawnCacheRenderer.RenderPawn(pawn, capture, Vector3.zero, 2f / WorldSize, 0f, facing,
                    renderHead: true, renderHeadgear: true, renderClothes: true, portrait: false);
                // As in MirageVisuals: rebind the square target so the eyes project with the capture's aspect.
                camera.targetTexture = capture;
                camera.orthographicSize = WorldSize * 0.5f;
                eyeA = MirageVisuals.EyeAnchor(pawn, facing, "LeftEye", camera);
                eyeB = MirageVisuals.EyeAnchor(pawn, facing, "RightEye", camera);
                RenderTexture.active = capture;
                var snapshot = new Texture2D(CaptureSize, CaptureSize, TextureFormat.RGBA32, false);
                snapshot.ReadPixels(new Rect(0, 0, CaptureSize, CaptureSize), 0, 0, false);
                snapshot.Apply(false, false);
                return snapshot;
            }
            finally
            {
                // RenderCache overwrites the live tree's draw requests with capture-space matrices.
                // Restore the exact prepared world draws before the ordinary pawn resumes this frame.
                CachedDrawParms(renderTree) = previousDrawParms;
                List<PawnGraphicDrawRequest> requests = CachedDrawRequests(renderTree);
                requests.Clear();
                requests.AddRange(previousDrawRequests);
                Capturing = wasCapturing;
                RenderTexture.active = previousActive;
                camera.targetTexture = previousTarget;
                camera.backgroundColor = previousBackground;
                camera.clearFlags = previousClear;
                camera.transform.position = previousPosition;
                camera.orthographicSize = previousSize;
                camera.rect = previousRect;
                capture.Release();
                UnityEngine.Object.Destroy(capture);
            }
        }

        private static void Release(Facing entry)
        {
            if (entry == null) return;
            // A released facing must never upload a late worker result; only observe its failure.
            entry.pending?.ContinueWith(task => GC.KeepAlive(task.Exception), TaskContinuationOptions.OnlyOnFaulted);
            entry.pending = null;
            if (entry.material != null) UnityEngine.Object.Destroy(entry.material);
            if (entry.snapshot != null) UnityEngine.Object.Destroy(entry.snapshot);
            if (entry.field != null) UnityEngine.Object.Destroy(entry.field);
            entry.material = null;
            entry.snapshot = null;
            entry.field = null;
        }

        public void Dispose()
        {
            for (int i = 0; i < facings.Length; i++)
            {
                Release(facings[i]);
                facings[i] = null;
            }
        }
    }
}

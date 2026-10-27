using System;
using RimWorld;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Bernael_Xenotype
{
    // Each decoy owns an immutable capture. Never recolor or patch the caster's render tree.
    [StaticConstructorOnStartup]
    public sealed class MirageVisuals : IDisposable
    {
        public const int CaptureSize = 512;
        public const float WorldSize = 3f;
        private static Shader shader;
        private static bool warned;
        private Texture2D snapshot;
        private Texture2D distanceField;
        private Material material;
        private Task<MirageDistanceField.Data> pendingDistanceField;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private Vector4 eyeA;
        private Vector4 eyeB;
        private Vector4 canvasRect;

        public static bool ShaderReady(ThingDef def)
        {
            if (shader != null) return shader.isSupported;
#if RIMWORLD_1_7
            shader = DefDatabase<ShaderDef>.GetNamed("BX_DarkMirage").Shader;
#else
            foreach (AssetBundle bundle in def.modContentPack.assetBundles.loadedAssetBundles)
            {
                shader = bundle.LoadAsset<Shader>("Assets/Shaders/DarkMirage.shader");
                if (shader != null) break;
            }
#endif
            if (shader != null && shader.isSupported) return true;
            if (!warned)
            {
                warned = true;
                Log.Error("[Dark Mirage] Missing/unsupported shader. Check the mod's version-specific shader assets and restart RimWorld.");
            }
            return false;
        }

        public void Initialize(DarkMirage owner, ref string savedImage, ref Vector4 savedEyeA, ref Vector4 savedEyeB)
        {
            if (!ShaderReady(owner.def)) throw new InvalidOperationException("Dark Mirage shader unavailable.");
            if (!savedImage.NullOrEmpty())
            {
                snapshot = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(snapshot, Convert.FromBase64String(savedImage)))
                    throw new InvalidOperationException("Could not restore caster snapshot.");
                eyeA = savedEyeA;
                eyeB = savedEyeB;
            }
            else
            {
                Capture(owner.Caster, owner.Rotation);
                savedEyeA = eyeA;
                savedEyeB = eyeB;
            }
            snapshot.name = "DarkMirage caster snapshot " + owner.ThingID;
            snapshot.wrapMode = TextureWrapMode.Clamp;
            snapshot.filterMode = FilterMode.Bilinear;
            Color32[] pixels = snapshot.GetPixels32();
            int width = snapshot.width, height = snapshot.height;
            pendingDistanceField = Task.Run(() => MirageDistanceField.Compute(pixels, width, height));
            material = new Material(shader) { name = "DarkMirage " + owner.ThingID, mainTexture = snapshot };
            material.SetVector("_EyeA", eyeA);
            material.SetVector("_EyeB", eyeB);
            material.SetFloat("_Seed", (owner.thingIDNumber % 997) * 0.13f);
        }

        public string EncodeSnapshot()
        {
            return snapshot == null ? null : Convert.ToBase64String(ImageConversion.EncodeToPNG(snapshot));
        }

        private bool FinishDistanceField(bool wait = false)
        {
            if (distanceField != null) return true;
            if (pendingDistanceField == null || (!wait && !pendingDistanceField.IsCompleted)) return false;
            Task<MirageDistanceField.Data> task = pendingDistanceField;
            pendingDistanceField = null;
            try
            {
                MirageDistanceField.Data data = task.GetAwaiter().GetResult();
                distanceField = MirageDistanceField.CreateTexture(data);
                material.SetTexture("_DistanceTex", distanceField);
                material.SetVector("_RevealBounds", data.RevealBounds);
                canvasRect = data.CanvasRect;
                material.SetVector("_CanvasRect", canvasRect);
                return true;
            }
            catch (Exception error)
            {
                Dispose();
                Log.Error("[Dark Mirage] Distance field generation failed: " + error);
                return false;
            }
        }

        private void Capture(Pawn caster, Rot4 facing)
        {
            caster.Drawer.renderer.EnsureGraphicsInitialized();
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
                name = "DarkMirage temporary capture", antiAliasing = 1,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            try
            {
                capture.Create();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                // Keep aspect automatic: assigning even its old value permanently
                // pins this shared camera and squashes later portraits/atlas frames.
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                Find.PawnCacheRenderer.RenderPawn(caster, capture, Vector3.zero, 2f / WorldSize, 0f,
                    facing, renderHead: true, renderHeadgear: true, renderClothes: true, portrait: false);
                // RenderPawn unbinds its target. Rebind our square target so eye
                // projection uses the capture's aspect without changing aspect mode.
                camera.targetTexture = capture;
                camera.orthographicSize = WorldSize * 0.5f;
                eyeA = EyeAnchor(caster, facing, "LeftEye", camera);
                eyeB = EyeAnchor(caster, facing, "RightEye", camera);
                RenderTexture.active = capture;
                snapshot = new Texture2D(CaptureSize, CaptureSize, TextureFormat.RGBA32, false);
                snapshot.ReadPixels(new Rect(0,0,CaptureSize,CaptureSize),0,0,false);
                snapshot.Apply(false,false);
            }
            finally
            {
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

        private static Vector4 EyeAnchor(Pawn pawn, Rot4 facing, string tag, Camera camera)
        {
            if (facing == Rot4.North || pawn.story?.bodyType == null || !pawn.health.hediffSet.HasHead)
                return Vector4.zero;
            foreach (BodyTypeDef.WoundAnchor anchor in pawn.story.bodyType.woundAnchors)
            {
                if (anchor.tag != tag || anchor.rotation != facing ||
                    (facing != Rot4.South && (anchor.narrowCrown == true) != pawn.story.headType.narrow)) continue;
                Vector3 offset;
                float range;
                PawnDrawUtility.CalcAnchorData(pawn, anchor, facing, out offset, out range);
                var parms = new PawnDrawParms {
                    pawn = pawn, facing = facing,
                    matrix = Matrix4x4.Translate(pawn.ageTracker.CurLifeStage.bodyDrawOffset),
                    rotDrawMode = pawn.Drawer.renderer.CurRotDrawMode,
                    posture = pawn.GetPosture(),
                    flags = PawnRenderFlags.Cache | PawnRenderFlags.DrawNow | PawnRenderFlags.Headgear | PawnRenderFlags.Clothes
                };
                PawnRenderNode headNode;
                Matrix4x4 headMatrix;
                if (!pawn.Drawer.renderer.renderTree.TryGetNodeByTag(PawnRenderNodeTagDefOf.Head, out headNode) ||
                    !pawn.Drawer.renderer.renderTree.TryGetMatrix(headNode,parms,out headMatrix)) return Vector4.zero;
                // Vanilla Eyes_Red/Gray use a -0.25 z offset below the head anchor.
                Vector3 worldEye = headMatrix.MultiplyPoint3x4(offset + new Vector3(0,0,-0.25f));
                float scale = pawn.ageTracker.CurLifeStage.eyeSizeFactor ?? 1f;
                Vector3 uv = camera.WorldToViewportPoint(worldEye);
                return new Vector4(uv.x, uv.y, 1f, scale);
            }
            return Vector4.zero;
        }

        public void Draw(Vector3 position, int ageTicks, float opacity)
        {
            if (material == null || opacity <= 0 || !FinishDistanceField()) return;
            position.y = AltitudeLayer.Pawn.AltitudeFor();
            position.z += Mathf.Sin(ageTicks / 60f * 2.2f) * 0.016f;
            // The effect canvas may extend beyond the captured pawn. Offset its
            // center with its size so the caster and eyes stay in the same place.
            position.x += (canvasRect.x + canvasRect.z * 0.5f - 0.5f) * WorldSize;
            position.z += (canvasRect.y + canvasRect.w * 0.5f - 0.5f) * WorldSize;
            properties.SetFloat("_Phase", ageTicks / 60f);
            properties.SetFloat("_SpawnAge", ageTicks / 60f);
            properties.SetFloat("_Opacity", opacity);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(position, Quaternion.identity,
                new Vector3(WorldSize*canvasRect.z,1f,WorldSize*canvasRect.w)), material, 0, null, 0, properties,
                UnityEngine.Rendering.ShadowCastingMode.Off, false);
        }

        // Explicit dev QA uses this same shader and capture, not a separate mockup.
        public void ExportPreview(string path, float time, float spawnAge = 10f, float opacity = 1f)
        {
            if (material == null || !FinishDistanceField(wait: true))
                throw new InvalidOperationException("Mirage material unavailable");
            var target = RenderTexture.GetTemporary(distanceField.width,distanceField.height,0,RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                RenderTexture.active = target;
                GL.Clear(true,true,new Color(0.24f,0.265f,0.23f,1f));
                material.SetFloat("_Phase", time);
                material.SetFloat("_SpawnAge", spawnAge);
                material.SetFloat("_Opacity", opacity);
                Graphics.Blit(snapshot,target,material);
                RenderTexture.active = target;
                image = new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);
                image.Apply();
                System.IO.File.WriteAllBytes(path,ImageConversion.EncodeToPNG(image));
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (image != null) UnityEngine.Object.Destroy(image);
            }
        }

        public void Dispose()
        {
            // A dismissed mirage must never upload a late worker result. Observe any
            // worker failure without retaining the mirage or touching Unity objects.
            pendingDistanceField?.ContinueWith(task => GC.KeepAlive(task.Exception),
                TaskContinuationOptions.OnlyOnFaulted);
            pendingDistanceField = null;
            if (material != null) UnityEngine.Object.Destroy(material);
            if (snapshot != null) UnityEngine.Object.Destroy(snapshot);
            if (distanceField != null) UnityEngine.Object.Destroy(distanceField);
            material = null;
            snapshot = null;
            distanceField = null;
        }

        public static void Burst(IntVec3 cell, Map map, float radius)
        {
            if (map != Find.CurrentMap || cell.Fogged(map)) return;
            for (int i=0; i<12; i++)
            {
                float a = i * Mathf.PI * 2f / 12;
                var data = FleckMaker.GetDataStatic(cell.ToVector3Shifted(),map,FleckDefOf.MicroSparks,0.3f);
                data.instanceColor = new Color(0.725f,0.85f,0.93f);
                data.velocity = new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;
                map.flecks.CreateFleck(data);
            }
        }
    }
}

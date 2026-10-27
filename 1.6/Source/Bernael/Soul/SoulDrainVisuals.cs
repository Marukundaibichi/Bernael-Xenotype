using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Bernael_Xenotype
{
    public static class SoulDrainVisuals
    {
        public static void Begin(Pawn caster, Pawn victim, Ability ability)
        {
            if (!CanBegin(caster, victim)) return;
            caster.Map.GetComponent<SoulDrainMapComponent>().Begin(caster, victim, ability);
        }

        public static void BeginFeeding(Pawn caster, Pawn victim, int remainingTicks)
        {
            if (!CanBegin(caster, victim)) return;
            caster.Map.GetComponent<SoulDrainMapComponent>().Begin(caster, victim, null,
                remainingTicks, prisonerFeeding: true);
        }

        private static bool CanBegin(Pawn caster, Pawn victim)
        {
            if (!CanShow(caster, victim) || SoulUtility.IsSoulFeeder(victim)) return false;
            DevelopmentalStage? stage = victim.ageTracker?.CurLifeStage?.developmentalStage;
            return stage is not (DevelopmentalStage.Baby or DevelopmentalStage.Newborn);
        }

        public static void Complete(Pawn caster, Pawn victim)
        {
            if (CanShow(caster, victim))
                caster.Map.GetComponent<SoulDrainMapComponent>().Complete(caster, victim);
            if (victim.Dead || victim.story == null) return;
            HediffDef def = DefDatabase<HediffDef>.GetNamed("BX_SoulDrainPallor");
            Hediff pallor = victim.health.hediffSet.GetFirstHediffOfDef(def);
            if (pallor == null) victim.health.AddHediff(def);
            else pallor.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
        }

        private static bool CanShow(Pawn caster, Pawn victim)
        {
            return caster != null && victim != null && caster != victim && caster.Spawned &&
                victim.Spawned && caster.Map == victim.Map && !caster.Dead && !victim.Dead;
        }
    }

    // Links save simulation state and silhouette PNG data. Draw resources are shared.
    [StaticConstructorOnStartup]
    public sealed class SoulDrainMapComponent : MapComponent
    {
        private List<SoulDrainLink> links = new List<SoulDrainLink>();
        private MaterialPropertyBlock properties;
        private static Material material;
        private static Mesh quad;
        private static Mesh soulGrid;
        private static bool attemptedLoad;
        public static Shader AshenShader { get; private set; }
        public static Texture2D SmokeTexture { get; private set; }
        internal static Material EffectMaterial => material;
        internal static Mesh SoulGrid => soulGrid;
        internal static Mesh Quad => quad;
        public SoulDrainMapComponent(Map map) : base(map) { }

        public void Begin(Pawn caster, Pawn victim, Ability ability, int duration = -1, bool prisonerFeeding = false)
        {
            for (int i = 0; i < links.Count; i++)
                if (links[i].caster == caster && links[i].victim == victim && links[i].endTick < 0) return;
            links.Add(new SoulDrainLink {
                caster = caster, victim = victim, ability = ability,
                prisonerFeeding = prisonerFeeding,
                startTick = Find.TickManager.TicksGame, endTick = -1,
                drainTicks = Math.Max(1, duration >= 0 ? duration :
                    ((caster.stances.curStance as Stance_Warmup)?.ticksLeft ?? 120))
            });
        }

        public void Complete(Pawn caster, Pawn victim)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = links.Count - 1; i >= 0; i--)
            {
                SoulDrainLink link = links[i];
                if (link.caster != caster || link.victim != victim || link.endTick >= 0) continue;
                link.endTick = now;
                link.committed = true;
                return;
            }
            // Instant/scripted drains have no channel to finish. Do not replay an
            // extraction after damage and pallor have already been applied.
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = links.Count - 1; i >= 0; i--)
            {
                SoulDrainLink link = links[i];
                // Refresh the cached pawn image as its color changes, without rebuilding its render tree.
                if (link.victim != null && (!link.committed || now - link.startTick <= link.drainTicks))
                    GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(link.victim);
                if (!link.OnMap(map)) { link.Dispose(); links.RemoveAt(i); continue; }
                if (link.endTick < 0 && (link.caster.Dead || link.caster.Downed || link.victim.Dead ||
                    !link.IsChanneling())) link.endTick = now;
                if (link.endTick >= 0 && now - link.endTick >= (link.committed ? 36 : 12))
                { link.Dispose(); links.RemoveAt(i); }
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref links, "soulDrainLinks", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && links == null) links = new List<SoulDrainLink>();
        }

        public override void MapRemoved()
        {
            foreach (SoulDrainLink link in links) link.Dispose();
            links.Clear();
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                    if (hediff is Hediff_SoulDrainPallor pallor) pallor.ReleaseMaterials();
        }

        public override void MapComponentDraw()
        {
            if (map != Find.CurrentMap || links.Count == 0 || !EnsureMaterial()) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < links.Count; i++)
            {
                SoulDrainLink link = links[i];
                if (!link.OnMap(map) || link.caster.Position.Fogged(map) || link.victim.Position.Fogged(map)) continue;
                float age = (now - link.startTick) / 60f;
                float release = link.endTick < 0 ? 0f : Mathf.Max(0f, (now - link.endTick) / 60f);
                float opacity = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / 0.5f)) *
                    (1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01(release / (link.committed ? 0.6f : 0.2f))));
                if (opacity <= 0f) continue;
                Vector3 eyeA, eyeB;
                int eyeCount = EyePoints(link.caster, out eyeA, out eyeB);
                Vector3 eye = eyeCount == 2 ? (eyeA + eyeB) * 0.5f : eyeA;
                Vector3 target = HeadPoint(link.victim);
                Vector3 delta = target - eye;
                delta.y = 0;
                float length = delta.magnitude;
                if (length < 0.1f) continue;
                Quaternion rotation = Quaternion.LookRotation(new Vector3(-delta.z, 0, delta.x));
                properties.Clear();
                properties.SetFloat("_Phase", age);
                properties.SetFloat("_Progress", link.Progress(now));
                properties.SetFloat("_Opacity", opacity);
                properties.SetFloat("_Seed", link.caster.thingIDNumber % 251 * 0.17f);
                properties.SetFloat("_Length", length);
                properties.SetFloat("_Width", 2.4f);
                properties.SetFloat("_Mode", 0f);
                Draw((eye + target) * 0.5f, rotation, new Vector3(length + 1.1f, 1, 2.4f));

                if (link.EnsureSnapshot())
                {
                    Vector3 origin = link.victim.DrawPos;
                    properties.SetFloat("_Mode", 2f);
                    properties.SetTexture("_MainTex", link.Snapshot);
                    properties.SetFloat("_SoulAngle", link.SnapshotAngle);
                    properties.SetVector("_Pull", new Vector4(eye.x-origin.x, eye.z-origin.z, 0, 0));
                    Draw(origin, Quaternion.identity, Vector3.one, soulGrid);
                }

                properties.SetFloat("_Mode", 1f);
                if (eyeCount > 0) Draw(eyeA, Quaternion.identity, Vector3.one * 0.52f);
                if (eyeCount > 1) Draw(eyeB, Quaternion.identity, Vector3.one * 0.52f);
            }
        }

        public bool TryGetPallor(Pawn victim, out SoulDrainAshenMaterials materials, out float amount)
        {
            materials = null;
            amount = 0f;
            int now = Find.TickManager.TicksGame;
            foreach (SoulDrainLink link in links)
            {
                if (link.victim != victim || !link.OnMap(map)) continue;
                float candidate = link.DrainAmount(now);
                if (candidate <= amount) continue;
                amount = candidate;
                materials = link.AshenMaterials;
            }
            return materials != null;
        }

        private void Draw(Vector3 position, Quaternion rotation, Vector3 scale, Mesh mesh = null)
        {
            position.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            Graphics.DrawMesh(mesh ?? quad, Matrix4x4.TRS(position, rotation, scale), material, 0, null, 0,
                properties, UnityEngine.Rendering.ShadowCastingMode.Off, false);
        }

        internal static Vector3 HeadPoint(Pawn pawn)
        {
            Vector3 first, second;
            int count = EyePoints(pawn, out first, out second);
            return count == 2 ? (first + second) * 0.5f : first;
        }

        private static int EyePoints(Pawn pawn, out Vector3 first, out Vector3 second)
        {
            Vector3 offset = pawn.story?.bodyType == null ? new Vector3(0, 0, 0.3f) :
                pawn.Drawer.renderer.BaseHeadOffsetAt(pawn.Rotation);
            Quaternion rotation = Quaternion.AngleAxis(pawn.Drawer.renderer.BodyAngle(PawnRenderFlags.None), Vector3.up);
            first = pawn.DrawPos + rotation * offset;
            first.y = 0;
            second = first;
            if (pawn.Rotation == Rot4.North || pawn.story?.headType == null || !pawn.health.hediffSet.HasHead) return 0;
            int count = 0;
            foreach (BodyTypeDef.WoundAnchor anchor in pawn.story.bodyType.woundAnchors)
            {
                if ((anchor.tag != "LeftEye" && anchor.tag != "RightEye") || anchor.rotation != pawn.Rotation ||
                    (pawn.Rotation != Rot4.South && (anchor.narrowCrown == true) != pawn.story.headType.narrow)) continue;
                Vector3 eyeOffset;
                float range;
                PawnDrawUtility.CalcAnchorData(pawn, anchor, pawn.Rotation, out eyeOffset, out range);
                // The same local offset used by the vanilla eye render nodes.
                Vector3 point = pawn.DrawPos + rotation * (offset + eyeOffset + new Vector3(0, 0, -0.25f));
                point.y = 0;
                if (count++ == 0) first = point;
                else { second = point; break; }
            }
            return count;
        }

        public static bool EnsureMaterial()
        {
            if (material != null) return true;
            if (attemptedLoad) return false;
            attemptedLoad = true;
            Shader shader = null;
#if RIMWORLD_1_7
            shader = DefDatabase<ShaderDef>.GetNamed("BX_SoulDrain").Shader;
            AshenShader = DefDatabase<ShaderDef>.GetNamed("BX_SoulDrainAshen").Shader;
            SmokeTexture = ContentFinder<Texture2D>.Get("Effects/BX_SoulDrainSmoke_Uncompressed");
#else
            AbilityDef def = DefDatabase<AbilityDef>.GetNamed("BX_SoulFeeding");
            foreach (AssetBundle bundle in def.modContentPack.assetBundles.loadedAssetBundles)
            {
                shader = bundle.LoadAsset<Shader>("Assets/Shaders/SoulDrain.shader");
                if (shader == null) continue;
                AshenShader = bundle.LoadAsset<Shader>("Assets/Shaders/SoulDrainAshen.shader");
                SmokeTexture = bundle.LoadAsset<Texture2D>("Assets/Textures/SoulDrainSmoke.png");
                break;
            }
#endif
            if (shader == null || !shader.isSupported || AshenShader == null || !AshenShader.isSupported || SmokeTexture == null)
            {
                Log.Error("[Bernael] Soul Drain shader unavailable. Check the mod's version-specific shader assets and restart RimWorld.");
                return false;
            }
            material = new Material(shader) { name = "Bernael Soul Drain (shared)" };
            material.SetTexture("_SmokeTex", SmokeTexture);
            quad = new Mesh { name = "Soul Drain UV plane" };
            quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f),
                new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.RecalculateBounds();
            quad.UploadMeshData(true);
            soulGrid = CreateSoulGrid();
            return true;
        }

        private static Mesh CreateSoulGrid()
        {
            const int cells = 64;
            int side = cells + 1;
            var vertices = new Vector3[side * side];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[cells * cells * 6];
            for (int y = 0; y <= cells; y++) for (int x = 0; x <= cells; x++)
            {
                int i = y * side + x;
                uv[i] = new Vector2(x / (float)cells, y / (float)cells);
                vertices[i] = new Vector3((uv[i].x - 0.5f) * SoulDrainPawnCapture.WorldSize, 0,
                    (uv[i].y - 0.5f) * SoulDrainPawnCapture.WorldSize);
                if (x == cells || y == cells) continue;
                int t = (y * cells + x) * 6;
                triangles[t] = i; triangles[t+1] = i+side; triangles[t+2] = i+side+1;
                triangles[t+3] = i; triangles[t+4] = i+side+1; triangles[t+5] = i+1;
            }
            var mesh = new Mesh { name = "Soul Drain deforming silhouette", vertices = vertices, uv = uv, triangles = triangles };
            // Vertex shader attraction can extend beyond the captured pawn's original bounds.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(24, 2, 24));
            mesh.UploadMeshData(true);
            return mesh;
        }
    }

    public sealed class SoulDrainLink : IExposable, IDisposable
    {
        public Pawn caster, victim;
        public Ability ability;
        public int startTick, endTick = -1;
        public bool committed;
        public bool prisonerFeeding;
        public int drainTicks = 120;
        private string snapshotPng;
        private Texture2D snapshot;
        private float snapshotAngle;
        private bool captureFailed;
        public Texture2D Snapshot => snapshot;
        public float SnapshotAngle => snapshotAngle;
        public readonly SoulDrainAshenMaterials AshenMaterials = new SoulDrainAshenMaterials();

        public float Progress(int now)
        {
            int sampleTick = !committed && endTick >= 0 ? Math.Min(now, endTick) : now;
            return Mathf.Clamp01((sampleTick - startTick) / (float)Math.Max(1, drainTicks));
        }

        public float DrainAmount(int now)
        {
            float amount = Mathf.SmoothStep(0, 1, Progress(now));
            if (!committed && endTick >= 0)
                amount *= 1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01((now - endTick) / 12f));
            return amount;
        }

        public bool EnsureSnapshot()
        {
            if (snapshot != null) return true;
            if (captureFailed) return false;
            try
            {
                if (!snapshotPng.NullOrEmpty())
                {
                    snapshot = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(snapshot, Convert.FromBase64String(snapshotPng)))
                        throw new InvalidOperationException("Saved soul silhouette could not be decoded");
                    snapshot.wrapMode = TextureWrapMode.Clamp;
                    snapshot.filterMode = FilterMode.Bilinear;
                }
                else
                {
                    snapshotAngle = victim.Drawer.renderer.BodyAngle(PawnRenderFlags.None);
                    snapshot = SoulDrainPawnCapture.Capture(victim);
                }
                return true;
            }
            catch (Exception error)
            {
                captureFailed = true;
                Dispose();
                Log.Error("[Bernael] Soul Drain silhouette capture failed: " + error);
                return false;
            }
        }

        public void Dispose()
        {
            AshenMaterials.Dispose();
            if (snapshot != null) UnityEngine.Object.Destroy(snapshot);
            snapshot = null;
        }

        public bool OnMap(Map map) => caster != null && victim != null && caster.Spawned && victim.Spawned &&
            caster.Map == map && victim.Map == map;

        public bool IsChanneling()
        {
            if (prisonerFeeding)
                return caster.jobs?.curDriver is JobDriver_PrisonerSoulFeed driver && driver.IsDraining(victim);
            return ability != null && ability.verb.WarmingUp && ability.verb.CurrentTarget.Pawn == victim;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref victim, "victim");
            Scribe_References.Look(ref ability, "ability");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref endTick, "endTick", -1);
            Scribe_Values.Look(ref committed, "committed");
            Scribe_Values.Look(ref prisonerFeeding, "prisonerFeeding");
            Scribe_Values.Look(ref drainTicks, "drainTicks", 120);
            if (Scribe.mode == LoadSaveMode.Saving && snapshot != null && snapshotPng.NullOrEmpty())
                snapshotPng = Convert.ToBase64String(ImageConversion.EncodeToPNG(snapshot));
            Scribe_Values.Look(ref snapshotPng, "soulSilhouette");
            Scribe_Values.Look(ref snapshotAngle, "soulBodyAngle");
        }
    }
}

using RimWorld;
using Verse;
using Verse.Sound;
using UnityEngine;

namespace Bernael_Xenotype
{
    public class CursedFire : Fire
    {
        private int ticksSinceSpawn;
        private Sustainer sustainer;

        public override string Label
        {
            get
            {
                if (parent != null)
                {
                    return "BX_CursedFireOn".Translate(parent.LabelCap, parent);
                }
                return def.label;
            }
        }

        public override string InspectStringAddon => "BX_CursedBurning".Translate() + " (" + "FireSizeLower".Translate((fireSize * 100f).ToString("F0")) + ")";

        public override int UpdateRateTicks => 15;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksSinceSpawn, "ticksSinceSpawn", 0);
            Scribe_Values.Look(ref fireSize, "fireSize", 0.1f);
            Scribe_References.Look(ref instigator, "instigator");
        }

        protected override void TickInterval(int delta)
        {
            ticksSinceSpawn += delta;
            float maxLifetime = 600f + (fireSize * 3000f); 
            if (ticksSinceSpawn >= maxLifetime)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }
            if ((sustainer == null || sustainer.Ended) && !Position.Fogged(Map))
            {
                SoundInfo info = SoundInfo.InMap(new TargetInfo(Position, Map), MaintenanceType.PerTick);
                sustainer = SustainerAggregatorUtility.AggregateOrSpawnSustainerFor(this, SoundDefOf.FireBurning, info);
            }
            if (parent == null && Spawned)
            {
                Pawn steppingPawn = Map.thingGrid.ThingAt<Pawn>(Position);
                if (steppingPawn != null && steppingPawn.Spawned && !steppingPawn.Dead)
                {
                    AttachTo(steppingPawn);
                }
            }
            if (parent != null && this.IsHashIntervalTick(150, delta))
            {
                DoCursedDamage(parent);
            }
            if (Spawned)
            {
                float coldPower = fireSize * -60f;
                GenTemperature.PushHeat(Position, Map, coldPower);
                
                fireSize += 0.0002f * delta;
                if (fireSize > MaxFireSize) 
                {
                    fireSize = MaxFireSize;
                }
            }
        }

        private void DoCursedDamage(Thing target)
        {
            if (target == null || target.Destroyed) return;
            int damageAmount = GenMath.RoundRandom(Mathf.Clamp(0.0125f + 0.0036f * fireSize, 0.0125f, 0.05f) * 150f);
            if (damageAmount < 1) damageAmount = 1;
            DamageInfo dinfo = new DamageInfo(DamageDefOf.Frostbite, damageAmount, 0f, -1f, instigator ?? this);
            dinfo.SetBodyRegion(BodyPartHeight.Undefined, BodyPartDepth.Outside);
            target.TakeDamage(dinfo);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            sustainer?.End();
            base.DeSpawn(mode);
        }
    }
}
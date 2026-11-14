using Verse;

namespace Bernael_Xenotype
{
    public class HediffCompProperties_NoPathCost : HediffCompProperties
    {
        public HediffCompProperties_NoPathCost()
        {
            compClass = typeof(HediffComp_NoPathCost);
        }
    }

    public class HediffComp_NoPathCost : HediffComp
    {
        public override void CompExposeData()
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                WorldComponent_NoPathCost.Add(Pawn);
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            WorldComponent_NoPathCost.Add(Pawn);
        }

        public override void CompPostPostRemoved()
        {
            bool hasOtherComp = false;
            if (Pawn.health?.hediffSet?.hediffs != null)
            {
                foreach (var hediff in Pawn.health.hediffSet.hediffs)
                {
                    if (hediff != parent && hediff.TryGetComp<HediffComp_NoPathCost>() != null)
                    {
                        hasOtherComp = true;
                        break;
                    }
                }
            }
            if (!hasOtherComp)
            {
                WorldComponent_NoPathCost.Remove(Pawn);
            }
        }
    }
}

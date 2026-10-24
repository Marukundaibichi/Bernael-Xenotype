using RimWorld;
using Verse;

namespace Bernael_Xenotype
{
    public class ThoughtWorker_Warlockracy : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn other)
        {
            if (p == null || other == null)
            {
                return ThoughtState.Inactive;
            }

            if (!p.RaceProps.Humanlike || !other.RaceProps.Humanlike)
            {
                return ThoughtState.Inactive;
            }

            if (p == other)
            {
                return ThoughtState.Inactive;
            }

            // Check if initiator possesses Warlockracy gene
            if (p.genes == null || !p.genes.HasActiveGene(BernaelDefOf.BX_Warlockracy))
            {
                return ThoughtState.Inactive;
            }

            // Retrieve target pawn's Psylink level
            int psylinkLevel = other.GetPsylinkLevel();

            // Mapping Psylink level to XML Thought Stage Index
            if (psylinkLevel <= 0)
            {
                return ThoughtState.ActiveAtStage(0); // Non-psionic (-15)
            }

            // Stage index for Psylink level 1..6+
            int stageIndex = UnityEngine.Mathf.Clamp(psylinkLevel, 1, 6);
            return ThoughtState.ActiveAtStage(stageIndex);
        }
    }
}
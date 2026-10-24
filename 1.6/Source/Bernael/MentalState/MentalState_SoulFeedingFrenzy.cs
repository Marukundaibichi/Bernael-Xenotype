using RimWorld;
using Verse;
using Verse.AI;

namespace Bernael_Xenotype
{
    public class MentalState_SoulFeedingFrenzy : MentalState
    {
        private int lastValidTargetTick = -1;

        protected override bool CanEndBeforeMaxDurationNow => false;

        public override RandomSocialMode SocialModeMax()
        {
            return RandomSocialMode.Off;
        }

        public override void PreStart()
        {
            if (pawn?.abilities != null)
            {
                if (pawn.abilities.GetAbility(BernaelDefOf.BX_SoulFeeding) == null)
                {
                    pawn.abilities.GainAbility(BernaelDefOf.BX_SoulFeeding);
                }
            }
        }

        public override void PostEnd()
        {
            base.PostEnd();
            if (pawn?.abilities != null)
            {
                Ability ability = pawn.abilities.GetAbility(BernaelDefOf.BX_SoulFeeding);
                if (ability != null && !pawn.genes.HasActiveGene(BernaelDefOf.BX_SoulStarved))
                {
                    pawn.abilities.RemoveAbility(BernaelDefOf.BX_SoulFeeding);
                }
            }
        }

        public override void MentalStateTick(int delta)
        {
            base.MentalStateTick(delta);
            if (pawn.IsHashIntervalTick(30, delta))
            {
                if (pawn.Dead || pawn.Downed || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
                {
                    RecoverFromState();
                    return;
                }
                Gene_Soul soulGene = pawn.genes?.GetFirstGeneOfType<Gene_Soul>();
                if (soulGene != null && soulGene.Active && soulGene.Value >= 0.99f)
                {
                    RecoverFromState();
                    return;
                }
            }
        }

        public void Notify_AbilityCastFinished()
        {
            if (pawn?.genes?.Xenotype != null && ModsConfig.IsActive("Sov.Nephilim") && pawn.genes.Xenotype == BernaelDefOf.GS_Nephilim)
            {
                XenotypeDef oldXenotype = BernaelDefOf.GS_Nephilim;
                if (!oldXenotype.genes.NullOrEmpty())
                {
                    var oldGeneDefs = oldXenotype.genes;
                    if (pawn.genes.Endogenes != null)
                    {
                        for (int i = pawn.genes.Endogenes.Count - 1; i >= 0; i--)
                        {
                            if (oldGeneDefs.Contains(pawn.genes.Endogenes[i]?.def))
                                pawn.genes.RemoveGene(pawn.genes.Endogenes[i]);
                        }
                    }
                    if (pawn.genes.Xenogenes != null)
                    {
                        for (int i = pawn.genes.Xenogenes.Count - 1; i >= 0; i--)
                        {
                            if (oldGeneDefs.Contains(pawn.genes.Xenogenes[i]?.def))
                                pawn.genes.RemoveGene(pawn.genes.Xenogenes[i]);
                        }
                    }
                }
                pawn.genes.SetXenotype(BernaelDefOf.BX_Bernael);
                SendIncidentLetter(pawn);
            }
            RecoverFromState();
        }

        private void SendIncidentLetter(Pawn transformedPawn)
        {
            if (PawnUtility.ShouldSendNotificationAbout(transformedPawn))
            {
                TaggedString letterLabel = "BX_MentalBreak.label".Translate(transformedPawn.LabelShort, transformedPawn.Named("PAWN"));
                TaggedString letterText = "BX_MentalBreak.text".Translate(transformedPawn.LabelShort, transformedPawn.Named("PAWN"));
                ChoiceLetter letter = LetterMaker.MakeLetter(letterLabel, letterText, LetterDefOf.PositiveEvent, transformedPawn);
                Find.LetterStack.ReceiveLetter(letter);
            }
        }

        public override bool ForceHostileTo(Faction f)
        {
            return true;
        }

        public override bool ForceHostileTo(Thing t)
        {
            return t != pawn;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastValidTargetTick, "lastValidTargetTick", -1);
        }
    }
}
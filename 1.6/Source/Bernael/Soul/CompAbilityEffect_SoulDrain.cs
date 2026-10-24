using System;
using RimWorld;
using System.Collections.Generic;
using Verse;
namespace Bernael_Xenotype
{
    public class CompAbilityEffect_SoulDrain : CompAbilityEffect
    {
        private new CompProperties_AbilitySoulDrain Props => (CompProperties_AbilitySoulDrain)props;

        public override IEnumerable<Mote> CustomWarmupMotes(LocalTargetInfo target)
        {
            SoulDrainVisuals.Begin(parent.pawn, target.Pawn, parent);
            yield break;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = target.Pawn;
            if (pawn == null || pawn.Dead)
            {
                return;
            }
            if (SoulUtility.IsSoulFeeder(pawn))
            {
                return;
            }
            if (SoulUtility.TryConvertBaby(parent.pawn, pawn))
            {
                return;
            }
            SoulUtility.DoDrain(parent.pawn, pawn, Props.soulGain, Props.resistanceGain, Props.hediffToGiveTarget, Props.hediffSeverity, Props.thoughtDefToGiveTarget, Props.opinionThoughtDefToGiveTarget);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target);
        }

        public override void PostApplied(List<LocalTargetInfo> targets, Map map)
        {
            if (parent?.pawn != null && parent.pawn.InMentalState && parent.pawn.MentalState is MentalState_SoulFeedingFrenzy frenzyState)
            {
                frenzyState.Notify_AbilityCastFinished();
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null || pawn.Dead)
            {
                return false;
            }
            if (!AbilityUtility.ValidateMustBeHumanOrWildMan(pawn, throwMessages, parent))
            {
                return false;
            }
            if (SoulUtility.IsSoulFeeder(pawn))
            {
                if (throwMessages)
                {
                    Messages.Message("BX_MessageCannotUseOnSoulFeeder".Translate(parent.def.Named("ABILITY")), pawn, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            if (pawn.Faction != null && !pawn.IsSlaveOfColony && !pawn.IsPrisonerOfColony)
            {
                if (pawn.Faction.HostileTo(parent.pawn.Faction))
                {
                    if (!pawn.Downed)
                    {
                        if (throwMessages)
                        {
                            Messages.Message("MessageCantUseOnResistingPerson".Translate(parent.def.Named("ABILITY")), pawn, MessageTypeDefOf.RejectInput, false);
                        }
                        return false;
                    }
                }
                else if (pawn.IsQuestLodger() || pawn.Faction != parent.pawn.Faction)
                {
                    if (throwMessages)
                    {
                        Messages.Message("MessageCannotUseOnOtherFactions".Translate(parent.def.Named("ABILITY")), pawn, MessageTypeDefOf.RejectInput, false);
                    }
                    return false;
                }
            }
            if (pawn.IsWildMan() && !pawn.IsPrisonerOfColony && !pawn.Downed || pawn.InMentalState || PrisonBreakUtility.IsPrisonBreaking(pawn))
            {
                if (throwMessages)
                {
                    Messages.Message("MessageCantUseOnResistingPerson".Translate(parent.def.Named("ABILITY")), pawn, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            if (!ModsConfig.AnomalyActive || !pawn.IsMutant || pawn.mutant.Def.canBleed) return true;
            if (throwMessages)
            {
                Messages.Message("MessageCannotUseOnNonBleeder".Translate(parent.def.Named("ABILITY")), pawn, MessageTypeDefOf.RejectInput, false);
            }
            return false;
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null) return base.ExtraLabelMouseAttachment(target);
            string text = null;
            if (pawn.HostileTo(parent.pawn) && !pawn.Downed)
            {
                text += "MessageCantUseOnResistingPerson".Translate(parent.def.Named("ABILITY"));
            }
            float num = HediffSeverityAfterAbility(pawn);
            if (num >= Props.hediffToGiveTarget.lethalSeverity)
            {
                if (!text.NullOrEmpty())
                {
                    text += "\n";
                }
                text += "WillKill".Translate();
            }
            else if (Props.hediffToGiveTarget.stages[Props.hediffToGiveTarget.StageAtSeverity(num)].lifeThreatening)
            {
                if (!text.NullOrEmpty())
                {
                    text += "\n";
                }
                text += "BX_WillCauseSeriousSoulDrain".Translate();
            }
            return text;
        }

        public override Window ConfirmationDialog(LocalTargetInfo target, Action confirmAction)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null) return null;
            if (pawn.genes != null && pawn.genes.HasActiveGene(GeneDefOf.Deathless))
            {
                return null;
            }
            float num = HediffSeverityAfterAbility(pawn);
            if (num >= Props.hediffToGiveTarget.lethalSeverity)
            {
                return Dialog_MessageBox.CreateConfirmation("BX_WarningPawnWillDieFromFeeding".Translate(pawn.Named("PAWN")), confirmAction, true);
            }
            return Props.hediffToGiveTarget.stages[Props.hediffToGiveTarget.StageAtSeverity(num)].lifeThreatening ? Dialog_MessageBox.CreateConfirmation("BX_WarningPawnWillHaveSeriousSoulDrainFromFeeding".Translate(pawn.Named("PAWN")), confirmAction, true) : null;
        }

        private float HediffSeverityAfterAbility(Pawn target)
        {
            if (target.Dead || !target.RaceProps.IsFlesh)
            {
                return 0f;
            }
            float num = Props.hediffSeverity;
            Hediff firstHediffOfDef = target.health.hediffSet.GetFirstHediffOfDef(Props.hediffToGiveTarget);
            if (firstHediffOfDef != null)
            {
                num += firstHediffOfDef.Severity;
            }
            return num;
        }
    }
}

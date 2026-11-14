using System.Collections.Generic;
using Verse;
using UnityEngine;

namespace Bernael_Xenotype
{
    public class HediffCompProperties_FoldShiftVisual : HediffCompProperties
    {
        public Color visualColor = Color.white;

        public HediffCompProperties_FoldShiftVisual()
        {
            compClass = typeof(HediffComp_FoldShiftVisual);
        }
    }

    public class HediffComp_FoldShiftVisual : HediffComp
    {
        public HediffCompProperties_FoldShiftVisual Props => (HediffCompProperties_FoldShiftVisual)props;
        private FoldShiftVisuals visuals;
        private int startedTick = -1;

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref startedTick, "foldVisualStarted", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                WorldComponent_FoldShiftVisual.Add(Pawn);
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            startedTick = Find.TickManager.TicksGame;
            Pawn.Map?.GetComponent<FoldVisualMapComponent>().CancelExit(Pawn);

            WorldComponent_FoldShiftVisual.Add(Pawn);
        }

        public override void CompPostPostRemoved()
        {
            // Traverse the Fold adds a second shadow hediff on top of Fold Shift, and the two end in the same tick.
            // While one remains it inherits this shadow, so only the last plays the exit, with captures that are
            // already made; ending twice used to show the pawn for a few frames, then a freshly captured shadow.
            HediffComp_FoldShiftVisual next = Pawn.Dead ? null : Remaining();
            if (next != null)
            {
                if (next.visuals == null)
                {
                    next.visuals = visuals;
                    if (startedTick >= 0 && (next.startedTick < 0 || startedTick < next.startedTick))
                        next.startedTick = startedTick;
                }
                else visuals?.Dispose();
            }
            else if (Pawn.Spawned && !Pawn.Dead)
                Pawn.Map.GetComponent<FoldVisualMapComponent>().EndShift(Pawn, visuals);
            else visuals?.Dispose();
            visuals = null;

            bool hasOtherComp = false;
            if (Pawn.health?.hediffSet?.hediffs != null)
            {
                foreach (var hediff in Pawn.health.hediffSet.hediffs)
                {
                    if (hediff != parent && hediff.TryGetComp<HediffComp_FoldShiftVisual>() != null)
                    {
                        hasOtherComp = true;
                        break;
                    }
                }
            }
            if (!hasOtherComp)
            {
                WorldComponent_FoldShiftVisual.Remove(Pawn);
            }
        }

        private HediffComp_FoldShiftVisual Remaining()
        {
            List<Hediff> hediffs = Pawn.health?.hediffSet?.hediffs;
            if (hediffs == null) return null;
            foreach (Hediff hediff in hediffs)
                if (hediff != parent && hediff is HediffWithComps withComps && withComps.comps != null)
                    foreach (HediffComp comp in withComps.comps)
                        if (comp is HediffComp_FoldShiftVisual visual) return visual;
            return null;
        }

        // Seconds since the shift began; a shift restored from a save shows its finished shadow at once.
        public float Seconds
        {
            get
            {
                if (startedTick < 0) startedTick = Find.TickManager.TicksGame - 120;
                return (Find.TickManager.TicksGame - startedTick) / 60f;
            }
        }

        // Draws the shadow. hidesPawn is false while it still rises over the pawn, which the game draws beneath it.
        public bool Draw(Vector3 drawLoc, Pawn pawn, Rot4? facing, out bool hidesPawn)
        {
            hidesPawn = false;
            visuals ??= new FoldShiftVisuals();
            float opacity = pawn.Map?.GetComponent<FoldGateMapComponent>().Opacity(pawn, drawLoc) ?? 1f;
            float seconds = Seconds;
            if (!visuals.Draw(pawn, drawLoc, facing ?? pawn.Rotation, seconds, opacity, Props.visualColor.a))
                return false;
            pawn.Map?.GetComponent<FoldVisualMapComponent>().Track(pawn, visuals);
            hidesPawn = seconds >= FoldShiftVisuals.EntrySeconds;
            return true;
        }
    }
}

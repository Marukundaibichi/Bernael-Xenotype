using System.Collections.Generic;
using Verse;
using UnityEngine;
using HarmonyLib;

namespace Bernael_Xenotype
{
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
    public static class Patch_PawnRenderer_RenderPawnAt
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn ___pawn, Vector3 drawLoc, Rot4? rotOverride, bool neverAimWeapon)
        {
            if (FoldShiftVisuals.Capturing || ___pawn == null || !___pawn.Spawned || ___pawn.Position.Fogged(___pawn.Map)) return true;
            if (___pawn?.health?.hediffSet?.hediffs == null) return true;
            float traversalOpacity = ___pawn.Map.GetComponent<FoldGateMapComponent>().Opacity(___pawn, drawLoc);
            if (traversalOpacity < 0.01f) return false;

            List<Hediff> hediffs = ___pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is HediffWithComps hediffWithComps && hediffWithComps.comps != null)
                {
                    for (int j = 0; j < hediffWithComps.comps.Count; j++)
                    {
                        if (hediffWithComps.comps[j] is HediffComp_FoldShiftVisual visualComp)
                        {
                            if (!visualComp.Draw(drawLoc, ___pawn, rotOverride, out bool hidesPawn)) return true;
                            // While the shadow still rises over the pawn, the game draws the pawn beneath it.
                            if (!hidesPawn) return true;
                            // The captured render tree contains clothes, but held weapons and
                            // worn extras are drawn separately by the game and must remain visible.
                            Rot4 facing = rotOverride ?? ___pawn.Rotation;
                            Vector3 equipmentPosition = drawLoc;
                            equipmentPosition.y = FoldShiftVisuals.ShadowAltitudeAt(drawLoc) +
                                PawnRenderUtility.AltitudeForLayer(facing == Rot4.North ? -10f : 90f);
                            if (traversalOpacity > 0.95f)
                                PawnRenderUtility.DrawEquipmentAndApparelExtras(___pawn, equipmentPosition, facing,
                                    neverAimWeapon ? PawnRenderFlags.NeverAimWeapon : PawnRenderFlags.None);
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}

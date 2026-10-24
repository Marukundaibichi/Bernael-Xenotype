using System;
using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;

namespace Bernael_Xenotype
{
    public class MentalBreakWorker_SoulFeedingFrenzy : MentalBreakWorker
    {
        public override bool BreakCanOccur(Pawn pawn)
        {
		    if (!pawn.Spawned)
		    {
		    	return false;
		    }
            if (!base.BreakCanOccur(pawn))
            {
                return false;
            }
            if (pawn.abilities == null)
            {
                return false;
            }

            Gene_Soul soulGene = pawn.genes?.GetFirstGeneOfType<Gene_Soul>();
            if (soulGene != null && soulGene.Active && soulGene.Value > 0f)
            {
                return false;
            }

            if (ModsConfig.IsActive("Sov.Nephilim") && pawn.genes != null)
            {
                Type resourceGeneType = AccessTools.TypeByName("EBSGFramework.ResourceGene");
                if (resourceGeneType != null)
                {
                    foreach (var gene in pawn.genes.GenesListForReading)
                    {
                        if (gene == null || !resourceGeneType.IsAssignableFrom(gene.GetType())) continue;
                        if (gene is Gene_Resource resourceGene && resourceGene.Active && resourceGene.Value > 0f)
                        {
                            return false;
                        }
                    }
                }
            }
            return HasValidSoulFeedingTarget(pawn);
        }

        private bool HasValidSoulFeedingTarget(Pawn pawn)
        {
            Map mapHeld = pawn.MapHeld;
            if (mapHeld == null)
            {
                return false;
            }

            int num = GenRadial.NumCellsInRadius(19.9f);
            for (int i = 0; i < num; i++)
            {
                IntVec3 intVec = pawn.Position + GenRadial.RadialPattern[i];
                if (intVec.InBounds(mapHeld) && !intVec.Fogged(mapHeld))
                {
                    var thingList = intVec.GetThingList(mapHeld);
                    for (int j = 0; j < thingList.Count; j++)
                    {
                        if (thingList[j] is Pawn target && target != pawn && !target.Dead && target.Spawned && target.RaceProps.Humanlike)
                        {
                            if (SoulUtility.IsSoulFeeder(target))
                            {
                                continue;
                            }
                            if (target.Faction != null && target.Faction.HostileTo(pawn.Faction) && !target.IsSlaveOfColony && !target.IsPrisonerOfColony && !target.Downed)
                            {
                                continue;
                            }
                            if (GenSight.LineOfSight(pawn.Position, intVec, mapHeld, skipFirstCell: true))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}
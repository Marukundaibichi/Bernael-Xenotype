using RimWorld;
using Verse;
using Verse.AI;

namespace Bernael_Xenotype
{
    public class JobGiver_SoulFeedingFrenzy : ThinkNode_JobGiver
    {
        private float maxSearchDistance = 45f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null || pawn.Downed || !pawn.Awake() || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
            {
                return null;
            }

            Ability ability = pawn.abilities?.GetAbility(BernaelDefOf.BX_SoulFeeding);
            if (ability == null || !ability.CanCast)
            {
                return null;
            }

            Pawn targetPawn = FindSoulFeedingTarget(pawn, ability);
            if (targetPawn != null)
            {
                LocalTargetInfo targetInfo = new LocalTargetInfo(targetPawn);
                if (pawn.Position.InHorDistOf(targetPawn.Position, ability.def.verbProperties.range))
                {
                    Job job = ability.GetJob(targetInfo, targetInfo);
                    job.playerForced = true;
                    job.ignoreForbidden = true;
                    return job;
                }
                else
                {
                    Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, targetPawn);
                    gotoJob.locomotionUrgency = LocomotionUrgency.Sprint;
                    gotoJob.expiryInterval = 180;
                    gotoJob.canBashDoors = true;
                    return gotoJob;
                }
            }

            return null;
        }

        private Pawn FindSoulFeedingTarget(Pawn caster, Ability ability)
        {
            return (Pawn)AttackTargetFinder.BestAttackTarget(
                caster, 
                TargetScanFlags.NeedReachable, 
                t => IsGoodSoulTarget(caster, ability, t), 
                0f, 
                maxSearchDistance, 
                default, 
                float.MaxValue, 
                canBashDoors: true
            );
        }

        private bool IsGoodSoulTarget(Pawn caster, Ability ability, Thing thing)
        {
            if (thing is Pawn target && target != null && !target.Dead && target.Spawned && !target.Downed && target.RaceProps.Humanlike)
            {
                if (target == caster) return false;
                return ability.CanApplyOn(new LocalTargetInfo(target));
            }
            return false;
        }

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_SoulFeedingFrenzy obj = (JobGiver_SoulFeedingFrenzy)base.DeepCopy(resolve);
            obj.maxSearchDistance = maxSearchDistance;
            return obj;
        }
    }
}
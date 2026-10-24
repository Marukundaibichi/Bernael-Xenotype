using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using Verse.AI;

namespace Bernael_Xenotype
{
    [DefOf]
    public class BernaelDefOf
    {
        public BernaelDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BernaelDefOf));
        }

        public static GeneDef BX_DarkSpeech;
        public static GeneDef BX_AbyssalReach;
        public static GeneDef BX_DepravedHead;
        public static GeneDef BX_BernaelBlind;
        public static GeneDef BX_Warlockracy;
        public static TaleDef BX_ConsumedGrace;
        public static XenotypeDef BX_Bernael;
        public static HediffDef BX_Blindness;
        public static HediffDef BX_SoulDrained;
        public static HediffDef BX_SoulCraving;
        public static HediffDef BX_ConsumedSoulStrength;
        public static HediffDef BX_BabyBond_Feeder;
        public static HediffDef BX_BabyBond_Victim;
        public static HediffDef BX_GazeCaligoBuff;
        public static HediffDef BX_GazeCaligoDebuff;
        public static HediffDef BX_PsychicOverchargeHediff;
        public static HediffDef BX_MaliciousSeal;
        public static DamageDef BX_CursedFlame;
        public static AbilityDef BX_SoulFeeding;
        public static ThoughtDef BX_ConsumedGraceThoughtMood;
        public static ThoughtDef BX_HeardDarkSpeech;
        public static ThoughtDef BX_FedOn;
        public static ThoughtDef BX_FedOn_Social;

        public static HistoryEventDef BX_ExtractedSoul;

        public static StatDef BX_SoulGainFactor;
        public static GeneDef BX_SoulStarved;
        public static GeneDef BX_SoulFeeder;

        public static ThingDef BX_WingBurstSmoke;
        public static ThingDef BX_WingBurstFlyer;
        public static ThingDef BX_BottledSoul;
        public static ThingDef BX_LifeVanquisher;
        public static RecipeDef BX_ExtractSoul;

        public static JobDef BX_PrisonerSoulFeed;
        public static JobDef BX_BogeymanKidnap;

        public static DutyDef BX_Kidnap;

        public static IncidentDef BX_GazeCaligo;
        public static IncidentDef BX_AstralAwakening;
        public static IncidentDef BX_EthersBlessing;
        public static IncidentDef BX_PsycasterPilgrimage;
        public static IncidentDef BX_PsychicOvercharge;
        public static IncidentDef BX_PsychicBurnout;
        public static IncidentDef BX_PsychicRupture;
        public static IncidentDef BX_Bogeyman;

        public static PawnKindDef BX_Bernael_Warlock;

        public static SoundDef BX_Ambient_Gaze;
        public static SoundDef BX_Gaze_Blink;
        public static SoundDef BX_Gaze_Move;

        [MayRequire("Sov.Nephilim")]
        public static GeneDef GS_Grace_New;
        [MayRequire("Sov.Nephilim")]
        public static GeneDef BX_Grace_Galling;
        [MayRequire("Sov.Nephilim")]
        public static GeneDef BX_GracePendulum;

        [MayRequire("Sov.Nephilim")]
        public static XenotypeDef GS_Nephilim;

        [MayRequire("Sov.Nephilim")]
        public static HediffDef BX_Mutation_Hediff;
    }
}

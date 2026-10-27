using HarmonyLib;
using Verse;

namespace Bernael_Xenotype
{
    [StaticConstructorOnStartup]
    public static class Main_HarmonyInitializer
    {
        static Main_HarmonyInitializer()
        {
            Harmony harmony = new Harmony("BernaelXenotype.Harmony.SentinelPatches");
            harmony.PatchAll(typeof(Main_HarmonyInitializer).Assembly);
        }
    }
}

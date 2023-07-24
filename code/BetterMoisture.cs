using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Vintagestory.API.MathTools;
using System;
using System.Reflection;
using System.Linq;

// Testing commands:
// /weather setprecip 1
// /time add 46 hour

namespace BetterMoisture
{
    public class BetterMoistureMod : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.Logger.Debug("[BetterMoistureMod] Start");
            base.Start(api);

            var harmony = new Harmony("me.amzd.bettermoisture");

            // Since there is multiple private `updateMoistureLevel` functions we need to get the specific one by filtering by parameters.
            var originalUpdateMoistureLevel = typeof(BlockEntityFarmland).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).First(
                m => m.Name == "updateMoistureLevel" && m.GetParameters().Length == 4
            );

            harmony.Patch(originalUpdateMoistureLevel, prefix: typeof(BetterMoistureMod).GetMethod("newUpdateMoistureLevel"));
        }

        // TODO: Make these configurable
        static float[] moistureValues = { 1f, 0.8f, 0.6f, 0f };
        static double hoursToFullyDryOut = 48f;
        static float rainImpact = 0.33f;

        /// <summary>
        ///
        /// </summary>
        /// <param name="waterDistance">The distance to water if under 4, otherwise 99. (1, 2, 3, 99) See `BlockEntityFarmland.GetNearbyWaterDistance`.</param>
        /// <returns>true if it was a longer interval check (checked rain as well) so that an UpdateFarmlandBlock() is advisable. If it returns true for short interval checks watering breaks!</returns>
        public static bool newUpdateMoistureLevel(BlockEntityFarmland __instance, ref bool __result, double totalDays, float waterDistance, bool skyExposed, ClimateCondition baseClimate)
        {
            float moistureLevel = __instance.GetField<float>("moistureLevel");
            double lastMoistureLevelUpdateTotalDays = __instance.GetField<double>("lastMoistureLevelUpdateTotalDays");

            // Original: Uses totalDays parameter which sometimes is `Api.World.Calendar.TotalDays` and sometimes total
            //           days since last update which causes buggy behavior of farmland sometimes drying out in a burst.
            // BetterMoisture: Always use the `Api.World.Calendar.TotalDays` to have consistent dry out behavior.
            totalDays = __instance.Api.World.Calendar.TotalDays;

            // Lowest moisture per block away from water where the first value
            // is 1 block away from water (so touching water).
            // Original %:        75, 50, 25, 0
            // BetterMoisture %: 100, 80, 60, 0
            float moistureFromWaterDistance = moistureValues[GameMath.Clamp((int)waterDistance - 1, 0, moistureValues.Length - 1)];


            // Note: This Math.Min also means we will never check rain further back than `hoursToFullyDryOut`
            double hoursPassed = Math.Min((totalDays - lastMoistureLevelUpdateTotalDays) * __instance.Api.World.Calendar.HoursPerDay, hoursToFullyDryOut);

            if (hoursPassed < 0.1f)
            {
                // Only get wet from a water source, no drying or rain calculation
                __instance.SetField("moistureLevel", Math.Max(moistureLevel, moistureFromWaterDistance));
                __result = false;

                if (lastMoistureLevelUpdateTotalDays > totalDays)
                {
                    __instance.SetField("lastMoistureLevelUpdateTotalDays", totalDays);
                    __instance.Api.Logger.Warning("[BetterMoistureMod] Fixed a farmland block with too high lastMoistureLevelUpdateTotalDays. (Probably caused by buggy behavior of vanilla)" +
                        "\n\tActual total days: " + totalDays +
                        "\n\tFarmland lastMoistureLevelUpdateTotalDays: " + lastMoistureLevelUpdateTotalDays
                    );
                }

                // Skip original
                return false;
            }

            // Dry out
            // Original: Drying out speed was not influenced by water distance.
            // BetterMoisture: Drying out is proportional to how close to water the farmland is.
            //                 So it takes the same time for every block to dry to their lowest moisture value.
            moistureLevel = Math.Max(moistureFromWaterDistance, moistureLevel - (float)hoursPassed / (float)hoursToFullyDryOut * (1 - moistureFromWaterDistance));

            // Get wet from all the rainfall since last update
            // Original: Incorrect hour to day calculation caused this to not work at all.
            // BetterMoisture: Fixed bug of not receiving moisture from rain.
            if (skyExposed && moistureLevel < 1)
            {
                BlockFarmland blockFarmland = __instance.GetField<BlockFarmland>("blockFarmland");
                if (baseClimate == null && hoursPassed > 0)
                    baseClimate = __instance.Api.World.BlockAccessor.GetClimateAt(__instance.Pos, EnumGetClimateMode.WorldGenValues, totalDays - hoursPassed / __instance.Api.World.Calendar.HoursPerDay / 2);
                while (hoursPassed > 0 && moistureLevel < 1)
                {
                    double rainLevel = blockFarmland.wsys.GetPrecipitation(__instance.Pos, totalDays - hoursPassed / __instance.Api.World.Calendar.HoursPerDay, baseClimate);
                    moistureLevel = GameMath.Clamp(moistureLevel + (float)rainLevel * rainImpact, moistureFromWaterDistance, 1);
                    hoursPassed--;
                }
            }

            __instance.SetField("lastMoistureLevelUpdateTotalDays", totalDays);
            __instance.SetField("moistureLevel", moistureLevel);
            __result = true;

            // Original: The moisture percentage on the UI is almost always wrong (unless farmland visibly changed between moist and dry or when updated eg by watering can).
            // BetterMoisture: Mark farmland as dirty each moisture update. To reduce anticipated performance drops I increased the cooldown on calculations to 10 times per hour.
            __instance.Api.World.BlockAccessor.MarkBlockEntityDirty(__instance.Pos);

            // Skip original
            return false;
        }
    }

    /// https://github.com/Craluminum-Mods/ExtraInfo/blob/main/src/Util/HarmonyReflectionExtensions.cs
    public static class HarmonyReflectionExtensions
    {
        /// <summary>
        ///     Gets a field within the calling instanced object. This can be an internal or private field within another assembly.
        /// </summary>
        /// <typeparam name="T">The type of field to return.</typeparam>
        /// <param name="instance">The instance in which the field resides.</param>
        /// <param name="fieldName">The name of the field to return.</param>
        /// <returns>An object containing the value of the field, reflected by this instance.</returns>
        public static T GetField<T>(this object instance, string fieldName)
        {
            return (T)AccessTools.Field(instance.GetType(), fieldName).GetValue(instance);
        }

        /// <summary>
        ///     Sets a field within the calling instanced object. This can be an internal or private field within another assembly.
        /// </summary>
        /// <param name="instance">The instance in which the field resides.</param>
        /// <param name="fieldName">The name of the field to set.</param>
        /// <param name="setVal">The value to set the field to.</param>
        public static void SetField(this object instance, string fieldName, object setVal)
        {
            AccessTools.Field(instance.GetType(), fieldName).SetValue(instance, setVal);
        }
    }
}

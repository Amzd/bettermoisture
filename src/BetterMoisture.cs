using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Vintagestory.API.MathTools;
using System;

namespace BetterMoisture
{
    public class BetterMoistureMod : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.Logger.Debug("[BetterMoistureMod] Start");
            base.Start(api);

            var harmony = new Harmony("me.amzd.bettermoisture");

            var originalUpdateMoistureLevel = typeof(BlockEntityFarmland).GetMethod("updateMoistureLevel");

            harmony.Patch(originalUpdateMoistureLevel, prefix: typeof(BetterMoistureMod).GetMethod("updateMoistureLevel"));

        }

        bool updateMoistureLevel(BlockEntityFarmland __instance, ref bool __result, double totalDays, float waterDistance, bool skyExposed, ClimateCondition baseClimate = null)
        {
            float moistureLevel = __instance.GetField<float>("moistureLevel");
            float lastMoistureLevelUpdateTotalDays = __instance.GetField<float>("lastMoistureLevelUpdateTotalDays");

            // Lowest moisture per block away from water where the first value
            // is 1 block away from water (so touching water).
            // Original %: 75, 50, 25, 0
            // New %: 100, 90, 70, 50, 0
            float[] moistureValues = { 1f, 0.8f, 0.6f, 0.4f, 0f }; // TODO: Make this configurable
            float moistureFromWaterDistance = moistureValues[GameMath.Clamp((int)waterDistance - 1, 0, moistureValues.Length - 1)];

            // Why does is this capped to 48?
            // Is it due to a calculation issue?
            // Possibly can't get precipitation over 48 hours back?
            // Is that related to days? then this should maybe be Api.World.Calendar.HoursPerDay not 48.
            double hoursPassed = Math.Min((totalDays - lastMoistureLevelUpdateTotalDays) * __instance.Api.World.Calendar.HoursPerDay, 48);
            if (hoursPassed < 0.03f)
            {
                // Get wet from a water source
                moistureLevel = Math.Max(moistureLevel, moistureFromWaterDistance);
                __instance.SetField("moistureLevel", moistureLevel);
                __result = false;

                // Skip original
                return false;
            }

            // Dry out
            // Original: Drying out speed was not influenced by water distance.
            // New: Drying out is proportional to how close to water the farmland is.
            //      So it takes the same time for every block to dry to their lowest moisture value.
            float hoursToFullyDryOut = 48f; // TODO: Make this configurable
            moistureLevel = Math.Max(moistureFromWaterDistance, moistureLevel - (float)hoursPassed / hoursToFullyDryOut * (1 - moistureFromWaterDistance));

            // Get wet from all the rainfall since last update
            // Original: Incorrect hour to day calculation caused this to not work at all.
            // New:
            if (skyExposed)
            {
                BlockFarmland blockFarmland = __instance.GetField<BlockFarmland>("blockFarmland");
                float rainImpact = 0.33f; // TODO: Make this configurable
                if (baseClimate == null && hoursPassed > 0) baseClimate = __instance.Api.World.BlockAccessor.GetClimateAt(__instance.Pos, EnumGetClimateMode.WorldGenValues, totalDays - hoursPassed / __instance.Api.World.Calendar.HoursPerDay / 2);
                while (hoursPassed > 0)
                {
                    double rainLevel = blockFarmland.wsys.GetPrecipitation(__instance.Pos, totalDays - hoursPassed / __instance.Api.World.Calendar.HoursPerDay, baseClimate);
                    moistureLevel = GameMath.Clamp(moistureLevel + (float)rainLevel * rainImpact, moistureFromWaterDistance, 1);
                    hoursPassed--;
                }
            }

            __instance.SetField("lastMoistureLevelUpdateTotalDays", totalDays);
            __instance.SetField("moistureLevel", moistureLevel);
            __result = true;

            // Skip original
            return false;
        }
    }

    /// https://github.com/Craluminum-Mods/ExtraInfo/blob/main/src/Util/HarmonyReflectionExtensions.cs
    public static class HarmonyReflectionExtensions
    {
        #region Fields

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

        #endregion
    }
}

using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Datastructures;
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Util;

// /tp Amzd ~ ~200 ~
//
// A thermal will be surrounded by sinking air only in the absence of wind. In wind, the sink will be on the downwind side of the core.
//
// Thermal lift
// Ridge soaring
// Wave soaring? might be hard to impl and hard to read for players


namespace RealisticGlider
{
    public class RealisticGliderMod : ModSystem
    {

        public const double altglider_speedMid = 0.0025;
        public const double altglider_speedMax = 0.01;
        public const double altglider_glideSpeedDec = 0.000005;
        public const double maxHeightAboveRidge = 80;
        static int lastFoundRidgeHeight = -1;

        static ICoreAPI _api;
        public override void Start(ICoreAPI api)
        {
            api.Logger.Debug("[RealisticGlider] Start");
            _api = api;
            base.Start(api);

            var harmony = new Harmony("me.amzd.RealisticGlider");
            harmony.UnpatchAll("me.amzd.RealisticGlider");
            harmony.Patch(
                typeof(PlayerEntityInAir).GetMethod("ApplyFlying", BindingFlags.NonPublic | BindingFlags.Instance),
                postfix: typeof(RealisticGliderMod).GetMethod("ApplyFlying")
            );
        }

        public static void ApplyFlying(PlayerEntityInAir __instance, float dt, EntityPos pos, EntityControls controls)
        {
            if (!controls.Gliding) return;

            BlockPos blPos = pos.AsBlockPos;

            Vec3d windSpeed = _api.World.BlockAccessor.GetWindSpeedAt(pos.XYZ);

            if (_api.World.Rand.NextDouble() < 0.1)
            {
                IBlockAccessor blAcc = _api.World.BlockAccessor;
                int maxHeight = MaxHeight(blPos, 10, blAcc, windSpeed);
                // TODO: If wind can change direction maybe use: blPos.AddCopy(windSpeed.Normalize().Mul(-30, 0, -30).AsVec3i);
                int maxHeightDownWind1 = MaxHeight(blPos.AddCopy(-30,0,0), 10, blAcc, windSpeed);
                int maxHeightDownWind2 = MaxHeight(blPos.AddCopy(-60,0,0), 10, blAcc, windSpeed);

                if (maxHeight > maxHeightDownWind1 + 8 && maxHeight > maxHeightDownWind2 + 12)
                    lastFoundRidgeHeight = maxHeight;
                else
                    lastFoundRidgeHeight = -1;
            }

            if (controls.GlideSpeed >= altglider_speedMid && lastFoundRidgeHeight > blPos.Y - maxHeightAboveRidge && lastFoundRidgeHeight != -1) {

                pos.Motion.Y += 0.008;

                controls.GlideSpeed = Math.Min(controls.GlideSpeed + altglider_glideSpeedDec * 2, altglider_speedMax);
            }



        }

        public static int MaxHeight(BlockPos blPos, int distance, IBlockAccessor blAcc, Vec3d windSpeed)
        {
            return Math.Max(
                blAcc.GetTerrainMapheightAt(blPos),
                Math.Max(
                    blAcc.GetTerrainMapheightAt(blPos.AddCopy(distance, 0, 0)),
                    blAcc.GetTerrainMapheightAt(blPos.AddCopy(-distance, 0, 0))
                )
            );
        }
    }
}

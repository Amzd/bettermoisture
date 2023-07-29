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

namespace BetterStepping
{
    public class BetterSteppingMod : ModSystem
    {
        static ICoreAPI _api;
        public override void Start(ICoreAPI api)
        {
            api.Logger.Debug("[BetterStepping] Start");
            _api = api;
            base.Start(api);

            var harmony = new Harmony("me.amzd.BetterStepping");
            harmony.UnpatchAll("me.amzd.BetterStepping");
            harmony.Patch(
                typeof(EntityBehaviorControlledPhysics).GetMethod("FindSteppableCollisionboxSmooth", BindingFlags.NonPublic | BindingFlags.Instance),
                prefix: typeof(BetterSteppingMod).GetMethod("FindSteppableCollisionboxSmooth")
            );
        }

        public static bool FindSteppableCollisionboxSmooth(EntityBehaviorControlledPhysics __instance, Cuboidd entityCollisionBox, ref Cuboidd entitySensorBox, double motionY, Vec3d walkVector)
        {
            Vec3d walkVecNormalized = walkVector.Clone().Normalize();

            double widthOffsetX = Math.Abs(walkVecNormalized.Z * entityCollisionBox.Width * 0.5);
            entitySensorBox.X1 -= widthOffsetX;
            entitySensorBox.X2 += widthOffsetX;

            double widthOffsetZ = Math.Abs(walkVecNormalized.X * entityCollisionBox.Width * 0.5);
            entitySensorBox.Z1 -= widthOffsetZ;
            entitySensorBox.Z2 += widthOffsetZ;

            return true;
        }
    }
}

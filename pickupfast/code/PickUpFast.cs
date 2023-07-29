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



namespace PickUpFast
{
    public class PickUpFastMod : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.Logger.Debug("[PickUpFast] Start");

            base.Start(api);

            var harmony = new Harmony("me.amzd.pickupfast");
            harmony.Patch(typeof(EntityItem).GetMethod("CanCollect"), prefix: typeof(PickUpFastMod).GetMethod("newCanCollect"));
            harmony.Patch(typeof(Block).GetMethod("OnBlockBroken"), postfix: typeof(PickUpFastMod).GetMethod("afterOnBlockBroken"));
        }

        public static bool newCanCollect(EntityItem __instance, ref bool __result, Entity byEntity)
        {
            if (byEntity is EntityPlayer collectingPlayer && __instance.ByPlayerUid == collectingPlayer.Player.PlayerUID)
            {
                // You broke the block that dropped this entity so can pick it up instantly.
                __result = __instance.Alive;
                // Skip original
                return false;
            }

            return true;
        }

        public static void afterOnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            if (byPlayer == null) return;
            // Get item entities in this blocks position, created in last 10 milliseconds
            Entity[] entities = world.GetEntitiesAround(pos.ToVec3d(), 1, 1, (e) => (e is EntityItem eI && eI.GetField<long>("itemSpawnedMilliseconds") > world.ElapsedMilliseconds - 10));
            // Set the byPlayerUID of the newly created entities
            foreach (Entity e in entities)
                e.WatchedAttributes.SetString("byPlayerUid", byPlayer.PlayerUID);
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
    }
}

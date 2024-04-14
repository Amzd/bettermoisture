using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace AltGliderImplementation {
	public class AltGliderConfig {
		public bool show_bar = true;
		public bool sneak_to_dive = false;
		public bool jump_to_brake = false;
		public bool glide_while_climbing = true;
	}

	public class AltGliderSystem: ModSystem {
		public const string configFilename = "altgliderimplementation.json";

		public const double speedMin = 0.0005;
		public const double speedMid = 0.0025;
		public const double speedMax = 0.01;

		public const double diveFallLimit = -0.10;
		public const double diveFallAcc = -0.0025;
		public const double diveFallMult = 1.25;

		public const double glideFallLimit = -0.05;
		public const double glideFallResist = 0.01;
		public const double glideSpeedAcc = 0.0001;
		public const double glideSpeedDec = 0.000005;

		public const double brakeSpeedDec = 0.00025;
		public const double brakeMotionPctDec = 1.0;
		public const double brakeLiftAcc = 0.035;
		public const double brakeHmotionLimit = 0.02;
		public const double brakeFallLimit = -0.025;
		public const double brakeFallResist = 0.01;

		public static AltGliderConfig config;

		protected AltGliderElement element;
		protected Harmony harmony;

		public override void Start(ICoreAPI api) {
			base.Start(api);

			this.harmony = new Harmony("altgliderimplementation");
			harmony.PatchAll(Assembly.GetExecutingAssembly());
		}

		public override void StartClientSide(ICoreClientAPI capi) {
			base.StartClientSide(capi);

			// Load or create config file.
			try {
				AltGliderSystem.config = capi.LoadModConfig<AltGliderConfig>(AltGliderSystem.configFilename);
			} catch(Exception) {}
			if(AltGliderSystem.config != null) {
				// Save config file in case of missing fields.
				capi.StoreModConfig<AltGliderConfig>(AltGliderSystem.config, AltGliderSystem.configFilename);
			} else {
				// Create new config file.
				AltGliderSystem.config = new AltGliderConfig();
				capi.StoreModConfig<AltGliderConfig>(AltGliderSystem.config, AltGliderSystem.configFilename);
			}

			if(AltGliderSystem.config.show_bar) {
				this.element = new AltGliderElement(capi);
			}
		}

		public override void Dispose() {
			base.Dispose();

			if(harmony != null) {
				this.harmony.UnpatchAll();
			}
		}
	}

	[HarmonyPatch(typeof(EntityInAir), "ApplyFlying")]
	public class AGI_EIA_ApplyFlying {
		[HarmonyReversePatch]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void ApplyFlying(EntityInAir __instance, float dt, EntityPos pos, EntityControls controls) {}
	}

	[HarmonyPatch(typeof(PlayerEntityInAir), "ApplyFlying")]
	public class AGI_PEIA_ApplyFlying {
		static bool Prefix(PlayerEntityInAir __instance, float dt, EntityPos pos, EntityControls controls) {
			if(controls.Gliding) {
				double cosYaw = Math.Cos((Math.PI / 2) - pos.Yaw);
				double sinYaw = Math.Sin((Math.PI / 2) - pos.Yaw);

				bool diving;
				bool braking;
				if(AltGliderSystem.config != null
						&& AltGliderSystem.config.sneak_to_dive) {
					diving = controls.Sneak;
				} else {
					diving = controls.Forward;
				}
				if(AltGliderSystem.config != null
						&& AltGliderSystem.config.jump_to_brake) {
					braking = controls.Jump;
				} else {
					braking = controls.Backward;
				}

				// Vertical.
				if(diving) {
					// Dive.
					double power = -(pos.Motion.Y / 60) * AltGliderSystem.diveFallMult;
					if(power > controls.GlideSpeed) {
						// Transfer fall speed to glide speed.
						controls.GlideSpeed = Math.Max(power, 0);
					}

					if(pos.Motion.Y > AltGliderSystem.diveFallLimit) {
						// Accelerate to terminal velocity.
						pos.Motion.Y = Math.Max(pos.Motion.Y + AltGliderSystem.diveFallAcc,
								AltGliderSystem.diveFallLimit);
					}
				} else if(braking && controls.GlideSpeed > AltGliderSystem.speedMin) {
					// Brake.
					double hmotion = Math.Sqrt(Math.Pow(pos.Motion.X / 60, 2) + Math.Pow(pos.Motion.Z / 60, 2));
					double dec = Math.Min(controls.GlideSpeed - AltGliderSystem.brakeSpeedDec,
								AltGliderSystem.brakeSpeedDec);
					controls.GlideSpeed = Math.Max(controls.GlideSpeed - dec,
								AltGliderSystem.speedMin);

					if(hmotion > AltGliderSystem.speedMid) {
						// Partial lift for partial speed decrease.
						double lift = (dec / AltGliderSystem.brakeSpeedDec) * AltGliderSystem.brakeLiftAcc;
						
						// Lift proportionally to horizontal motion.
						pos.Motion.Y += lift * Math.Min(hmotion / AltGliderSystem.brakeHmotionLimit, 1);
					} else {
						// Reduce fall limit proportionally to speed.
						double fallLimit = AltGliderSystem.brakeFallLimit * (1 - (controls.GlideSpeed / AltGliderSystem.speedMax));

						if(pos.Motion.Y < fallLimit) {
							// Break fall until glide terminal velocity.
							pos.Motion.Y = Math.Min(pos.Motion.Y + AltGliderSystem.brakeFallResist,
									fallLimit);
						}
					}

					// Further decrease horizontal motion.
					double motionDec = hmotion * -AltGliderSystem.brakeMotionPctDec;
					pos.Motion.Add(sinYaw * motionDec,
							0,
							cosYaw * -motionDec);
				} else {
					// Glide.
					if(controls.GlideSpeed < AltGliderSystem.speedMid) {
						// Accelerate to minimum speed.
						controls.GlideSpeed = Math.Min(controls.GlideSpeed + AltGliderSystem.glideSpeedAcc,
								AltGliderSystem.speedMid);
					} else {
						// Decelerate to minimum speed.
						controls.GlideSpeed = Math.Max(controls.GlideSpeed - AltGliderSystem.glideSpeedDec,
								AltGliderSystem.speedMid);
					}

					// Reduce fall limit proportionally to speed.
					double fallLimit = AltGliderSystem.glideFallLimit * (1 - (controls.GlideSpeed / AltGliderSystem.speedMax));

					if(pos.Motion.Y < fallLimit) {
						// Break fall until glide terminal velocity.
						pos.Motion.Y = Math.Min(pos.Motion.Y + AltGliderSystem.glideFallResist,
								fallLimit);
					}
				}

				// Horizontal.
				pos.Motion.X += sinYaw * controls.GlideSpeed;
				pos.Motion.Z += cosYaw * -controls.GlideSpeed;

				// Punish sharp turns.
				double speed = Math.Sqrt(Math.Pow(pos.Motion.X / 60, 2) + Math.Pow(pos.Motion.Y / 60, 2) + Math.Pow(pos.Motion.Z / 60, 2));
				controls.GlideSpeed = Math.Min(controls.GlideSpeed, speed);
			} else {
				AGI_EIA_ApplyFlying.ApplyFlying(__instance, dt, pos, controls);
			}

			// Skip original method.
			return false;
		}
	}

	// Match pitch to glide direction.
	[HarmonyPatch(typeof(EntityBehaviorPlayerPhysics), "TickEntityPhysicsPre")]
	public class AGI_EBPP_TickEntityPhysicsPre {
		protected const float brakePitch = (float)(-30 * (Math.PI / 180));
		protected const float pitchGrace = (float)(1 * (Math.PI / 180));

		static void Postfix(EntityBehaviorPlayerPhysics __instance, Entity entity, float dt) {
			Traverse self = Traverse.Create(__instance);
			EntityPlayer eplr = self.Field("eplr").GetValue() as EntityPlayer;

			IPlayer player = eplr.Player;
			EntityControls controls = eplr.Controls;

			// Copy original checks.
			if(entity.World.Side == EnumAppSide.Server
					&& ((IServerPlayer)player).ConnectionState != EnumClientState.Playing) {
				return;
			}

			EntityPos pos = entity.World.Side == EnumAppSide.Server
					? entity.ServerPos
					: entity.Pos;

			if(controls.Gliding) {
				eplr.WalkPitch = (float)-pos.Motion.Y;
			} else if(!entity.Swimming) {
				// Fix pitch bug.
				eplr.WalkPitch = 0;
			}
		}
	}

	// Glide while climing option.
	[HarmonyPatch(typeof(ModSystemGliding), "Input_InWorldAction")]
	public class AGI_MSG_Input_InWorldAction {
		static bool Prefix(ModSystemGliding __instance, EnumEntityAction action, bool on, ref EnumHandling handled) {
			if(AltGliderSystem.config.glide_while_climbing) {
				// Call original method.
				return true;
			}

			Traverse self = Traverse.Create(__instance);
			ICoreClientAPI capi = self.Field("capi").GetValue() as ICoreClientAPI;
			bool HasGilder = (bool)self.Property("HasGilder").GetValue();

			var eplr = capi.World.Player.Entity;
			if(action == EnumEntityAction.Jump
					&& on
					&& !eplr.OnGround
					&& HasGilder
					&& !eplr.Controls.IsFlying
					&& !eplr.Controls.IsClimbing) {
				eplr.Controls.Gliding = true;
				eplr.Controls.IsFlying = true;
				(eplr.Properties.Client.Renderer as EntityShapeRenderer)?.MarkShapeModified();
			}

			if(action == EnumEntityAction.Glide && !on) {
				(eplr.Properties.Client.Renderer as EntityShapeRenderer)?.MarkShapeModified();
			}

			// Skip original method.
			return false;
		}
	}
}
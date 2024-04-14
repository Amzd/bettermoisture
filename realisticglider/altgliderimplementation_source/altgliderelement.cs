using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AltGliderImplementation {
	public class AltGliderElement: HudElement {
		protected const int listenInterval = 100;
		protected const string dialogName = "altglider";

		protected const string barKey = "altgliderbar";
		protected const float barX = 0;
		protected const float barY = -256;
		protected const float barWidth = 256;
		protected const float barHeight = 10;

		protected readonly double[] colour = { 1, 1, 1, 1 };

		protected AltGliderStatbar bar;

		private long listenerId;

		public AltGliderElement(ICoreClientAPI capi): base(capi) {
			this.listenerId = capi.Event.RegisterGameTickListener(this.OnGameTick, listenInterval);

			// Create bar.
			ElementBounds dialogBounds = new ElementBounds() {
				Alignment = EnumDialogArea.CenterBottom,
				BothSizing = ElementSizing.Fixed,
				fixedWidth = barWidth,
				fixedHeight = barHeight
			};

			ElementBounds barBounds = ElementBounds.Fixed(barX, barY, barWidth, barHeight);

			Composers[dialogName] = capi.Gui
					.CreateCompo(dialogName, dialogBounds)
					.AddInteractiveElement(new AltGliderStatbar(capi, barBounds, this.colour, false), barKey)
					.Compose();

			this.bar = (AltGliderStatbar)Composers[dialogName].GetElement(barKey);
			this.bar.SetMinMax((float)AltGliderSystem.speedMin, (float)AltGliderSystem.speedMax);
			
			this.TryOpen();
		}

		private void OnGameTick(float dt) {
			// Update bar.
			if(this.bar == null) {
				return;
			}

			EntityPlayer entity = this.capi.World.Player.Entity;
			if(entity == null) {
				return;
			}

			if(entity.Controls == null) {
				return;
			}

			this.bar.visible = entity.Controls.Gliding;
			this.bar.SetValue((float)entity.Controls.GlideSpeed);
		}

		public override bool ShouldReceiveKeyboardEvents() {
			return false;
		}

		public override void OnMouseDown(MouseEvent args) {
			// Cannot be clicked.
		}

		public override void Dispose() {
			base.Dispose();

			capi.Event.UnregisterGameTickListener(this.listenerId);
		}

		protected override void OnFocusChanged(bool on) {
			// Cannot be focused.
		}
	}
}
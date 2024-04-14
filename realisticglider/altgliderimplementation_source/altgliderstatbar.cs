using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AltGliderImplementation {
	public class AltGliderStatbar: GuiElementTextBase {
		public bool visible = false;

		protected double[] colour;
		protected bool rightToLeft = false;

		protected float minValue = 0;
		protected float maxValue = 100;
		protected float value = 0;
		protected bool valuesSet;

		protected float lineInterval = 10;

		protected LoadedTexture baseTexture;
		protected LoadedTexture barTexture;
		protected LoadedTexture valueTexture;

		private int valueHeight;

		public AltGliderStatbar(ICoreClientAPI capi, ElementBounds bounds, double[] colour, bool rightToLeft): base(capi, "", CairoFont.WhiteDetailText(), bounds) {
			this.barTexture = new LoadedTexture(capi);
			this.valueTexture = new LoadedTexture(capi);
			this.baseTexture = new LoadedTexture(capi);

			this.colour = colour;
			this.rightToLeft = rightToLeft;
		}

		public override void ComposeElements(Context ctx, ImageSurface surface) {
			Bounds.CalcWorldBounds();

			surface = new ImageSurface(Format.Argb32, Bounds.OuterWidthInt+1, Bounds.OuterHeightInt + 1);
			ctx = new Context(surface);

			RoundRectangle(ctx, 0, 0, Bounds.InnerWidth, Bounds.InnerHeight, 1);

			ctx.SetSourceRGBA(0.15, 0.15, 0.15, 1);
			ctx.Fill();
			EmbossRoundRectangleElement(ctx, 0, 0, Bounds.InnerWidth, Bounds.InnerHeight, false, 3, 1);

			if(this.valuesSet) {
				this.RecomposeOverlays();
			}

			generateTexture(surface, ref this.baseTexture);
			surface.Dispose();
			ctx.Dispose();
		}

		protected void RecomposeOverlays() {
			TyronThreadPool.QueueTask(() => {
						ComposeValueOverlay();
					});
		}

		protected void ComposeValueOverlay() {
			Bounds.CalcWorldBounds();

			// Fix width formula.
			double widthRel = (double)((this.value - this.minValue) / (this.maxValue - this.minValue));
			this.valueHeight = (int)Bounds.OuterHeight + 1;
			ImageSurface surface = new ImageSurface(Format.Argb32, Bounds.OuterWidthInt + 1, this.valueHeight);
			Context ctx = new Context(surface);

			if(widthRel > 0.01) {
				double width = Bounds.OuterWidth * widthRel;
				double x = this.rightToLeft
						? Bounds.OuterWidth - width
						: 0;

				RoundRectangle(ctx, x, 0, width, Bounds.OuterHeight, 1);
				ctx.SetSourceRGB(this.colour[0], this.colour[1], this.colour[2]);
				ctx.FillPreserve();

				ctx.SetSourceRGB(this.colour[0] * 0.4, this.colour[1] * 0.4, this.colour[2] * 0.4);
				ctx.LineWidth = scaled(3);
				ctx.StrokePreserve();
				surface.BlurFull(3);

				width = Bounds.InnerWidth * widthRel;
				x = this.rightToLeft
						? Bounds.InnerWidth - width
						: 0;

				EmbossRoundRectangleElement(ctx, x, 0, width, Bounds.InnerHeight, false, 2, 1);
			}

			ctx.SetSourceRGBA(0, 0, 0, 0.5);
			ctx.LineWidth = scaled(2.2);

			int lines = Math.Min(50, (int)((this.maxValue - this.minValue) / this.lineInterval));
			
			for(int i = 1; i < lines; i ++) {
				ctx.NewPath();
				ctx.SetSourceRGBA(0, 0, 0, 0.5);

				double x = (Bounds.InnerWidth * i) / lines;

				ctx.MoveTo(x, 0);
				ctx.LineTo(x, Math.Max(3, Bounds.InnerHeight - 1));
				ctx.ClosePath();
				ctx.Stroke();
			}

			api.Event.EnqueueMainThreadTask(() => {
						generateTexture(surface, ref this.barTexture);

						ctx.Dispose();
						surface.Dispose();
					}, "recompstatbar");
		}

		public override void RenderInteractiveElements(float deltaTime) {
			if(!this.visible) {
				return;
			}

			double x = Bounds.renderX;
			double y = Bounds.renderY;

			api.Render.RenderTexture(this.baseTexture.TextureId, x, y, Bounds.OuterWidthInt + 1, Bounds.OuterHeightInt + 1);

			if(this.barTexture.TextureId > 0) {
				api.Render.RenderTexture(this.barTexture.TextureId, x, y, Bounds.OuterWidthInt + 1, this.valueHeight);
			}
		}

		public void SetLineInterval(float value) {
			this.lineInterval = value;
		}

		public void SetValue(float value) {
			this.value = value;
			this.valuesSet = true;
			this.RecomposeOverlays();
		}

		public float GetValue() {
			return this.value;
		}

		public void SetValues(float value, float min, float max) {
			this.value = value;
			this.minValue = min;
			this.maxValue = max;
			this.valuesSet = true;
			this.RecomposeOverlays();
		}

		public void SetMinMax(float min, float max) {
			this.minValue = min;
			this.maxValue = max;
			this.RecomposeOverlays();
		}
		
		public override void Dispose() {
			base.Dispose();

			this.baseTexture.Dispose();
			this.barTexture.Dispose();
			this.valueTexture.Dispose();
		}
	}
}
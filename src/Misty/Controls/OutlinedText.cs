using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Misty.Controls
{
    /// <summary>
    /// Texte avec contour et ombre portée "en bloc" (style titre pixel).
    /// </summary>
    public class OutlinedText : FrameworkElement
    {
        public static readonly DependencyProperty TextProperty = Dp(nameof(Text), "", FrameworkPropertyMetadataOptions.AffectsMeasure);
        public static readonly DependencyProperty FontFamilyProperty = Dp<FontFamily>(nameof(FontFamily), new FontFamily("Segoe UI"), FrameworkPropertyMetadataOptions.AffectsMeasure);
        public static readonly DependencyProperty FontSizeProperty = Dp(nameof(FontSize), 48.0, FrameworkPropertyMetadataOptions.AffectsMeasure);
        public static readonly DependencyProperty FontWeightProperty = Dp(nameof(FontWeight), FontWeights.Bold, FrameworkPropertyMetadataOptions.AffectsMeasure);
        public static readonly DependencyProperty FillProperty = Dp<Brush>(nameof(Fill), Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender);
        public static readonly DependencyProperty StrokeProperty = Dp<Brush>(nameof(Stroke), Brushes.SlateGray, FrameworkPropertyMetadataOptions.AffectsRender);
        public static readonly DependencyProperty StrokeThicknessProperty = Dp(nameof(StrokeThickness), 4.0, FrameworkPropertyMetadataOptions.AffectsMeasure);
        public static readonly DependencyProperty ShadowBrushProperty = Dp<Brush?>(nameof(ShadowBrush), null, FrameworkPropertyMetadataOptions.AffectsRender);
        public static readonly DependencyProperty ShadowDepthProperty = Dp(nameof(ShadowDepth), 0.0, FrameworkPropertyMetadataOptions.AffectsMeasure);

        public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
        public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
        public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
        public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
        public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
        public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
        public Brush? ShadowBrush { get => (Brush?)GetValue(ShadowBrushProperty); set => SetValue(ShadowBrushProperty, value); }
        public double ShadowDepth { get => (double)GetValue(ShadowDepthProperty); set => SetValue(ShadowDepthProperty, value); }

        private Geometry? geometry;
        private Size tailleTexte;

        private FormattedText Formater() => new(
            Text ?? "",
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal),
            FontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        protected override Size MeasureOverride(Size availableSize)
        {
            var ft = Formater();
            geometry = ft.BuildGeometry(new Point(StrokeThickness, StrokeThickness));
            tailleTexte = new Size(ft.WidthIncludingTrailingWhitespace, ft.Height);
            return new Size(tailleTexte.Width + StrokeThickness * 2, tailleTexte.Height + StrokeThickness * 2 + ShadowDepth);
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (geometry == null) return;

            var pen = new Pen(Stroke, StrokeThickness * 2) { LineJoin = PenLineJoin.Miter };

            if (ShadowBrush != null && ShadowDepth > 0)
            {
                dc.PushTransform(new TranslateTransform(0, ShadowDepth));
                dc.DrawGeometry(ShadowBrush, new Pen(ShadowBrush, StrokeThickness * 2) { LineJoin = PenLineJoin.Miter }, geometry);
                dc.Pop();
            }

            dc.DrawGeometry(null, pen, geometry);
            dc.DrawGeometry(Fill, null, geometry);
        }

        // Toute propriété redessine le texte (AffectsMeasure seul ne garantit pas un nouvel OnRender)
        private static DependencyProperty Dp<T>(string nom, T defaut, FrameworkPropertyMetadataOptions options) =>
            DependencyProperty.Register(nom, typeof(T), typeof(OutlinedText),
                new FrameworkPropertyMetadata(defaut, options | FrameworkPropertyMetadataOptions.AffectsRender));
    }
}

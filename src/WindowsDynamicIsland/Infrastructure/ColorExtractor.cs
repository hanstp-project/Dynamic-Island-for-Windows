using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsDynamicIsland.Infrastructure;

/// <summary>
/// Picks a vivid accent colour from album artwork (like iOS tints the island waveform).
/// Hue histogram weighted by saturation × brightness; the winning bucket is averaged and
/// brightened so it reads well on black.
/// </summary>
public static class ColorExtractor
{
    private const int Buckets = 12;

    public static Color? GetAccent(BitmapSource? source)
    {
        if (source is null) return null;
        try
        {
            BitmapSource bmp = source;
            if (bmp.Format != PixelFormats.Bgra32)
                bmp = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
            double scale = 32.0 / Math.Max(bmp.PixelWidth, bmp.PixelHeight);
            if (scale < 1)
                bmp = new TransformedBitmap(bmp, new ScaleTransform(scale, scale));

            int w = bmp.PixelWidth, h = bmp.PixelHeight;
            if (w == 0 || h == 0) return null;
            var px = new byte[w * h * 4];
            bmp.CopyPixels(px, w * 4, 0);

            var weight = new double[Buckets];
            var rs = new double[Buckets];
            var gs = new double[Buckets];
            var bs = new double[Buckets];
            double greyR = 0, greyG = 0, greyB = 0, greyW = 0;

            for (int i = 0; i < px.Length; i += 4)
            {
                byte b = px[i], g = px[i + 1], r = px[i + 2], a = px[i + 3];
                if (a < 128) continue;
                ToHsv(r, g, b, out double hue, out double sat, out double val);
                if (sat < 0.15 || val < 0.15)
                {
                    greyR += r; greyG += g; greyB += b; greyW += 1;
                    continue;
                }
                int k = Math.Min(Buckets - 1, (int)(hue / 360.0 * Buckets));
                double wgt = sat * sat * val;
                weight[k] += wgt;
                rs[k] += r * wgt; gs[k] += g * wgt; bs[k] += b * wgt;
            }

            int best = -1;
            double bestW = 0;
            for (int k = 0; k < Buckets; k++)
                if (weight[k] > bestW) { bestW = weight[k]; best = k; }

            if (best < 0 || bestW < 0.5)
            {
                // Monochrome artwork → soft white, exactly like iOS falls back to.
                return Color.FromRgb(235, 235, 245);
            }

            var avg = Color.FromRgb((byte)(rs[best] / bestW), (byte)(gs[best] / bestW), (byte)(bs[best] / bestW));
            ToHsv(avg.R, avg.G, avg.B, out double hh, out double ss, out double vv);
            ss = Math.Clamp(Math.Max(ss, 0.45), 0, 0.9);
            vv = Math.Clamp(Math.Max(vv, 0.85), 0, 1);
            return FromHsv(hh, ss, vv);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void ToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
    {
        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
        double max = Math.Max(rf, Math.Max(gf, bf)), min = Math.Min(rf, Math.Min(gf, bf));
        double d = max - min;
        v = max;
        s = max <= 0 ? 0 : d / max;
        if (d <= 0) { h = 0; return; }
        if (max == rf) h = 60 * (((gf - bf) / d) % 6);
        else if (max == gf) h = 60 * ((bf - rf) / d + 2);
        else h = 60 * ((rf - gf) / d + 4);
        if (h < 0) h += 360;
    }

    public static Color FromHsv(double h, double s, double v)
    {
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}

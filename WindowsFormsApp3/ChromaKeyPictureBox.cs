using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowsFormsApp3
{
    /// <summary>
    /// 将深色底、洋红类色键转为与窗体 TransparencyKey 一致的洋红像素；图源等比「铺满」控件（同 UniformToFill），不变形且尽量大，边缘可裁切留白。
    /// 按下时仅压暗非镂空像素。
    /// </summary>
    public class ChromaKeyPictureBox : PictureBox
    {
        private Bitmap _cached;
        private Bitmap _pressedTint;
        private Image _cacheSource;
        private Size _cacheSize;
        private bool _visualPressed;

        public bool VisualPressed
        {
            get => _visualPressed;
            set
            {
                if (_visualPressed == value)
                    return;
                _visualPressed = value;
                if (!value)
                {
                    _pressedTint?.Dispose();
                    _pressedTint = null;
                }
                Invalidate();
            }
        }

        public ChromaKeyPictureBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Image == null)
            {
                base.OnPaint(e);
                return;
            }

            if (SizeMode != PictureBoxSizeMode.StretchImage)
            {
                base.OnPaint(e);
                return;
            }

            if (ClientSize.Width < 1 || ClientSize.Height < 1)
                return;

            if (!ReferenceEquals(_cacheSource, Image) || _cacheSize != ClientSize)
            {
                _cacheSource = Image;
                _cacheSize = ClientSize;
                _cached?.Dispose();
                _cached = BuildKeyedBitmap(Image, ClientSize.Width, ClientSize.Height);
                _pressedTint?.Dispose();
                _pressedTint = null;
            }

            if (_cached == null)
                return;

            if (_visualPressed)
            {
                if (_pressedTint == null)
                    _pressedTint = BuildPressedTint(_cached);
                if (_pressedTint != null)
                    e.Graphics.DrawImageUnscaled(_pressedTint, 0, 0);
            }
            else
                e.Graphics.DrawImageUnscaled(_cached, 0, 0);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            pevent.Graphics.Clear(Color.Fuchsia);
        }

        private static bool IsTransparencyKeyPixel(byte r, byte g, byte b)
        {
            return g <= 12 && r >= 240 && b >= 240;
        }

        private static Bitmap BuildPressedTint(Bitmap keyed)
        {
            var bmp = (Bitmap)keyed.Clone();
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                int h = bmp.Height;
                int bytes = Math.Abs(stride) * h;
                byte[] buf = new byte[bytes];
                Marshal.Copy(data.Scan0, buf, 0, bytes);
                const float dim = 0.78f;
                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        int i = row + x * 4;
                        byte blue = buf[i];
                        byte green = buf[i + 1];
                        byte red = buf[i + 2];
                        if (IsTransparencyKeyPixel(red, green, blue))
                            continue;
                        buf[i] = (byte)(blue * dim);
                        buf[i + 1] = (byte)(green * dim);
                        buf[i + 2] = (byte)(red * dim);
                    }
                }
                Marshal.Copy(buf, 0, data.Scan0, bytes);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            return bmp;
        }

        private static Rectangle ComputeCoverDestRect(int srcW, int srcH, int boxW, int boxH)
        {
            if (srcW < 1 || srcH < 1 || boxW < 1 || boxH < 1)
                return new Rectangle(0, 0, boxW, boxH);
            float scale = Math.Max((float)boxW / srcW, (float)boxH / srcH);
            int dw = Math.Max(1, (int)Math.Round(srcW * scale));
            int dh = Math.Max(1, (int)Math.Round(srcH * scale));
            int x = (boxW - dw) / 2;
            int y = (boxH - dh) / 2;
            return new Rectangle(x, y, dw, dh);
        }

        private static Bitmap BuildKeyedBitmap(Image src, int w, int h)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Fuchsia);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                Rectangle dest = ComputeCoverDestRect(src.Width, src.Height, w, h);
                g.DrawImage(src, dest);
            }

            Color bg = SampleCornerBackground(bmp);
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                int bytes = Math.Abs(stride) * h;
                byte[] buf = new byte[bytes];
                Marshal.Copy(data.Scan0, buf, 0, bytes);

                const float magentaDistSqMax = 52000f;
                const float bgDistSqMax = 200f;

                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int i = row + x * 4;
                        byte blue = buf[i];
                        byte green = buf[i + 1];
                        byte red = buf[i + 2];

                        if (ShouldBeTransparent(red, green, blue, bg, magentaDistSqMax, bgDistSqMax))
                        {
                            buf[i] = 255;
                            buf[i + 1] = 0;
                            buf[i + 2] = 255;
                            buf[i + 3] = 255;
                        }
                        else
                            buf[i + 3] = 255;
                    }
                }

                Marshal.Copy(buf, 0, data.Scan0, bytes);
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            return bmp;
        }

        private static Color SampleCornerBackground(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            if (w < 1 || h < 1)
                return Color.Black;

            int r = 0, g = 0, b = 0, n = 0;
            void add(int x, int y)
            {
                Color c = bmp.GetPixel(x, y);
                r += c.R;
                g += c.G;
                b += c.B;
                n++;
            }

            add(0, 0);
            add(w - 1, 0);
            add(0, h - 1);
            add(w - 1, h - 1);
            return Color.FromArgb(r / n, g / n, b / n);
        }

        private static bool ShouldBeTransparent(byte r, byte g, byte b, Color bg, float magentaDistSqMax, float bgDistSqMax)
        {
            int mx = Math.Max(r, Math.Max(g, b));
            if (mx < 40 && (r + g + b) < 105)
                return true;

            float dr = r - 255f;
            float dg = g;
            float db = b - 255f;
            float magSq = dr * dr + dg * dg + db * db;
            if (magSq <= magentaDistSqMax)
                return true;

            if (r > 95 && b > 95 && g < (r + b) / 2f - 22f)
                return true;

            float br = r - bg.R;
            float bgG = g - bg.G;
            float bb = b - bg.B;
            float bgSq = br * br + bgG * bgG + bb * bb;
            return bgSq <= bgDistSqMax;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cached?.Dispose();
                _cached = null;
                _pressedTint?.Dispose();
                _pressedTint = null;
                _cacheSource = null;
            }
            base.Dispose(disposing);
        }
    }
}

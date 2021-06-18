using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiFishTouchResponse
{
    class Class1
    {
        public static System.Windows.Media.Imaging.BitmapSource GetBitmapSource(System.Drawing.Bitmap image)
        {
            var rect = new System.Drawing.Rectangle(0, 0, image.Width, image.Height);
            var bitmap_data = image.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, image.PixelFormat);

            try
            {
                System.Windows.Media.Imaging.BitmapPalette palette = null;

                if (image.Palette.Entries.Length > 0)
                {
                    var palette_colors = image.Palette.Entries.Select(entry => System.Windows.Media.Color.FromArgb(entry.A, entry.R, entry.G, entry.B)).ToList();
                    palette = new System.Windows.Media.Imaging.BitmapPalette(palette_colors);
                }

                return System.Windows.Media.Imaging.BitmapSource.Create(
                    image.Width,
                    image.Height,
                    image.HorizontalResolution,
                    image.VerticalResolution,
                    ConvertPixelFormat(image.PixelFormat),
                    palette,
                    bitmap_data.Scan0,
                    bitmap_data.Stride * image.Height,
                    bitmap_data.Stride
                );
            }
            finally
            {
                image.UnlockBits(bitmap_data);
            }
        }

        private static System.Windows.Media.PixelFormat ConvertPixelFormat(System.Drawing.Imaging.PixelFormat sourceFormat)
        {
            switch (sourceFormat)
            {
                case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                    return System.Windows.Media.PixelFormats.Bgr24;

                case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                    return System.Windows.Media.PixelFormats.Bgra32;

                case System.Drawing.Imaging.PixelFormat.Format32bppRgb:
                    return System.Windows.Media.PixelFormats.Bgr32;
            }

            return new System.Windows.Media.PixelFormat();
        }
    }
}

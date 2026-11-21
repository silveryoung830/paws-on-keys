using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PawsOnKeys
{
    public static class PhotoPalette
    {
        public static BitmapSource Read(string path)
        {
            using (Stream stream = File.OpenRead(path))
            {
                BitmapDecoder info = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                int w = info.Frames[0].PixelWidth, h = info.Frames[0].PixelHeight;
                if (w < 1 || h < 1 || (long)w * h > 110000000)
                    throw new InvalidDataException("照片过大，请使用缩小后的照片。");
                stream.Position = 0;
                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = stream;
                image.CacheOption = BitmapCacheOption.OnLoad;
                if (Math.Max(w, h) > 768)
                {
                    if (w >= h)
                        image.DecodePixelWidth = 768;
                    else
                        image.DecodePixelHeight = 768;
                }

                image.EndInit();
                image.Freeze();
                var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
                converted.Freeze();
                return converted;
            }
        }

        public static string Sample(BitmapSource image, int x, int y)
        {
            if (image.Format != PixelFormats.Bgra32)
                image = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
            byte[] pixel = new byte[4];
            image.CopyPixels(new Int32Rect(Math.Max(0, Math.Min(x, image.PixelWidth - 1)), Math.Max(0, Math.Min(y, image.PixelHeight - 1)), 1, 1), pixel, 4, 0);
            return String.Format("#{0:X2}{1:X2}{2:X2}", pixel[2], pixel[1], pixel[0]);
        }

        public static string EstimateFur(BitmapSource image)
        {
            if (image.Format != PixelFormats.Bgra32)
                image = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
            int w = image.PixelWidth, h = image.PixelHeight;
            var pixels = new byte[w * h * 4];
            image.CopyPixels(pixels, w * 4, 0);
            int[] counts = new int[512], reds = new int[512], greens = new int[512], blues = new int[512];
            for (int y = h / 4; y < Math.Max(h / 4 + 1, (3 * h + 3) / 4); y += Math.Max(1, h / 80))
                for (int x = w / 4; x < Math.Max(w / 4 + 1, (3 * w + 3) / 4); x += Math.Max(1, w / 80))
                {
                    int j = 4 * (y * w + x);
                    if (pixels[j + 3] < 128)
                        continue;
                    int r = pixels[j + 2], g = pixels[j + 1], b = pixels[j], bin = ((r / 32) * 8 + g / 32) * 8 + b / 32;
                    counts[bin]++;
                    reds[bin] += r;
                    greens[bin] += g;
                    blues[bin] += b;
                }

            int best = 0;
            for (int i = 1; i < 512; i++)
                if (counts[i] > counts[best])
                    best = i;
            if (counts[best] == 0)
                return "#232127";
            return String.Format("#{0:X2}{1:X2}{2:X2}", reds[best] / counts[best], greens[best] / counts[best], blues[best] / counts[best]);
        }
    }
}

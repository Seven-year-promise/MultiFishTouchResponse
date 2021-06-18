using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;
using NumSharp;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;

namespace MultiFishTouchResponse
{
    public class DataStructure<T>
    {
        public T[] GetColumn(T[,] matrix, int columnNumber)
        {
            return Enumerable.Range(0, matrix.GetLength(0))
                    .Select(x => matrix[x, columnNumber])
                    .ToArray();
        }

        public T[] GetRow(T[,] matrix, int rowNumber)
        {
            return Enumerable.Range(0, matrix.GetLength(1))
                    .Select(x => matrix[rowNumber, x])
                    .ToArray();
        }

    }

    class DataComputation 
    { 
        public List<List<int>> WhereEqual(Mat matrix, int num)
        {
            var height = matrix.Height;
            var width = matrix.Width;
            List<int> h_index = new List<int>();
            List<int> w_index = new List<int>();
            for (int h=0; h<height; h++)
            {
                for (int w=0; w<width; w++)
                {
                    Scalar colour = matrix.At<byte>(h, w);
                    if (colour.Val0 == num)
                    {
                        h_index.Add(h);
                        w_index.Add(w);
                    }
                }
            }
            List<List<int>> result = new List<List<int>>();
            result.Add(h_index);
            result.Add(w_index);
            return result;
        }

        public List<List<int>> WhereGreater(Mat matrix, int num)
        {
            var height = matrix.Height;
            var width = matrix.Width;
            List<int> h_index = new List<int>();
            List<int> w_index = new List<int>();
            for (int h = 0; h < height; h++)
            {
                for (int w = 0; w < width; w++)
                {
                    Scalar colour = matrix.At<byte>(h, w);
                    if (colour.Val0 > num)
                    {
                        h_index.Add(h);
                        w_index.Add(w);
                    }
                }
            }
            List<List<int>> result = new List<List<int>>();
            result.Add(h_index);
            result.Add(w_index);
            return result;
        }

        public List<List<int>> Wherelower(Mat matrix, int num)
        {
            var height = matrix.Height;
            var width = matrix.Width;
            List<int> h_index = new List<int>();
            List<int> w_index = new List<int>();
            for (int h = 0; h < height; h++)
            {
                for (int w = 0; w < width; w++)
                {
                    Scalar colour = matrix.At<byte>(h, w);
                    if (colour.Val0 < num)
                    {
                        h_index.Add(h);
                        w_index.Add(w);
                    }
                }
            }
            List<List<int>> result = new List<List<int>>();
            result.Add(h_index);
            result.Add(w_index);
            return result;
        }

        public NDArray SetArrayValue2D(NDArray src, List<List<int>> inds, int num)
        {
            for (int i = 0; i < inds[0].Count(); i++)
            {
                src[inds[0][i], inds[1][i]] = num;
            }
            return src;
        }

        public NDArray SetSelection(NDArray src, List<int> rowNums, List<int> colNums, int value = 255)
        {

            foreach (var tuple in rowNums.Zip(colNums, (x, y) => (x, y)))
            {
                src[tuple.Item1, tuple.Item2] = value;
            }

            return src;
        }
    }

    class DataTransformation
    {
        public NDArray Mat2Array(Mat src, Type dtpye)
        {
            var height = src.Height;
            var width = src.Width;
            var out_array = np.zeros((height, width), dtype: dtpye);
            
            for (int h=0; h<height; h++)
            {
                using (var mat_Row = new Mat())
                {
                    src.Row(h).CopyTo(mat_Row);
                    mat_Row.GetArray(out byte[] plainArray);
                    out_array[h, Slice.All] = np.array(plainArray, dtype: dtpye);
                };
            }
            return out_array;
        }

        public Mat Array2Mat(NDArray src)
        {
            var height = src.shape[0];
            var width = src.shape[1];
            Mat out_mat = new Mat(height, width, MatType.CV_8UC1, new Scalar(0));

            for (int h = 0; h < height; h++)
            {
                for (int w=0; w<width; w++)
                {
                    int value = src[h, w];
                    out_mat.Set(h, w, value);
                }
                    
            }
            return out_mat;
        }

        public Mat Prob2Mat(NDArray src, double threshold)
        {
            var height = src.shape[0];
            var width = src.shape[1];
            Mat out_mat = new Mat(height, width, MatType.CV_8UC1, new Scalar(0));

            for (int h = 0; h < height; h++)
            {
                for (int w = 0; w < width; w++)
                {
                    float value = src[h, w];
                    if(value > threshold)
                        out_mat.Set(h, w, 255);
                    else
                        out_mat.Set(h, w, 0);
                }

            }
            return out_mat;
        }

        public NDArray List2NDArray2D(List<List<int>> src)
        {
            int s_num = src[0].Count();
            NDArray src_array = new NDArray(np.int32, new Shape(new int[2] { s_num, 2 }));
            src_array[Slice.All, 0] = src[0].ToArray();
            src_array[Slice.All, 1] = src[1].ToArray();

            return src_array;
        }

        public Bitmap Mat2Bitmap(Mat src)
        {
            return OpenCvSharp.Extensions.BitmapConverter.ToBitmap(src);
        }

        public Mat Bitmap2Mat(Bitmap src)
        {
            return OpenCvSharp.Extensions.BitmapConverter.ToMat(src);
        }
        /*
        public BitmapSource Mat2BitmapSource(Mat src)
        {
            var temp = Mat2Bitmap(src);
            return GetBitmapSource(temp);
        }

        public Mat BitmapSource2Mat(BitmapSource src)
        {
            var temp = BitmapFromSource(src);
            return Bitmap2Mat(temp);
        }

        
        public  Bitmap BitmapFromSource(BitmapSource bitmapsource)
        {
            System.Drawing.Bitmap bitmap;
            using (System.IO.MemoryStream outStream = new System.IO.MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapsource));
                enc.Save(outStream);
                bitmap = new System.Drawing.Bitmap(outStream);
            }
            return bitmap;
        }


        public BitmapSource GetBitmapSource(Bitmap image)
        {
            var rect = new Rectangle(0, 0, image.Width, image.Height);
            var bitmap_data = image.LockBits(rect, ImageLockMode.ReadOnly, image.PixelFormat);

            try
            {
                BitmapPalette palette = null;

                if (image.Palette.Entries.Length > 0)
                {
                    var palette_colors = image.Palette.Entries.Select(entry => System.Windows.Media.Color.FromArgb(entry.A, entry.R, entry.G, entry.B)).ToList();
                    palette = new BitmapPalette(palette_colors);
                }

                return BitmapSource.Create(
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
        */

    }
}

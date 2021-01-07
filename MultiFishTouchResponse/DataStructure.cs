using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;
using NumSharp;

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

        public System.Drawing.Bitmap Mat2Bitmap(Mat src)
        {
            return OpenCvSharp.Extensions.BitmapConverter.ToBitmap(src);
        }

        public Mat Bitmap2Mat(System.Drawing.Bitmap src)
        {
            return OpenCvSharp.Extensions.BitmapConverter.ToMat(src);
        }
    }
}

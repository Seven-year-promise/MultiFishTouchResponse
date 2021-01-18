using Accord.Imaging;
using NumSharp;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tensorflow;
using static Tensorflow.Binding;

namespace MultiFishTouchResponse
{
    public static class Globals{
        public static double epsilon = 1e-5;
    }
    class ImBlob
    {
        public List<int> xs;
        public List<int> ys;

        public int Height { get; set; }
        public int Width { get; set; }
        public Point2d CenterGravity;
        public Point2d CenterRect;
        public System.Drawing.Rectangle rect;

        public ImBlob(List<List<int>> blob)
        {
            this.ys = blob[0];
            this.xs = blob[1];
            int y_min = ys.Min();
            int y_max = ys.Max();
            int x_min = xs.Min();
            int x_max = xs.Max();
            this.Height = y_max - y_min;
            this.Width = x_max - x_min;
            this.CenterGravity.Y = ys.Average();
            this.CenterGravity.X = xs.Average();
            this.CenterRect.Y = (y_min + y_max) / 2.0;
            this.CenterRect.X = (x_min + x_max) / 2.0;
            this.rect = new System.Drawing.Rectangle(x_min, y_min, this.Width, this.Height);
        }
    }
    class ImageProcessing
    {
        private UNet_tf unet = null;
        private RegionGrowing rg = null;
        private NeedleDetectionThre needle_detector = null;
        private DataComputation dataOperator;
        private DataTransformation dataTransfer;
        private PostProcessing post_processor;

        private ViewModel viewModel;
        private DebugView DebugWindow;
        private System.Windows.Media.Imaging.BitmapSource image;

        public ImageProcessing(ViewModel viewmodel, DebugView debugview)
        {
            viewModel = viewmodel;
            DebugWindow = debugview;
            post_processor = new PostProcessing();
            unet = new UNet_tf("UNet18000.pb");
            dataOperator = new DataComputation();
            dataTransfer = new DataTransformation();
            rg = new RegionGrowing(5);
            needle_detector = new NeedleDetectionThre(50);
            unet.load_graph();
        }

        //To end infinite while loop
        private bool disposed = false;
        public void Dispose()
        {
            disposed = true;
        }

        public void Begin()
        {
            disposed = false;
        }

        public bool detection_failed = false;
        public void run()
        {
            Task.Run(() =>
            {
                while (disposed == false)
                {
                    AnalyseImage();
                }
            });
        }

        public void AnalyseImage() {

            bool succesful = Ximea.CameraImageQueue.TryTake(out image);
            
            if ((succesful == true)&(image!=null))
            {
                if (viewModel.CannyChecked == true)
                {
                    try {
                        if (viewModel.debug)
                        {
                            string this_time = System.DateTime.Now.ToString("HHmmss");
                            Console.WriteLine("image processing begin" + this_time);
                        }
                        var im_bitmap = BitmapFromSource(image);
                        viewModel.Videoname = "WT" + "_" + System.DateTime.Now.ToString("HHmmss") + "_Speed" + viewModel.final_speed_factor;
                        Mat src_from_bitmap = dataTransfer.Bitmap2Mat(im_bitmap);
                        Mat src_rotated = new Mat();
                        Cv2.Rotate(src_from_bitmap, src_rotated, RotateFlags.Rotate90Counterclockwise);
                        Mat src = new Mat();
                        Cv2.Flip(src_rotated, src, FlipMode.Y);
                        //var src = new Mat("lenna.png", ImreadModes.Grayscale);
                        Cv2.ImWrite(Ximea.Path + "\\" + viewModel.Videoname + "_ori.jpg", src);
                        var analyzed_color = new Mat();
                        src.CopyTo(analyzed_color);
                        var gray = new Mat();
                        //Cv2.CvtColor(src: src, dst: gray, code: ColorConversionCodes.BGR2GRAY);
                        int[] well_info = new int[3];
                        var masked_im = well_detection(src, out well_info);
                        Rect well_area = new Rect((well_info[1] - 120), (well_info[0] - 120), 240, 240);
                        var im_block = masked_im[well_area];
                        // TODO  I do now know why, need to check it later
                        //Ximea.StopCamera = true;
                        //Task.Delay(500).Wait();  // wait until camera stops
                        if (viewModel.debug)
                        {
                            string this_time = System.DateTime.Now.ToString("HHmmss");
                            Console.WriteLine("unet begin " + this_time);
                        }
                        //var needle_point = needle_detector.run(im_block);
                        var binaries = unet.run(im_block);
                        if (viewModel.debug)
                        {
                            string this_time = System.DateTime.Now.ToString("HHmmss");
                            Console.WriteLine("unet end " + this_time);
                        }
                        Mat needle_binary = new Mat(src.Rows, src.Cols, MatType.CV_8UC1, new Scalar(0));
                        Mat larva_binary = new Mat(src.Rows, src.Cols, MatType.CV_8UC1, new Scalar(0));
                        needle_binary[well_area] = binaries[0];
                        larva_binary[well_area] = binaries[1];

                        Cv2.ImWrite(Ximea.Path + "\\" + viewModel.Videoname + "_binary_needle.jpg", needle_binary);
                        Cv2.ImWrite(Ximea.Path + "\\" + viewModel.Videoname + "_binary_larva.jpg", larva_binary);
                        var needle_point = post_processor.find_needle_point(needle_binary, src);
                        if (needle_point != null)
                        {
                            //Mat needle_display = new Mat();
                            //src.CopyTo(needle_display);
                            Cv2.Circle(analyzed_color, centerX: needle_point[1], centerY: needle_point[0], 3, new Scalar(0, 255, 0), 3);
                            var fish_binary = post_processor.select_big_blobs(larva_binary, out List<List<List<int>>> fish_blobs, out List<List<int>> closest_blob, needle_point, size: 44);
                            if (closest_blob != null)
                            {
                                //Cv2.Canny(src, dst, 50, 200);
                                var fish_display = dataTransfer.Array2Mat(fish_binary);
                                var fish_bitmap = dataTransfer.Mat2Bitmap(fish_display);

                                var fish_point = post_processor.find_fish_point(fish_bitmap, out double angle, closest_blob, 0.75);
                                //var fish_point = find_fish_skeleton_point(fish_display, closest_blob, 0.75);
                                TrajectoryGenerate(fish_point, needle_point, angle, 30);

                                Cv2.Circle(analyzed_color, centerX: fish_point[1], centerY: fish_point[0], 3, new Scalar(0, 0, 255), 3);
                                Cv2.ImWrite(Ximea.Path + "\\" + viewModel.Videoname + "_analyzed.jpg", analyzed_color);
                                
                                // using (new Window("needle image", needle_display))
                                //uing (new Window("needle_binary", needle_binary))
                                //using (new Window("larva_binary", larva_binary))
                                //{
                                    //Cv2.WaitKey();
                                //}
                                
                                src = fish_display;
                                viewModel.CannyChecked = false;
                            }
                            {
                                detection_failed = true;
                            }
                        }
                        else
                        {
                            detection_failed = true;
                        }
                        if (detection_failed)
                        {
                            viewModel.CannyChecked = false;
                        }
                        
                        //var analysed_bitmap = dataTransfer.Mat2Bitmap(src);
                        //var bitmapsource = ConvertBitmap(analysed_bitmap);

                        //bitmapsource.Freeze();
                        //viewModel.AnalysedImage = bitmapsource;
                        src.Release();
                        if (viewModel.debug)
                        {
                            string this_time = System.DateTime.Now.ToString("HHmmss");
                            Console.WriteLine("image processing end" + this_time);
                        }
                       
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }
                

                viewModel.AnalysedImage = image;
                /*
                 * if (Ximea.StopCamera)
                {
                    Ximea.StopCamera = false;
                    Ximea.StartCamera();
                }
                */

            }
        }


        public Mat well_detection(Mat im, out int[] well_info, int threshold = 50)
        {
            var rows = im.Height;
            var circles = Cv2.HoughCircles(im, HoughMethods.Gradient, 1, rows / 5,
                                            param1: 240, param2: 50,
                                            minRadius: 95, maxRadius: 105);
            int well_centerx = 0;
            int well_centery = 0;
            int well_radius = 0;
            var circle_num = circles.Count();

            if (circle_num > 0) {
                float[,] circles_array = new float[circle_num, 2];
                for (int i = 0; i < circle_num; i++)
                {
                    circles_array[i, 0] = circles[i].Center.Y;
                    circles_array[i, 1] = circles[i].Center.X;
                }
                var ArrayOperate = new DataStructure<float>();
                var xs = ArrayOperate.GetColumn(circles_array, 1);
                well_centerx = (int)(Queryable.Average(xs.AsQueryable()));
                var ys = ArrayOperate.GetColumn(circles_array, 0);
                well_centery = (int)(Queryable.Average(ys.AsQueryable()));
                well_radius = 115; //np.uint16(np.round(np.average(circles[0, :, 2])))
            }
            else {
                well_centerx = 240;
                well_centery = 240;
                well_radius = 115;
            }
            //return False, (240, 240, 110)

            //first rough mask for well detection
            var gray_masked = new Mat();

            using (var mask = new Mat(rows, rows, MatType.CV_8UC1, new Scalar(0)))
            {
                Cv2.Circle(img: mask,
                           centerX: well_centerx, centerY: well_centery,
                           radius: well_radius, new Scalar(255), -1, LineTypes.Link8, 0);
                Cv2.BitwiseAnd(im, mask, gray_masked);
            }


            // second fine-tuned mask
            var th = new Mat();
            Cv2.Threshold(gray_masked, th, threshold, 255, ThresholdTypes.Binary);
            Mat element = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
            var closing = new Mat();
            Cv2.MorphologyEx(th, closing, MorphTypes.Close, element);
            var im_closing = new Mat();
            Cv2.BitwiseAnd(im, closing, im_closing);

            var white_indexes = dataOperator.WhereEqual(closing, 255);
            well_centery = (int)(Math.Round(white_indexes[0].Average()));
            well_centerx = (int)(Math.Round(white_indexes[1].Average()));

            // third fine-tuned mask for background white
            var closing_inv = new Mat();
            Cv2.BitwiseNot(closing, closing_inv);
            //closing_inv = np.array((closing_inv, closing_inv, closing_inv)).transpose(1, 2, 0);
            var im_closing_inv = closing_inv + im_closing;


            // cv2.circle(gray, (well_centerx, well_centery), 1, (0, 255, 0), 5)
            // cv2.imshow("detected circles", im_closing_inv)
            // cv2.waitKey(1000)

            well_info = new int[3] { well_centery, well_centerx, well_radius };

            return im_closing_inv;
        }

        public void TrajectoryGenerate(int[] fish_point, int[] needle_point, double angle, int distancefromfish)
        {
            //DebugWindow.MoveToPoint(Needle, middlepoint);
            //Calculate Path
            //Perpendicular line through fish and Point C
            //L(x) = Cy+1/tan(angle)*Cx-1/tan(angle)*x
            //need to define a minimum distance the needle should go to next to the fish
            //to do this we search intersection with circle of radius d and center C
            //d^2 = (x-Cx)^2+(y-Cy)^2 and L(x)=y
            //will give us two Points P1 and P2 
            //with special conditions for angle = 0, 180 or -180 -> P1(Cx+d Cy) and P2(Cx-d Cy)
            //with special conditions for angle = 90 or -90      -> P1(Cx Cy+d) and P2(Cx Cy-d)
            //We want the P with smallest distance D to Needle N
            //D^2=(Nx-Px)^2+(Ny-Py)^2 will give us either P1 or P2 as smallest
            //check if chosen P is inside of valid area in well (not outside the ring), well center Z
            //if D^2>r^2 for D^2=(Zx-Px)^2+(Zy-Py)^2 then P is inside circle       
            //Needle can then be moved from N to P through C to opposing P
            double tan = Math.Tan(angle * (Math.PI / 180));
            double cubictan = Math.Pow(tan, 3);
            double squaretan = Math.Pow(tan, 2);
            double sqrt = Math.Sqrt(Math.Pow(distancefromfish, 2) * (Math.Pow(squaretan, 2) + squaretan));
            System.Drawing.Point[] P = new System.Drawing.Point[2];
            if (0.1 > Math.Abs(tan))
            {

                P[0].X = fish_point[1];
                P[0].Y = fish_point[0] + distancefromfish;

                P[1].X = fish_point[1];
                P[1].Y = fish_point[0] - distancefromfish;
            }
            else if (1000 < Math.Abs(tan))
            {
                P[0].X = fish_point[1] + distancefromfish;
                P[0].Y = fish_point[0];
                P[1].X = fish_point[1] - distancefromfish;
                P[1].Y = fish_point[0];
            }
            else
            {
                P[0].X = (int)((fish_point[1] * (squaretan + 1) - sqrt) / (squaretan + 1));
                P[0].Y = (int)((fish_point[0] * (cubictan + tan) + sqrt) / (cubictan + tan));
                P[1].X = (int)((fish_point[1] * (squaretan + 1) + sqrt) / (squaretan + 1));
                P[1].Y = (int)((fish_point[0] * (cubictan + tan) - sqrt) / (cubictan + tan));
            }
            //viewModel.CannyChecked = false;
            int D1 = (needle_point[1] - P[0].X) * (needle_point[1] - P[0].X) + (needle_point[0] - P[0].Y) * (needle_point[0] - P[0].Y);
            int D2 = (needle_point[1] - P[1].X) * (needle_point[1] - P[1].X) + (needle_point[0] - P[1].Y) * (needle_point[0] - P[1].Y);
            if (D1 > D2)
            {
                Array.Reverse(P);
            }

            System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate {
                viewModel.Lines.Clear();
                double pixelratio = viewModel.Border.ActualWidth / 480;
                System.Windows.Shapes.Line line1 = new System.Windows.Shapes.Line();
                line1.X1 = needle_point[1] * pixelratio;
                line1.Y1 = needle_point[0] * pixelratio;
                line1.X2 = P[0].X * pixelratio;
                line1.Y2 = P[0].Y * pixelratio;
                System.Windows.Shapes.Line line2 = new System.Windows.Shapes.Line();
                line2.X1 = P[0].X * pixelratio;
                line2.Y1 = P[0].Y * pixelratio;
                line2.X2 = P[1].X * pixelratio;
                line2.Y2 = P[1].Y * pixelratio;
                line2.X2 = (P[0].X * 0.4 + P[1].X * 0.6) * pixelratio;
                line2.Y2 = (P[0].Y * 0.4 + P[1].Y * 0.6) * pixelratio;
                viewModel.Lines.Add(line1);
                viewModel.Lines.Add(line2);
            });
        }


        #region Bitmap Conversion Methods
        private System.Drawing.Bitmap BitmapFromSource(System.Windows.Media.Imaging.BitmapSource bitmapsource)
        {
            System.Drawing.Bitmap bitmap;
            using (System.IO.MemoryStream outStream = new System.IO.MemoryStream())
            {
                System.Windows.Media.Imaging.BitmapEncoder enc = new System.Windows.Media.Imaging.BmpBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmapsource));
                enc.Save(outStream);
                bitmap = new System.Drawing.Bitmap(outStream);
            }
            return bitmap;
        }


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

        //Convert Aforge Bitmap back to WPF Bitmapsource
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
        private System.Windows.Media.Imaging.BitmapSource ConvertBitmap(System.Drawing.Bitmap source)
        {
            using (source)
            {
                IntPtr hBitmap = source.GetHbitmap();
                var image = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                            hBitmap,
                            IntPtr.Zero,
                            System.Windows.Int32Rect.Empty,
                            System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                DeleteObject(hBitmap); //otherwise memory leak due to hbitmap
                return image;
            }

        }
        #endregion
    }

    
}

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
        private Mat analyzed_color = new Mat();

        public List<List<List<int>>> old_larva_blobs = null;
        public List<List<List<int>>> new_larva_blobs = null;

        Mat needle_binary = null;
        Mat larva_binary = null;
        Mat gray_now = null;

        int[] needle_point = new int[2];
        int[] larva_point = new int[2];
        int[] well_info_ori = new int[3];

        public int detected_larva_num = 0;
        public int touched_larva_cnt = 0;

        double[] percentages;

        List<String> model_file_list = new List<String>() {
                "./models_update/UNet30000-well6.pb",
                "./models_update/UNet14000.pb"                
            };

        List<int> well_radius_list = new List<int>() {
                175,
                100
            };

        List<int> unet_input_size_list = new List<int>() {
                400,
                240
            };

        public ImageProcessing(ViewModel viewmodel, DebugView debugview)
        {
            viewModel = viewmodel;
            DebugWindow = debugview;
            post_processor = new PostProcessing();
            
            unet = new UNet_tf(model_file_list[wellInformation.wellTpyeIndex]);
            dataOperator = new DataComputation();
            dataTransfer = new DataTransformation();
            rg = new RegionGrowing(5);
            needle_detector = new NeedleDetectionThre(50);
            unet.load_graph();
            percentages = new double[3] { 0.05, 0.3, 0.65 };
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
        public bool got_next_fished = false;
        public void run()
        {
            Task.Run(() =>
            {
                while (disposed == false)
                {
                    TakeImage();
                }
            });
        }

        public bool DetectionINIT = false;
        public void InitDetection()
        {
            DetectionINIT = true;
        }

        public void chooseOneLarva()
        {
            DetectionINIT = false;
        }

        public void TakeImage() {
            System.Windows.Media.Imaging.BitmapSource taken_image = null;
            bool succesful = Ximea.CameraImageQueue.TryTake(out taken_image);
            if ((succesful == true) & (taken_image != null))
            {
                this.image = taken_image;
                if (this.DetectionINIT == true)
                {
                    try
                    {
                        InitAnalyseImage();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                    
                }

                viewModel.AnalysedImage = this.image;
            }
        }
        public void InitAnalyseImage() {
            if (viewModel.debug)
            {
                string this_time = System.DateTime.Now.ToString("HHmmss");
                Console.WriteLine("image processing begin" + this_time);
            }
            var im_bitmap = BitmapFromSource(this.image);
            viewModel.Videoname = "WT" + "_" + System.DateTime.Now.ToString("HHmmss") + "_Speed" + viewModel.final_speed_factor;
            Mat src_from_bitmap = dataTransfer.Bitmap2Mat(im_bitmap);
            this.well_info_ori = new int[3];
            //var masked_im_bat = well_detection(src_from_bitmap, out this.well_info_ori);

            Mat src_rotated = new Mat();
            Cv2.Rotate(src_from_bitmap, src_rotated, RotateFlags.Rotate90Counterclockwise);
            Mat src = new Mat();
            Cv2.Flip(src_rotated, src, FlipMode.Y);
            this.gray_now = src;
            //var src = new Mat("lenna.png", ImreadModes.Grayscale);
            Cv2.ImWrite(Ximea.Path + "\\" + viewModel.Videoname + "_ori.jpg", src);
            src.CopyTo(this.analyzed_color);
            Cv2.CvtColor(this.analyzed_color, this.analyzed_color, ColorConversionCodes.GRAY2BGR);
            var gray = new Mat();
            //Cv2.CvtColor(src: src, dst: gray, code: ColorConversionCodes.BGR2GRAY);
            var masked_im = well_detection(src, out well_info_ori);
            if (masked_im == null)
            {
                detection_failed = true;
                this.DetectionINIT = false;
                src.Release();
            }
            else
            {
                Cv2.Circle(this.analyzed_color, centerX: this.well_info_ori[1], 
                    centerY: this.well_info_ori[0], this.well_info_ori[2], new Scalar(255, 255, 0), 2);
                Cv2.ImWrite(Ximea.Path + "\\" + viewModel.Videoname + "_well.jpg", masked_im);
                Rect well_area = new Rect((this.well_info_ori[1] - unet_input_size_list[wellInformation.wellTpyeIndex]/2), 
                    (this.well_info_ori[0] - unet_input_size_list[wellInformation.wellTpyeIndex] / 2), 
                    unet_input_size_list[wellInformation.wellTpyeIndex],
                    unet_input_size_list[wellInformation.wellTpyeIndex]);
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
                var binary_inds = post_processor.compare_binary(binaries); // make sure the first binary is the needle.
                if (viewModel.debug)
                {
                    string this_time = System.DateTime.Now.ToString("HHmmss");
                    Console.WriteLine("unet end " + this_time);
                }
                this.needle_binary = new Mat(src.Rows, src.Cols, MatType.CV_8UC1, new Scalar(0));
                this.larva_binary = new Mat(src.Rows, src.Cols, MatType.CV_8UC1, new Scalar(0));
                this.needle_binary[well_area] = binaries[binary_inds[0]];
                this.larva_binary[well_area] = binaries[binary_inds[1]];

                Mat element = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
                var closing0 = new Mat();
                Cv2.MorphologyEx(this.needle_binary, closing0, MorphTypes.Close, element);

                Mat labels = new Mat();
                int needle_num = Cv2.ConnectedComponents(closing0, labels);
                /*
                if(needle_num != 1)
                {
                    Rect needle_area = new Rect(200, 200, 80, 80);
                    this.needle_binary[needle_area] = new Mat(80, 80, MatType.CV_8UC1, new Scalar(1)); 
                }
                */
                Rect needle_area = new Rect(this.well_info_ori[1] - 50, this.well_info_ori[0] - 50, 100, 100);
                this.needle_binary[needle_area] = new Mat(100, 100, MatType.CV_8UC1, new Scalar(255));

                this.needle_point = post_processor.find_needle_point(this.needle_binary, src);
                Cv2.ImWrite(Ximea.Path + "\\" + viewModel.Videoname + "_binary_needle.jpg", this.needle_binary);
                Cv2.ImWrite(Ximea.Path + "\\" + viewModel.Videoname + "_binary_larva.jpg", this.larva_binary);

               
                //needle_point = needle_detector.run(im_block);
                //needle_point[0] = 100;
                //needle_point[1] = 300;
                if (this.needle_point != null)
                {
                    //Mat needle_display = new Mat();
                    //src.CopyTo(needle_display);
                
                    post_processor.select_big_blobs(this.larva_binary, out List<List<List<int>>> larva_blobs, this.needle_point, size: 12);
                    //closest_blob = null;
                    this.detected_larva_num = larva_blobs.Count();
                    if (this.detected_larva_num > 0)
                    {
                        this.new_larva_blobs = larva_blobs;
                    }
                    else{
                        detection_failed = true;
                    }
                }
                else
                {
                    detection_failed = true;
                }
                this.DetectionINIT = false;
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
        }

        public void SecondaryAnalyseImage()
        {
            try
            {
                if (viewModel.debug)
                {
                    string this_time = System.DateTime.Now.ToString("HHmmss");
                    Console.WriteLine("image processing begin" + this_time);
                }
                var im_bitmap = BitmapFromSource(this.image);
                viewModel.Videoname = "WT" + "_" + System.DateTime.Now.ToString("HHmmss") + "_Speed" + viewModel.final_speed_factor;
                Mat src_from_bitmap = dataTransfer.Bitmap2Mat(im_bitmap);
                this.well_info_ori = new int[3];
                //var masked_im_bat = well_detection(src_from_bitmap, out this.well_info_ori);

                Mat src_rotated = new Mat();
                Cv2.Rotate(src_from_bitmap, src_rotated, RotateFlags.Rotate90Counterclockwise);
                Mat src = new Mat();
                Cv2.Flip(src_rotated, src, FlipMode.Y);
                this.gray_now = src;
                //var src = new Mat("lenna.png", ImreadModes.Grayscale);
                Cv2.ImWrite(Ximea.Path + "\\" + viewModel.Videoname + "_ori.jpg", src);
                src.CopyTo(this.analyzed_color);
                Cv2.CvtColor(this.analyzed_color, this.analyzed_color, ColorConversionCodes.GRAY2BGR);
                var gray = new Mat();
                //Cv2.CvtColor(src: src, dst: gray, code: ColorConversionCodes.BGR2GRAY);
                var masked_im = well_detection(src, out this.well_info_ori);
                var masked_im_strong = well_detection_strong(src, out this.well_info_ori);
                if (masked_im == null)
                {
                    this.larva_binary = null;
                }
                else
                {
                    Cv2.Circle(this.analyzed_color, centerX: this.well_info_ori[1], centerY: this.well_info_ori[0], this.well_info_ori[2], new Scalar(255, 255, 0), 2);
                    Cv2.ImWrite(Ximea.Path + "\\" + viewModel.Videoname + "_well.jpg", masked_im);
                    //make sure where the needle is
                    this.needle_point = post_processor.needle_usingMaxima(masked_im_strong, this.needle_point, 14);
                    Rect well_area = new Rect((this.well_info_ori[1] - unet_input_size_list[wellInformation.wellTpyeIndex] / 2),
                        (this.well_info_ori[0] - unet_input_size_list[wellInformation.wellTpyeIndex] / 2),
                        unet_input_size_list[wellInformation.wellTpyeIndex], 
                        unet_input_size_list[wellInformation.wellTpyeIndex]);
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
                    var binary_inds = post_processor.compare_binary(binaries); // make sure the first binary is the needle.
                    if (viewModel.debug)
                    {
                        string this_time = System.DateTime.Now.ToString("HHmmss");
                        Console.WriteLine("unet end " + this_time);
                    }
                    this.larva_binary = new Mat(src.Rows, src.Cols, MatType.CV_8UC1, new Scalar(0));
                    this.larva_binary[well_area] = binaries[binary_inds[1]];
                    Cv2.ImWrite(Ximea.Path + "\\" + viewModel.Videoname + "_binary_needle.jpg", this.needle_binary);
                    Cv2.ImWrite(Ximea.Path + "\\" + viewModel.Videoname + "_binary_larva.jpg", this.larva_binary);
                    src.Release();
                    if (viewModel.debug)
                    {
                        string this_time = System.DateTime.Now.ToString("HHmmss");
                        Console.WriteLine("image processing end" + this_time);
                    }
                }
            }
            catch (Exception ex)
            {
                this.larva_binary = null;
                Console.WriteLine(ex.ToString());
            }
        }

        public void goToNestLarva()
        {
            if (this.old_larva_blobs == null)
            {
                var closest_blob = this.new_larva_blobs[0];
                var larva_binary = post_processor.get_binary_one_blob(this.larva_binary.Rows, this.larva_binary.Cols, closest_blob);
                var fish_display = dataTransfer.Array2Mat(larva_binary);
                var fish_bitmap = dataTransfer.Mat2Bitmap(fish_display);

                var fish_point = post_processor.find_fish_point(fish_bitmap,
                                                                out double angle,
                                                                closest_blob,
                                                                percentages[viewModel.fishSelectedPart]);
                //var fish_point = find_fish_skeleton_point(fish_display, closest_blob, 0.75);
                Cv2.Circle(this.analyzed_color, centerX: this.needle_point[1], centerY: this.needle_point[0], 2, new Scalar(0, 255, 0), 2);
                Cv2.Circle(this.analyzed_color, centerX: fish_point[1], centerY: fish_point[0], 2, new Scalar(255, 0, 0), 2);
                if (viewModel.debug)
                {
                    string this_time = System.DateTime.Now.ToString("HHmmss");
                    Console.WriteLine("image processing end" + this_time);
                }
                TrajectoryGenerate(fish_point, this.well_info_ori, angle, 30);
                this.old_larva_blobs = this.new_larva_blobs;
                this.touched_larva_cnt += 1;
            }
            else
            {
                SecondaryAnalyseImage();
                if(this.larva_binary != null)
                {
                    var new_blob = post_processor.find_new_blob(this.old_larva_blobs[this.touched_larva_cnt], this.larva_binary, size: 12, iou_thre: 0.5);
                    if(new_blob != null)
                    {
                        var larva_binary = post_processor.get_binary_one_blob(this.larva_binary.Rows, this.larva_binary.Cols, new_blob);
                        var fish_display = dataTransfer.Array2Mat(larva_binary);
                        var fish_bitmap = dataTransfer.Mat2Bitmap(fish_display);

                        var fish_point = post_processor.find_fish_point(fish_bitmap,
                                                                        out double angle,
                                                                        new_blob,
                                                                        percentages[viewModel.fishSelectedPart]);
                        //var fish_point = find_fish_skeleton_point(fish_display, closest_blob, 0.75);
                        Cv2.Circle(this.analyzed_color, centerX: this.needle_point[1], centerY: this.needle_point[0], 2, new Scalar(0, 255, 0), 2);
                        Cv2.Circle(this.analyzed_color, centerX: fish_point[1], centerY: fish_point[0], 2, new Scalar(255, 0, 0), 2);
                        TrajectoryGenerate(fish_point, this.well_info_ori, angle, 30);
                    }
                }
                this.touched_larva_cnt += 1;
            }
            System.IO.File.WriteAllLines(Ximea.Path + "\\" + viewModel.Videoname + "_needle_point.txt", this.needle_point.Select(diff => diff.ToString()));
            Cv2.ImWrite(Ximea.Path + "\\" + viewModel.Videoname + "_analyzed.jpg", this.analyzed_color);
            this.got_next_fished = true;
        }

        public Mat well_detection(Mat im, out int[] well_info, int threshold = 50)
        {
            var rows = im.Height;
            var circles = Cv2.HoughCircles(im, HoughMethods.Gradient, 1, rows / 5,
                                            param1: 220, param2: 30,
                                            minRadius: well_radius_list[wellInformation.wellTpyeIndex] - 5, 
                                            maxRadius: well_radius_list[wellInformation.wellTpyeIndex] + 5);
            int well_centerx = 0;
            int well_centery = 0;
            int well_radius = 0;
            var circle_num = circles.Count();

            if (circle_num > 0)
            {
                float[,] circles_array = new float[circle_num, 3];
                for (int i = 0; i < circle_num; i++)
                {
                    circles_array[i, 0] = circles[i].Center.Y;
                    circles_array[i, 1] = circles[i].Center.X;
                    circles_array[i, 2] = circles[i].Radius;
                }
                var ArrayOperate = new DataStructure<float>();
                var xs = ArrayOperate.GetColumn(circles_array, 1);
                well_centerx = (int)(Queryable.Average(xs.AsQueryable()));
                var ys = ArrayOperate.GetColumn(circles_array, 0);
                well_centery = (int)(Queryable.Average(ys.AsQueryable()));
                var rs = ArrayOperate.GetColumn(circles_array, 2);
                well_radius = well_radius_list[wellInformation.wellTpyeIndex] + 10; // (int)(Queryable.Average(rs.AsQueryable())); //np.uint16(np.round(np.average(circles[0, :, 2])))
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

                /*
                // second fine-tuned mask
                var th = new Mat();
                Cv2.Threshold(gray_masked, th, threshold, 255, ThresholdTypes.Binary);
                Mat element = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(10, 10));
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
                */

                /*
                Cv2.Circle(im, well_centerx, well_centery, 104, new Scalar(0, 255, 0), 1);
                using (new Window("well", im))
                {
                    Cv2.WaitKey(0);
                }
                */
                well_radius = well_radius_list[wellInformation.wellTpyeIndex] - 5; // for safeties of trajectory
                well_info = new int[3] { well_centery, well_centerx, well_radius };

                return gray_masked;
            }
            else
            {
                well_info = null;
                return null;
            }

        }

        public Mat well_detection_strong(Mat im, out int[] well_info, int threshold = 50)
        {
            var rows = im.Height;
            var circles = Cv2.HoughCircles(im, HoughMethods.Gradient, 1, rows / 5,
                                            param1: 220, param2: 30,
                                            minRadius: well_radius_list[wellInformation.wellTpyeIndex] - 5, 
                                            maxRadius: well_radius_list[wellInformation.wellTpyeIndex] + 5);
            int well_centerx = 0;
            int well_centery = 0;
            int well_radius = 0;
            var circle_num = circles.Count();

            if (circle_num > 0) {
                float[,] circles_array = new float[circle_num, 3];
                for (int i = 0; i < circle_num; i++)
                {
                    circles_array[i, 0] = circles[i].Center.Y;
                    circles_array[i, 1] = circles[i].Center.X;
                    circles_array[i, 2] = circles[i].Radius;
                }
                var ArrayOperate = new DataStructure<float>();
                var xs = ArrayOperate.GetColumn(circles_array, 1);
                well_centerx = (int)(Queryable.Average(xs.AsQueryable()));
                var ys = ArrayOperate.GetColumn(circles_array, 0);
                well_centery = (int)(Queryable.Average(ys.AsQueryable()));
                var rs = ArrayOperate.GetColumn(circles_array, 2);
                well_radius = well_radius_list[wellInformation.wellTpyeIndex] - 10; // (int)(Queryable.Average(rs.AsQueryable())); //np.uint16(np.round(np.average(circles[0, :, 2])))
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
                Mat element = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(10, 10));
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
                

                /*
                Cv2.Circle(im, well_centerx, well_centery, 104, new Scalar(0, 255, 0), 1);
                using (new Window("well", im))
                {
                    Cv2.WaitKey(0);
                }
                */
                well_radius = well_radius_list[wellInformation.wellTpyeIndex] - 5; // for safeties of trajectory
                well_info = new int[3] { well_centery, well_centerx, well_radius };

                return im_closing_inv;
            }
            else {
                well_info = null;
                return null;
            }
            
        }

        public void TrajectoryGenerate(int[] fish_point, int[] well_info, double angle, int distancefromfish)
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
            int D1 = (this.needle_point[1] - P[0].X) * (this.needle_point[1] - P[0].X) + (this.needle_point[0] - P[0].Y) * (this.needle_point[0] - P[0].Y);
            int D2 = (this.needle_point[1] - P[1].X) * (this.needle_point[1] - P[1].X) + (this.needle_point[0] - P[1].Y) * (this.needle_point[0] - P[1].Y);
            if (D1 > D2)
            {
                Array.Reverse(P);
            }

            P[1].X = (int)(P[0].X * 0.5 + P[1].X * 0.5);
            P[1].Y = (int)(P[0].Y * 0.5 + P[1].Y * 0.5);

            double distance_well_center = Math.Sqrt((well_info[1] - P[0].X) * (well_info[1] - P[0].X) 
                + (well_info[0] - P[0].Y) * (well_info[0] - P[0].Y));
            if (distance_well_center > well_info[2])
            {
                double cut_ratio = well_info[2] / distance_well_center;
                P[0].X = (int)(well_info[1] * (1 - cut_ratio) + P[0].X * cut_ratio);
                P[0].Y = (int)(well_info[0] * (1 - cut_ratio) + P[0].Y * cut_ratio);
            }
            Cv2.Circle(this.analyzed_color, centerX: well_info[1], centerY: well_info[0], well_info[2], new Scalar(0, 128, 255), 2);
            Cv2.Circle(this.analyzed_color, centerX: P[0].X, centerY: P[0].Y, 2, new Scalar(0, 255, 255), 2);
            /*
            distance_well_center = Math.Sqrt((well_info[1] - P[1].X) * (well_info[1] - P[1].X)
                + (well_info[0] - P[1].Y) * (well_info[0] - P[1].Y));

            if (distance_well_center > well_info[2])
            {
                double cut_ratio = well_info[2] / distance_well_center;
                P[1].X = (int)(well_info[1] * (1 - cut_ratio) + P[1].X * cut_ratio);
                P[1].Y = (int)(well_info[0] * (1 - cut_ratio) + P[1].Y * cut_ratio);
            }
            */

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
                //line2.X2 = P[1].X * pixelratio;
                //line2.Y2 = P[1].Y * pixelratio;
                line2.X2 = P[1].X * pixelratio;
                line2.Y2 = P[1].Y * pixelratio;
                viewModel.Lines.Add(line1);
                viewModel.Lines.Add(line2);
            });

            //this.needle_point = post_processor.needle_usingMaxima(this.gray_now, new int[2] { P[1].Y, P[1].X }, 14);
            this.needle_point[0] = P[1].Y;
            this.needle_point[1] = P[1].X;
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

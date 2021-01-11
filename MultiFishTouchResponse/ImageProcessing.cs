using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tensorflow;
using static Tensorflow.Binding;
using System.IO;
using OpenCvSharp;
using NumSharp;
using Accord.Imaging;
using System.Windows.Media.Imaging;
using System.Collections.Concurrent;

namespace MultiFishTouchResponse
{
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
        private DataComputation dataOperator;
        private DataTransformation dataTransfer;

        private ViewModel viewModel;
        private DebugView DebugWindow;
        private BitmapSource image;

        public ImageProcessing( ViewModel viewmodel, DebugView debugview)
        {
            viewModel = viewmodel;
            DebugWindow = debugview;

            unet = new UNet_tf("UNet18000.pb");
            dataOperator = new DataComputation();
            dataTransfer = new DataTransformation();
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

            if (succesful == true)
            {
                Mat src = dataTransfer.BitmapSource2Mat(image);
                if (viewModel.CannyChecked == true)
                {
                    //var src = new Mat("lenna.png", ImreadModes.Grayscale);

                    var dst = new Mat();
                    var gray = new Mat();
                    //Cv2.CvtColor(src: src, dst: gray, code: ColorConversionCodes.BGR2GRAY);
                    int[] well_info = new int[3];
                    var masked_im = well_detection(src, out well_info);
                    Rect well_area = new Rect((well_info[1] - 120), (well_info[0] - 120), 240, 240);
                    var im_block = masked_im[well_area];
                    var binaries = unet.run(im_block);

                    Mat needle_binary = new Mat(src.Rows, src.Cols, MatType.CV_8UC1, new Scalar(0));
                    Mat larva_binary = new Mat(src.Rows, src.Cols, MatType.CV_8UC1, new Scalar(0));
                    needle_binary[well_area] = binaries[0];
                    larva_binary[well_area] = binaries[1];
                    var needle_point = find_needle_point(needle_binary, src);
                    Mat needle_display = new Mat();
                    src.CopyTo(needle_display);
                    Cv2.Circle(needle_display, centerX: needle_point[1], centerY: needle_point[0], 3, new Scalar(0, 255, 0), 3);
                    var fish_binary = select_big_blobs(larva_binary, out List<List<List<int>>> fish_blobs, out List<List<int>> closest_blob, needle_point, size: 44);
                    //Cv2.Canny(src, dst, 50, 200);
                    var fish_display = dataTransfer.Array2Mat(fish_binary);
                    var fish_bitmap = dataTransfer.Mat2Bitmap(fish_display);
                    var fish_point = find_fish_point(fish_bitmap, closest_blob, 0.75);

                    Cv2.Circle(needle_display, centerX: fish_point[1], centerY: fish_point[0], 3, new Scalar(0, 255, 0), 3);
                    /*using (new Window("needle image", needle_display))
                    using (new Window("larva image", fish_display))
                    {
                        Cv2.WaitKey();
                    }
                    */
                    src = fish_display;
                    viewModel.CannyChecked = false;
                }

                var bitmapsource = dataTransfer.Mat2BitmapSource(src);

                bitmapsource.Freeze();
                viewModel.AnalysedImage = bitmapsource;
                src.Release();
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

        public NDArray select_big_blobs(Mat binary,
            out List<List<List<int>>> blobs_tuned,
            out List<List<int>> closest_blob,
            int[] needle_point,
            int size = 44)
        {
            Mat labels = new Mat();
            int label_num = Cv2.ConnectedComponents(binary, labels);
            //var labels_array = dataTransfer.Mat2Array(labels, np.int32);
            blobs_tuned = new List<List<List<int>>>();
            List<double> distances = new List<double>(9);
            double distance = 0;
            int distance_inx = 0;
            for (int l = 1; l < label_num; l++)
            {
                var coordinates = dataOperator.WhereEqual(labels, l);
                if (coordinates[0].Count() > size)
                {
                    blobs_tuned.append(coordinates);
                    var centerod_y = coordinates[0].Average();
                    var centerod_x = coordinates[1].Average();
                    var new_distance = (centerod_x - needle_point[1]) * (centerod_x - needle_point[1]) + (centerod_y - needle_point[0]) * (centerod_y - needle_point[0]);
                    if (new_distance < distance)
                    {
                        distance_inx = blobs_tuned.Count() - 1;
                        distance = new_distance;
                    }
                }
            }
            var tuned_binary = np.zeros((binary.Rows, binary.Cols), np.int32);
            if (blobs_tuned.Count() > 0)
            {
                closest_blob = blobs_tuned[distance_inx];
                
                tuned_binary = dataOperator.SetSelection(tuned_binary, closest_blob[0], closest_blob[1], value: 255);
            }
            else
            {
                closest_blob = null;
            }
            

            return tuned_binary;
        }

        public int[] find_needle_point(Mat mask, Mat ori)
        {
            Mat element = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
            var mask_erode = new Mat();
            Cv2.Erode(mask, mask_erode, element);

            using (new Window("mask_erode", mask_erode))
            {
                Cv2.WaitKey();
            }
            Mat mask_inv = new Mat();
            Cv2.BitwiseNot(mask_erode, mask_inv);

            Mat gray_masked = new Mat();
            Cv2.BitwiseAnd(ori, mask_erode, gray_masked);
            gray_masked = gray_masked + mask_inv;

            int[] minIdx = new int[2];
            int[] maxIdx = new int[2];
            gray_masked.MinMaxIdx(out double minVal, out double maxVal, minIdx, maxIdx);

            return minIdx; //h, w
        }

        public int[] find_fish_point(System.Drawing.Bitmap fish_mask, List<List<int>> fish_blob, double percentagy)
        {
            ImBlob fish_area = new ImBlob(fish_blob);
            var cropfilter = new AForge.Imaging.Filters.Crop(fish_area.rect);
            var croppedImage = cropfilter.Apply(fish_mask);
            var display = dataTransfer.Bitmap2Mat(croppedImage);
            
            Accord.Imaging.Moments.CentralMoments cm = new Accord.Imaging.Moments.CentralMoments(croppedImage, order: 2);
            // Get size and orientation of the image
            double angle = cm.GetOrientation() * 180 / Math.PI;
            //double centerpointX = fish_area.CenterRect.X;
            //double centerpointY = fish_area.CenterRect.Y;

            //double newfish_areaX = DetectedObjects.Blobs[i].Rectangle.X;
            //double newfish_areaY = DetectedObjects.Blobs[i].Rectangle.Y;
            //double newfish_width = DetectedObjects.Blobs[i].Rectangle.Width;
            //double newfish_height = DetectedObjects.Blobs[i].Rectangle.Height;
            //adjust orientation angle of fish left 0 to right 180°
            if (fish_area.Width > fish_area.Height)
            {
                if (fish_area.CenterGravity.X < fish_area.CenterRect.X && angle > 90)
                {
                    angle = angle - 180;
                }
                if (fish_area.CenterGravity.X > fish_area.CenterRect.X && angle < 90)
                {
                    angle = angle - 180;
                }
            }
            else
            {
                if (fish_area.CenterGravity.Y > fish_area.CenterRect.Y)
                {
                    angle = angle - 180;
                }
            }
            //get Head point via rotation and minimal bounding rectangle
            var rotatefilter = new AForge.Imaging.Filters.RotateNearestNeighbor(angle, false);
            var rotatedImage = rotatefilter.Apply(croppedImage);

            BlobCounter blobCounter = new BlobCounter();
            blobCounter.FilterBlobs = true;
            blobCounter.MinWidth = 0;
            blobCounter.MaxWidth = 100;
            blobCounter.MinHeight = 0;
            blobCounter.MaxHeight = 100;
            blobCounter.ObjectsOrder = ObjectsOrder.Area;
            blobCounter.ProcessImage(rotatedImage);
            Blob[] blob = blobCounter.GetObjectsInformation();
            //newBmp = rotatedImage;
            int[] fish_point = new int[2];
            if (blob.Count() > 0)
            {
                cropfilter = new AForge.Imaging.Filters.Crop(blob[0].Rectangle);
                double distance = blob[0].CenterOfGravity.X * (percentagy - 0.3) / 0.3; //blob[0].Rectangle.Width - blob[0].CenterOfGravity.X;

                float xdistance = (float)(distance * Math.Cos(Math.PI * angle / 180.0));
                float ydistance = (float)(distance * Math.Sin(Math.PI * angle / 180.0));
                fish_point = new int[2] { (int)(fish_area.CenterGravity.Y + ydistance), (int)(fish_area.CenterGravity.X + xdistance) };
            }
            return fish_point;
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
                int some_value = 0; // viewModel.Border.ActualWidth
                double pixelratio = some_value / 480;
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
    }
    class UNet_tf
    {
        private string model_filepath = "";
        private Graph graph;
        private Operation input_operation;
        private Tensor binary;
        private DataTransformation data_transfer;
        public UNet_tf(String model_filepath)
        {
            this.model_filepath = model_filepath;
            this.data_transfer = new DataTransformation();
        }

        public void load_graph()
        {
            /*
             Lode trained model.
             
            Console.WriteLine("Loading model...");
            var graph_def = GraphDef.Parser.ParseFrom(File.ReadAllBytes(this.model_filepath));

            var graph = new Graph().as_default();

            tf.import_graph_def(graph_def);
            var sess = tf.Session(graph: graph);
            */
            //tf.enable_eager_execution();
            this.graph = new Graph();
            graph.Import(this.model_filepath);
            Console.WriteLine("model loaded");

            var input_name = "x";
            var output_name = "cnn/output";

            this.input_operation = graph.OperationByName(input_name);
            this.binary = graph.OperationByName(output_name);
        }

        public List<Mat> run(Mat src)
        {
            //tf.com.disable_eager_execution();
            var nd = PrepareImage(im:src,
                input_height: 240,
                input_width: 240,
                input_mean: 0.5,
                input_std: 255);

            using (var sess = tf.Session(this.graph))
            {
                var results = sess.run(this.binary, (this.input_operation.outputs[0], nd));
                
                //results = np.squeeze(results);
                //results = np.expand_dims(results, 0);
                //var result = results[Slice.All, Slice.All, 0];
                //result = np.expand_dims(result, 0);
                //result = np.expand_dims(result, 3);
                var results_tensor = tf.convert_to_tensor(results);
                
                var results_sig_tensor = tf.nn.sigmoid(results_tensor);
                //int[] perm = new int[4] { 0, 3, 1, 2 };
                //var results_sig_int_tensor = tf.cast(results_sig_tensor, tf.int32);
                using (var one_sess = tf.Session()) 
                {
                    results = one_sess.run(results_sig_tensor);
                }
                
                //int[] perm = new int[4] { 0, 3, 1, 2 };
                //results = np.transpose(results, perm);
                //results = results[0];
                var needle_binary = results[0, Slice.All, Slice.All, 0];
                var larva_binary = results[0, Slice.All, Slice.All, 1];
                //Console.WriteLine(needle_binary.ToString());
                Mat im_needle = data_transfer.Prob2Mat(needle_binary, threshold: 0.95);
                Mat im_larva = data_transfer.Prob2Mat(larva_binary, threshold: 0.95);

                var out_ims = new List<Mat>();
                out_ims.Add(im_needle);
                out_ims.Add(im_larva);

                return out_ims;
            }
        }

        private NDArray PrepareImage(Mat im,
                                int input_height = 299,
                                int input_width = 299,
                                double input_mean = 0,
                                double input_std = 255)
        {
            
            var graph = tf.Graph().as_default();
            var im_array = data_transfer.Mat2Array(im, np.float32);

            //Mat im_show = data_transfer.Array2Mat(im_np);

            //im.GetArray(out byte[] plainArray); //there it is, c# array for nparray constructor
            //var array = np.array(plainArray, dtype: np.int32); //party party
            //var im_array = array.reshape(input_height, input_width);
            im_array = im_array / input_std;
            im_array = im_array - input_mean;
            
            im_array = np.expand_dims(im_array, 0);
            im_array = np.expand_dims(im_array, 3);
            var out_tensor = new Tensor(im_array);

            return im_array;
        }

        private NDArray ReadTensorFromImageFile(string file_name,
                                int input_height = 299,
                                int input_width = 299,
                                int input_mean = 0,
                                int input_std = 255)
        {
            var graph = tf.Graph().as_default();

            var file_reader = tf.read_file(file_name, "file_reader");
            var decodeJpeg = tf.image.decode_jpeg(file_reader, channels: 1, name: "DecodeJpeg");
            var cast = tf.cast(decodeJpeg, tf.float32);
            var dims_expander = tf.expand_dims(cast, 0);
            var resize = tf.constant(new int[] { input_height, input_width });
            var bilinear = tf.image.resize_bilinear(dims_expander, resize);
            var sub = tf.subtract(bilinear, new float[] { input_mean });
            var normalized = tf.divide(sub, new float[] { input_std });

            using (var sess = tf.Session(graph))
                return sess.run(normalized);
        }
    }
}

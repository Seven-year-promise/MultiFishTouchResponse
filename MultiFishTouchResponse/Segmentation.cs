using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using OpenCvSharp;
using static Tensorflow.Binding;
using Tensorflow;
using NumSharp;
using Accord.Imaging;

namespace MultiFishTouchResponse
{

    class UNet_tf
    {
        private string model_filepath = "";
        private Graph graph;
        private Operation input_operation;
        private Tensor binary;
        private DataTransformation data_transfer;
        private Tensor result_place_holder;
        private Tensor results_sig_tensor;
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

            this.result_place_holder = tf.placeholder(tf.float32, new Shape(1, 240, 240, 2), name: "results");

            this.results_sig_tensor = tf.nn.sigmoid(result_place_holder);
        }

        public List<Mat> run(Mat src)
        {
            //tf.com.disable_eager_execution();
            var nd = PrepareImage(im: src,
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
                //var results_tensor = tf.convert_to_tensor(results);

                //int[] perm = new int[4] { 0, 3, 1, 2 };
                //var results_sig_int_tensor = tf.cast(results_sig_tensor, tf.int32);
                using (var one_sess = tf.Session())
                {
                    results = one_sess.run(this.results_sig_tensor, (this.result_place_holder, results));
                }

                //int[] perm = new int[4] { 0, 3, 1, 2 };
                //results = np.transpose(results, perm);
                //results = results[0];
                var needle_binary = results[0, Slice.All, Slice.All, 0];
                var larva_binary = results[0, Slice.All, Slice.All, 1];
                //Console.WriteLine(needle_binary.ToString());
                Mat im_needle = data_transfer.Prob2Mat(needle_binary, threshold: 0.9);
                Mat im_larva = data_transfer.Prob2Mat(larva_binary, threshold: 0.9);

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


    class RegionGrowing
    {
        private int diff_thre;
        private DataTransformation data_transfer;
        public RegionGrowing(int diff_thre)
        {
            this.diff_thre = diff_thre;
            this.data_transfer = new DataTransformation();
        }

        private List<Point> selectConnects(int p)
        {
            List<Point> connects = new List<Point>();
            if (p != 0){
                connects.Add(new Point(-1, -1));
                connects.Add(new Point(1, -1));
                connects.Add(new Point(1, 0));
                connects.Add(new Point(1, 1));
                connects.Add(new Point(0, 1));
                connects.Add(new Point(-1, 1));
                connects.Add(new Point(-1, 0));
                }
            else
            {
                connects.Add(new Point(0, -1));
                connects.Add(new Point(1, 0));
                connects.Add(new Point(0, 1));
                connects.Add(new Point(-1, 0));
            }
                
            return connects;
        }

        private int getGrayDiff(NDArray img, Point currentPoint, Point tmpPoint)
        {
            return Math.Abs((int)(img[currentPoint.X, currentPoint.Y]) - (int)(img[tmpPoint.X, tmpPoint.Y]));
        }
        

        public Mat run(Mat src, List<Point> seeds, int p = 1)
        {
            NDArray img = data_transfer.Mat2Array(src, np.uint8);
            var height = img.Shape[0];
            var width = img.Shape[1];
            var seedMark = np.zeros(img.Shape, dtype: np.uint8);
            //listseedList = []
            //for seed in seeds:
                //seedList.append(seed)
            int label = 255;
            var connects = this.selectConnects(p);
            while(seeds.Count() > 0)
            {
                var currentPoint = seeds[0];
                seeds.RemoveAt(0);
                seedMark[currentPoint.X, currentPoint.Y] = label;

                for(int i=0; i<8; i++)
                {
                    var tmpX = currentPoint.X + connects[i].X;
                    var tmpY = currentPoint.Y + connects[i].Y;
                    if ((tmpX < 0) | (tmpY < 0) | (tmpX >= height) | (tmpY >= width))
                        continue;
                    var grayDiff = this.getGrayDiff(img, currentPoint, new Point(tmpX, tmpY));
                    if((grayDiff < this.diff_thre) & (seedMark[tmpX, tmpY] == 0))
                    {
                        seedMark[tmpX, tmpY] = label;
                        seeds.append(new Point(tmpX, tmpY));
                    }
                    var seedmarkMat = data_transfer.Array2Mat(seedMark);
                    using (new Window("seed", seedmarkMat))
                    {
                        Cv2.WaitKey(0);
                    }
                    //print(seedMark.shape)
                    //cv2.imshow("seed",seedMark)
                    //cv2.waitKey(0)
                
                }
                    
            }
            var markMat = data_transfer.Array2Mat(seedMark);
            Mat seedMasrkInv = new Mat();
            Cv2.BitwiseNot(markMat, seedMasrkInv);
            return seedMasrkInv;
        }
    }

    class NeedleDetectionThre
    {
        private int thre;
        private DataComputation dataOperator;
        private PostProcessing post_processor;
        public NeedleDetectionThre(int thre)
        {
            dataOperator = new DataComputation();
            post_processor = new PostProcessing();
            this.thre = thre;
        }

        public int[] run(Mat src)
        {
            Mat th = new Mat();
            Cv2.Threshold(src, th, this.thre, 255, ThresholdTypes.BinaryInv);
            Mat Median = new Mat();
            Cv2.MedianBlur(th, Median, 3);
            var needle_point = post_processor.find_needle_point(Median, src);

            using (new Window("median", Median))
            {
                Cv2.WaitKey(0);
            }
            /*
            var blobs = dataOperator.WhereGreater(Median, 0);
            int[] needle_point = new int[2];
            if (blobs[0].Count()>0)
            {
                needle_point[0] = (int)Math.Round(blobs[0].Average());
                needle_point[1] = (int)Math.Round(blobs[1].Average());
            }
            else
            {
                needle_point = null;
            }
            */

            return needle_point;
            //Mat labels = new Mat();
            //Cv2.ConnectedComponents(th, labels);
        }
    }

    class PostProcessing
    {
        private DataComputation dataOperator;
        private DataTransformation dataTransfer;

        public PostProcessing()
        {
            dataOperator = new DataComputation();
            dataTransfer = new DataTransformation();
        }

        public void select_big_blobs(Mat binary,
            out List<List<List<int>>> blobs_tuned,
            int[] needle_point,
            int size = 44)
        {
            Mat labels = new Mat();
            int label_num = Cv2.ConnectedComponents(binary, labels);
            blobs_tuned = new List<List<List<int>>>();
            // background: 0, if there exsits larva, the number should be >= 2
            if (label_num >= 2)
            {
                //var labels_array = dataTransfer.Mat2Array(labels, np.int32);
                var blobs_temp = new List<List<List<int>>>();
                List<double> distances = new List<double>(9);
                for (int l = 1; l < label_num; l++)
                {
                    var coordinates = dataOperator.WhereEqual(labels, l);
                    if (coordinates[0].Count() > size)
                    {
                        
                        var centerod_y = coordinates[0].Average();
                        var centerod_x = coordinates[1].Average();
                        var new_distance = (centerod_x - needle_point[1]) * (centerod_x - needle_point[1]) + (centerod_y - needle_point[0]) * (centerod_y - needle_point[0]);
                        if (new_distance > 225) //when the blob is 15 pixels away from the needle
                        {
                            blobs_temp.append(coordinates);
                            distances.Add(new_distance);
                        }

                    }
                }
                NDArray distances_array = distances.ToArray();
                var sorted_inds = distances_array.argsort<List<int>>();
                //int distance_inx = sorted_inds[0];
                if (blobs_temp.Count() > 0)
                {
                    foreach(int ind in sorted_inds)
                    {
                        blobs_tuned.Add(blobs_temp[ind]);
                    }
                }
            }
            else
            {
                blobs_tuned = null;
            }
        }

        public NDArray get_binary_one_blob(int Rows, int Cols, List<List<int>> blob)
        {
            
            var tuned_binary = np.zeros((Rows, Cols), np.int32);
            tuned_binary = dataOperator.SetSelection(tuned_binary, blob[0], blob[1], value: 255);
            return tuned_binary;
        }

        public int[] compare_binary(List<Mat> src)
        {
            List<Mat> compared_binaries = new List<Mat>();
            Mat src_0 = src[0];
            Mat src_1 = src[1];

            Mat labels = new Mat();
            int label_num_0 = Cv2.ConnectedComponents(src_0, labels);
            int label_num_1 = Cv2.ConnectedComponents(src_1, labels);
            if(label_num_0 > label_num_1)
            {
                return new int[2] { 1, 0 };
            }
            else
            {
                return new int[2] { 0, 1 };
            }
        }

        public int[] find_needle_point(Mat mask, Mat gray)
        {
            Mat element = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
            var mask_erode = new Mat();
            Cv2.Erode(mask, mask_erode, element);


            Mat mask_inv = new Mat();
            Cv2.BitwiseNot(mask_erode, mask_inv);

            // if the area of needle is detected, otherwise there is not needle area
            var lower_points = dataOperator.Wherelower(mask_inv, 1);
            if (lower_points[0].Count() > 0)
            {
                int raw_center_y = (int)Math.Round(lower_points[0].Average());
                int raw_center_x = (int)Math.Round(lower_points[1].Average());

                /*
                Mat gray_masked = new Mat();
                Cv2.BitwiseAnd(gray, mask_erode, gray_masked);
                gray_masked = gray_masked + mask_inv;
                */


                return needle_usingMaxima(gray, new int[2] { raw_center_y, raw_center_x }, 14); //h, w

            }
            else
            {
                return null;
            }

        }

        public int[] needle_usingMaxima (Mat gray, int[] raw_center, int area_size)
        {
            var needleRect = new Rect(new Point(raw_center[1] - area_size/2, raw_center[0] - area_size/2), new Size(area_size, area_size));

            var needleArea = new Mat(gray, needleRect);

            /*
            Mat gray_masked = new Mat();
            Cv2.BitwiseAnd(gray, mask_erode, gray_masked);
            gray_masked = gray_masked + mask_inv;
            */

            int[] minIdx = new int[2];
            int[] maxIdx = new int[2];
            needleArea.MinMaxIdx(out double minVal, out double maxVal, minIdx, maxIdx);
            minIdx[0] += (raw_center[0] - area_size / 2);
            minIdx[1] += (raw_center[1] - area_size / 2);
            return minIdx; //h, w

        }

        public Mat Skeletonize(Mat binary)
        {
            var kernel = Cv2.GetStructuringElement(MorphShapes.Cross, new Size(3, 3));

            bool finished = false;
            var skeleton = new Mat(binary.Rows, binary.Cols, MatType.CV_8UC1, new Scalar(0));
            var size = binary.Rows * binary.Cols;
            /*using (new Window("binary", binary))
            {
                Cv2.WaitKey();
            }
            */

            using (var eroded = new Mat())
            using (var temp = new Mat())
            using (var open = new Mat())
            {
                while (!finished)
                {
                    // Step 2: Open the image
                    Cv2.MorphologyEx(binary, open, MorphTypes.Open, kernel);
                    // Step 3: Substract open from the original image
                    Cv2.Subtract(binary, open, temp);
                    // Step 4: Erode the original image and refine the skeleton
                    Cv2.Erode(binary, eroded, kernel);

                    Cv2.BitwiseOr(skeleton, temp, skeleton);
                    eroded.CopyTo(binary);


                    var zeros = size - Cv2.CountNonZero(binary);
                    if (Cv2.CountNonZero(binary) == 0)
                        finished = true;
                }
            }
            Mat median = new Mat();
            Cv2.MorphologyEx(skeleton, median, MorphTypes.Close, kernel);
            /*using (new Window("skeleton", median))
            {
                Cv2.WaitKey(0);
            }
            */

            return skeleton;
        }


        public int[] find_fish_skeleton_point(Mat fish_mask, List<List<int>> fish_blob, double percentage)
        {
            /*
            :param fish_mask: the binary of the fish: 0/1
            :param needle_center: the center of the needle: y, x
            :param fish_blobs: the coordinates of the area of the fish
            :param percentages: list of the points to be touched in percentage coordinate system
            :return: list of the coordinates to be touched for the closest fish to the needle
            */
            var skeleton = Skeletonize(fish_mask);
            var skeleton_cor = dataOperator.WhereGreater(skeleton, 0);
            int blob_cor_num = skeleton_cor[0].Count();


            int[] point1 = new int[2] { skeleton_cor[0][0], skeleton_cor[1][0] };
            int[] point2 = new int[2] { skeleton_cor[0][blob_cor_num - 1], skeleton_cor[1][blob_cor_num - 1] };

            int[] fish_center = new int[2];
            fish_center[0] = (int)Math.Round(fish_blob[0].Average());
            fish_center[1] = (int)Math.Round(fish_blob[1].Average());
            return get_point(point1, point2, fish_center, percentage);

        }


        public int[] get_point(int[] point1, int[] point2, int[] fish_center, double percentage)
        {
            var y1 = point1[0];
            var x1 = point1[1];

            var y2 = point2[0];
            var x2 = point2[1];

            var f_y = fish_center[0];
            var f_x = fish_center[1];
            var distance1 = (f_x - x1) * (f_x - x1) + (f_y - y1) * (f_y - y1);
            var distance2 = (f_x - x2) * (f_x - x2) + (f_y - y2) * (f_y - y2);

            double k = (y2 - y1) / (x2 - x1 + Globals.epsilon) + Globals.epsilon;
            int[] top_head = new int[2];
            int[] tail_end = new int[2];
            if (distance1 < distance2)
            {
                top_head = point1;
                tail_end = point2;
            }
            else
            {
                top_head = point2;
                tail_end = point1;
            }

            int[] percent_point = new int[2];
            percent_point[0] = (int)(Math.Round((1 - percentage) * top_head[0] + percentage * tail_end[0]));
            percent_point[1] = (int)(Math.Round((1 - percentage) * top_head[1] + percentage * tail_end[1]));

            return percent_point;
        }


        public int[] find_fish_point(System.Drawing.Bitmap fish_mask, out double angle, List<List<int>> fish_blob, double percentagy)
        {
            ImBlob fish_area = new ImBlob(fish_blob);
            var cropfilter = new AForge.Imaging.Filters.Crop(fish_area.rect);
            var croppedImage = cropfilter.Apply(fish_mask);
            var display = dataTransfer.Bitmap2Mat(croppedImage);

            Accord.Imaging.Moments.CentralMoments cm = new Accord.Imaging.Moments.CentralMoments(croppedImage, order: 2);
            // Get size and orientation of the image
            angle = cm.GetOrientation() * 180 / Math.PI;
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
                double gravityPercent = blob[0].CenterOfGravity.X / blob[0].Rectangle.Width; //0.3
                cropfilter = new AForge.Imaging.Filters.Crop(blob[0].Rectangle);
                double distance = blob[0].CenterOfGravity.X * (percentagy - gravityPercent) / gravityPercent; //blob[0].Rectangle.Width - blob[0].CenterOfGravity.X;

                float xdistance = (float)(distance * Math.Cos(Math.PI * angle / 180.0));
                float ydistance = (float)(distance * Math.Sin(Math.PI * angle / 180.0));
                fish_point = new int[2] { (int)(fish_area.CenterGravity.Y + ydistance), (int)(fish_area.CenterGravity.X + xdistance) };

                /*
                double topHead_distance = 0 - blob[0].CenterOfGravity.X;
                float xtopHead_distance = (float)(topHead_distance * Math.Cos(Math.PI * angle / 180.0));
                float ytopHead_distance = (float)(topHead_distance * Math.Sin(Math.PI * angle / 180.0));
                int[] topHead = new int[2] { (int)(fish_area.CenterGravity.Y + ytopHead_distance), (int)(fish_area.CenterGravity.X + xtopHead_distance) };
                */
            }
            return fish_point;
        }


        public double get_iou(List<List<int>> blobA, List<List<int>> blobB, Shape ori_shape)
        {
            var A_array = dataTransfer.List2NDArray2D(blobA);
            var B_array = dataTransfer.List2NDArray2D(blobB);

            NDArray maskA = np.zeros(ori_shape, np.uint8);
            NDArray maskB = np.zeros(ori_shape, np.uint8);

            //maskA[A_array] = 1;
            //maskB[B_array] = 1;
            maskA = dataOperator.SetArrayValue2D(maskA, blobA, 1);
            maskB = dataOperator.SetArrayValue2D(maskB, blobB, 1);

            /*
            NDArray maskA1 = np.zeros(ori_shape, np.uint8);
            NDArray maskB1 = np.zeros(ori_shape, np.uint8);
            maskA1 = dataOperator.SetArrayValue2D(maskA1, blobA, 255);
            maskB1 = dataOperator.SetArrayValue2D(maskB1, blobB, 255);
            //Console.WriteLine(maskA.ToString());

            var maskA_display = dataTransfer.Array2Mat(maskA1);
            var maskB_display = dataTransfer.Array2Mat(maskB1);

            using (new Window("maskA", maskA_display))
            using (new Window("maskB", maskB_display))
            {
                Cv2.WaitKey(0);
            }
            */
            //cv2.imshow("maskA", maskA*255)
            //cv2.imshow("maskB", maskB*255)
            //cv2.waitKey(0)
            var A = maskA.sum();
            var B = maskB.sum();

            var AplusB = maskA + maskB;

            var AplusB_Mat = dataTransfer.Array2Mat(AplusB);
            var equal2 = dataOperator.WhereEqual(AplusB_Mat, 2);
            var AB = equal2[0].Count();
            //print(A, B, AB)

            return AB * 1.0 / ((A + B - AB)*1.0);
        }
           
        public List<List<int>> find_new_blob(List<List<int>> larva_blob, Mat binary, int size = 44, double iou_thre = 0.5)
        {
            List<List<int>> new_blob = null;
            Mat labels = new Mat();
            int label_num = Cv2.ConnectedComponents(binary, labels);
            // background: 0, if there exsits larva, the number should be >= 2
            if (label_num >= 2)
            {
                //var labels_array = dataTransfer.Mat2Array(labels, np.int32);

                List<double> distances = new List<double>();
                for (int l = 1; l < label_num; l++)
                {
                    var coordinates = dataOperator.WhereEqual(labels, l);
                    if (coordinates[0].Count() > size)
                    {
                        var iou = get_iou(larva_blob, coordinates, new Shape(binary.Rows, binary.Cols));
                        if (iou > iou_thre)
                        {
                            new_blob = coordinates;
                            break;
                        }
                    }
                }
            }
            return new_blob;
        }
    }
}

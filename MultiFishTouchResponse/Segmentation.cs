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


namespace MultiFishTouchResponse
{
    class Segmentation
    {
    }

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
}

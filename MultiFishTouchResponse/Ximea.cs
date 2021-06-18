using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using xiApi.NET;
//Sharpavi
using SharpAvi;
using SharpAvi.Output;
using SharpAvi.Codecs;
//Ffmpeg wrapper
using NReco;

namespace MultiFishTouchResponse
{
    static class Ximea
    {
        static xiCam myCam = new xiCam();
        public static BitmapSource CurrentCameraImage;
        public static WriteableBitmap CurrentAnalysisImage;
        public static System.Collections.Concurrent.BlockingCollection<BitmapSource> CameraImageQueue = new System.Collections.Concurrent.BlockingCollection<BitmapSource>(1);
        public static List<WriteableBitmap> AnalysisImageQueue = new List<WriteableBitmap>(1);
        public static bool Recording = false;
        public static bool Recordingsaving = false;
        static bool stopCamera = false;
        static int counter = 0;
        static public System.Diagnostics.Stopwatch recordingwatch;
        static List<BitmapSource> RecordingQueue = new List<BitmapSource>();
        public static ViewModel viewModel;
        public static string Path;
        static List<double> timestampbuffer = new List<double>();
        static double lastupdated = 0;

        public static int Framerate
        {
            get { try { return myCam.GetParamInt(PRM.FRAMERATE); } catch { return 0; } }
        }

        public static bool StopCamera
        {
            get { return stopCamera; }
            set { stopCamera = value; }
        }

        private static void GetCameraImages()
        {
            while (stopCamera != true)
            {
                myCam.GetImage(out CurrentCameraImage, 1000);
                CurrentCameraImage.Freeze();

                CameraImageQueue.TryAdd(CurrentCameraImage);

                var Info = myCam.GetLastImageParams();
                var Timestamp = Info.GetTimestamp();
                if (Timestamp - lastupdated > 0.05)
                {
                    viewModel.CurrentCameraImage = CurrentCameraImage;

                    if (AnalysisImageQueue.Count == 0)
                    {
                        CurrentAnalysisImage = new WriteableBitmap(CurrentCameraImage);
                        AnalysisImageQueue.Add(CurrentAnalysisImage);
                    }
                    lastupdated = Timestamp;
                }
                if (Recording == true)
                {
                    if (recordingwatch == null)
                    {
                        recordingwatch = new System.Diagnostics.Stopwatch();
                        recordingwatch.Start();
                        if (System.Runtime.GCSettings.LatencyMode != System.Runtime.GCLatencyMode.NoGCRegion)
                        {
                            GC.TryStartNoGCRegion(100000000, true); //0.1GB
                        }
                    }
                    timestampbuffer.Add(Timestamp);
                    RecordingQueue.Add(CurrentCameraImage);
                    viewModel.RecordingButtonText = (recordingwatch.ElapsedMilliseconds / 1000.0).ToString("0.0s");
                    if ((recordingwatch.ElapsedMilliseconds / 1000.0) > 10)
                    {
                        Recording = false;
                    }
                }
                else if (RecordingQueue.Count > 0 & viewModel.IsAvailableForRecording == true)
                {
                    try { System.GC.EndNoGCRegion(); } catch { }
                    //save 
                    recordingwatch.Stop();
                    recordingwatch = null;
                    viewModel.IsAvailableForRecording = false;
                    int height = RecordingQueue[0].PixelHeight;
                    int width = RecordingQueue[0].PixelWidth;
                    int stride = width * ((RecordingQueue[0].Format.BitsPerPixel + 7) / 8);
                    byte[] bits = new byte[height * stride];
                    //check if filename exists and if yes add a counter
                    int filecounter = 1;
                    string fileNameOnly = viewModel.Videoname;
                    string extension = ".avi";
                    string newFullPath = Path + "\\" + fileNameOnly + extension;
                    System.IO.File.WriteAllLines(Path + "\\" + fileNameOnly + "_timestamps.txt", timestampbuffer.Select(d => d.ToString()));
                    timestampbuffer.Clear();

                    while (System.IO.File.Exists(newFullPath))
                    {
                        string tempFileName = string.Format("{0}{1}", fileNameOnly, filecounter++);
                        newFullPath = System.IO.Path.Combine(Path, tempFileName + extension);
                    }

                    string this_time = System.DateTime.Now.ToString("HHmmss");
                    Console.WriteLine("saving begin" + this_time);
                    //create File
                    //var memstream = new System.IO.MemoryStream();
                    var memstream = new LiquidEngine.Tools.MemoryTributary();
                    //var writer = new AviWriter(RamdiskFilename)
                    var writer = new AviWriter(memstream)
                    {
                        FramesPerSecond = Framerate,
                        EmitIndex1 = true
                    };
                    var stream = writer.AddVideoStream(width, height, BitsPerPixel.Bpp8);
                    Task.Run(() => {
                        while (RecordingQueue.Count > 0)
                        {
                            RecordingQueue[0].CopyPixels(bits, stride, 0);
                            try
                            {
                                stream.WriteFrame(true, // is key frame? (many codecs use concept of key frames, for others - all frames are keys)
                                    bits, // array with frame data
                                    0, // starting index in the array
                                    bits.Length // length of the data
                                 );
                            }
                            catch { viewModel.RecordingButtonText = "Too long"; break; }
                            RecordingQueue.RemoveAt(0);
                        }
                        writer.Close();
                        RecordingQueue.Clear();
                        NReco.VideoConverter.FFMpegConverter ff = new NReco.VideoConverter.FFMpegConverter();
                        ff.ConvertProgress += Ff_ConvertProgress;
                        NReco.VideoConverter.ConvertSettings ffsetting = new NReco.VideoConverter.ConvertSettings();
                        ffsetting.VideoCodec = "libx264";
                        ffsetting.VideoFrameRate = Framerate;
                        ffsetting.SetVideoFrameSize(width, height);
                        //ffsetting.CustomOutputArgs = "-preset ultrafast -qp 0 -pix_fmt yuv420p"; //lossless
                        ffsetting.CustomOutputArgs = "-preset ultrafast -crf 18 -pix_fmt yuv420p"; //visually lossless yuv420p necessary for matlab compatibility
                        //ff.ConvertMedia(RamdiskFilename, "avi", newFullPath, "avi", ffsetting);
                        //System.IO.File.Delete(RamdiskFilename);
                        memstream.Position = 0;
                        var task = ff.ConvertLiveMedia(memstream, "avi", newFullPath, "avi", ffsetting);
                        task.Start();
                        task.Wait();
                        GC.Collect();
                        Recordingsaving = true;
                        this_time = System.DateTime.Now.ToString("HHmmss");
                        Console.WriteLine("saving end" + this_time);
                    });
                    
                }
                counter++;
                //viewModel.RecordingButtonText = counter + "-" + watch.ElapsedMilliseconds/1000 + "-" + Framerate.ToString("0");
            }
            myCam.StopAcquisition();
        }

        private static void Ff_ConvertProgress(object sender, NReco.VideoConverter.ConvertProgressEventArgs e)
        {
            if (e.Processed == e.TotalDuration)
            {
                viewModel.IsAvailableForRecording = true;
                viewModel.RecordingButtonText = "Recording";
            }
            else
            {
                viewModel.RecordingButtonText = (e.Processed.TotalMilliseconds / e.TotalDuration.TotalMilliseconds * 100).ToString("0.0") + "%";
            }
        }

        public static void StartCamera()
        {
            myCam.OpenDevice(0);
            int exposure_us = 800;
            myCam.SetParam(PRM.EXPOSURE, exposure_us);
            float gainVal = 0;
            myCam.SetParam(PRM.GAIN, gainVal);
            myCam.SetParam(PRM.IMAGE_DATA_FORMAT, IMG_FORMAT.RAW8);
            myCam.SetParam(PRM.OUTPUT_DATA_BIT_DEPTH, 8);
            myCam.SetParam(PRM.BUFFER_POLICY, BUFF_POLICY.UNSAFE);
            myCam.SetParam(PRM.WIDTH, 480);
            myCam.SetParam(PRM.HEIGHT, 480);
            myCam.SetParam(PRM.OFFSET_X, 400);
            myCam.SetParam(PRM.OFFSET_Y, 270);
            myCam.SetParam(PRM.ACQ_TIMING_MODE, xiApi.NET.ACQ_TIMING_MODE.FRAME_RATE);
            int framerate = 0;
            myCam.SetParam(PRM.SENSOR_FEATURE_SELECTOR, xiApi.NET.SENSOR_FEATURE_SELECTOR.SENSOR_FEATURE_ZEROROT_ENABLE);
            myCam.SetParam(PRM.SENSOR_FEATURE_VALUE, 1);
            myCam.GetParam(PRM.FRAMERATE_MAX, out framerate);
            if (framerate > 1000)
                framerate = 1000;
            myCam.SetParam(PRM.FRAMERATE, framerate);

            //speed optimizations according to
            //https://www.ximea.com/support/wiki/usb3/How_to_optimize_software_performance_on_high_frame_rates
            int payload = myCam.GetParamInt(PRM.IMAGE_PAYLOAD_SIZE);
            // get default transport buffer size - that should be OK on all controllers
            int buffersize = myCam.GetParamInt(PRM.ACQ_TRANSPORT_BUFFER_SIZE);
            int packetsize = myCam.GetParamInt(PRM.ACQ_TRANSPORT_PACKET_SIZE);
            int buffersizemin = myCam.GetParamInt(PRM.ACQ_TRANSPORT_BUFFER_SIZE_MIN);

            if (payload < buffersize)
            {
                // use optimized transport buffer size, as nearest increment to payload
                int transport_buffer_size = payload;
                // round up to nearest increment
                int remainder = transport_buffer_size % packetsize;
                if (remainder != 0)
                    transport_buffer_size += packetsize - remainder;
                // check the minimum
                if (transport_buffer_size < buffersizemin)
                    transport_buffer_size = buffersizemin;
                myCam.SetParam(PRM.ACQ_TRANSPORT_BUFFER_SIZE, transport_buffer_size);
            }
            myCam.SetParam(PRM.BUFFERS_QUEUE_SIZE, myCam.GetParamFloat(PRM.BUFFERS_QUEUE_SIZE_MAX));
            /////////////////////

            myCam.StartAcquisition();

            Task.Run(() => { GetCameraImages(); });
        }

        public static void ChangeSetting(string PRM, int value)
        {
            try
            {
                myCam.SetParam(PRM, value);
            }
            catch { }
        }
        public static int GetSetting(string PRM)
        {
            try
            {
                return myCam.GetParamInt(PRM);
            }
            catch { return 0; }
        }
    }
}

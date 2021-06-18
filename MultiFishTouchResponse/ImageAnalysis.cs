using System.ComponentModel;
using xiApi.NET;

using System.Drawing.Drawing2D;
//using System.Drawing.Imaging;
using System.IO;
using System.Web;
using System.Windows;
//Sharpavi
using SharpAvi;
using SharpAvi.Output;
using SharpAvi.Codecs;
//Ffmpeg wrapper
using NReco;



using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing.Imaging;


using AForge;
using AForge.Imaging;

using System.Windows.Media.Imaging;
using System.Drawing;
using System.Collections.Concurrent;

namespace MultiFishTouchResponse
{
    class ImageAnalysis
    {
        private BlockingCollection<BitmapSource> ImageQueue;
        private ViewModel viewModel;
        private DebugView DebugWindow;
        private BitmapSource image;
        private DetectedObjectsClass DetectedObjects;
        public List<System.Drawing.Point> fishPartPoints = new List<System.Drawing.Point>(3);
        public List<System.Drawing.Point> lastFishPartPoints = new List<System.Drawing.Point>(3);
        public int count = 0;
        public class DetectedObjectsClass
        {
            public Blob[] Blobs;

            public void UpdateBlobs(Blob[] NewBlobs)
            {
                for (int i = 0; i < Blobs.Count(); i++)
                {
                    for (int j = 0; j < NewBlobs.Count(); j++)
                    {
                        if (CompareBlob(Blobs[i], NewBlobs[j]))
                        {
                            Blobs[i] = NewBlobs[j];
                            break;
                        }
                    }
                }
            }

            private bool CompareBlob(Blob A, Blob B)
            {
                int allowedDistance = 75;
                if (B.CenterOfGravity.X > A.CenterOfGravity.X - allowedDistance
                    && B.CenterOfGravity.X < A.CenterOfGravity.X + allowedDistance
                    && B.CenterOfGravity.Y > A.CenterOfGravity.Y - allowedDistance
                    && B.CenterOfGravity.Y < A.CenterOfGravity.Y + allowedDistance)
                    return true;
                else
                    return false;
            }

        }

        public struct FishCoordinates
        {
            AForge.Point Head { get; set; }
            AForge.Point Tail { get; set; }
            AForge.Point Middle { get; set; }
        }
        public AForge.Point Needle;

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
        public bool needleAdded = false;
        private bool existing_video_analysis = false;
        //public Aforge filter
        private AForge.Imaging.Filters.CannyEdgeDetector canny = new AForge.Imaging.Filters.CannyEdgeDetector();
        public bool image_not_saving = false;
        //Constructor takes BlockingCollection as Consumer and viewModel for variable exchange
        public ImageAnalysis(BlockingCollection<BitmapSource> GivenCollection, ViewModel viewmodel, DebugView debugview)
        {
            viewModel = viewmodel;
            ImageQueue = GivenCollection;
            DebugWindow = debugview;

            if (existing_video_analysis)
            {
                // TO DO
            }
            else
            {
                Task.Run(() =>
                {
                    while (disposed == false)
                    {
                        AnalyseImage2();
                        //GC.Collect();
                    }
                    //GC.Collect();
                });
            }

        }

        //Take Image from BlockingCollection and do Aforge filtering or other image analysis

        public void AnalyseImage2()
        {
            DateTime now = DateTime.Now;
            string TIME = now.ToString("HHmmss");
            count++;
            if (DetectedObjects == null)
            {
                DetectedObjects = new DetectedObjectsClass();
            }

            //bool succesful = ImageQueue.TryTake(out image);
            bool succesful = Ximea.CameraImageQueue.TryTake(out image);

            if (succesful == true)
            {
                int width = 480;// image.PixelWidth;
                int height = 480;// image.PixelHeight;

                System.Drawing.Bitmap origgrayscaledBitmap = BitmapFromSource(image);
                System.Drawing.Bitmap saving_image = BitmapFromSource(image);
                //var origgrayscaledBitmap = reader.ReadVideoFrame();

                string saving_name = "1";

                var newHeight = (int)(height * viewModel.imageratio * 0.7);



                /*
                var newWidth = width * viewModel.imageratio;
                var newHeight = height * viewModel.imageratio;

                AForge.Imaging.Filters.Crop cropfilter = new AForge.Imaging.Filters.Crop(new Rectangle((int)(width / 2.0 - 20),
                                                                                                       (int)(height / 2.0 - 20),
                                                                                                       40,
                                                                                                       40));
                
                
                cropfilter = new AForge.Imaging.Filters.Crop(new Rectangle((int)(width / 2.0 - newWidth/2.0),
                                                                           (int)(height / 2.0 - newHeight/2.0),
                                                                           (int)newWidth,
                                                                           (int)newHeight));
                var croppedNeedleBitmap = cropfilter.Apply(origgrayscaledBitmap);
                origgrayscaledBitmap = cropfilter.Apply(origgrayscaledBitmap);
                //croppedNeedleBitmap.Save("croppedNeedleBitmap.png");
                width = (int)newWidth;
                height = (int)newHeight;
                */
                // create filter - rotate for -90 degrees keeping original image size
                AForge.Imaging.Filters.RotateNearestNeighbor rotatefilter = new AForge.Imaging.Filters.RotateNearestNeighbor(90, true);
                // apply the filter
                origgrayscaledBitmap = rotatefilter.Apply(origgrayscaledBitmap);
                saving_image = rotatefilter.Apply(saving_image);
                AForge.Imaging.Filters.Mirror mirrorfilter = new AForge.Imaging.Filters.Mirror(false, true);
                mirrorfilter.ApplyInPlace(origgrayscaledBitmap);
                mirrorfilter.ApplyInPlace(saving_image);

                if (image_not_saving)
                {
                    saving_name = "./result/original_image" + TIME + "_" + string.Format("{0}", count) + ".png";
                    saving_image.Save(saving_name);
                }
                //origgrayscaledBitmap.Save("1.png");
                AForge.Imaging.Filters.Threshold bwFilter = new AForge.Imaging.Filters.Threshold(180); // 120 .viewModel.BwThreshold);
                AForge.Imaging.Filters.Threshold bwFilter2 = new AForge.Imaging.Filters.Threshold(30);
                AForge.Imaging.Filters.Threshold bwFilter3 = new AForge.Imaging.Filters.Threshold(50);
                AForge.Imaging.Filters.Invert invertFilter = new AForge.Imaging.Filters.Invert();
                AForge.Imaging.Filters.SimpleSkeletonization skeletonFilter = new AForge.Imaging.Filters.SimpleSkeletonization();
                AForge.Imaging.Filters.BilateralSmoothing filter = new AForge.Imaging.Filters.BilateralSmoothing();
                filter.KernelSize = 15;
                filter.SpatialFactor = 10;
                filter.ColorFactor = 60;
                filter.ColorPower = 0.5;

                var grayscaledBitmap = bwFilter.Apply(origgrayscaledBitmap);
                //grayscaledBitmap.Save("2.png");
                if (image_not_saving)
                {
                    saving_name = "./result/binary_fish" + TIME + "_" + string.Format("{0}", count) + ".png";
                    //grayscaledBitmap.Save(saving_name);
                }
                var needleimage = bwFilter2.Apply(origgrayscaledBitmap);
                //needleimage.Save("3.png");
                if (image_not_saving)
                {
                    saving_name = "./result/binary_needle" + TIME + "_" + string.Format("{0}", count) + ".png";
                    //needleimage.Save(saving_name);
                }
                var binarizationHough = bwFilter3.Apply(origgrayscaledBitmap);
                if (image_not_saving)
                {
                    saving_name = "./result/1binarizationHough" + TIME + "_" + string.Format("{0}", count) + ".png";
                    binarizationHough.Save(saving_name);
                }
                /*
                using (Bitmap btm = new Bitmap(width, height))
                {
                    using (Graphics grf = Graphics.FromImage(btm))
                    {
                        Rectangle ImageSize = new Rectangle(0, 0, width, height);
                        grf.FillRectangle(Brushes.Black, ImageSize);

                        int distance = height - newHeight;
                        grf.FillEllipse(Brushes.White, distance / 2, distance / 2, width - distance, height - distance);
                        AForge.Imaging.Filters.ApplyMask masking = new AForge.Imaging.Filters.ApplyMask(btm);

                        AForge.Imaging.Filters.MaskedFilter mask = new AForge.Imaging.Filters.MaskedFilter(invertFilter, btm);
                        masking.ApplyInPlace(grayscaledBitmap);
                        mask.ApplyInPlace(grayscaledBitmap);

                        masking.ApplyInPlace(needleimage);
                        mask.ApplyInPlace(needleimage);
                        //grayscaledBitmap.Save("4.png");
                        //needleimage.Save("5.png");
                        if (image_not_saving)
                        {
                            saving_name = "./result/binary_masked_fish" + TIME + "_" + string.Format("{0}", count) + ".png";
                            grayscaledBitmap.Save(saving_name);
                        }
                        if (image_not_saving)
                        {
                            saving_name = "./result/binary_masked_needle" + "_" + string.Format("{0}", count) + ".png";
                            needleimage.Save(saving_name);
                        }
                    }
                }
                */
                if (viewModel.CannyChecked == true)
                {
                    HoughCircle circleResult = CircleDetection(binarizationHough, 110, 100);
                    int distanceCircle = (circleResult.X - 240) * (circleResult.X - 240) + (circleResult.Y - 240) * (circleResult.Y - 240);
                    if (circleResult == null)
                    {
                        circleResult = new HoughCircle(240, 240, 110, 1, 1);
                    }
                    if (distanceCircle > 2500)
                    {
                        circleResult = new HoughCircle(240, 240, 110, 1, 1);
                    }

                    circleResult = new HoughCircle(circleResult.X, circleResult.Y, 110, 1, 1);
                    AForge.Imaging.Filters.Crop cropfilter = new AForge.Imaging.Filters.Crop(new Rectangle(circleResult.X - 100,
                                                           circleResult.Y - 100,
                                                           200,
                                                           200));
                    var croppedNeedleBitmap = cropfilter.Apply(origgrayscaledBitmap);

                    //var binarizationHough2 = CircleCropImage(origgrayscaledBitmap, circleResult, 0);

                    bwFilter3 = new AForge.Imaging.Filters.Threshold(200);
                    var binarizationHough2 = bwFilter3.Apply(croppedNeedleBitmap);
                    HoughCircle circleResult2 = CircleCenterDetection(binarizationHough2);

                    circleResult2 = new HoughCircle(circleResult.X - 100 + circleResult2.X,
                                                   circleResult.Y - 100 + circleResult2.Y,
                                                   82, 1, 1);
                    //AForge.Imaging.Filters.Invert invertFilterHough = new AForge.Imaging.Filters.Invert();
                    //binarizationHough2 = invertFilterHough.Apply(binarizationHough2);
                    //HoughCircle circleResult2 = CircleDetection(binarizationHough2, 85, 100);

                    //circleResult2 = new HoughCircle(circleResult2.X, circleResult2.Y, 81, 1, 1);
                    //var show = CircleCropImage(croppedNeedleBitmap, circleResult2, 0);
                    if (image_not_saving)
                    {
                        saving_name = "./result/2binarizationHough" + TIME + "_" + string.Format("{0}", count) + ".png";
                        binarizationHough2.Save(saving_name);
                    }


                    //Bitmap btm = new Bitmap(width, height);
                    //Graphics grf = Graphics.FromImage(btm);
                    //Rectangle ImageSize = new Rectangle(0, 0, width, height);
                    //grf.FillRectangle(Brushes.Black, ImageSize);

                    int distance = height - newHeight;
                    //grf.FillEllipse(Brushes.White, circleResult.X - circleResult.Radius, circleResult.Y - circleResult.Radius, circleResult.Radius * 2, circleResult.Radius * 2);
                    //AForge.Imaging.Filters.ApplyMask masking = new AForge.Imaging.Filters.ApplyMask(btm);

                    //AForge.Imaging.Filters.MaskedFilter mask = new AForge.Imaging.Filters.MaskedFilter(invertFilter, btm);
                    //masking.ApplyInPlace(grayscaledBitmap);
                    //mask.ApplyInPlace(grayscaledBitmap);

                    //masking.ApplyInPlace(needleimage);
                    //mask.ApplyInPlace(needleimage);

                    circleResult = new HoughCircle(circleResult.X, circleResult.Y, 83, 1, 1);
                    grayscaledBitmap = CircleCropImage(grayscaledBitmap, circleResult2, 1);
                    needleimage = CircleCropImage(needleimage, circleResult2, 1);



                    //grayscaledBitmap.Save("4.png");
                    //needleimage.Save("5.png");
                    if (image_not_saving)
                    {
                        saving_name = "./result/binary_masked_fish" + TIME + "_" + string.Format("{0}", count) + ".png";
                        //grayscaledBitmap.Save(saving_name);
                    }
                    if (image_not_saving)
                    {
                        saving_name = "./result/binary_masked_needle" + TIME + "_" + string.Format("{0}", count) + ".png";
                        //needleimage.Save(saving_name);
                    }


                    AForge.Imaging.Filters.Closing closingfilter = new AForge.Imaging.Filters.Closing();
                    AForge.Imaging.Filters.BinaryDilatation3x3 dilatationfilter = new AForge.Imaging.Filters.BinaryDilatation3x3();
                    AForge.Imaging.Filters.BinaryErosion3x3 erosionfilter = new AForge.Imaging.Filters.BinaryErosion3x3();
                    // apply the filter
                    dilatationfilter.ApplyInPlace(grayscaledBitmap);
                    erosionfilter.ApplyInPlace(grayscaledBitmap);
                    if (image_not_saving)
                    {
                        saving_name = "./result/morphography_fish" + TIME + "_" + string.Format("{0}", count) + ".png";
                        //grayscaledBitmap.Save(saving_name);
                    }
                    AForge.Imaging.Filters.FillHoles fillfilter = new AForge.Imaging.Filters.FillHoles();
                    fillfilter.MaxHoleHeight = 20;
                    fillfilter.MaxHoleWidth = 20;
                    fillfilter.CoupledSizeFiltering = false;
                    // apply the filter
                    fillfilter.ApplyInPlace(grayscaledBitmap);
                    //grayscaledBitmap.Save("6.png");
                    if (image_not_saving)
                    {
                        saving_name = "./result/FillHoles_fish" + TIME + "_" + string.Format("{0}", count) + ".png";
                        //grayscaledBitmap.Save(saving_name);
                    }
                    System.Drawing.Bitmap newBmp;
                    System.Drawing.Bitmap newgrayscale;
                    System.Drawing.Bitmap saving_figure;


                    // create Graphics object to draw on the image and a pen
                    newBmp = new System.Drawing.Bitmap(grayscaledBitmap.Width, grayscaledBitmap.Height);
                    newgrayscale = new System.Drawing.Bitmap(grayscaledBitmap.Width, grayscaledBitmap.Height);
                    saving_figure = new System.Drawing.Bitmap(saving_image.Width, saving_image.Height);
                    System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(newBmp);
                    System.Drawing.Graphics g_newgrayscale = System.Drawing.Graphics.FromImage(newgrayscale);
                    System.Drawing.Graphics g_saving = System.Drawing.Graphics.FromImage(saving_figure);
                    g.DrawImage(grayscaledBitmap, 0, 0);
                    g_newgrayscale.DrawImage(grayscaledBitmap, 0, 0);
                    g_saving.DrawImage(saving_image, 0, 0);
                    System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.Red, 3);
                    //check each object and draw circle around objects, which
                    //are recognized as circles
                    System.Drawing.Point Needle = new System.Drawing.Point();
                    System.Drawing.Point[] P = new System.Drawing.Point[2];

                    float radius = 1;
                    // detect Needle in additional step with different threshold as it is the darkest object in the well 
                    // BlobCounter: countng and extracting the ojects detected
                    BlobCounter blobCounterNeedle = new BlobCounter();
                    blobCounterNeedle.FilterBlobs = true;
                    blobCounterNeedle.MinWidth = 2;
                    blobCounterNeedle.MaxWidth = 8;
                    blobCounterNeedle.MinHeight = 2;
                    blobCounterNeedle.MaxHeight = 8;
                    blobCounterNeedle.ObjectsOrder = ObjectsOrder.Area;
                    blobCounterNeedle.ProcessImage(needleimage);
                    DetectedObjectsClass DetectedObjectsNeedle = new DetectedObjectsClass();
                    DetectedObjectsNeedle.Blobs = blobCounterNeedle.GetObjectsInformation();

                    if (Needle.IsEmpty == true && DetectedObjectsNeedle.Blobs.Count() != 0)
                    {
                        pen.Color = Color.Red;
                        Needle.X = (int)DetectedObjectsNeedle.Blobs[0].CenterOfGravity.X;
                        Needle.Y = (int)DetectedObjectsNeedle.Blobs[0].CenterOfGravity.Y;
                        g.DrawEllipse(pen, Needle.X - radius, Needle.Y - radius, 2 * radius, 2 * radius);
                        g_saving.DrawEllipse(pen, Needle.X - radius, Needle.Y - radius, 2 * radius, 2 * radius);
                        // color in needle as black circle into grayscaledBitmap for fish detection

                    }

                    BlobCounter blobCounter = new BlobCounter();
                    blobCounter.FilterBlobs = true;
                    blobCounter.MinWidth = 5;
                    blobCounter.MaxWidth = 50;
                    blobCounter.MinHeight = 5;
                    blobCounter.MaxHeight = 50;
                    blobCounter.ObjectsOrder = ObjectsOrder.Area;
                    blobCounter.ProcessImage(grayscaledBitmap);
                    DetectedObjects.Blobs = blobCounter.GetObjectsInformation();

                    if (DetectedObjects.Blobs.Count() >= 2 && DetectedObjectsNeedle.Blobs.Count() >= 1)
                    {


                        //Which blob is rounder
                        int i = 0;

                        double roundness1 = Math.Min(DetectedObjects.Blobs[0].Rectangle.Height, DetectedObjects.Blobs[0].Rectangle.Width) * 1.00 / (Math.Max(DetectedObjects.Blobs[0].Rectangle.Width, DetectedObjects.Blobs[0].Rectangle.Height) * 1.00);
                        double roundness2 = Math.Min(DetectedObjects.Blobs[1].Rectangle.Height, DetectedObjects.Blobs[1].Rectangle.Width) * 1.00 / (Math.Max(DetectedObjects.Blobs[1].Rectangle.Width, DetectedObjects.Blobs[1].Rectangle.Height) * 1.00);
                        if (roundness1 > roundness2)
                        {
                            i = 1;
                            if (DetectedObjects.Blobs[i].CenterOfGravity.SquaredDistanceTo(DetectedObjectsNeedle.Blobs[0].CenterOfGravity) < 100)
                                i = 0;
                        }
                        else
                        {
                            i = 0;
                            if (DetectedObjects.Blobs[i].CenterOfGravity.SquaredDistanceTo(DetectedObjectsNeedle.Blobs[0].CenterOfGravity) < 100)
                                i = 1;
                        }

                        double distanceFromNeedle0 = (DetectedObjects.Blobs[0].CenterOfGravity.X - Needle.X) * (DetectedObjects.Blobs[0].CenterOfGravity.X - Needle.X) + (DetectedObjects.Blobs[0].CenterOfGravity.Y - Needle.Y) * (DetectedObjects.Blobs[0].CenterOfGravity.Y - Needle.Y);
                        double distanceFromNeedle1 = (DetectedObjects.Blobs[1].CenterOfGravity.X - Needle.X) * (DetectedObjects.Blobs[1].CenterOfGravity.X - Needle.X) + (DetectedObjects.Blobs[1].CenterOfGravity.Y - Needle.Y) * (DetectedObjects.Blobs[1].CenterOfGravity.Y - Needle.Y);
                        if (distanceFromNeedle0 > distanceFromNeedle1)
                            i = 0;
                        else
                            i = 1;


                        //Get Orientation via Accord
                        // Compute the center moments of up to third order
                        // create filter
                        cropfilter = new AForge.Imaging.Filters.Crop(DetectedObjects.Blobs[i].Rectangle);
                        Bitmap croppedImage = cropfilter.Apply(grayscaledBitmap);
                        Accord.Imaging.Moments.CentralMoments cm = new Accord.Imaging.Moments.CentralMoments(croppedImage, order: 2);
                        // Get size and orientation of the image
                        double angle = cm.GetOrientation() * 180 / Math.PI;
                        double centerpointX = DetectedObjects.Blobs[i].Rectangle.X + DetectedObjects.Blobs[i].Rectangle.Width / 2.0;
                        double centerpointY = DetectedObjects.Blobs[i].Rectangle.Y + DetectedObjects.Blobs[i].Rectangle.Height / 2.0;

                        double newfish_areaX = DetectedObjects.Blobs[i].Rectangle.X;
                        double newfish_areaY = DetectedObjects.Blobs[i].Rectangle.Y;
                        double newfish_width = DetectedObjects.Blobs[i].Rectangle.Width;
                        double newfish_height = DetectedObjects.Blobs[i].Rectangle.Height;
                        //adjust orientation angle of fish left 0 to right 180°
                        if (DetectedObjects.Blobs[i].Rectangle.Width > DetectedObjects.Blobs[i].Rectangle.Height)
                        {
                            if (DetectedObjects.Blobs[i].CenterOfGravity.X < centerpointX && angle > 90)
                            {
                                angle = angle - 180;
                            }
                            if (DetectedObjects.Blobs[i].CenterOfGravity.X > centerpointX && angle < 90)
                            {
                                angle = angle - 180;
                            }
                        }
                        else
                        {
                            if (DetectedObjects.Blobs[i].CenterOfGravity.Y > centerpointY)
                            {
                                angle = angle - 180;
                            }
                        }
                        //get Head point via rotation and minimal bounding rectangle
                        rotatefilter = new AForge.Imaging.Filters.RotateNearestNeighbor(angle, false);
                        Bitmap rotatedImage = rotatefilter.Apply(croppedImage);
                        blobCounter.ProcessImage(rotatedImage);
                        Blob[] blob = blobCounter.GetObjectsInformation();
                        //newBmp = rotatedImage;
                        if (blob.Count() > 0)
                        {
                            cropfilter = new AForge.Imaging.Filters.Crop(blob[0].Rectangle);
                            double distancehead = blob[0].CenterOfGravity.X * 0.8;
                            double distancebody = blob[0].CenterOfGravity.X / 6.0;
                            double distancetail = blob[0].CenterOfGravity.X * 3.0 / 4.0; //blob[0].Rectangle.Width - blob[0].CenterOfGravity.X;

                            //Draw Circle on Blob
                            pen.Color = Color.Red;
                            g.DrawEllipse(pen, DetectedObjects.Blobs[i].CenterOfGravity.X - radius, DetectedObjects.Blobs[i].CenterOfGravity.Y - radius, 2 * radius, 2 * radius);
                            g_saving.DrawEllipse(pen, DetectedObjects.Blobs[i].CenterOfGravity.X - radius, DetectedObjects.Blobs[i].CenterOfGravity.Y - radius, 2 * radius, 2 * radius);
                            float xdistancehead = (float)(distancehead * Math.Cos(Math.PI * angle / 180.0));
                            float ydistancehead = (float)(distancehead * Math.Sin(Math.PI * angle / 180.0));
                            float xdistancebody = (float)(distancebody * Math.Cos(Math.PI * angle / 180.0));
                            float ydistancebody = (float)(distancebody * Math.Sin(Math.PI * angle / 180.0));
                            float xdistancetail = (float)(distancetail * Math.Cos(Math.PI * angle / 180.0));
                            float ydistancetail = (float)(distancetail * Math.Sin(Math.PI * angle / 180.0));
                            System.Drawing.Point LocationBody = new System.Drawing.Point((int)(DetectedObjects.Blobs[i].CenterOfGravity.X - xdistancebody), (int)(DetectedObjects.Blobs[i].CenterOfGravity.Y - ydistancebody));
                            System.Drawing.Point LocationHead = new System.Drawing.Point((int)(DetectedObjects.Blobs[i].CenterOfGravity.X - xdistancehead), (int)(DetectedObjects.Blobs[i].CenterOfGravity.Y - ydistancehead));
                            System.Drawing.Point LocationTail = new System.Drawing.Point((int)(DetectedObjects.Blobs[i].CenterOfGravity.X + xdistancetail), (int)(DetectedObjects.Blobs[i].CenterOfGravity.Y + ydistancetail));
                            fishPartPoints.Clear();
                            fishPartPoints.Add(LocationHead);
                            fishPartPoints.Add(LocationBody);
                            fishPartPoints.Add(LocationTail);
                            /*
                            cropfilter = new AForge.Imaging.Filters.Crop(blob[0].Rectangle);
                            float distancehead = blob[0].CenterOfGravity.X;
                            float distancetail = blob[0].CenterOfGravity.X; //blob[0].Rectangle.Width - blob[0].CenterOfGravity.X;

                            //Draw Circle on Blob
                            
                            double GravityX = DetectedObjects.Blobs[i].CenterOfGravity.X;
                            double GravityY = DetectedObjects.Blobs[i].CenterOfGravity.Y;

                            double HeadX = 0.0;
                            double HeadY = 0.0;
                            double BodyX = 0.0;
                            double BodyY = 0.0;
                            double TailX = 0.0;
                            double TailY = 0.0;

                            if (GravityX < centerpointX)
                            {
                                if (GravityY < centerpointY)
                                {
                                    HeadX = 3.0 / 4.0 * newfish_areaX + 1.0 / 4.0 * GravityX;
                                    HeadY = 3.0 / 4.0 * newfish_areaY + 1.0 / 4.0 * GravityY;
                                    BodyX = 1.0 / 4.0 * newfish_areaX + 3.0 / 4.0 * GravityX;
                                    BodyY = 1.0 / 4.0 * newfish_areaY + 3.0 / 4.0 * GravityY;
                                    TailX = 3.0 / 4.0 * (newfish_areaX + newfish_width) + 1.0 / 4.0 * GravityX;
                                    TailY = 3.0 / 4.0 * (newfish_areaY + newfish_height) + 1.0 / 4.0 * GravityY;
                                }
                                else
                                {
                                    HeadX = 3.0 / 4.0 * newfish_areaX + 1.0 / 4.0 * GravityX;
                                    HeadY = 3.0 / 4.0 * (newfish_areaY + newfish_height) + 1.0 / 4.0 * GravityY;
                                    BodyX = 1.0 / 4.0 * newfish_areaX + 3.0 / 4.0 * GravityX;
                                    BodyY = 1.0 / 4.0 * (newfish_areaY + newfish_height) + 3.0 / 4.0 * GravityY;
                                    TailX = 3.0 / 4.0 * (newfish_areaX + newfish_width) + 1.0 / 4.0 * GravityX;
                                    TailY = 3.0 / 4.0 * newfish_areaY + 1.0 / 4.0 * GravityY;
                                }
                            }
                            else
                            {
                                if (GravityY < centerpointY)
                                {
                                    HeadX = 3.0 / 4.0 * (newfish_areaX + newfish_width) + 1.0 / 4.0 * GravityX;
                                    HeadY = 3.0 / 4.0 * newfish_areaY + 1.0 / 4.0 * GravityY;
                                    BodyX = 1.0 / 4.0 * (newfish_areaX + newfish_width) + 3.0 / 4.0 * GravityX;
                                    BodyY = 1.0 / 4.0 * newfish_areaY + 3.0 / 4.0 * GravityY;
                                    TailX = 3.0 / 4.0 * newfish_areaX + 1.0 / 4.0 * GravityX;
                                    TailY = 3.0 / 4.0 * (newfish_areaY + newfish_height) + 1.0 / 4.0 * GravityY;
                                }
                                else
                                {
                                    HeadX = 3.0 / 4.0 * (newfish_areaX + newfish_width) + 1.0 / 4.0 * GravityX;
                                    HeadY = 3.0 / 4.0 * (newfish_areaY + newfish_height) + 1.0 / 4.0 * GravityY;
                                    BodyX = 1.0 / 4.0 * (newfish_areaX + newfish_width) + 3.0 / 4.0 * GravityX;
                                    BodyY = 1.0 / 4.0 * (newfish_areaY + newfish_height) + 3.0 / 4.0 * GravityY;
                                    TailX = 3.0 / 4.0 * newfish_areaX + 1.0 / 4.0 * GravityX;
                                    TailY = 3.0 / 4.0 * newfish_areaY + 1.0 / 4.0 * GravityY;
                                }
                            }

                            System.Drawing.Point LocationBody = new System.Drawing.Point((int)(BodyX), (int)(BodyY));
                            System.Drawing.Point LocationHead = new System.Drawing.Point((int)(HeadX), (int)(HeadY));
                            System.Drawing.Point LocationTail = new System.Drawing.Point((int)(TailX), (int)(TailY));
                            fishPartPoints.Clear();
                            //g.DrawString(distancehead.ToString("0"), new Font("Arial", 30), Brushes.Red, TextLocationCG);

                            //fishPartPoints.Add(DetectedObjects.Blobs[i].CenterOfGravity );
                            fishPartPoints.Add(LocationHead);
                            fishPartPoints.Add(LocationBody);
                            fishPartPoints.Add(LocationTail);
                            */
                            var touchpart = fishPartPoints[viewModel.fishSelectedPart];
                            pen.Color = Color.Red;
                            g.DrawEllipse(pen, LocationBody.X - radius, LocationBody.Y - radius, 2 * radius, 2 * radius);
                            g_saving.DrawEllipse(pen, LocationBody.X - radius, LocationBody.Y - radius, 2 * radius, 2 * radius);
                            pen.Color = Color.Blue;
                            g.DrawEllipse(pen, LocationHead.X - radius, LocationHead.Y - radius, 2 * radius, 2 * radius);
                            g_saving.DrawEllipse(pen, LocationHead.X - radius, LocationHead.Y - radius, 2 * radius, 2 * radius);
                            pen.Color = Color.Yellow;
                            g.DrawEllipse(pen, LocationTail.X - radius, LocationTail.Y - radius, 2 * radius, 2 * radius);
                            g_saving.DrawEllipse(pen, LocationTail.X - radius, LocationTail.Y - radius, 2 * radius, 2 * radius);
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
                            int distancefromfish = 30;
                            double tan = Math.Tan(angle * (Math.PI / 180));
                            double cubictan = Math.Pow(tan, 3);
                            double squaretan = Math.Pow(tan, 2);
                            double sqrt = Math.Sqrt(Math.Pow(distancefromfish, 2) * (Math.Pow(squaretan, 2) + squaretan));
                            if (0.1 > Math.Abs(tan))
                            {

                                P[0].X = touchpart.X;
                                P[0].Y = touchpart.Y + distancefromfish;

                                P[1].X = touchpart.X;
                                P[1].Y = touchpart.Y - distancefromfish;
                            }
                            else if (1000 < Math.Abs(tan))
                            {
                                P[0].X = touchpart.X + distancefromfish;
                                P[0].Y = touchpart.Y;
                                P[1].X = touchpart.X - distancefromfish;
                                P[1].Y = touchpart.Y;
                            }
                            else
                            {
                                P[0].X = (int)((touchpart.X * (squaretan + 1) - sqrt) / (squaretan + 1));
                                P[0].Y = (int)((touchpart.Y * (cubictan + tan) + sqrt) / (cubictan + tan));
                                P[1].X = (int)((touchpart.X * (squaretan + 1) + sqrt) / (squaretan + 1));
                                P[1].Y = (int)((touchpart.Y * (cubictan + tan) - sqrt) / (cubictan + tan));
                            }
                            //P[1].X = touchpart.X;
                            //P[1].Y = touchpart.Y;
                            if (image_not_saving)
                            {
                                saving_name = "./result/analyzed_image_withpoints" + TIME + "_" + string.Format("{0}", count) + ".png";
                                saving_figure.Save(saving_name);
                            }
                            viewModel.CannyChecked = false;
                        }
                    }

                    if (Needle.IsEmpty == false && P[0].IsEmpty == false)
                    {
                        int D1 = (Needle.X - P[0].X) * (Needle.X - P[0].X) + (Needle.Y - P[0].Y) * (Needle.Y - P[0].Y);
                        int D2 = (Needle.X - P[1].X) * (Needle.X - P[1].X) + (Needle.Y - P[1].Y) * (Needle.Y - P[1].Y);
                        pen.Color = Color.Green;
                        if (D1 > D2)
                        {
                            Array.Reverse(P);
                        }
                        g.DrawLine(pen, Needle.X, Needle.Y, P[0].X, P[0].Y);
                        g.DrawLine(pen, P[0].X, P[0].Y, P[1].X, P[1].Y);
                        g_saving.DrawLine(pen, Needle.X, Needle.Y, P[0].X, P[0].Y);
                        g_saving.DrawLine(pen, P[0].X, P[0].Y, P[1].X, P[1].Y);

                        System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate {
                            viewModel.Lines.Clear();
                            double pixelratio = viewModel.Border.ActualWidth / 480;
                            System.Windows.Shapes.Line line1 = new System.Windows.Shapes.Line();
                            line1.X1 = Needle.X * pixelratio;
                            line1.Y1 = Needle.Y * pixelratio;
                            line1.X2 = P[0].X * pixelratio;
                            line1.Y2 = P[0].Y * pixelratio;
                            System.Windows.Shapes.Line line2 = new System.Windows.Shapes.Line();
                            line2.X1 = P[0].X * pixelratio;
                            line2.Y1 = P[0].Y * pixelratio;
                            line2.X2 = P[1].X * pixelratio;
                            line2.Y2 = P[1].Y * pixelratio;/*
                            int edge = (int)(viewModel.imageratio * 160);
                            D1 = (160 - P[1].X) * (160 - P[1].X) + (160 - P[1].Y) * (160 - P[1].Y);
                            if (D1 > Math.Pow(edge, 2))
                            {
                               
                            }
                            */
                            line2.X2 = (P[0].X * 0.4 + P[1].X * 0.6) * pixelratio;
                            line2.Y2 = (P[0].Y * 0.4 + P[1].Y * 0.6) * pixelratio;
                            viewModel.Lines.Add(line1);
                            viewModel.Lines.Add(line2);
                        });



                    }

                    grayscaledBitmap = newBmp;
                    saving_image = saving_figure;
                    if (image_not_saving)
                    {
                        saving_name = "./result/binary_analyzed_image" + TIME + "_" + string.Format("{0}", count) + ".png";
                        grayscaledBitmap.Save(saving_name);
                    }

                    if (viewModel.CannyChecked == false)
                        image_not_saving = false;
                    //grayscaledBitmap.Save("7.png");
                    pen.Dispose();
                    g.Dispose();
                    g_newgrayscale.Dispose();
                    g_saving.Dispose();
                }
                if (image_not_saving)
                {
                    saving_name = "./result/analyzed_image" + TIME + "_" + string.Format("{0}", count) + ".png";
                    //saving_image.Save(saving_name);
                }
                var bitmapsource = ConvertBitmap(saving_image);

                bitmapsource.Freeze();
                viewModel.AnalysedImage = bitmapsource;  //ConvertBitmap(needleimage);//
                grayscaledBitmap.Dispose();
            }
        }
        public void AnalyseImage(Bitmap Image)
        {
            if (DetectedObjects == null)
            {
                DetectedObjects = new DetectedObjectsClass();
            }

            bool succesful = ImageQueue.TryTake(out image);
            image = GetBitmapSource(Image);
            succesful = true;

            if (succesful == true)
            {
                int width = 480;// image.PixelWidth;
                int height = 480;// image.PixelHeight;

                System.Drawing.Bitmap origgrayscaledBitmap = BitmapFromSource(image);
                //var origgrayscaledBitmap = reader.ReadVideoFrame();


                var newHeight = (int)(height * viewModel.imageratio * 0.7);

                /*
                var newWidth = width * viewModel.imageratio;
                var newHeight = height * viewModel.imageratio;

                AForge.Imaging.Filters.Crop cropfilter = new AForge.Imaging.Filters.Crop(new Rectangle((int)(width / 2.0 - 20),
                                                                                                       (int)(height / 2.0 - 20),
                                                                                                       40,
                                                                                                       40));
                
                
                cropfilter = new AForge.Imaging.Filters.Crop(new Rectangle((int)(width / 2.0 - newWidth/2.0),
                                                                           (int)(height / 2.0 - newHeight/2.0),
                                                                           (int)newWidth,
                                                                           (int)newHeight));
                var croppedNeedleBitmap = cropfilter.Apply(origgrayscaledBitmap);
                origgrayscaledBitmap = cropfilter.Apply(origgrayscaledBitmap);
                //croppedNeedleBitmap.Save("croppedNeedleBitmap.png");
                width = (int)newWidth;
                height = (int)newHeight;
                */
                // create filter - rotate for -90 degrees keeping original image size
                AForge.Imaging.Filters.RotateNearestNeighbor rotatefilter = new AForge.Imaging.Filters.RotateNearestNeighbor(90, true);
                // apply the filter
                //origgrayscaledBitmap = rotatefilter.Apply(origgrayscaledBitmap);
                AForge.Imaging.Filters.Mirror mirrorfilter = new AForge.Imaging.Filters.Mirror(false, true);
                //mirrorfilter.ApplyInPlace(origgrayscaledBitmap);
                //origgrayscaledBitmap.Save("1.png");
                AForge.Imaging.Filters.Threshold bwFilter = new AForge.Imaging.Filters.Threshold(130); // 120 .viewModel.BwThreshold);
                AForge.Imaging.Filters.Threshold bwFilter2 = new AForge.Imaging.Filters.Threshold(30);
                AForge.Imaging.Filters.Invert invertFilter = new AForge.Imaging.Filters.Invert();
                AForge.Imaging.Filters.SimpleSkeletonization skeletonFilter = new AForge.Imaging.Filters.SimpleSkeletonization();
                AForge.Imaging.Filters.BilateralSmoothing filter = new AForge.Imaging.Filters.BilateralSmoothing();
                filter.KernelSize = 15;
                filter.SpatialFactor = 10;
                filter.ColorFactor = 60;
                filter.ColorPower = 0.5;

                var grayscaledBitmap = bwFilter.Apply(origgrayscaledBitmap);
                //grayscaledBitmap.Save("2.png");
                var needleimage = bwFilter2.Apply(origgrayscaledBitmap);
                //needleimage.Save("3.png");

                HoughCircle circleResult = CircleDetection(grayscaledBitmap, 102, 1);
                if (circleResult == null)
                {
                    circleResult = new HoughCircle(240, 240, 101, 1, 1);
                }

                using (Bitmap btm = new Bitmap(width, height))
                {
                    using (Graphics grf = Graphics.FromImage(btm))
                    {
                        Rectangle ImageSize = new Rectangle(0, 0, width, height);
                        grf.FillRectangle(Brushes.Black, ImageSize);

                        int distance = height - newHeight;
                        grf.FillEllipse(Brushes.White, circleResult.X - circleResult.Radius, circleResult.Y - circleResult.Radius, circleResult.Radius * 2, circleResult.Radius * 2);
                        AForge.Imaging.Filters.ApplyMask masking = new AForge.Imaging.Filters.ApplyMask(btm);
                        masking.ApplyInPlace(grayscaledBitmap);
                        AForge.Imaging.Filters.MaskedFilter mask = new AForge.Imaging.Filters.MaskedFilter(invertFilter, btm);
                        mask.ApplyInPlace(grayscaledBitmap);

                        masking.ApplyInPlace(needleimage);
                        mask.ApplyInPlace(needleimage);
                        //grayscaledBitmap.Save("4.png");
                        //needleimage.Save("5.png");

                    }
                }

                AForge.Imaging.Filters.Closing closingfilter = new AForge.Imaging.Filters.Closing();
                AForge.Imaging.Filters.BinaryDilatation3x3 dilatationfilter = new AForge.Imaging.Filters.BinaryDilatation3x3();
                AForge.Imaging.Filters.BinaryErosion3x3 erosionfilter = new AForge.Imaging.Filters.BinaryErosion3x3();
                // apply the filter
                dilatationfilter.ApplyInPlace(grayscaledBitmap);
                erosionfilter.ApplyInPlace(grayscaledBitmap);
                AForge.Imaging.Filters.FillHoles fillfilter = new AForge.Imaging.Filters.FillHoles();
                fillfilter.MaxHoleHeight = 20;
                fillfilter.MaxHoleWidth = 20;
                fillfilter.CoupledSizeFiltering = false;
                // apply the filter
                fillfilter.ApplyInPlace(grayscaledBitmap);
                //grayscaledBitmap.Save("6.png");

                System.Drawing.Bitmap newBmp;
                System.Drawing.Bitmap newgrayscale;

                if (viewModel.CannyChecked == true)
                {

                    // create Graphics object to draw on the image and a pen
                    newBmp = new System.Drawing.Bitmap(grayscaledBitmap.Width, grayscaledBitmap.Height);
                    newgrayscale = new System.Drawing.Bitmap(grayscaledBitmap.Width, grayscaledBitmap.Height);
                    System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(newBmp);
                    System.Drawing.Graphics g_newgrayscale = System.Drawing.Graphics.FromImage(newgrayscale);
                    g.DrawImage(grayscaledBitmap, 0, 0);
                    g_newgrayscale.DrawImage(grayscaledBitmap, 0, 0);
                    System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.Red, 5);
                    //check each object and draw circle around objects, which
                    //are recognized as circles
                    System.Drawing.Point Needle = new System.Drawing.Point();
                    System.Drawing.Point[] P = new System.Drawing.Point[2];

                    float radius = 10;
                    // detect Needle in additional step with different threshold as it is the darkest object in the well 
                    // BlobCounter: countng and extracting the ojects detected
                    BlobCounter blobCounterNeedle = new BlobCounter();
                    blobCounterNeedle.FilterBlobs = true;
                    blobCounterNeedle.MinWidth = 2;
                    blobCounterNeedle.MaxWidth = 8;
                    blobCounterNeedle.MinHeight = 2;
                    blobCounterNeedle.MaxHeight = 8;
                    blobCounterNeedle.ObjectsOrder = ObjectsOrder.Area;
                    blobCounterNeedle.ProcessImage(needleimage);
                    DetectedObjectsClass DetectedObjectsNeedle = new DetectedObjectsClass();
                    DetectedObjectsNeedle.Blobs = blobCounterNeedle.GetObjectsInformation();

                    if (Needle.IsEmpty == true && DetectedObjectsNeedle.Blobs.Count() != 0)
                    {
                        pen.Color = Color.Red;
                        Needle.X = (int)DetectedObjectsNeedle.Blobs[0].CenterOfGravity.X;
                        Needle.Y = (int)DetectedObjectsNeedle.Blobs[0].CenterOfGravity.Y;
                        g.DrawEllipse(pen, Needle.X - radius, Needle.Y - radius, 2 * radius, 2 * radius);

                        // color in needle as black circle into grayscaledBitmap for fish detection

                    }

                    BlobCounter blobCounter = new BlobCounter();
                    blobCounter.FilterBlobs = true;
                    blobCounter.MinWidth = 8;
                    blobCounter.MaxWidth = 60;
                    blobCounter.MinHeight = 8;
                    blobCounter.MaxHeight = 60;
                    blobCounter.ObjectsOrder = ObjectsOrder.Area;
                    blobCounter.ProcessImage(grayscaledBitmap);
                    DetectedObjects.Blobs = blobCounter.GetObjectsInformation();

                    if (DetectedObjects.Blobs.Count() >= 2 && DetectedObjectsNeedle.Blobs.Count() >= 1)
                    {


                        //Which blob is rounder
                        int i = 0;

                        double roundness1 = Math.Min(DetectedObjects.Blobs[0].Rectangle.Height, DetectedObjects.Blobs[0].Rectangle.Width) * 1.00 / (Math.Max(DetectedObjects.Blobs[0].Rectangle.Width, DetectedObjects.Blobs[0].Rectangle.Height) * 1.00);
                        double roundness2 = Math.Min(DetectedObjects.Blobs[1].Rectangle.Height, DetectedObjects.Blobs[1].Rectangle.Width) * 1.00 / (Math.Max(DetectedObjects.Blobs[1].Rectangle.Width, DetectedObjects.Blobs[1].Rectangle.Height) * 1.00);
                        if (roundness1 > roundness2)
                        {
                            i = 1;
                            if (DetectedObjects.Blobs[i].CenterOfGravity.SquaredDistanceTo(DetectedObjectsNeedle.Blobs[0].CenterOfGravity) < 1000)
                                i = 0;
                        }
                        else
                        {
                            i = 0;
                            if (DetectedObjects.Blobs[i].CenterOfGravity.SquaredDistanceTo(DetectedObjectsNeedle.Blobs[0].CenterOfGravity) < 1000)
                                i = 1;
                        }
                        //Get Orientation via Accord
                        // Compute the center moments of up to third order
                        // create filter
                        AForge.Imaging.Filters.Crop cropfilter = new AForge.Imaging.Filters.Crop(DetectedObjects.Blobs[i].Rectangle);
                        Bitmap croppedImage = cropfilter.Apply(grayscaledBitmap);
                        Accord.Imaging.Moments.CentralMoments cm = new Accord.Imaging.Moments.CentralMoments(croppedImage, order: 2);
                        // Get size and orientation of the image
                        double angle = cm.GetOrientation() * 180 / Math.PI;
                        int centerpointX = DetectedObjects.Blobs[i].Rectangle.X + DetectedObjects.Blobs[i].Rectangle.Width / 2;
                        int centerpointY = DetectedObjects.Blobs[i].Rectangle.Y + DetectedObjects.Blobs[i].Rectangle.Height / 2;
                        //adjust orientation angle of fish left 0 to right 180°
                        if (DetectedObjects.Blobs[i].Rectangle.Width > DetectedObjects.Blobs[i].Rectangle.Height)
                        {
                            if (DetectedObjects.Blobs[i].CenterOfGravity.X < centerpointX && angle > 90)
                            {
                                angle = angle - 180;
                            }
                            if (DetectedObjects.Blobs[i].CenterOfGravity.X > centerpointX && angle < 90)
                            {
                                angle = angle - 180;
                            }
                        }
                        else
                        {
                            if (DetectedObjects.Blobs[i].CenterOfGravity.Y > centerpointY)
                            {
                                angle = angle - 180;
                            }
                        }
                        //get Head point via rotation and minimal bounding rectangle
                        rotatefilter = new AForge.Imaging.Filters.RotateNearestNeighbor(angle, false);
                        Bitmap rotatedImage = rotatefilter.Apply(croppedImage);
                        blobCounter.ProcessImage(rotatedImage);
                        Blob[] blob = blobCounter.GetObjectsInformation();
                        //newBmp = rotatedImage;
                        if (blob.Count() > 0)
                        {

                            cropfilter = new AForge.Imaging.Filters.Crop(blob[0].Rectangle);
                            float distancehead = blob[0].CenterOfGravity.X;
                            float distancetail = blob[0].CenterOfGravity.X; //blob[0].Rectangle.Width - blob[0].CenterOfGravity.X;

                            //Draw Circle on Blob
                            pen.Color = Color.Red;
                            g.DrawEllipse(pen, DetectedObjects.Blobs[i].CenterOfGravity.X - radius, DetectedObjects.Blobs[i].CenterOfGravity.Y - radius, 2 * radius, 2 * radius);
                            float xdistancehead = (float)(distancehead * Math.Cos(Math.PI * angle / 180.0));
                            float ydistancehead = (float)(distancehead * Math.Sin(Math.PI * angle / 180.0));
                            float xdistancetail = (float)(distancetail * Math.Cos(Math.PI * angle / 180.0));
                            float ydistancetail = (float)(distancetail * Math.Sin(Math.PI * angle / 180.0));
                            System.Drawing.Point LocationBody = new System.Drawing.Point((int)(DetectedObjects.Blobs[i].CenterOfGravity.X), (int)(DetectedObjects.Blobs[i].CenterOfGravity.Y));
                            System.Drawing.Point LocationHead = new System.Drawing.Point((int)(DetectedObjects.Blobs[i].CenterOfGravity.X - xdistancehead), (int)(DetectedObjects.Blobs[i].CenterOfGravity.Y - ydistancehead));
                            System.Drawing.Point LocationTail = new System.Drawing.Point((int)(DetectedObjects.Blobs[i].CenterOfGravity.X + xdistancetail), (int)(DetectedObjects.Blobs[i].CenterOfGravity.Y + ydistancetail));
                            fishPartPoints.Clear();
                            //g.DrawString(distancehead.ToString("0"), new Font("Arial", 30), Brushes.Red, TextLocationCG);

                            //fishPartPoints.Add(DetectedObjects.Blobs[i].CenterOfGravity );
                            fishPartPoints.Add(LocationHead);
                            fishPartPoints.Add(LocationBody);
                            fishPartPoints.Add(LocationTail);
                            var touchpart = fishPartPoints[viewModel.fishSelectedPart];
                            pen.Color = Color.Blue;
                            g.DrawEllipse(pen, LocationHead.X - radius, LocationHead.Y - radius, 2 * radius, 2 * radius);
                            pen.Color = Color.Yellow;
                            g.DrawEllipse(pen, LocationTail.X - radius, LocationTail.Y - radius, 2 * radius, 2 * radius);

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
                            int distancefromfish = 80;
                            double tan = Math.Tan(angle * (Math.PI / 180));
                            double cubictan = Math.Pow(tan, 3);
                            double squaretan = Math.Pow(tan, 2);
                            double sqrt = Math.Sqrt(Math.Pow(distancefromfish, 2) * (Math.Pow(squaretan, 2) + squaretan));
                            if (0.1 > Math.Abs(tan))
                            {

                                P[0].X = touchpart.X;
                                P[0].Y = touchpart.Y + distancefromfish;
                                P[1].X = touchpart.X;
                                P[1].Y = touchpart.Y - distancefromfish;
                            }
                            else if (1000 < Math.Abs(tan))
                            {
                                P[0].X = touchpart.X + distancefromfish;
                                P[0].Y = touchpart.Y;
                                P[1].X = touchpart.X - distancefromfish;
                                P[1].Y = touchpart.Y;
                            }
                            else
                            {
                                P[0].X = (int)((touchpart.X * (squaretan + 1) - sqrt) / (squaretan + 1));
                                P[0].Y = (int)((touchpart.Y * (cubictan + tan) + sqrt) / (cubictan + tan));
                                P[1].X = (int)((touchpart.X * (squaretan + 1) + sqrt) / (squaretan + 1));
                                P[1].Y = (int)((touchpart.Y * (cubictan + tan) - sqrt) / (cubictan + tan));
                            }
                            viewModel.CannyChecked = false;
                        }
                    }

                    if (Needle.IsEmpty == false && P[0].IsEmpty == false)
                    {
                        int D1 = (Needle.X - P[0].X) * (Needle.X - P[0].X) + (Needle.Y - P[0].Y) * (Needle.Y - P[0].Y);
                        int D2 = (Needle.X - P[1].X) * (Needle.X - P[1].X) + (Needle.Y - P[1].Y) * (Needle.Y - P[1].Y);
                        pen.Color = Color.Green;
                        if (D1 > D2)
                        {
                            Array.Reverse(P);
                        }
                        g.DrawLine(pen, Needle.X, Needle.Y, P[0].X, P[0].Y);
                        g.DrawLine(pen, P[0].X, P[0].Y, P[1].X, P[1].Y);
                        int edge = (int)(viewModel.imageratio * 220 - 10);
                        D1 = (edge - P[1].X) * (edge - P[1].X) + (edge - P[1].Y) * (edge - P[1].Y);
                        if (D1 > Math.Pow(220, 2))
                        {
                            //check if P[1] inside circle
                        }

                        System.Windows.Application.Current.Dispatcher.Invoke((Action)delegate {
                            viewModel.Lines.Clear();
                            double pixelratio = viewModel.Border.ActualWidth / 480;
                            System.Windows.Shapes.Line line1 = new System.Windows.Shapes.Line();
                            line1.X1 = Needle.X * pixelratio;
                            line1.Y1 = Needle.Y * pixelratio;
                            line1.X2 = P[0].X * pixelratio;
                            line1.Y2 = P[0].Y * pixelratio;
                            System.Windows.Shapes.Line line2 = new System.Windows.Shapes.Line();
                            line2.X1 = P[0].X * pixelratio;
                            line2.Y1 = P[0].Y * pixelratio;
                            line2.X2 = P[1].X * pixelratio;
                            line2.Y2 = P[1].Y * pixelratio;
                            viewModel.Lines.Add(line1);
                            viewModel.Lines.Add(line2);
                        });

                    }

                    grayscaledBitmap = newBmp;
                    //grayscaledBitmap.Save("7.png");
                    pen.Dispose();
                    g.Dispose();
                    g_newgrayscale.Dispose();
                }

                var bitmapsource = ConvertBitmap(grayscaledBitmap);

                bitmapsource.Freeze();
                viewModel.AnalysedImage = bitmapsource;  //ConvertBitmap(needleimage);//
                grayscaledBitmap.Dispose();
            }
        }

        private HoughCircle CircleDetection(System.Drawing.Bitmap sourceImage, int radius, int intensityNum)
        {
            HoughCircleTransformation circleTransform = new HoughCircleTransformation(radius);
            // apply Hough circle transform
            circleTransform.ProcessImage(sourceImage);
            Bitmap houghCirlceImage = circleTransform.ToBitmap();

            // get circles using relative intensity
            //HoughCircle[] circles = circleTransform.GetCirclesByRelativeIntensity(0.5);
            HoughCircle[] circles = circleTransform.GetMostIntensiveCircles(intensityNum);
            double[] averageCircle = { 0.0, 0.0, 0.0 };
            int circleNum = 0;
            foreach (HoughCircle circle in circles)
            {
                // ...
                if ((circle.X - 240) * (circle.X - 240) < 2500)
                {
                    if ((circle.Y - 240) * (circle.Y - 240) < 2500)
                    {
                        averageCircle[0] += circle.X;
                        averageCircle[1] += circle.Y;
                        averageCircle[2] += circle.Radius;
                        circleNum++;
                    }
                }
            }
            averageCircle[0] /= circleNum;
            averageCircle[1] /= circleNum;
            averageCircle[2] /= circleNum;
            HoughCircle circleResult = new HoughCircle((int)averageCircle[0], (int)averageCircle[1], (int)averageCircle[2], 1, 1);
            //return circleResult;
            return circleResult;
        }


        private HoughCircle CircleCenterDetection(System.Drawing.Bitmap Image)
        {


            AForge.Imaging.Filters.FillHoles fillfilter = new AForge.Imaging.Filters.FillHoles();
            fillfilter.MaxHoleHeight = 100;
            fillfilter.MaxHoleWidth = 100;
            fillfilter.CoupledSizeFiltering = false;
            // apply the filter
            Image = fillfilter.Apply(Image);
            //grayscaledBitmap.Save("6.png");
            if (image_not_saving)
            {
                String saving_name = "./result/circlecenter" + "_" + string.Format("{0}", count) + ".png";
                Image.Save(saving_name);
            }

            BlobCounter blobCounterCenter = new BlobCounter();
            blobCounterCenter.FilterBlobs = true;
            blobCounterCenter.MinWidth = 100;
            blobCounterCenter.MaxWidth = 500;
            blobCounterCenter.MinHeight = 100;
            blobCounterCenter.MaxHeight = 500;
            blobCounterCenter.ObjectsOrder = ObjectsOrder.Area;
            blobCounterCenter.ProcessImage(Image);
            DetectedObjectsClass DetectedObjectsCenter = new DetectedObjectsClass();
            DetectedObjectsCenter.Blobs = blobCounterCenter.GetObjectsInformation();

            int X = (int)DetectedObjectsCenter.Blobs[0].CenterOfGravity.X;
            int Y = (int)DetectedObjectsCenter.Blobs[0].CenterOfGravity.Y;
            HoughCircle circleResult = new HoughCircle(X, Y, 81, 1, 1);

            return circleResult;

        }

        private System.Drawing.Bitmap CircleCropImage(System.Drawing.Bitmap Image, HoughCircle circleResult, int outerblack)
        {
            var width = Image.Width;
            var height = Image.Height;
            Bitmap btm = new Bitmap(width, height);
            Graphics grf = Graphics.FromImage(btm);
            Rectangle ImageSize = new Rectangle(0, 0, width, height);
            if (outerblack == 1)
            {
                grf.FillRectangle(Brushes.Black, ImageSize);

                grf.FillEllipse(Brushes.White, circleResult.X - circleResult.Radius, circleResult.Y - circleResult.Radius, circleResult.Radius * 2, circleResult.Radius * 2);
                AForge.Imaging.Filters.ApplyMask masking = new AForge.Imaging.Filters.ApplyMask(btm);
                AForge.Imaging.Filters.Invert invertFilter = new AForge.Imaging.Filters.Invert();
                AForge.Imaging.Filters.MaskedFilter mask = new AForge.Imaging.Filters.MaskedFilter(invertFilter, btm);
                masking.ApplyInPlace(Image);
                mask.ApplyInPlace(Image);
            }
            else
            {
                grf.FillRectangle(Brushes.Black, ImageSize);

                grf.FillEllipse(Brushes.White, circleResult.X - circleResult.Radius, circleResult.Y - circleResult.Radius, circleResult.Radius * 2, circleResult.Radius * 2);
                AForge.Imaging.Filters.ApplyMask masking = new AForge.Imaging.Filters.ApplyMask(btm);
                AForge.Imaging.Filters.Invert invertFilter = new AForge.Imaging.Filters.Invert();
                AForge.Imaging.Filters.MaskedFilter mask = new AForge.Imaging.Filters.MaskedFilter(invertFilter, btm);
                masking.ApplyInPlace(Image);
                //mask.ApplyInPlace(Image);
            }


            return Image;

        }

        private static WriteableBitmap CropImage(WriteableBitmap source,
                                                         int xOffset, int yOffset,
                                                         int width, int height)
        {
            // Get the width of the source image
            var sourceWidth = source.PixelWidth;

            // Get the resultant image as WriteableBitmap with specified size
            var result = new WriteableBitmap(width, height, source.DpiX, source.DpiX, source.Format, null);

            // Create the array of bytes+
            byte[] Pixels = new byte[source.PixelWidth * source.PixelHeight];
            source.CopyPixels(Pixels, source.PixelWidth, 0);
            Int32Rect rect = new Int32Rect(xOffset, yOffset, width, height);
            result.WritePixels(rect, Pixels, source.PixelWidth, 0);
            return result;
        }

        #region Bitmap Conversion Methods
        private System.Drawing.Bitmap BitmapFromSource(BitmapSource bitmapsource)
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


        public static BitmapSource GetBitmapSource(Bitmap image)
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
        //Convert WPF Bitmapsource to Aforge Bitmap - Colorspace wrong - potential for improvement
        //private System.Drawing.Bitmap BitmapFromSource(BitmapSource bitmapsource)
        //{
        //    System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(
        //        bitmapsource.PixelWidth,
        //        bitmapsource.PixelHeight,
        //        System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        //    System.Drawing.Imaging.BitmapData data = bmp.LockBits(
        //        new System.Drawing.Rectangle(System.Drawing.Point.Empty, bmp.Size),
        //        System.Drawing.Imaging.ImageLockMode.WriteOnly,
        //        System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
        //    bitmapsource.CopyPixels(
        //      System.Windows.Int32Rect.Empty,
        //      data.Scan0,
        //      data.Height * data.Stride,
        //      data.Stride);
        //    bmp.UnlockBits(data);

        //    System.Drawing.Bitmap clone = new System.Drawing.Bitmap(bmp.Width, bmp.Height,
        //        System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        //    using (System.Drawing.Graphics gr = System.Drawing.Graphics.FromImage(clone))
        //    {
        //        gr.DrawImage(bmp, new System.Drawing.Rectangle(0, 0, clone.Width, clone.Height));
        //        return clone;
        //    }
        //}


        //Convert Aforge Bitmap back to WPF Bitmapsource
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
        private BitmapSource ConvertBitmap(System.Drawing.Bitmap source)
        {
            using (source)
            {
                IntPtr hBitmap = source.GetHbitmap();
                var image = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                            hBitmap,
                            IntPtr.Zero,
                            System.Windows.Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                DeleteObject(hBitmap); //otherwise memory leak due to hbitmap
                return image;
            }

        }
        #endregion
    }
}

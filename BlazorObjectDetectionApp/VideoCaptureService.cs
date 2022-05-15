using BlazorObjectDetectionApp.DataStructures;
using BlazorObjectDetectionApp.YoloParser;
using LibVLCSharp.Shared;
using Microsoft.ML;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.ML.Transforms.Image.ImageResizingEstimator;

namespace BlazorObjectDetectionApp
{
    public class VideoCaptureService
    {
        private const uint Width = 416;
        private const uint Height = 416;

        /// <summary>
        /// RGBA is used, so 4 byte per pixel, or 32 bits.
        /// </summary>
        private const uint BytePerPixel = 4;

        /// <summary>
        /// the number of bytes per "line"
        /// For performance reasons inside the core of VLC, it must be aligned to multiples of 32.
        /// </summary>
        private readonly uint Pitch;

        /// <summary>
        /// The number of lines in the buffer.
        /// For performance reasons inside the core of VLC, it must be aligned to multiples of 32.
        /// </summary>
        private readonly uint Lines;

        private SKBitmap CurrentBitmap;
        private long FrameCounter = 0;
        private readonly ConcurrentQueue<SKBitmap> FilesToProcess = new ConcurrentQueue<SKBitmap>();
        
        public delegate void SnapshotEventHandler(string bitmap);
        public event SnapshotEventHandler Snapshot;

        public VideoCaptureService()
        {
            Pitch = Align(Width * BytePerPixel);
            Lines = Align(Height);

            uint Align(uint size)
            {
                if (size % 32 == 0)
                {
                    return size;
                }

                return ((size / 32) + 1) * 32;// Align on the next multiple of 32
            }
        }

        public OnnxModelScorer LoadModelScorer()
        {
            // Create instance of model scorer
            MLContext mlContext = new MLContext();
            var modelFilePath = Path.Combine(AppContext.BaseDirectory, @"assets\models\tiny-yolov2.onnx");
            var modelScorer = new OnnxModelScorer(modelFilePath, mlContext);
            return modelScorer;
        }

        void DrawBoundingBox(IList<YoloBoundingBox> filteredBoundingBoxes, SKCanvas canvas)
        {
            
            var originalImageHeight = 416;
            var originalImageWidth = 416;

            foreach (var box in filteredBoundingBoxes)
            {
                var x = (uint)Math.Max(box.Dimensions.X, 0);
                var y = (uint)Math.Max(box.Dimensions.Y, 0);
                var width = (uint)Math.Min(originalImageWidth - x, box.Dimensions.Width);
                var height = (uint)Math.Min(originalImageHeight - y, box.Dimensions.Height);

                x = (uint)originalImageWidth * x / OnnxModelScorer.ImageNetSettings.imageWidth;
                y = (uint)originalImageHeight * y / OnnxModelScorer.ImageNetSettings.imageHeight;
                width = (uint)originalImageWidth * width / OnnxModelScorer.ImageNetSettings.imageWidth;
                height = (uint)originalImageHeight * height / OnnxModelScorer.ImageNetSettings.imageHeight;
                string text = $"{box.Label} ({(box.Confidence * 100).ToString("0")}%)";
                Font drawFont = new Font("Arial", 12, FontStyle.Bold);
                
                SKPaint fontPaint = new SKPaint(new SKFont(SKTypeface.FromFamilyName("Arial")))
                {
                    Color = Color.Black.ToSKColor(),
                   
                };
                var length = fontPaint.MeasureText(text);                              
                canvas.DrawRect(x, y - 12, length, 12, new SKPaint() { Color = box.BoxColor.ToSKColor(), StrokeWidth = 2.2f, Style = SKPaintStyle.StrokeAndFill });
                canvas.DrawRect(x, y, width, height, new SKPaint() { Color = box.BoxColor.ToSKColor(), StrokeWidth = 2.2f, Style = SKPaintStyle.Stroke });
                canvas.DrawText(text, x, y - 2, fontPaint);
            }
        }

        public IList<YoloBoundingBox> DetectObjects(Bitmap bitmap)
        {
            // Use model to score data
            
            IEnumerable<float[]> probabilities = ModelScorer?.Score(bitmap);

            YoloOutputParser parser = new YoloOutputParser();

            var boundingBoxes =
                probabilities
                .Select(probability => parser.ParseOutputs(probability))
                 .Select(boxes => parser.FilterBoundingBoxes(boxes, 20, 0.5F));
            
           IList<YoloBoundingBox> detectedObjects = boundingBoxes.First();

           return detectedObjects;
             
        }

        IList<YoloBoundingBox> LastYoloBoundingBoxes = new List<YoloBoundingBox>();
        DateTime LastBoxDetection = DateTime.Now;

        private async Task ProcessThumbnailsAsync(CancellationToken token)
        {
            var frameNumber = 0;
            var surface = SKSurface.Create(new SKImageInfo((int)Width, (int)Height));
            var canvas = surface.Canvas;
            while (!token.IsCancellationRequested)
            {
                if (FilesToProcess.TryDequeue(out var bitmap))
                {
                    canvas.DrawBitmap(bitmap, 0, 0); // Effectively crops the original bitmap to get only the visible area

                    if((DateTime.Now - LastBoxDetection).TotalMilliseconds > 250)
                    {
                      var currentDateTime = DateTime.Now;
                      // Debug.Write(currentDateTime);
                      LastYoloBoundingBoxes =  DetectObjects(bitmap.ToBitmap());
                      LastBoxDetection = DateTime.Now;
                      Debug.WriteLine((LastBoxDetection - currentDateTime).TotalMilliseconds);
                    }
      
                    DrawBoundingBox(LastYoloBoundingBoxes, canvas);                

                    using (var outputImage = surface.Snapshot())
                    using (var data = outputImage.Encode(SKEncodedImageFormat.Jpeg, 50))
                    {
                        var str = Convert.ToBase64String(data.ToArray());
                        Snapshot?.Invoke(str);
                    }
                    // https://base64.guru/converter/decode/image
                    bitmap.Dispose();
                    frameNumber++;
                }
                else
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), token);
                }
            }
        }

        private IntPtr Lock(IntPtr opaque, IntPtr planes)
        {
            CurrentBitmap = new SKBitmap(new SKImageInfo((int)(Pitch / BytePerPixel), (int)Lines, SKColorType.Bgra8888));
            Marshal.WriteIntPtr(planes, CurrentBitmap.GetPixels());
            return IntPtr.Zero;
        }

        private void Display(IntPtr opaque, IntPtr picture)
        {
            if (FrameCounter % 3 == 0 && CurrentBitmap != null) // take only every 100. image
            {
                FilesToProcess.Enqueue(CurrentBitmap);
                CurrentBitmap = null;
            }
            else
            {
                CurrentBitmap.Dispose();
                CurrentBitmap = null;
            }
            FrameCounter++;
        }

        public bool IsRunning { get; set; } = false;
        public OnnxModelScorer ModelScorer { get; private set; }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            if (IsRunning)
                return;

            IsRunning = true;

            ModelScorer =  LoadModelScorer();

            // Load native libvlc library
            Core.Initialize();

            using (var libvlc = new LibVLC())
            using (var mediaPlayer = new MediaPlayer(libvlc))
            {
                cancellationToken.Register(() =>
                {
                    mediaPlayer.Stop();
                });
                // Listen to events
                var processingCancellationTokenSource = new CancellationTokenSource();
                mediaPlayer.Stopped += (s, e) => processingCancellationTokenSource.CancelAfter(1);
                
                // Create new media
                var path = Path.Combine(AppContext.BaseDirectory, @"assets\videos\traffic_chicago.mp4");
                var media = new Media(libvlc, path, FromType.FromPath);
                media.AddOption(":no-audio");
                // Set the size and format of the video here.
                mediaPlayer.SetVideoFormat("RV32", Width, Height, Pitch);
                mediaPlayer.SetRate(1f);
                mediaPlayer.SetVideoCallbacks(Lock, null, Display);
                
                // Start recording
                mediaPlayer.Play(media);

                // Waits for the processing to stop
                try
                {
                    await ProcessThumbnailsAsync(processingCancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                { }
            }
            IsRunning= false;
            await Task.CompletedTask;
        }

     
    }
}

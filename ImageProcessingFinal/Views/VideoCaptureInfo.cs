using System;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace ImageProcessingFinal.Views;


public class VideoCaptureInfo
{
    public VideoCapture Video { get; set; }                 // Emgu.Cv.VideoCapture object (it can be a camera input or video input)
    public long? TotalFrames { get; set; }                  // Number of frames in the video (estimated from video length and FPS)
    public long? TotalDuration { get; set; }                // Length of video input in ms (milliseconds)
    public long? CurrentFrameNumber { get; set; }           // Current number of frame
    public double? FPS { get; set; }                        // FPS (frames per second) of the video
    public double? DeltaFrameTime { get; set; }             // Time elapsed between frames in ms (milliseconds)
    private bool? IsWebcam { get; set; }                    // Webcamera input?

    public VideoCaptureInfo(VideoCapture Video, bool IsWebCam)
    {
        this.Video = Video;
        this.IsWebcam = IsWebCam;
        this.FPS = Convert.ToInt32(Video.Get(CapProp.Fps));
        this.DeltaFrameTime = 1000.0 / this.FPS;
        this.CurrentFrameNumber = 0;
        if (!IsWebCam)
        {
            this.TotalFrames = Convert.ToInt64(this.Video.Get(CapProp.FrameCount));
            this.TotalDuration = Convert.ToInt64(DeltaFrameTime * Convert.ToDouble(Video.Get(CapProp.FrameCount)));
        }
    }

    /*~VideoCaptureInfo()
    {
        this.Video.Release();
        this.Video.Dispose();
        this.CurrentFrameNumber = null;
        this.FPS = null;
        this.DeltaFrameTime = null;
        this.IsWebcam = null;
        GC.Collect();
    }*/
}
using System;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace ImageProcessingFinal.Views;


public class VideoCaptureInfo
{
    public VideoCapture Video { get; }     // Emgu.Cv.VideoCapture object (it can be a camera input or video input)
    public long? TotalFrames { get;}               // Number of frames in the video (estimated from video length and FPS)
    public long? TotalDuration { get;}             // Length of video input in ms (milliseconds)
    public long? CurrentFrameNumber { get; }       // Current number of frame
    public double FPS { get; }                     // FPS (frames per second) of the video
    public double DeltaFrameTime { get; }          // Time elapsed between frames in ms (milliseconds)
    bool IsWebcam { get; }                  // Webcamera input?

    public VideoCaptureInfo(VideoCapture Video, bool IsWebCam)
    {
        this.Video = Video;
        this.Video.Set(CapProp.PosMsec, 0.0);
        this.IsWebcam = IsWebCam;
        this.FPS = Convert.ToDouble(Video.Get(CapProp.Fps));
        this.DeltaFrameTime = 1000.0 / this.FPS;
        this.CurrentFrameNumber = 0;
        if (!IsWebCam)
        {
            this.TotalFrames = Convert.ToInt64(Video.Get(CapProp.FrameCount));
            this.TotalDuration = Convert.ToInt64(DeltaFrameTime * Convert.ToDouble(Video.Get(CapProp.FrameCount)));
        }
    }

    public void Dispose() => this.Dispose();
}
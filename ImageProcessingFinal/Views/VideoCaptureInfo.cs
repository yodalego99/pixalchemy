using System;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace ImageProcessingFinal.Views;


public class VideoCaptureInfo
{
    private VideoCapture Video {get;}           // Emgu.Cv.VideoCapture object (it can be a camera input or video input)
    private long? TotalFrames { get;}            // Number of frames in the video (estimated from video length and FPS)
    private long? TotalDuration { get;}          // Length of video input in ms (milliseconds)
    private long? CurrentFrameNumber { get; }  // Current number of frame
    private double FPS { get; }                 // FPS (frames per second) of the video
    private double DeltaFrameTime { get; }      // Time elapsed between frames in ms (milliseconds)
    private bool IsWebcam { get; }              // Webcamera input?

    public VideoCaptureInfo(VideoCapture Video, bool IsWebCam)
    {
        this.Video = Video;
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
}
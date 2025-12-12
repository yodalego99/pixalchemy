using System;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace ImageProcessingFinal.Views;

public class VideoCaptureInfo
{
    public VideoCaptureInfo(VideoCapture video, bool isWebCam, string filePath)
    {
        Video = video;
        IsWebcam = isWebCam;
        Fps = Convert.ToInt32(video.Get(CapProp.Fps));
        DeltaFrameTime = 1000.0 / Fps;
        if (isWebCam) return;
        TotalDuration = Convert.ToInt64(
            DeltaFrameTime * Convert.ToDouble(video.Get(CapProp.FrameCount))
        );
        FilePath = filePath;
    }

    public VideoCapture Video { get; set; } // Emgu.Cv.VideoCapture object (it can be a camera input or video input)
    public string? FilePath { get; set; } // Video file path
    public long? TotalDuration { get; set; } // Length of video input in ms (milliseconds)
    public double Fps { get; set; } // FPS (frames per second) of the video
    public double? DeltaFrameTime { get; set; } // Time elapsed between frames in ms (milliseconds)
    private bool? IsWebcam { get; set; } // Webcamera input?
}
using System;

namespace AutoOverlay.AviSynth
{
    public class FrameEventArgs : EventArgs
    {
        public int FrameNumber { get; }

        public FrameEventArgs(int frameNumber)
        {
            FrameNumber = frameNumber;
        }
    }
}

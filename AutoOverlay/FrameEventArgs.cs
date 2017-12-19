using System;

namespace AutoOverlay
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

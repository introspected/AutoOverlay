using System;
using System.Collections.Concurrent;
using System.IO;
using AutoOverlay.AviSynth;
using AvsFilterNet;

namespace AutoOverlay
{
    public abstract class SupportFilter : OverlayFilter
    {
        private static readonly ConcurrentDictionary<Type, Action<SupportFilter, BinaryReader>> Readers = new();
        private static readonly ConcurrentDictionary<Type, Action<SupportFilter, BinaryWriter>> Writers = new();

        public static T FindFilter<T>(Clip clip, int position) where T : SupportFilter
        {
            return (T) FindFilter(typeof(T), clip, position);
        }

        public static SupportFilter FindFilter(Type type, Clip clip, int position)
        {
            using var frame = clip.GetFrame(position, DynamicEnvironment.Env);
            unsafe
            {
                using var stream = new UnmanagedMemoryStream(
                    (byte*)frame.GetReadPtr().ToPointer(),
                    frame.GetRowSize() * frame.GetHeight(),
                    frame.GetRowSize() * frame.GetHeight(),
                    FileAccess.Read);
                using var reader = new BinaryReader(stream);
                if (reader.ReadString() != type.Name) return null;
                return Filters[reader.ReadString()] as SupportFilter;
            }
        }

        protected sealed override VideoFrame GetFrame(int n)
        {
            var frame = base.GetFrame(n);
            StaticEnv.MakeWritable(frame);
            unsafe
            {
                using var stream = new UnmanagedMemoryStream((byte*)frame.GetWritePtr().ToPointer(),
                    frame.GetRowSize(), frame.GetRowSize(), FileAccess.Write);
                using var writer = new BinaryWriter(stream);
                writer.Write(GetType().Name);
                writer.Write(FilterId);
            }
            return frame;
        }
    }
}

using AvsFilterNet;

namespace AutoOverlay
{
    public class ClipProperty<T>(Clip clip, string key, int index = 0, int offset = 0)
    {
        private object value;
        private bool cached;

        public T Value
        {
            get
            {
                if (!cached)
                {
                    value = clip.Dynamic().propGetAny(key, index, offset);
                    cached = true;
                }
                return value is T typed ? typed : default;
            }
        }

        public dynamic WriteIfExists(dynamic clip)
        {
            if (clip is Clip c) 
                clip = c.Dynamic();
            var unboxed = Value.Unbox();
            return unboxed == null ? clip : clip.Dynamic().propSet(key, unboxed);
        }

        public dynamic Write(dynamic clip)
        {
            if (clip is Clip c)
                clip = c.Dynamic();
            var unboxed = Value.Unbox();
            return unboxed == null ? clip.propDelete(key) : clip.Dynamic().propSet(key, unboxed);
        }
    }
}

using System;
using System.Collections.Generic;
using AvsFilterNet;

using PresetMap = System.Collections.Generic.Dictionary<System.Enum, System.Collections.Generic.Dictionary<string, System.Func<AvsFilterNet.AvisynthFilter, object>>>;

namespace AutoOverlay
{
    public sealed class Presets
    {
        private static readonly Dictionary<Tuple<Type, Type>, PresetMap> cache = new();

        public static void Add<TPreset, TFilter>(PresetMap preset)
            where TPreset : Enum
            where TFilter : AvisynthFilter
        {
            cache[Tuple.Create(typeof(TPreset), typeof(TFilter))] = preset;
        }

        public static IDictionary<string, Func<AvisynthFilter, object>> Find(Enum preset, AvisynthFilter filter)
        {
            for (var type = filter.GetType(); type != typeof(object); type = type.BaseType)
            {
                var key = Tuple.Create(preset.GetType(), type);
                if (cache.TryGetValue(key, out var set) && set != null)
                    return set[preset];
            }
            return null;
        }
    }
}

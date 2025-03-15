using System;
using System.Collections;
using System.Collections.Generic;
using AvsFilterNet;

namespace AutoOverlay
{
    public sealed class Presets
    {
        private static readonly Dictionary<Tuple<Type, Type>, Dictionary<Enum, Dictionary<string, Func<AvisynthFilter, object>>>> cache = new();

        public static void Add<TPreset, TFilter>(Dictionary<Enum, Dictionary<string, Func<TFilter, object>>> preset)
            where TPreset : Enum
            where TFilter : AvisynthFilter
        {
            var wrappedPreset = new Dictionary<Enum, Dictionary<string, Func<AvisynthFilter, object>>>();

            foreach (var kvp in preset)
            {
                var innerDict = new Dictionary<string, Func<AvisynthFilter, object>>();
                foreach (var innerKvp in kvp.Value)
                {
                    innerDict[innerKvp.Key] = (filter) => innerKvp.Value((TFilter)filter);
                }
                wrappedPreset[kvp.Key] = innerDict;
            }

            cache[Tuple.Create(typeof(TPreset), typeof(TFilter))] = wrappedPreset;
        }

        public static IDictionary<string, Func<AvisynthFilter, object>> Find(Enum preset, AvisynthFilter filter)
        {
            for (var type = filter.GetType(); type != typeof(OverlayFilter); type = type.BaseType)
            {
                var key = Tuple.Create(preset.GetType(), type);
                if (cache.TryGetValue(key, out var set) && set != null && set.TryGetValue(preset, out var values))
                    return values;
            }
            return null;
        }
    }
}

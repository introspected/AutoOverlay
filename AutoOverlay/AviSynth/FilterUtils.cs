using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using AutoOverlay.Overlay;
using AvsFilterNet;

namespace AutoOverlay.AviSynth
{
    public static class FilterUtils
    {
        public sealed record PropertyMetadata(string Name, Type Type, AvsArgumentAttribute Attribute, MethodInfo Getter, MethodInfo Setter, string[] Values, HashSet<string> LowValues);

        public sealed record FilterMetadata(PropertyMetadata[] Properties, string Name, int ParamCount, MethodInfo[] Clips);

        private static readonly ConcurrentDictionary<Type, FilterMetadata> FilterProperties = new();

        public static void InitArgs(AvisynthFilter filter, AVSValue args)
        {
            var metadata = GetFilterMetadata(filter.GetType());
            var argsCount = args.ArraySize();

            if (metadata.ParamCount != argsCount && metadata.ParamCount != argsCount - 1)
                throw new AvisynthException("Instance attributes count not match to declared");

            var zeroBased = metadata.ParamCount == argsCount;
            var argIndex = zeroBased ? 0 : 1;

            var annotatedPropertiesWithArguments = metadata.Properties.Select(p =>
            {
                AVSValue argument;
                if (p.Type == typeof(Space))
                {
                    argument = new AVSValue(args[argIndex++], args[argIndex++]);
                    if (!argument[0].Defined() && !argument[1].Defined())
                        argument = new AVSValue();
                }
                else
                {
                    argument = args[argIndex++];
                }
                
                return new
                {
                    Argument = argument,
                    Name = p.Name,
                    QualifiedName = $"{metadata.Name}.{p.Name}",
                    Type = p.Type,
                    Defined = argument.Defined(),
                    Attribute = p.Attribute,
                    Setter = p.Setter,
                    Getter = p.Getter,
                    AllowedValues = p.Values,
                    LowValues = p.LowValues,
                };
            }).ToArray();

            foreach (var tuple in annotatedPropertiesWithArguments)
            {
                if (tuple.Attribute.Required && !tuple.Defined)
                    throw new AvisynthException($"{tuple.QualifiedName} is required but not defined");

                dynamic value = tuple.Argument.AsObject(tuple.Type);

                if (value == null)
                {
                    if (tuple.Type.IsArray)
                    {
                        var emptyArray = Array.CreateInstance(tuple.Type.GetElementType(), 0);
                        tuple.Setter.Invoke(filter, [emptyArray]);
                    }
                    continue;
                }

                if (value != null && tuple.Attribute.Unused)
                {
                    throw new AvisynthException($"Parameter {tuple.QualifiedName} not implemented yet");
                }

                var defValue = tuple.Getter.Invoke(filter, null);

                RectangleD ReadRect(Clip clip, int i)
                {
                    using var frame = clip.GetFrame(i, DynamicEnvironment.Env);
                    return Rect.FromFrame(frame, tuple.Attribute.LTRB);
                }

                void InitCollection<T>(Func<int, T> convert)
                {
                    var col = (ICollection<T>)Activator.CreateInstance(tuple.Type);

                    var clip = (Clip)value;
                    var numFrames = clip.GetVideoInfo().num_frames;
                    if (numFrames > tuple.Attribute.Max)
                        throw new AvisynthException($"{tuple.QualifiedName} contains {value} values but limit is {tuple.Attribute.Max}");
                    for (var i = 0; i < numFrames; i++)
                        col.Add(convert(i));
                    tuple.Setter.Invoke(filter, [col]);
                }

                if (value is Clip rect)
                {
                    if (tuple.Type == typeof(Rectangle))
                    {
                        tuple.Setter.Invoke(filter, [(Rectangle)ReadRect(rect, 0)]);
                        continue;
                    }

                    if (tuple.Type == typeof(RectangleD))
                    {
                        tuple.Setter.Invoke(filter, [ReadRect(rect, 0)]);
                        continue;
                    }

                    if (typeof(ICollection<Rectangle>).IsAssignableFrom(tuple.Type))
                    {
                        InitCollection(i => (Rectangle)ReadRect(rect, i));
                        continue;
                    }

                    if (typeof(ICollection<RectangleD>).IsAssignableFrom(tuple.Type))
                    {
                        InitCollection(i => ReadRect(rect, i));
                        continue;
                    }
                }

                if (value is Clip[] rects)
                {
                    if (typeof(ICollection<Rectangle[]>).IsAssignableFrom(tuple.Type))
                    {
                        InitCollection(i => rects.Select(rect => (Rectangle)ReadRect(rect, i)).ToArray());
                        continue;
                    }

                    if (typeof(ICollection<RectangleD[]>).IsAssignableFrom(tuple.Type))
                    {
                        InitCollection(i => rects.Select(rect => ReadRect(rect, i)).ToArray());
                        continue;
                    }
                }

                if (tuple.Type == typeof(Space))
                {
                    var defSpace = (Space)defValue;
                    var space = new Space(
                        tuple.Argument[0].AsFloat(tuple.Argument[1].AsFloat(defSpace.X)),
                        tuple.Argument[1].AsFloat(tuple.Argument[0].AsFloat(defSpace.Y)));
                    ValidateRange(tuple.QualifiedName + "X", tuple.Attribute, space.X);
                    ValidateRange(tuple.QualifiedName + "Y", tuple.Attribute, space.Y);
                    tuple.Setter.Invoke(filter, [space]);
                    continue;
                }

                ValidateRange(tuple.QualifiedName, tuple.Attribute, value);
                if (filter is OverlayFilter overlayFilter && value is Clip supportClip)
                {
                    overlayFilter.Attach(supportClip);
                    if (typeof(SupportFilter).IsAssignableFrom(tuple.Type))
                    {
                        value = SupportFilter.FindFilter(tuple.Type, supportClip, 0);
                        overlayFilter.Attach(supportClip);
                    }

                    if (typeof(SupportFilter[]).IsAssignableFrom(tuple.Type))
                    {
                        var length = supportClip.GetVideoInfo().num_frames;
                        var filterType = tuple.Type.GetElementType();
                        value = Enumerable.Range(0, length)
                            .Select(n => new { n, Filter = SupportFilter.FindFilter(filterType, supportClip, n) })
                            .Aggregate(Array.CreateInstance(filterType, length), (array, pair) =>
                            {
                                array.SetValue(pair.Filter, pair.n);
                                return array;
                            });
                    }
                }

                if (tuple.LowValues.Count > 0)
                {
                    if (!tuple.LowValues.Contains(value.ToString().ToLower()))
                        throw new AvisynthException($"{tuple.QualifiedName} is equal to '{value}' but allowed values are [{string.Join(", ", tuple.AllowedValues)}]");
                }
                if (tuple.Type.IsEnum)
                {
                    if (tuple.Type.IsEnum && value is string)
                        value = Enum.Parse(tuple.Type, value.ToString(), true);
                    else if (tuple.Type.IsEnum && value is int)
                        value = Enum.ToObject(tuple.Type, value);
                    else throw new AvisynthException("Internal filter enum error");
                }

                if (tuple.Attribute.Percent)
                    value /= 100;
                
                tuple.Setter.Invoke(filter, [value]);
            }

            var presets = from tuple in annotatedPropertiesWithArguments
                          where tuple.Type.IsEnum
                          let value = (Enum)tuple.Getter.Invoke(filter, null)
                          where value != null
                          let preset = Presets.Find(value, filter)
                          where preset != null
                          select preset;

            var lazyMap = () => annotatedPropertiesWithArguments.ToDictionary(p => p.Name, p => p);

            foreach (var preset in presets)
            {
                var map = lazyMap();
                lazyMap = () => map;
                foreach (var pair in preset)
                {
                    var tuple = map[pair.Key];
                    if (!tuple.Defined)
                        tuple.Setter.Invoke(filter, [pair.Value(filter)]);
                }
            }

            foreach (var tuple in annotatedPropertiesWithArguments)
            {
                if (tuple.Attribute.NotNull && tuple.Getter.Invoke(filter, null) == null)
                    throw new AvisynthException($"{tuple.QualifiedName} is required but not defined");
            }
        }

        public static FilterMetadata GetFilterMetadata(Type filter) => FilterProperties.GetOrAdd(filter, type =>
        {
            var properties = type.GetProperties(
                    BindingFlags.NonPublic | BindingFlags.Public |
                    BindingFlags.Instance | BindingFlags.GetProperty |
                    BindingFlags.SetProperty)
                .Select(property =>
                {
                    var attribute = (AvsArgumentAttribute)Attribute.GetCustomAttribute(property, typeof(AvsArgumentAttribute));
                    if (attribute == null)
                        return null;
                    string[] values;
                    HashSet<string> lowValues;
                    var propertyType = property.PropertyType;
                    if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                        propertyType = Nullable.GetUnderlyingType(propertyType);
                    if (propertyType.IsEnum)
                    {
                        values = Enum.GetNames(propertyType);
                        lowValues = [];
                        foreach (var name in Enum.GetNames(propertyType))
                            lowValues.Add(name.ToLower());
                        foreach (var value in Enum.GetValues(propertyType).Cast<object>().Select(Convert.ToInt32))
                            lowValues.Add(value.ToString());
                    }
                    else
                    {
                        values = attribute.Values;
                        lowValues = [.. attribute.Values];
                    }
                    var setter = property.GetSetMethod(true);
                    var getter = property.GetGetMethod(true);
                    return new PropertyMetadata(property.Name, propertyType, attribute, getter, setter, values, lowValues);
                })
                .Where(p => p != null).ToArray();

            var paramCount = properties.Sum(p => p.Type == typeof(Space) ? 2 : 1);

            var filterName = type.Assembly
                .GetCustomAttributes(typeof(AvisynthFilterClassAttribute), true)
                .OfType<AvisynthFilterClassAttribute>()
                .FirstOrDefault(p => p.FilterType == type)
                ?.FilterName ?? type.Name;

            var clips = properties
                .Where(p => p.Type == typeof(Clip) || p.Type == typeof(Clip[]))
                .Select(p => p.Getter)
                .ToArray();

            return new FilterMetadata(properties, filterName, paramCount, clips);
        });

        public static void Dispose(AvisynthFilter filter)
        {
            var props = GetFilterMetadata(filter.GetType()).Clips
                .Select(p => p.Invoke(filter, null))
                .Where(p => p != null);
            foreach (var prop in props)
            {
                switch (prop)
                {
                    case Clip clip:
                        clip.Dispose();
                        break;
                    case Clip[] clips:
                        foreach (var itemClip in clips)
                            itemClip.Dispose();
                        break;
                }
            }
        }

        private static void ValidateRange(string propertyName, AvsArgumentAttribute attr, object value)
        {
            if (!attr.Min.Equals(double.MinValue) && Convert.ToDouble(value) < attr.Min)
                throw new AvisynthException($"{propertyName} is equal to {value} but must be greater or equal to {attr.Min}");
            if (!attr.Max.Equals(double.MaxValue) && Convert.ToDouble(value) > attr.Max)
                throw new AvisynthException($"{propertyName} is equal to {value} but must be less or equal to {attr.Max}");
        }
    }
}

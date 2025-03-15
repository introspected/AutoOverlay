﻿using System;
using System.Linq;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AvsFilterNet;
using NUnit.Framework;

namespace AutoOverlayTests
{
    [TestFixture]
    public class FilterDescGenerator
    {
        [Test]
        public void Generate()
        {
            var filters = typeof(OverlayFilter).Assembly
                .GetCustomAttributes(typeof(AvisynthFilterClassAttribute), true)
                .OfType<AvisynthFilterClassAttribute>()
                .Where(p => typeof(OverlayFilter).IsAssignableFrom(p.FilterType));
            foreach (var filter in filters)
            {
                var annotatedProperties = FilterUtils.GetFilterMetadata(filter.FilterType).Properties;
                var paramList = annotatedProperties.Select(p => $"{TypeLabel(p.Property.PropertyType)} {p.Property.Name.ToLower()[0] + p.Property.Name.Substring(1)}");
                var signature = $"{filter.FilterName}({string.Join(", ", paramList)})";
                Console.WriteLine(signature);
                Console.WriteLine();
            }
        }

        private static string TypeLabel(Type type)
        {
            if (type == typeof(int))
                return "int";
            if (type == typeof(double))
                return "float";
            if (type == typeof(bool))
                return "bool";
            if (type.IsEnum)
                return "<ENUM>";
            return type.Name.ToLower();
        }
    }
}

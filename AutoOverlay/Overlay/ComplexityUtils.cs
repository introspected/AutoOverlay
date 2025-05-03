using System;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace AutoOverlay
{
    public static class ComplexityUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe double Byte(byte* data, int x, int y, int pitch, int pixelSize, ref Size size, int stepCount)
        {
            var value = data[x];
            var sum = 0;
            var count = 0;
            for (var step = -stepCount; step <= stepCount; step++)
            {
                var xTest = x + step * pixelSize;
                if (xTest < 0 || xTest >= size.Width)
                    continue;
                var subStepCount = stepCount - Math.Abs(step);
                for (var subStep = -subStepCount; subStep <= subStepCount; subStep++)
                {
                    if (step == 0 && subStep == 0)
                        continue;

                    var yTest = y + subStep;
                    if (yTest >= 0 && yTest < size.Height)
                    {
                        sum += (data + pitch * subStep)[xTest];
                        count++;
                    }
                }
            }

            return Math.Abs(value - (double)sum / count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe double Short(ushort* data, int x, int y, int pitch, int pixelSize, ref Size size, int stepCount)
        {
            var value = data[x];
            var sum = 0;
            var count = 0;
            for (var step = -stepCount; step <= stepCount; step++)
            {
                var xTest = x + step * pixelSize;
                if (xTest < 0 || xTest >= size.Width)
                    continue;
                var subStepCount = stepCount - Math.Abs(step);
                for (var subStep = -subStepCount; subStep <= subStepCount; subStep++)
                {
                    if (step == 0 && subStep == 0)
                        continue;

                    var yTest = y + subStep;
                    if (yTest >= 0 && yTest < size.Height)
                    {
                        sum += (data + pitch * subStep)[xTest];
                        count++;
                    }
                }
            }

            return Math.Abs(value - (double)sum / count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe double Float(float* data, int x, int y, int pitch, int pixelSize, ref Size size, int stepCount)
        {
            var value = data[x];
            var sum = 0d;
            var count = 0;
            for (var step = -stepCount; step <= stepCount; step++)
            {
                var xTest = x + step * pixelSize;
                if (xTest < 0 || xTest >= size.Width)
                    continue;
                var subStepCount = stepCount - Math.Abs(step);
                for (var subStep = -subStepCount; subStep <= subStepCount; subStep++)
                {
                    if (step == 0 && subStep == 0)
                        continue;

                    var yTest = y + subStep;
                    if (yTest >= 0 && yTest < size.Height)
                    {
                        sum += (data + pitch * subStep)[xTest];
                        count++;
                    }
                }
            }

            return Math.Abs(value - sum / count);
        }
    }
}

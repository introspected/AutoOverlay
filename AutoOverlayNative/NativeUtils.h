#ifndef SIMDUTILS_H
#define SIMDUTILS_H

#include <stdlib.h>
using namespace System::Runtime::InteropServices;
using namespace System;
using namespace System::Collections::Generic;

namespace AutoOverlay {
	public ref class NativeUtils sealed {

	public:
		static double SquaredDifferenceSum(
			const unsigned char *src, int srcStride, 
			const unsigned char *over, int overStride, 
			int width, int height);

		static double SquaredDifferenceSumMasked(
			const unsigned char *src, int srcStride,
			const unsigned char *srcMask, int srcMaskStride,
			const unsigned char *over, int overStride,
			const unsigned char *overMask, int overMaskStride,
			int width, int height);

		static array<int>^ Histogram8bit(
			IntPtr image, int stride, int height, int rowSize, int pixelSize);

		static array<int>^ Histogram8bitMasked(
			int width, int height,
			IntPtr image, int imageStride, int imagePixelSize,
			IntPtr mask, int maskStride, int maskPixelSize);

		static void ApplyColorMap(IntPtr image, IntPtr write, int height, int stride, int rowSize, int pixelSize,
			array<unsigned char>^ fixedColors, array<unsigned char, 2>^ dynamicColors, array<double, 2>^ dynamicWeights);
	};
};
#endif
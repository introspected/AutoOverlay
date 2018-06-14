#ifndef SIMDUTILS_H
#define SIMDUTILS_H

#include "Stdafx.h"

using namespace System::Runtime::InteropServices;
using namespace System;
using namespace System::Collections::Generic;

const double EPSILON = std::numeric_limits<double>::epsilon();

namespace AutoOverlay {
	struct ColorMatching { 
		IntPtr input;
		int strideIn;
		IntPtr output;
		int strideOut;
		int inputRowSize;
		int height;
		int pixelSize;
		int channel;
		array<int>^ *fixedColors; 
		array<array<int>^>^ *dynamicColors;
		array<array<double>^>^ *dynamicWeights;
	};

	struct HistogramFilling {
		array<int>^ *histogram;
		int rowSize;
		int height;
		int channel;
		IntPtr image;
		int imageStride;
		int imagePixelSize;
		IntPtr mask;
		int maskStride;
		int maskPixelSize;
	};

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

		static void FillHistogram(
			array<int>^ histogram, int rowSize, int height, int channel,
			IntPtr image, int imageStride, int imagePixelSize,
			IntPtr mask, int maskStride, int maskPixelSize)
		{
			HistogramFilling params = {
				&histogram, rowSize, height, channel,
				image, imageStride, imagePixelSize,
				mask, maskStride, maskPixelSize
			};
			if (histogram->Length == 256)
				NativeUtils::FillHistogram<unsigned char>(params);
			else NativeUtils::FillHistogram<unsigned short>(params);
		}

		template <typename TColor>
		static void FillHistogram(HistogramFilling params)
		{
			params.imageStride /= sizeof(TColor);
			params.rowSize /= sizeof(TColor);
			pin_ptr<int> first = &(*params.histogram)[0];
			int* hist = first;
			TColor* data = reinterpret_cast<TColor*>(params.image.ToPointer()) + params.channel;
			if (params.mask.ToPointer() == NULL)
			{
				for (int y = 0; y < params.height; y++, data += params.imageStride)
					for (int x = 0; x < params.rowSize; x += params.imagePixelSize)
						hist[data[x]]++;
			}
			else 
			{
				unsigned char* maskData = reinterpret_cast<unsigned char*>(params.mask.ToPointer());
				for (int y = 0; y < params.height; y++, data += params.imageStride, maskData += params.maskStride)
					for (int x = 0, xMask = 0; x < params.rowSize; x += params.imagePixelSize, xMask += params.maskPixelSize)
						if (maskData[xMask] > 0)
							hist[data[x]]++;
			}
		}

		static void ApplyColorMap(
			IntPtr input, int strideIn, bool hdrIn, 
			IntPtr output, int strideOut, bool hdrOut,
			int inputRowSize, int height, int pixelSize, int channel,
			array<int>^ fixedColors, array<array<int>^>^ dynamicColors, array<array<double>^>^ dynamicWeights)
		{
			ColorMatching params = {
				input, strideIn, output, strideOut, 
				inputRowSize, height, pixelSize, channel,
				&fixedColors, &dynamicColors, &dynamicWeights
			};
			if (!hdrIn && !hdrOut)
				NativeUtils::ApplyColorMap<unsigned char, unsigned char>(params);
			else if (hdrIn && !hdrOut)
				NativeUtils::ApplyColorMap<unsigned short, unsigned char>(params);
			else if(!hdrIn && hdrOut)
				NativeUtils::ApplyColorMap<unsigned char, unsigned short>(params);
			else
				NativeUtils::ApplyColorMap<unsigned short, unsigned short>(params);
		}

		template <typename TInputColor, typename TOutputColor>
			static void ApplyColorMap(ColorMatching params)
			{
				TInputColor* readData = reinterpret_cast<TInputColor*>(params.input.ToPointer()) + params.channel;
				TOutputColor* writeData = reinterpret_cast<TOutputColor*>(params.output.ToPointer()) + params.channel;
				XorshiftRandom^ rand = gcnew XorshiftRandom();
				params.strideIn /= sizeof(TInputColor);
				params.strideOut /= sizeof(TOutputColor);
				params.inputRowSize /= sizeof(TInputColor);

				for (int y = 0; y < params.height; y++, readData += params.strideIn, writeData += params.strideOut)
					for (int x = 0; x < params.inputRowSize; x += params.pixelSize)
					{
						TInputColor oldColor = readData[x];
						int newColor = (*params.fixedColors)[oldColor];
						if (newColor == -1)
						{
							double weight = rand->NextDouble();
							auto colors = (*params.dynamicColors)[oldColor];
							auto weights = (*params.dynamicWeights)[oldColor];
							for (int i = 0;; i++)
								if ((weight -= weights[i]) < EPSILON)
								{
									writeData[x] = colors[i];
									break;
								}
						}
						else
						{
							writeData[x] = newColor;
						}
					}
			}

		static void BilinearRotate(
			IntPtr srcImage, int srcWidth, int srcHeight, int srcStride,
			IntPtr dstImage, int dstWidth, int dstHeight, int dstStride,
			double angle, int pixelSize);
	};
};
#endif
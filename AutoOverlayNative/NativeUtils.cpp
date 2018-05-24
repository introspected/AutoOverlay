#include "Stdafx.h"
#include <iostream>

namespace AutoOverlay {

	const double EPSILON = std::numeric_limits<double>::epsilon();

	double NativeUtils::SquaredDifferenceSum(
		const unsigned char *src, int srcStride,
		const unsigned char *over, int overStride,
		int width, int height)
	{
		__int64 sum = 0;
		for (int row = 0; row < height; ++row)
		{
			for (int col = 0; col < width; ++col)
			{
				int diff = src[col] - over[col];
				int square = diff * diff;
				sum += square;
			}
			src += srcStride;
			over += overStride;
		}
		return (double)sum / (width*height);
	}

	double NativeUtils::SquaredDifferenceSumMasked(
		const unsigned char *src, int srcStride,
		const unsigned char *srcMask, int srcMaskStride,
		const unsigned char *over, int overStride,
		const unsigned char *overMask, int overMaskStride,
		int width, int height)
	{
		__int64 sum = 0;
		int pixelCount = width * height;
		for (int row = 0; row < height; ++row)
		{
			for (int col = 0; col < width; ++col)
			{
				if ((srcMask == nullptr || srcMask[col] > 0) 
					&& (overMask == nullptr || overMask[col] > 0)) 
				{
					int diff = src[col] - over[col];
					int square = diff * diff;
					sum += square;
				} 
				else
				{
					pixelCount--;
				}
			}
			src += srcStride;
			over += overStride;
			if (srcMask != nullptr)
				srcMask += srcMaskStride;
			if (overMask != nullptr)
				overMask += overMaskStride;
		}
		return (double)sum / pixelCount;
	}

	array<int>^ NativeUtils::Histogram8bit(
		IntPtr image, int stride, int height, int rowSize, int pixelSize)
	{
		array<int>^ histogram = gcnew array<int>(256);
		pin_ptr<int> first = &histogram[0];
		int* hist = first;
		unsigned char* data = reinterpret_cast<unsigned char*>(image.ToPointer());
		for (int y = 0; y < height; y++, data += stride)
			for (int x = 0; x < rowSize; x += pixelSize)
				hist[data[x]]++;
		return histogram;
	}

	array<int>^ NativeUtils::Histogram8bitMasked(
		int width, int height,
		IntPtr image, int imageStride, int imagePixelSize,
		IntPtr mask, int maskStride, int maskPixelSize)
	{		
		array<int>^ histogram = gcnew array<int>(256);
		pin_ptr<int> first = &histogram[0];
		int* hist = first;
		int imageRowSize = width * imagePixelSize;
		unsigned char* data = reinterpret_cast<unsigned char*>(image.ToPointer());
		unsigned char* maskData = reinterpret_cast<unsigned char*>(mask.ToPointer());
		for (int y = 0; y < height; y++, data += imageStride, maskData += maskStride)
			for (int x = 0, xMask = 0; x < imageRowSize; x += imagePixelSize, xMask += maskPixelSize)
				if (maskData[xMask] > 0)
					hist[data[x]]++;
		return histogram;
	}

	void NativeUtils::ApplyColorMap(IntPtr read, IntPtr write, int height, int stride, int rowSize, int pixelSize,
		array<unsigned char>^ fixedColors, array<unsigned char, 2>^ dynamicColors, array<double, 2>^ dynamicWeights)
	{
		unsigned char* readData = reinterpret_cast<unsigned char*>(read.ToPointer());
		unsigned char* writeData = reinterpret_cast<unsigned char*>(write.ToPointer());
		pin_ptr<unsigned char> firstFixedColor = &fixedColors[0];
		unsigned char* fixedColor = firstFixedColor;
		pin_ptr<unsigned char> firstDynamicColor = &dynamicColors[0, 0];
		unsigned char* dynamicColor = firstDynamicColor;
		pin_ptr<double> firstDynamicWeight = &dynamicWeights[0,0];
		double* dynamicWeight = firstDynamicWeight;		
		XorshiftRandom^ rand = gcnew XorshiftRandom();
		for (int y = 0; y < height; y++, readData += stride, writeData += stride)
			for (int x = 0; x < rowSize; x += pixelSize)
			{
				unsigned char oldColor = readData[x];
				unsigned char newColor = fixedColor[oldColor];
				if (newColor == 0)
				{
					double weight = rand->NextDouble();
					for (int offset = oldColor << 8;; offset++)
						if ((weight -= dynamicWeight[offset]) < EPSILON)
						{
							newColor = dynamicColor[offset];
							break;
						}
				}
				writeData[x] = newColor;
			}
	}
}

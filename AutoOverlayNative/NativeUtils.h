#pragma once
#include <Simd/SimdLib.h>
using namespace System;
using namespace Collections::Generic;
using namespace Runtime::InteropServices;

const double EPSILON = 0.000000001;

namespace AutoOverlay
{
	struct ColorMatching
	{
		int frameNumber;
		IntPtr input;
		int strideIn;
		IntPtr output;
		int strideOut;
		int inputRowSize;
		int height;
		int pixelSize;
		int channel;
		array<int>^ * fixedColors;
		array<array<int>^>^ * dynamicColors;
		array<array<double>^>^ * dynamicWeights;
	};

	struct HistogramFilling
	{
		array<uint32_t>^ * histogram;
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

	struct SquaredDiffParams
	{
		IntPtr src;
		int srcStride;
		IntPtr srcMask;
		int srcMaskStride;
		IntPtr over;
		int overStride;
		IntPtr overMask;
		int overMaskStride;
		int width;
		int height;
		bool simd;
	};

	public ref class NativeUtils sealed
	{
	public:
		static double SquaredDifferenceSum(
			IntPtr src, int srcStride,
			IntPtr srcMask, int srcMaskStride,
			IntPtr over, int overStride,
			IntPtr overMask, int overMaskStride,
			int width, int height, int depth, bool simd)
		{
			SquaredDiffParams params = {
				src, srcStride, srcMask, srcMaskStride,
				over, overStride, overMask, overMaskStride,
				width, height, simd
			};
			switch(depth)
			{
				case 8: return SquaredDifferenceSumImpl<unsigned char>(params);
				case 10: return SquaredDifferenceSumImpl<unsigned short>(params);
				case 12: return SquaredDifferenceSumImpl<unsigned short>(params);
				case 14: return SquaredDifferenceSumImpl<unsigned short>(params);
				case 16: return SquaredDifferenceSumImpl<unsigned short>(params);
				default: throw gcnew InvalidOperationException();
			}
		}

		template <typename TColor>
		static double SquaredDifferenceSumImpl(SquaredDiffParams params)
		{
			uint64_t sum = 0;
			TColor* src = reinterpret_cast<TColor*>(params.src.ToPointer());
			TColor* srcMask = reinterpret_cast<TColor*>(params.srcMask.ToPointer());
			TColor* over = reinterpret_cast<TColor*>(params.over.ToPointer());
			TColor* overMask = reinterpret_cast<TColor*>(params.overMask.ToPointer());
			params.srcStride /= sizeof(TColor);
			params.srcMaskStride /= sizeof(TColor);
			params.overStride /= sizeof(TColor);
			params.overMaskStride /= sizeof(TColor);
			params.width /= sizeof(TColor);

			int pixelCount = params.width * params.height;
			TColor max = (1 << sizeof(TColor) * 8) - 1;
			
			if (params.simd)
			{
				if (typeid(TColor) == typeid(unsigned char))
				{
					const uint8_t* srcp = reinterpret_cast<uint8_t*>(params.src.ToPointer());
					const uint8_t* overp = reinterpret_cast<uint8_t*>(params.over.ToPointer());
					bool hasSrcMask = params.srcMask.ToPointer() != nullptr;
					bool hasOverMask = params.overMask.ToPointer() != nullptr;
					if (!hasSrcMask && !hasOverMask)
					{
						SimdSquaredDifferenceSum(
							srcp,
							params.srcStride,
							overp,
							params.overStride,
							params.width,
							params.height,
							&sum);
						return static_cast<double>(sum) / pixelCount;
					}
					if (hasSrcMask != hasOverMask)
					{
						const uint8_t* maskp = reinterpret_cast<uint8_t*>(
							(hasSrcMask ? params.srcMask : params.overMask).ToPointer());
						SimdSquaredDifferenceSumMasked(
							srcp,
							params.srcStride,
							overp,
							params.overStride,
							maskp,
							hasSrcMask ? params.srcMaskStride : params.overMaskStride,
							max,
							params.width,
							params.height,
							&sum);
						return static_cast<double>(sum) / pixelCount;
					}
				}
			}
			if (srcMask == nullptr && overMask == nullptr) {
				for (auto row = 0; row < params.height; ++row)
				{
					for (auto col = 0; col < params.width; ++col)
					{
						auto diff = src[col] - over[col];
						auto square = diff * diff;
						sum += square;
					}
					src += params.srcStride;
					over += params.overStride;
				}
			} 
			else
			{
				for (int row = 0; row < params.height; ++row)
				{
					for (int col = 0; col < params.width; ++col)
					{
						if ((srcMask == nullptr || srcMask[col] == max)
							&& (overMask == nullptr || overMask[col] == max))
						{
							auto diff = src[col] - over[col];
							auto square = diff * diff;
							sum += square;
						}
						else
						{
							pixelCount--;
						}
					}
					src += params.srcStride;
					over += params.overStride;
					if (srcMask != nullptr)
						srcMask += params.srcMaskStride;
					if (overMask != nullptr)
						overMask += params.overMaskStride;
				}
			}
			return static_cast<double>(sum) / pixelCount;
		}

		static void SecondDerivativeHistogram(array<uint32_t>^ histogram, int width, int height, int stride, IntPtr image)
		{
			const pin_ptr<uint32_t> first = &(histogram)[0];
			uint32_t* hist = first;
			const auto ptr = reinterpret_cast<unsigned char*>(image.ToPointer());
			SimdAbsSecondDerivativeHistogram(ptr, width, height, stride, 1, 0, hist);
		}

		static void FillHistogram(
			array<uint32_t>^ histogram, int rowSize, int height, int channel,
			IntPtr image, int imageStride, int imagePixelSize,
			IntPtr mask, int maskStride, int maskPixelSize, bool simd)
		{
			HistogramFilling params = {
				&histogram, rowSize, height, channel,
				image, imageStride, imagePixelSize,
				mask, maskStride, maskPixelSize
			};
			if (simd && histogram->Length == 1 << 8 && params.imagePixelSize == 1)
			{
				const auto ptr = reinterpret_cast<unsigned char*>(params.image.ToPointer()) + params.channel;
				const pin_ptr<uint32_t> first = &(*params.histogram)[0];
				uint32_t* hist = first;
				if (params.mask.ToPointer() == nullptr)
				{
					SimdHistogram(ptr, params.rowSize / params.imagePixelSize, params.height, params.imageStride, hist);
				}
				else if (params.maskPixelSize == params.imagePixelSize)
				{
					const auto maskPtr = reinterpret_cast<unsigned char*>(params.mask.ToPointer()) + params.channel;
					SimdHistogramMasked(ptr, params.imageStride, params.rowSize / params.imagePixelSize, params.height, maskPtr, params.maskStride, 255, hist);
				}
				else
				{
					FillHistogramImpl<unsigned char>(params);
				}
			}
			else if (histogram->Length == 1 << 8) 
			{
				FillHistogramImpl<unsigned char>(params);
			}
			else
			{
				FillHistogramImpl<unsigned short>(params);
			}
		}

		template <typename TColor>
		static void FillHistogramImpl(HistogramFilling params)
		{
			params.imageStride /= sizeof(TColor);
			params.rowSize /= sizeof(TColor);
			pin_ptr<uint32_t> first = &(*params.histogram)[0];
			uint32_t* hist = first;
			TColor* data = reinterpret_cast<TColor*>(params.image.ToPointer()) + params.channel;
			if (params.mask.ToPointer() == nullptr)
			{
				for (int y = 0; y < params.height; y++, data += params.imageStride)
					for (int x = 0; x < params.rowSize; x += params.imagePixelSize)
						++hist[data[x]];
			}
			else
			{
				unsigned char* maskData = reinterpret_cast<unsigned char*>(params.mask.ToPointer());
				for (int y = 0; y < params.height; y++, data += params.imageStride, maskData += params.maskStride)
					for (int x = 0, xMask = 0; x < params.rowSize; x += params.imagePixelSize, xMask += params.maskPixelSize)
						if (maskData[xMask] == 255)
							++hist[data[x]];
			}
		}

		static void ApplyColorMap(int n,
			IntPtr input, int strideIn, bool hdrIn,
			IntPtr output, int strideOut, bool hdrOut,
			int inputRowSize, int height, int pixelSize, int channel,
			array<int>^ fixedColors, array<array<int>^>^ dynamicColors, array<array<double>^>^ dynamicWeights)
		{
			ColorMatching params = {
				n, input, strideIn, output, strideOut,
				inputRowSize, height, pixelSize, channel,
				&fixedColors, &dynamicColors, &dynamicWeights
			};
			if (!hdrIn && !hdrOut)
				ApplyColorMapImpl<unsigned char, unsigned char>(params);
			else if (hdrIn && !hdrOut)
				ApplyColorMapImpl<unsigned short, unsigned char>(params);
			else if (!hdrIn && hdrOut)
				ApplyColorMapImpl<unsigned char, unsigned short>(params);
			else
				ApplyColorMapImpl<unsigned short, unsigned short>(params);
		}

		template <typename TInputColor, typename TOutputColor>
		static void ApplyColorMapImpl(ColorMatching params)
		{
			TInputColor* readData = reinterpret_cast<TInputColor*>(params.input.ToPointer()) + params.channel;
			TOutputColor* writeData = reinterpret_cast<TOutputColor*>(params.output.ToPointer()) + params.channel;
			FastRandom^ rand = gcnew FastRandom(params.frameNumber);
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

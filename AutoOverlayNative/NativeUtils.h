#pragma once
#include <Simd/SimdLib.h>
#include <unordered_map>
#include <numbers>
#include <random>
#include <unordered_set>

using namespace System;
using namespace Collections::Generic;
using namespace Runtime::InteropServices;
using namespace MathNet::Numerics::Interpolation;

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
			TColor* src = static_cast<TColor*>(params.src.ToPointer());
			TColor* srcMask = static_cast<TColor*>(params.srcMask.ToPointer());
			TColor* over = static_cast<TColor*>(params.over.ToPointer());
			TColor* overMask = static_cast<TColor*>(params.overMask.ToPointer());
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
					const uint8_t* srcp = static_cast<uint8_t*>(params.src.ToPointer());
					const uint8_t* overp = static_cast<uint8_t*>(params.over.ToPointer());
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
						const uint8_t* maskp = static_cast<uint8_t*>(
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
				const auto ptr = static_cast<unsigned char*>(params.image.ToPointer()) + params.channel;
				const pin_ptr<uint32_t> first = &(*params.histogram)[0];
				uint32_t* hist = first;
				if (params.mask.ToPointer() == nullptr)
				{
					SimdHistogram(ptr, params.rowSize / params.imagePixelSize, params.height, params.imageStride, hist);
				}
				else if (params.maskPixelSize == params.imagePixelSize)
				{
					const auto maskPtr = static_cast<unsigned char*>(params.mask.ToPointer()) + params.channel;
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
			TColor* data = static_cast<TColor*>(params.image.ToPointer()) + params.channel;
			if (params.mask.ToPointer() == nullptr)
			{
				for (int y = 0; y < params.height; y++, data += params.imageStride)
					for (int x = 0; x < params.rowSize; x += params.imagePixelSize)
						++hist[data[x]];
			}
			else
			{
				unsigned char* maskData = static_cast<unsigned char*>(params.mask.ToPointer());
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
		static void ApplyColorMapImpl(ColorMatching& params)
		{
			TInputColor* readData = static_cast<TInputColor*>(params.input.ToPointer()) + params.channel;
			TOutputColor* writeData = static_cast<TOutputColor*>(params.output.ToPointer()) + params.channel;
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

		static Tuple<double, double>^ FindMinMax(FramePlane^ framePlane)
		{
			switch (framePlane->byteDepth)
			{
			case 1:
				if (framePlane->pixelSize == 1) 
				{
					uint8_t min, max, avg;
					SimdGetStatistic(static_cast<const uint8_t*>(framePlane->pointer), framePlane->stride, framePlane->width, framePlane->height, &min, &max, &avg);
					return gcnew Tuple<double, double>(min, max);
				}
				return FindMinMax<unsigned char>(*framePlane);
			case 2:
				return FindMinMax<unsigned short>(*framePlane);
			case 4:
				return FindMinMax<float>(*framePlane);
			default:
				throw gcnew InvalidOperationException();
			}
		}

		template <typename T> static Tuple<double, double>^ FindMinMax(FramePlane^ framePlane)
		{
			auto data = static_cast<T*>(framePlane->pointer);
			T min = std::numeric_limits<T>().max(), max = std::numeric_limits<T>().min();
			const auto width = framePlane->width;
			const auto pixelSize = framePlane->pixelSize;
			const auto height = framePlane->height;
			const auto stride = framePlane->stride;
			for (auto y = 0; y < height; y++, data += stride)
			{
				for (auto x = 0; x < width; x += pixelSize)
				{
					T value = data[x];
					if (value < min) min = value;
					if (value > max) max = value;
				}
			}
			return gcnew Tuple<double, double>(min, max);
		}

		static int FindColorCount(FramePlane^ framePlane)
		{
			switch (framePlane->byteDepth)
			{
			case 1:
				return FindColorCount<unsigned char>(*framePlane);
			case 2:
				return FindColorCount<unsigned short>(*framePlane);
			case 4:
				return FindColorCount<float>(*framePlane);
			default:
				throw gcnew InvalidOperationException();
			}
		}

		template <typename T> static int FindColorCount(FramePlane^ framePlane)
		{
			auto data = static_cast<T*>(framePlane->pointer);
			const auto width = framePlane->width;
			const auto pixelSize = framePlane->pixelSize;
			const auto height = framePlane->height;
			const auto stride = framePlane->stride;
			std::unordered_set<T> set;
			set.reserve(100000);
			for (auto y = 0; y < height; y++, data += stride)
			{
				for (auto x = 0; x < width; x += pixelSize)
				{
					set.insert(data[x]);
				}
			}
			return set.size();
		}

		static int FillHistogram(FramePlane^ framePlane, Nullable<FramePlane> maskPlane, array<double>^ values, const double offset, const double step)
		{
			const pin_ptr<double> valuesPtr = &values[0];
			switch (framePlane->byteDepth)
			{
			case 1:
				if (framePlane->pixelSize == 1) {
					auto ints = new uint32_t[256];
					if (maskPlane.HasValue)
					{
						SimdHistogramMasked(static_cast<const uint8_t*>(framePlane->pointer), framePlane->stride, framePlane->width, framePlane->height,
						                    static_cast<const uint8_t*>(maskPlane.Value.pointer), maskPlane.Value.stride, 255, ints);
					}
					else
					{
						SimdHistogram(static_cast<const uint8_t*>(framePlane->pointer), framePlane->width, framePlane->height, framePlane->stride, ints);
					}
					int total = 0;
					for (auto color = 0; color < 256; color++)
					{
						const auto count = ints[color];
						if (count == 0) continue;
						const auto realColor = (color - offset) / step;
						const auto minColor = static_cast<unsigned int>(std::floor(realColor));
						const auto diff = realColor - minColor;
						values[minColor] += (1 - diff) * count;
						if (diff > 0)
							values[minColor + 1] += diff * count;
						total += count;
					}
					delete ints;
					return total;
				}
				return FillHistogram<unsigned char>(framePlane, maskPlane, valuesPtr, offset, step);
			case 2:
				return FillHistogram<unsigned short>(framePlane, maskPlane, valuesPtr, offset, step);
			case 4:
				return FillHistogram<float>(framePlane, maskPlane, valuesPtr, offset, step);
			default:
				throw gcnew InvalidOperationException();
			}
		}

		template <typename T> static int FillHistogram(FramePlane^ framePlane, Nullable<FramePlane> maskPlane, double* values, const double offset, const double step)
		{
			auto data = static_cast<T*>(framePlane->pointer);
			const auto width = framePlane->width;
			const auto pixelSize = framePlane->pixelSize;
			const auto height = framePlane->height;
			const auto stride = framePlane->stride;
			const auto max = std::numeric_limits<T>().max();
			int total = width * height;
			if (std::abs(step - 1) < EPSILON && offset == 0)
			{
				if (maskPlane.HasValue) {
					auto mask = static_cast<T*>(maskPlane.Value.pointer);
					const auto maskStride = maskPlane.Value.stride;
					for (auto y = 0; y < height; y++, data += stride, mask += maskStride)
					{
						for (auto x = 0; x < width; x += pixelSize)
						{
							if (mask[x] != max)
							{
								total--;
								continue;
							}
							const double realColor = data[x];
							const auto minColor = static_cast<unsigned int>(std::floor(realColor));
							const auto diff = realColor - minColor;
							values[minColor] += 1 - diff;
							if (diff > 0)
								values[minColor + 1] += diff;
						}
					}
				}
				else
				{
					for (auto y = 0; y < height; y++, data += stride)
					{
						for (auto x = 0; x < width; x += pixelSize)
						{
							const double realColor = data[x];
							const auto minColor = static_cast<unsigned int>(std::floor(realColor));
							const auto diff = realColor - minColor;
							values[minColor] += 1 - diff;
							if (diff > 0)
								values[minColor + 1] += diff;
						}
					}
				}
			}
			else
			{
				if (maskPlane.HasValue) {
					auto mask = static_cast<T*>(maskPlane.Value.pointer);
					const auto maskStride = maskPlane.Value.stride;
					for (auto y = 0; y < height; y++, data += stride, mask += maskStride)
					{
						for (auto x = 0; x < width; x += pixelSize)
						{
							if (mask[x] != max)
							{
								total--;
								continue;
							}
							const auto realColor = (data[x] - offset) / step;
							const auto minColor = static_cast<unsigned int>(std::floor(realColor));
							const auto diff = realColor - minColor;
							values[minColor] += 1 - diff;
							if (diff > 0)
								values[minColor + 1] += diff;
						}
					}
				}
				else
				{
					for (auto y = 0; y < height; y++, data += stride)
					{
						for (auto x = 0; x < width; x += pixelSize)
						{
							const auto realColor = (data[x] - offset) / step;
							const auto minColor = static_cast<unsigned int>(std::floor(realColor));
							const auto diff = realColor - minColor;
							values[minColor] += 1 - diff;
							if (diff > 0)
								values[minColor + 1] += diff;
						}
					}
				}
			}
			return total;
		}

		static void ApplyHistogram(FramePlane^ input, FramePlane^ output, IInterpolation^ averageInterpolator, double min, double max, Nullable<int> seed)
		{
			switch (input->byteDepth)
			{
			case 1:
				switch (output->byteDepth)
				{
				case 1:
					ApplyHistogram<unsigned char, unsigned char>(input, output, averageInterpolator, min, max, seed);
					break;
				case 2:
					ApplyHistogram<unsigned char, unsigned short>(input, output, averageInterpolator, min, max, seed);
					break;
				case 4:
					ApplyHistogram<unsigned char, float>(input, output, averageInterpolator, min, max, seed);
					break;
				default:
					throw gcnew InvalidOperationException();
				}
				break;
			case 2:
				switch (output->byteDepth)
				{
				case 1:
					ApplyHistogram<unsigned short, unsigned char>(input, output, averageInterpolator, min, max, seed);
					break;
				case 2:
					ApplyHistogram<unsigned short, unsigned short>(input, output, averageInterpolator, min, max, seed);
					break;
				case 4:
					ApplyHistogram<unsigned short, float>(input, output, averageInterpolator, min, max, seed);
					break;
				default:
					throw gcnew InvalidOperationException();
				}
				break;
			case 4:
				switch (output->byteDepth)
				{
				case 1:
					ApplyHistogram<float, unsigned char>(input, output, averageInterpolator, min, max, seed);
					break;
				case 2:
					ApplyHistogram<float, unsigned short>(input, output, averageInterpolator, min, max, seed);
					break;
				case 4:
					ApplyHistogram<float, float>(input, output, averageInterpolator, min, max, seed);
					break;
				default:
					throw gcnew InvalidOperationException();
				}
				break;
			default:
				throw gcnew InvalidOperationException();
			}
		}

		template <typename TInput, typename TOutput> static void ApplyHistogram(
			FramePlane^ input, FramePlane^ output, IInterpolation^ averageInterpolator, 
			double min, double max, Nullable<int> seed)
		{
			const auto random = seed.HasValue ? gcnew FastRandom(seed.Value) : gcnew FastRandom();
			auto inData = static_cast<TInput*>(input->pointer);
			auto outData = static_cast<TOutput*>(output->pointer);
			const auto width = input->width;
			const auto pixelSize = input->pixelSize;
			const auto height = input->height;
			const auto inStride = input->stride;
			const auto outStride = output->stride;
			std::unordered_map<TInput, TOutput> cache;
			cache.reserve(10000);
			if (output->byteDepth == 4)
			{
				for (auto y = 0; y < height; y++, inData += inStride, outData += outStride)
				{
					for (auto x = 0; x < width; x += pixelSize)
					{
						TInput& src = inData[x];
						auto iter = cache.find(src);
						if (iter == cache.end())
						{
							outData[x] = cache[src] = static_cast<TOutput>(Interpolate(src, min, max, averageInterpolator));
						}
						else
						{
							outData[x] = iter->second;
						}
					}
				}
			}
			else
			{
				for (auto y = 0; y < height; y++, inData += inStride, outData += outStride)
				{
					for (auto x = 0; x < width; x += pixelSize)
					{
						TInput& src = inData[x];
						auto iter = cache.find(src);
						if (iter == cache.end())
						{
							outData[x] = cache[src] = static_cast<TOutput>(InterpolateInteger(src, min, max, averageInterpolator, random));
						}
						else
						{
							outData[x] = iter->second;
						}
					}
				}
			}
		}

		static double InterpolateInteger(const double color, const double min, const double max, IInterpolation^ interpolator, FastRandom^ random)
		{
			auto interpolated = interpolator->Interpolate(color);
			const auto floor = std::floor(interpolated);
			const auto diff = interpolated - floor;
			if (diff < EPSILON)
				return floor;
			interpolated = random->NextDouble() < diff ? floor : floor + 1;
			if (interpolated < min)
				return min;
			if (interpolated > max)
				return max;
			return interpolated;
		}

		static double Interpolate(const double color, const double min, const double max, IInterpolation^ interpolator)
		{
			const auto interpolated = interpolator->Interpolate(color);
			if (interpolated < min)
				return min;
			if (interpolated > max)
				return max;
			return interpolated;
		}

		static double NextNormal(FastRandom^ random)
		{
			double u1 = random->NextDouble();
			double u2 = random->NextDouble();
			return std::sqrt(-2.0 * std::log(u1)) * std::sin(2.0 * std::numbers::pi * u2) / std::numbers::pi;
		}
	};
};

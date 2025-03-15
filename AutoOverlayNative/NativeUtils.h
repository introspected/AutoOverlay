#pragma once
#include <Simd/SimdLib.h>
#include <random>
#include <unordered_set>

#include "ColorTable.h"
#include "FramePlane.h"

using namespace System;
using namespace System::Drawing;
using namespace Collections::Generic;
using namespace Runtime::InteropServices;

namespace AutoOverlay
{
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
			TColor max = sizeof(TColor) == 4 ? 1 : (1 << sizeof(TColor) * 8) - 1;
			
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
			const auto ptr = static_cast<unsigned char*>(image.ToPointer());
			SimdAbsSecondDerivativeHistogram(ptr, width, height, stride, 1, 0, hist);
		}

		static void FillHistogram(array<uint32_t>^ histogram, FramePlane frame, FramePlane^ mask, bool simd)
		{
			const auto ptr = static_cast<unsigned char*>(frame.pointer);
			const pin_ptr<uint32_t> first = &histogram[0];
			uint32_t* hist = first;
			if (simd && histogram->Length == 1 << 8 && frame.pixelSize == 1)
			{
				if (mask == nullptr)
				{
					SimdHistogram(ptr, frame.row, frame.height, frame.stride, hist);
				}
				else if (frame.pixelSize == mask->pixelSize)
				{
					const auto maskPtr = static_cast<unsigned char*>(mask->pointer);
					SimdHistogramMasked(ptr, frame.stride, frame.row, frame.height, maskPtr, mask->stride, 255, hist);
				}
				else
				{
					FillHistogramImpl<unsigned char>(hist, frame, mask);
				}
			}
			else if (histogram->Length == 1 << 8) 
			{
				FillHistogramImpl<unsigned char>(hist, frame, mask);
			}
			else
			{
				FillHistogramImpl<unsigned short>(hist, frame, mask);
			}
		}

		template <typename TColor>
		static void FillHistogramImpl(uint32_t* histogram, const FramePlane& frame, const FramePlane^ mask)
		{
			const pin_ptr<uint32_t> first = &histogram[0];
			uint32_t* hist = first;
			TColor* data = static_cast<TColor*>(frame.pointer);
			if (mask == nullptr)
			{
				for (int y = 0; y < frame.height; y++, data += frame.stride)
					for (int x = 0; x < frame.row; x += frame.pixelSize)
						++hist[data[x]];
			}
			else
			{
				unsigned char* maskData = static_cast<unsigned char*>(mask->pointer);
				for (int y = 0; y < frame.height; y++, data += frame.stride, maskData += mask->stride)
					for (int x = 0, xMask = 0; x < frame.row; x += frame.pixelSize, xMask += mask->pixelSize)
						if (maskData[xMask] == 255)
							++hist[data[x]];
			}
		}

		static void ApplyColorMap(int n, FramePlane input, FramePlane output, ColorTable^ colorTable)
		{
			auto hdrIn = input.byteDepth > 1;
			auto hdrOut = output.byteDepth > 1;
			if (!hdrIn && !hdrOut)
				ApplyColorMapImpl<unsigned char, unsigned char>(n, input, output, colorTable);
			else if (hdrIn && !hdrOut)
				ApplyColorMapImpl<unsigned short, unsigned char>(n, input, output, colorTable);
			else if (!hdrIn && hdrOut)
				ApplyColorMapImpl<unsigned char, unsigned short>(n, input, output, colorTable);
			else
				ApplyColorMapImpl<unsigned short, unsigned short>(n, input, output, colorTable);
		}

		static void ApplyGradientColorMap(int n, FramePlane input, FramePlane output, ColorTable^ tl, ColorTable^ tr, ColorTable^ br, ColorTable^ bl)
		{
			auto hdrIn = input.byteDepth > 1;
			auto hdrOut = output.byteDepth > 1;
			if (!hdrIn && !hdrOut)
				ApplyGradientColorMapImpl<unsigned char, unsigned char>(n, input, output, tl, tr, br, bl);
			else if (hdrIn && !hdrOut)
				ApplyGradientColorMapImpl<unsigned short, unsigned char>(n, input, output, tl, tr, br, bl);
			else if (!hdrIn && hdrOut)
				ApplyGradientColorMapImpl<unsigned char, unsigned short>(n, input, output, tl, tr, br, bl);
			else
				ApplyGradientColorMapImpl<unsigned short, unsigned short>(n, input, output, tl, tr, br, bl);
		}

		template <typename TInputColor, typename TOutputColor>
		static void ApplyColorMapImpl(int seed, FramePlane input, FramePlane output, ColorTable^ colorTable)
		{
			TInputColor* readData = static_cast<TInputColor*>(input.pointer);
			TOutputColor* writeData = static_cast<TOutputColor*>(output.pointer);

			const auto random = new NativeRandom(seed);
			for (int y = 0; y < input.height; y++, readData += input.stride, writeData += output.stride)
				for (int x = 0; x < input.row; x += input.pixelSize)
				{
					writeData[x] = colorTable->Map<TInputColor, TOutputColor>(readData[x], random);
				}
			delete random;
		}

		template <typename TInputColor, typename TOutputColor>
		static void ApplyGradientColorMapImpl(int seed, FramePlane input, FramePlane output, ColorTable^ tl, ColorTable^ tr, ColorTable^ br, ColorTable^ bl)
		{
			TInputColor* readData = static_cast<TInputColor*>(input.pointer);
			TOutputColor* writeData = static_cast<TOutputColor*>(output.pointer);

			const auto random = new NativeRandom(seed);
			const auto pixelSize = input.pixelSize;
			const auto width = input.row;
			const auto width1 = width - 1.0;
			for (int y = 0; y < input.height; y++, readData += input.stride, writeData += output.stride) 
			{
				auto yRatio = y / (input.height - 1.0);
				for (int x = 0; x < width; x += pixelSize)
				{
					TInputColor oldColor = readData[x];

					auto tlColor = tl->Map<TInputColor, TOutputColor>(oldColor, random);
					auto trColor = tr->Map<TInputColor, TOutputColor>(oldColor, random);
					auto brColor = br->Map<TInputColor, TOutputColor>(oldColor, random);
					auto blColor = bl->Map<TInputColor, TOutputColor>(oldColor, random);

					auto xRatio = x / width1;
					auto top = tlColor * (1 - xRatio) + trColor * xRatio;
					auto bottom = blColor * (1 - xRatio) + brColor * xRatio;
					writeData[x] = top * (1 - yRatio) + bottom * yRatio;
				}
			}
		}

		static void CalculateRotationBounds(double width, double height, double angle,
			[System::Runtime::InteropServices::Out] double% canvasWidth,
			[System::Runtime::InteropServices::Out] double% canvasHeight)
		{
			if (angle == 0)
			{
				canvasWidth = width;
				canvasHeight = height;
				return;
			}

			double angleRad = -angle * (Math::PI / 180.0);
			double angleCos = std::cos(angleRad);
			double angleSin = std::sin(angleRad);

			double halfWidth = width / 2.0;
			double halfHeight = height / 2.0;

			double cx1 = halfWidth * angleCos;
			double cy1 = halfWidth * angleSin;

			double cx2 = halfWidth * angleCos - halfHeight * angleSin;
			double cy2 = halfWidth * angleSin + halfHeight * angleCos;

			double cx3 = -halfHeight * angleSin;
			double cy3 = halfHeight * angleCos;

			double cx4 = 0.0;
			double cy4 = 0.0;

			double newHalfWidth = std::max({ cx1, cx2, cx3, cx4 }) - std::min({ cx1, cx2, cx3, cx4 });
			double newHalfHeight = std::max({ cy1, cy2, cy3, cy4 }) - std::min({ cy1, cy2, cy3, cy4 });

			canvasWidth = newHalfWidth * 2.0;
			canvasHeight = newHalfHeight * 2.0;
		}

		static void BilinearRotate(FramePlane input, FramePlane output, double angle)
		{
			switch (input.byteDepth)
			{
			case 1:
				BilinearRotate<unsigned char>(input, output, angle);
				break;
			case 2:
				BilinearRotate<unsigned short>(input, output, angle);
				break;
			case 4:
				BilinearRotate<float>(input, output, angle);
				break;
			}
		}

		template <typename TColor> static void BilinearRotate(FramePlane input, FramePlane output, double angle)
		{
			TColor* in = static_cast<TColor*>(input.pointer);
			TColor* out = static_cast<TColor*>(output.pointer);

			const auto pixelSize = output.pixelSize;
			const auto inStride = input.stride;
			const auto inWidth = input.width;
			const auto inHeight = input.height;
			const auto outWidth = output.width;
			const auto outHeight = output.height;

			double canvasWidth, canvasHeight;
			CalculateRotationBounds(inWidth, inHeight, angle, canvasWidth, canvasHeight);
			const auto yOffset = (canvasHeight - std::floor(canvasHeight)) / 2.0;
			const auto xOffset = (canvasWidth - std::floor(canvasWidth)) / 2.0;

			const double oldXradius = (inWidth - 1) / 2.0;
			const double oldYradius = (inHeight - 1) / 2.0;
			const double newXradius = (canvasWidth - 1) / 2.0;
			const double newYradius = (canvasHeight - 1) / 2.0;

			const double angleRad = -angle * Math::PI / 180;
			const double angleCos = std::cos(angleRad);
			const double angleSin = std::sin(angleRad);

			const int ymax = inHeight - 1;
			const int xmax = inWidth - 1;

			double cy = yOffset - newYradius;

			if (input.pixelSize == 1)
			{
				for (int y = 0; y < outHeight; y++, cy++, out += output.stride)
				{
					double tx = angleSin * cy + oldXradius;
					double ty = angleCos * cy + oldYradius;

					double cx = xOffset - newXradius;

					for (int x = 0; x < outWidth; x++, cx++)
					{
						// coordinates of source point
						double ox = tx + angleCos * cx;
						double oy = ty - angleSin * cx;

						// top-left coordinate
						int ox1 = static_cast<int>(ox);
						int oy1 = static_cast<int>(oy);

						// validate source pixel's coordinates
						if (ox1 >= 0 && oy1 >= 0 && ox1 < inWidth && oy1 < inHeight)
						{
							// bottom-right coordinate
							int ox2 = ox1 == xmax ? ox1 : ox1 + 1;
							int oy2 = oy1 == ymax ? oy1 : oy1 + 1;

							double dx1 = std::max(0.0, ox - ox1);
							double dx2 = 1.0 - dx1;

							double dy1 = std::max(0.0, oy - oy1);
							double dy2 = 1.0 - dy1;

							// get four points
							TColor* p = in + oy1 * inStride;
							TColor p1 = p[ox1];
							TColor p2 = p[ox2];

							p = in + oy2 * inStride;
							TColor p3 = p[ox1];
							TColor p4 = p[ox2];

							out[x] = static_cast<TColor>(dy2 * (dx2 * p1 + dx1 * p2) + dy1 * (dx2 * p3 + dx1 * p4));
						}
					}
				}
			}
			else
			{
				const int dstOffset = output.stride - outWidth * pixelSize;
				for (int y = 0; y < outHeight; y++, cy++, out += dstOffset)
				{
					double tx = angleSin * cy + oldXradius;
					double ty = angleCos * cy + oldYradius;

					double cx = xOffset - newXradius;
					for (int x = 0; x < outWidth; x++, cx++, out += pixelSize)
					{
						// coordinates of source point
						double ox = tx + angleCos * cx;
						double oy = ty - angleSin * cx;

						// top-left coordinate
						int ox1 = static_cast<int>(ox);
						int oy1 = static_cast<int>(oy);

						// validate source pixel's coordinates
						if (ox1 >= 0 && oy1 >= 0 && ox1 < inWidth && oy1 < inHeight)
						{
							// bottom-right coordinate
							int ox2 = ox1 == xmax ? ox1 : ox1 + 1;
							int oy2 = oy1 == ymax ? oy1 : oy1 + 1;

							double dx1 = std::max(0.0, ox - ox1);
							double dx2 = 1.0 - dx1;

							double dy1 = std::max(0.0, oy - oy1);
							double dy2 = 1.0 - dy1;

							// get four points
							TColor* p1 = in + oy1 * inStride;
							TColor* p2 = p1;
							p1 += ox1 * pixelSize;
							p2 += ox2 * pixelSize;

							TColor* p3 = in + oy2 * inStride;
							TColor* p4 = p3;
							p3 += ox1 * pixelSize;
							p4 += ox2 * pixelSize;

							// interpolate using 4 points
							for (int z = 0; z < pixelSize; z++)
							{
								out[z] = static_cast<TColor>(dy2 * (dx2 * p1[z] + dx1 * p2[z]) + dy1 * (dx2 * p3[z] + dx1 * p4[z]));
							}
						}
					}
				}
			}
		}

		static Tuple<double, double>^ FindMinMax(FramePlane^ framePlane)
		{
			switch (framePlane->byteDepth)
			{
			case 1:
				if (framePlane->pixelSize == 1) 
				{
					uint8_t min, max, avg;
					SimdGetStatistic(static_cast<const uint8_t*>(framePlane->pointer), framePlane->stride, framePlane->row, framePlane->height, &min, &max, &avg);
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
			const auto width = framePlane->row;
			const auto pixelSize = framePlane->pixelSize;
			const auto height = framePlane->height;
			const auto stride = framePlane->stride;
			for (auto y = 0; y < height; y++, data += stride)
			{
				for (auto x = 0; x < width; x += pixelSize)
				{
					T& value = data[x];
					min = std::min(min, value);
					max = std::max(max, value);
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
			const auto width = framePlane->row;
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
	};
};

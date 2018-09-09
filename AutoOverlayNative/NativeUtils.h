using namespace System;
using namespace Collections::Generic;
using namespace Runtime::InteropServices;

const double EPSILON = std::numeric_limits<double>::epsilon();

namespace AutoOverlay
{
	struct ColorMatching
	{
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
		array<int>^ * histogram;
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
	};

	public ref class NativeUtils sealed
	{
	public:
		static double SquaredDifferenceSum(
			IntPtr src, int srcStride,
			IntPtr srcMask, int srcMaskStride,
			IntPtr over, int overStride,
			IntPtr overMask, int overMaskStride,
			int width, int height, int depth)
		{
			SquaredDiffParams params = {
				src, srcStride, srcMask, srcMaskStride,
				over, overStride, overMask, overMaskStride,
				width, height
			};
			switch(depth)
			{
				case 8: return SquaredDifferenceSum<unsigned char>(params);
				case 10: return SquaredDifferenceSum<unsigned short>(params);
				case 12: return SquaredDifferenceSum<unsigned short>(params);
				case 14: return SquaredDifferenceSum<unsigned short>(params);
				case 16: return SquaredDifferenceSum<unsigned short>(params);
				default: throw gcnew InvalidOperationException();
			}
		}

		template <typename TColor>
		static double SquaredDifferenceSum(SquaredDiffParams params)
		{
			__int64 sum = 0;
			TColor* src = reinterpret_cast<TColor*>(params.src.ToPointer());
			TColor* srcMask = reinterpret_cast<TColor*>(params.srcMask.ToPointer());
			TColor* over = reinterpret_cast<TColor*>(params.over.ToPointer());
			TColor* overMask = reinterpret_cast<TColor*>(params.overMask.ToPointer());
			params.srcStride /= sizeof(TColor);
			params.srcMaskStride /= sizeof(TColor);
			params.overStride /= sizeof(TColor);
			params.overMaskStride /= sizeof(TColor);

			int pixelCount = params.width * params.height;

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
						if ((srcMask == nullptr || srcMask[col] > 0)
							&& (overMask == nullptr || overMask[col] > 0))
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
				FillHistogram<unsigned char>(params);
			else FillHistogram<unsigned short>(params);
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
						++hist[data[x]];
			}
			else
			{
				unsigned char* maskData = reinterpret_cast<unsigned char*>(params.mask.ToPointer());
				for (int y = 0; y < params.height; y++, data += params.imageStride, maskData += params.maskStride)
					for (int x = 0, xMask = 0; x < params.rowSize; x += params.imagePixelSize, xMask += params.
					     maskPixelSize)
						if (maskData[xMask] > 0)
							++hist[data[x]];
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
				ApplyColorMap<unsigned char, unsigned char>(params);
			else if (hdrIn && !hdrOut)
				ApplyColorMap<unsigned short, unsigned char>(params);
			else if (!hdrIn && hdrOut)
				ApplyColorMap<unsigned char, unsigned short>(params);
			else
				ApplyColorMap<unsigned short, unsigned short>(params);
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

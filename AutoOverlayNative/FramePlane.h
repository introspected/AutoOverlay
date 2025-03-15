#pragma once

#include <omp.h>
#include <thread>
#include <unordered_map>
#include <Simd/SimdLib.h>

#include "IInterpolator.h"
#include "Lut.h"
#include "ManagedInterpolator.h"
#include "OverlayType.h"
#include "PlaneChannel.h"

using namespace System;
using namespace AvsFilterNet;
using namespace AutoOverlay;
using namespace Drawing;

constexpr double EPSILON = 0.000000001;

public value class FramePlane sealed
{
public:
    void* pointer;
    int bitDepth;
    int byteDepth;
    int pixelSize;
    int width;
    int row;
    int height;
    int stride;

    FramePlane(PlaneChannel^ planeChannel, VideoFrame^ frame, bool read)
    {
        InitCrop(planeChannel, frame, read, Rectangle::Empty);
    }

    FramePlane(PlaneChannel^ planeChannel, VideoFrame^ frame, bool read, Rectangle rect, bool ltrb)
    {
        if (ltrb)
            InitCrop(planeChannel, frame, read, rect);
        else InitRoi(planeChannel, frame, read, rect);
    }

    array<FramePlane>^ Split(int count)
    {
	    const auto minHeight = height / count;
        auto planes = gcnew array<FramePlane>(count);
        for (auto i = 0; i < count; i++)
        {
            auto plane = gcnew FramePlane();
            plane->pointer = static_cast<unsigned char*>(pointer) + minHeight * stride * byteDepth * i;
            plane->bitDepth = bitDepth;
            plane->byteDepth = byteDepth;
            plane->pixelSize = pixelSize;
            plane->width = width;
            plane->row = row;
            plane->height = i == count - 1 ? height - minHeight * i : minHeight;
            plane->stride = stride;
            planes->SetValue(plane, i);
        }
        return planes;
    }

    FramePlane TakeLeft(int length)
    {
        return Crop(0, 0, width - length, 0);
    }

    FramePlane TakeTop(int length)
    {
        return Crop(0, 0, 0, height - length);
    }

    FramePlane TakeRight(int length)
    {
        return Crop(width - length, 0, 0, 0);
    }

    FramePlane TakeBottom(int length)
    {
        return Crop(0, height - length, 0, 0);
    }

    FramePlane CropLeft(int length)
    {
        return Crop(length, 0, 0, 0);
    }

    FramePlane CropTop(int length)
    {
        return Crop(0, length, 0, 0);
    }

    FramePlane CropRight(int length)
    {
        return Crop(0, 0, length, 0);
    }

    FramePlane CropBottom(int length)
    {
        return Crop(0, 0, 0, length);
    }

    FramePlane ROI(Rectangle roi)
    {
        auto framePlane = new FramePlane();
        framePlane->bitDepth = bitDepth;
        framePlane->byteDepth = byteDepth;
        framePlane->pixelSize = pixelSize;
        framePlane->width = roi.Width;
        framePlane->row = roi.Width * pixelSize;
        framePlane->height = roi.Height;
        framePlane->stride = stride;
        framePlane->pointer = static_cast<unsigned char*>(pointer) + (roi.Left + roi.Top * stride) * byteDepth;
        return *framePlane;
    }

    FramePlane Crop(Rectangle crop)
    {
        return Crop(crop.Left, crop.Top, crop.Right, crop.Bottom);
    }

    FramePlane Crop(int left, int top, int right, int bottom)
    {
        auto framePlane = new FramePlane();
        framePlane->bitDepth = bitDepth;
        framePlane->byteDepth = byteDepth;
        framePlane->pixelSize = pixelSize;
        framePlane->width = width - left - right;
        framePlane->row = framePlane->width *pixelSize;
        framePlane->height = height - top - bottom;
        framePlane->stride = stride;
        framePlane->pointer = static_cast<unsigned char*>(pointer) + (left + top * stride) * byteDepth;
        return *framePlane;
    }

    void Fill(int color)
    {
        Fill(color, OverlayType::BLEND);
    }

    void Fill(int color, OverlayType mode)
    {
        switch (byteDepth)
        {
        case 1:
            Fill<unsigned char>(color, mode);
            break;

        case 2:
            Fill<unsigned short>(color, mode);
            break;
        default:
            throw gcnew AvisynthException("Unsupported color depth");
        }
    }

    void FillGradient(Rectangle colors)
    {
        switch (byteDepth)
        {
        case 1:
            FillGradient<unsigned char>(colors);
            break;

        case 2:
            FillGradient<unsigned short>(colors);
            break;
        default:
            throw gcnew AvisynthException("Unsupported color depth");
        }
    }

    void FillGradient(int tl, int tr, int br, int bl)
    {
        FillGradient(tl, tr, br, bl, OverlayType::BLEND);
    }

    void FillGradient(int tl, int tr, int br, int bl, OverlayType mode)
    {
        FillGradient(tl, tr, br, bl, false, 0, mode);
    }

    void FillGradient(int tl, int tr, int br, int bl, bool noise, int seed)
    {
        FillGradient(tl, tr, br, bl, noise, seed, OverlayType::BLEND);
    }

    void FillGradient(int tl, int tr, int br, int bl, bool noise, int seed, OverlayType mode)
    {
        switch (byteDepth)
        {
        case 1:
            FillGradient<unsigned char>(tl, tr, br, bl, noise, seed, mode);
            break;
        case 2:
            FillGradient<unsigned short>(tl, tr, br, bl, noise, seed, mode);
            break;
        case 4:
            FillGradient<float>(tl, tr, br, bl, noise, seed, mode);
            break;
        default:
            throw gcnew AvisynthException("Unsupported color depth");
        }
    }

    int FillHistogram(FramePlane^ maskPlane, array<double>^ values, const double offset, const double step)
    {
        const pin_ptr<double> valuesPin = &values[0];
        double* valuesPtr = valuesPin;
        switch (byteDepth)
        {
        case 1:
            if (pixelSize == 1) {
                auto ints = new uint32_t[256];
                if (maskPlane)
                {
                    SimdHistogramMasked(static_cast<const uint8_t*>(pointer), stride, row, height,
                        static_cast<const uint8_t*>(maskPlane->pointer), maskPlane->stride, 255, ints);
                }
                else
                {
                    SimdHistogram(static_cast<const uint8_t*>(pointer), row, height, stride, ints);
                }
                int total = 0;
                for (auto color = 0; color < 256; color++)
                {
                    const auto count = ints[color];
                    if (count == 0) continue;
                    const auto realColor = (color - offset) / step;
                    const auto minColor = static_cast<unsigned int>(std::floor(realColor));
                    const auto diff = realColor - minColor;
                    valuesPtr[minColor] += (1 - diff) * count;
                    if (diff > 0)
                        valuesPtr[minColor + 1] += diff * count;
                    total += count;
                }
                delete ints;
                return total;
            }
            return FillHistogram<unsigned char>(maskPlane, valuesPtr, offset, step);
        case 2:
            return FillHistogram<unsigned short>(maskPlane, valuesPtr, offset, step);
        case 4:
            return FillHistogram<float>(maskPlane, valuesPtr, offset, step);
        }
        throw gcnew AvisynthException("Unsupported color depth");
    }

    int FillGradientHistogram(CornerGradient gradient, Nullable<FramePlane> maskPlane, array<double>^ values, const double offset, const double step)
    {
        const pin_ptr<double> valuesPtr = &values[0];
        switch (byteDepth)
        {
        case 1:
            return FillGradientHistogram<unsigned char>(gradient, maskPlane, valuesPtr, offset, step);
        case 2:
            return FillGradientHistogram<unsigned short>(gradient, maskPlane, valuesPtr, offset, step);
        case 4:
            return FillGradientHistogram<float>(gradient, maskPlane, valuesPtr, offset, step);
        }
        throw gcnew AvisynthException("Unsupported color depth");
    }

    int FillGradientHistograms(FramePlane^ maskPlane, 
        array<double>^ tlValues, array<double>^ trValues, array<double>^ brValues, array<double>^ blValues,
        const double offset, const double step)
    {
        const pin_ptr<double> tlValuesPtr = &tlValues[0];
        const pin_ptr<double> trValuesPtr = &trValues[0];
        const pin_ptr<double> brValuesPtr = &brValues[0];
        const pin_ptr<double> blValuesPtr = &blValues[0];

        switch (byteDepth)
        {
        case 1:
            return FillGradientHistograms<unsigned char>(maskPlane, tlValuesPtr, trValuesPtr, brValuesPtr, blValuesPtr, offset, step);
        case 2:
            return FillGradientHistograms<unsigned short>(maskPlane, tlValuesPtr, trValuesPtr, brValuesPtr, blValuesPtr, offset, step);
        case 4:
            return FillGradientHistograms<float>(maskPlane, tlValuesPtr, trValuesPtr, brValuesPtr, blValuesPtr, offset, step);
        }
        throw gcnew AvisynthException("Unsupported color depth");
    }

    void ApplyHistogram(FramePlane input, IInterpolator^ averageInterpolator, Nullable<int> seed)
    {
        switch (input.byteDepth)
        {
        case 1:
            switch (byteDepth)
            {
            case 1:
                ApplyHistogram<unsigned char, unsigned char>(input, averageInterpolator, seed);
                break;
            case 2:
                ApplyHistogram<unsigned char, unsigned short>(input, averageInterpolator, seed);
                break;
            case 4:
                ApplyHistogram<unsigned char, float>(input, averageInterpolator, seed);
                break;
            default:
                throw gcnew AvisynthException("Unsupported color depth");
            }
            break;
        case 2:
            switch (byteDepth)
            {
            case 1:
                ApplyHistogram<unsigned short, unsigned char>(input, averageInterpolator, seed);
                break;
            case 2:
                ApplyHistogram<unsigned short, unsigned short>(input, averageInterpolator, seed);
                break;
            case 4:
                ApplyHistogram<unsigned short, float>(input, averageInterpolator, seed);
                break;
            default:
                throw gcnew AvisynthException("Unsupported color depth");
            }
            break;
        case 4:
            switch (byteDepth)
            {
            case 1:
                ApplyHistogram<float, unsigned char>(input, averageInterpolator, seed);
                break;
            case 2:
                ApplyHistogram<float, unsigned short>(input, averageInterpolator, seed);
                break;
            case 4:
                ApplyHistogram<float, float>(input, averageInterpolator, seed);
                break;
            default:
                throw gcnew AvisynthException("Unsupported color depth");
            }
            break;
        default:
            throw gcnew AvisynthException("Unsupported color depth");
        }
    }

    void ApplyLut(FramePlane input, Lut^ lut, Nullable<int> seed)
    {
        switch (input.byteDepth)
        {
        case 1:
            switch (byteDepth)
            {
            case 1:
                ApplyLut<unsigned char, unsigned char>(input, lut, seed);
                break;
            case 2:
                ApplyLut<unsigned char, unsigned short>(input, lut, seed);
                break;
            default:
                throw gcnew AvisynthException("Unsupported color depth");
            }
            break;
        case 2:
            switch (byteDepth)
            {
            case 1:
                ApplyLut<unsigned short, unsigned char>(input, lut, seed);
                break;
            case 2:
                ApplyLut<unsigned short, unsigned short>(input, lut, seed);
                break;
            default:
                throw gcnew AvisynthException("Unsupported color depth");
            }
            break;
        default:
            throw gcnew AvisynthException("Unsupported color depth");
        }
    }

    void FillNoise(double tl, double tr, double br, double bl, int color, int seed)
    {
        auto random = NativeRandom(seed);
        switch (byteDepth)
        {
        case 1:
            FillNoise<unsigned char>(tl, tr, br, bl, color, random);
            break;

        case 2:
            FillNoise<unsigned short>(tl, tr, br, bl, color, random);
            break;
        default:
            throw gcnew AvisynthException("Unsupported color depth");
        }
    }

    void Min(FramePlane other)
    {
        switch (byteDepth)
        {
        case 1:
            MinImpl<unsigned char>(other);
            break;

        case 2:
            MinImpl<unsigned short>(other);
            break;
        default:
            throw gcnew AvisynthException("Unsupported color depth");
        }
    }

    void GradientMerge(FramePlane tl, FramePlane tr, FramePlane br, FramePlane bl)
    {
        switch (byteDepth)
        {
        case 1:
            MergeGradient<unsigned char>(tl ,tr, br, bl);
            break;
        case 2:
            MergeGradient<unsigned short>(tl, tr, br, bl);
            break;
        case 4:
            MergeGradient<float>(tl, tr, br, bl);
            break;
        default:
            throw gcnew AvisynthException("Unsupported color depth");
        }
    }

private:
    void InitCrop(PlaneChannel^ planeChannel, VideoFrame^ frame, bool read, Rectangle crop)
    {
        bitDepth = planeChannel->Depth;
        byteDepth = bitDepth / 8 + (bitDepth % 8 == 0 ? 0 : 1);
        pixelSize = planeChannel->PixelSize;
        width = frame->GetRowSize(planeChannel->Plane) / (byteDepth*pixelSize) - crop.Left - crop.Right;
        row = width * pixelSize;
        height = frame->GetHeight(planeChannel->Plane) - crop.Top - crop.Height;
        stride = frame->GetPitch(planeChannel->Plane) / byteDepth;
        auto ptr = read ? frame->GetReadPtr(planeChannel->Plane) : frame->GetWritePtr(planeChannel->Plane);
        pointer = static_cast<unsigned char*>(ptr.ToPointer()) + (planeChannel->ChannelOffset + crop.Left + crop.Top * stride) * byteDepth;
    }

    void InitRoi(PlaneChannel^ planeChannel, VideoFrame^ frame, bool read, Rectangle roi)
    {
        bitDepth = planeChannel->Depth;
        byteDepth = bitDepth / 8 + (bitDepth % 8 == 0 ? 0 : 1);
        pixelSize = planeChannel->PixelSize;
        width = roi.Width;
        row = width * pixelSize;
        height = roi.Height;
        stride = frame->GetPitch(planeChannel->Plane) / byteDepth;
        auto ptr = read ? frame->GetReadPtr(planeChannel->Plane) : frame->GetWritePtr(planeChannel->Plane);
        pointer = static_cast<unsigned char*>(ptr.ToPointer()) + (planeChannel->ChannelOffset + roi.Left + roi.Top * stride) * byteDepth;
    }

    template <typename TColor> void Fill(int color, OverlayType mode)
    {
        auto ptr = static_cast<TColor*>(pointer);
        auto shift = bitDepth - 8;
        TColor filler = color << shift;
        switch (mode)
        {
        case OverlayType::BLEND:
            for (auto y = 0; y < height; y++, ptr += stride)
                for (auto x = 0; x < row; x += pixelSize)
                {
                    ptr[x] = filler;
                }
	        break;
        case OverlayType::LIGHTEN:
            for (auto y = 0; y < height; y++, ptr += stride)
                for (auto x = 0; x < row; x += pixelSize)
                {
                    ptr[x] = std::max(ptr[x], filler);
                }
	        break;
        case OverlayType::DARKEN:
            for (auto y = 0; y < height; y++, ptr += stride)
                for (auto x = 0; x < row; x += pixelSize)
                {
                    ptr[x] = std::min(ptr[x], filler);
                }
	        break;
        }
    }

    template <typename TColor> void FillGradient(Rectangle colors)
    {
        auto ptr = static_cast<TColor*>(pointer);
        auto shift = bitDepth - 8;
        TColor left = colors.Left << shift;
        TColor top = colors.Top << shift;
        TColor right = colors.Right << shift;
        TColor bottom = colors.Bottom << shift;
        TColor horizontalDelta = right - left;
        TColor verticalDelta = bottom - top;
        auto width1 = row - 1.0;
        for (auto y = 0; y < height; y++, ptr += stride)
        {
            auto yRatio = y / (height - 1.0);
            for (auto x = 0; x < row; x += pixelSize)
            {
                auto xRatio = x / width1;
                auto horizontalGradient = left + xRatio * horizontalDelta;
                auto verticalGradient = top + yRatio * verticalDelta;
                ptr[x] = static_cast<TColor>((horizontalGradient + verticalGradient) / 2);
            }
        }
    }

    template <typename TColor> void FillGradient(int tl, int tr, int br, int bl, bool noise, int seed, OverlayType mode)
    {
        auto ptr = static_cast<TColor*>(pointer);

        TColor tlColor, trColor, brColor, blColor;

        if (byteDepth == 4)
        {
            tlColor = tl / 255.0;
            trColor = tl / 255.0;
            brColor = tl / 255.0;
            blColor = tl / 255.0;
        }
    	else
        {
            auto shift = bitDepth - 8;
            tlColor = tl << shift;
            trColor = tr << shift;
            brColor = br << shift;
            blColor = bl << shift;
        }

        TColor min = std::min({ tlColor, trColor, brColor, blColor });
        TColor max = std::max({ tlColor, trColor, brColor, blColor });
        auto diff = max - min;
        auto median = min + diff / 2;
        noise = noise && diff > 1;
        auto random = NativeRandom(seed);

        for (auto y = 0; y < height; y++, ptr += stride)
        {
            auto yRatio = y / (height - 1.0);
            for (auto x = 0; x < row; x += pixelSize)
            {
                auto xRatio = x / (row - 1.0);

                auto top = tlColor * (1 - xRatio) + trColor * xRatio;
                auto bottom = blColor * (1 - xRatio) + brColor * xRatio;

                //auto gradient = static_cast<TColor>(std::round(top * (1 - yRatio) + bottom * yRatio));
                auto gradient = static_cast<TColor>(top * (1 - yRatio) + bottom * yRatio);

                if (noise)
                {
                    auto darken = gradient <= median;
                    if (darken && random.Next(diff) + min < gradient || !darken && random.Next(diff) + min > gradient)
                    {
                        gradient = median;
                    }
                }
                switch (mode)
                {
                case OverlayType::BLEND:
                    ptr[x] = gradient;
                    break;
                case OverlayType::DARKEN:
                    ptr[x] = std::min(ptr[x], gradient);
                    break;
                case OverlayType::LIGHTEN:
                    ptr[x] = std::max(ptr[x], gradient);
                    break;
                }
            }
        }
    }

    template <typename TColor> void MergeGradient(FramePlane tl, FramePlane tr, FramePlane br, FramePlane bl)
    {
        auto ptr = static_cast<TColor*>(pointer);
        auto tlPtr = static_cast<TColor*>(tl.pointer);
        auto trPtr = static_cast<TColor*>(tr.pointer);
        auto brPtr = static_cast<TColor*>(br.pointer);
        auto blPtr = static_cast<TColor*>(bl.pointer);

        const auto width1 = row - 1.0;

        for (auto y = 0; y < height; y++, ptr += stride, tlPtr += tl.stride, trPtr += tr.stride, brPtr += br.stride, blPtr += bl.stride)
        {
            auto yRatio = y / (height - 1.0);
            for (auto x = 0; x < row; x += pixelSize)
            {
                const auto xRatio = x / width1;

                const auto top = tlPtr[x] * (1 - xRatio) + trPtr[x] * xRatio;
                const auto bottom = blPtr[x] * (1 - xRatio) + brPtr[x] * xRatio;

                ptr[x] = static_cast<TColor>(top * (1 - yRatio) + bottom * yRatio);
            }
        }
    }

    template <typename TColor> void FillNoise(double tl, double tr, double br, double bl, int color, NativeRandom random)
    {
        auto ptr = static_cast<TColor*>(pointer);
        auto shift = bitDepth - 8;
        TColor filler = color << shift;
        auto width1 = row - 1.0;
        for (auto y = 0; y < height; y++, ptr += stride)
        {
            auto yRatio = y / (height - 1.0);
            for (auto x = 0; x < row; x += pixelSize)
            {
                auto xRatio = x / width1;

                auto top = tl * (1 - xRatio) + tr * xRatio;
                auto bottom = bl * (1 - xRatio) + br * xRatio;
                auto weight = top * (1 - yRatio) + bottom * yRatio;

                if (random.NextDouble() < weight)
                {
                    ptr[x] = filler;
                }
            }
        }
    }

    template <typename TColor> void MinImpl(FramePlane overlay)
    {
        auto ptr = static_cast<TColor*>(pointer);
        auto overPtr = static_cast<TColor*>(overlay.pointer);
        for (auto y = 0; y < height; y++, ptr += stride, overPtr += overlay.stride)
            for (auto x = 0; x < row; x++)
            {
                ptr[x] = std::min(ptr[x], overPtr[x]);
            }
    }

    template <typename T> int FillGradientHistogram(
        CornerGradient gradient, Nullable<FramePlane> maskPlane, 
        double* values, const double offset, const double step)
    {

        auto tl = gradient.topLeft;
        auto tr = gradient.topRight;
        auto br = gradient.bottomRight;
        auto bl = gradient.bottomLeft;

        auto width1 = row - 1.0;
        auto height1 = height - 1.0;

        auto data = static_cast<T*>(pointer);
        const auto max = std::numeric_limits<T>().max();
        int total = width * height;
        if (std::abs(step - 1) < EPSILON && offset == 0)
        {
            if (maskPlane.HasValue) {
                auto mask = static_cast<T*>(maskPlane.Value.pointer);
                const auto maskStride = maskPlane.Value.stride;
                for (auto y = 0; y < height; y++, data += stride, mask += maskStride)
                {
                    auto yRatio = y / height1;
                    for (auto x = 0; x < row; x += pixelSize)
                    {
                        if (mask[x] != max)
                        {
                            total--;
                            continue;
                        }
                        auto xRatio = x / width1;
                        auto top = tl * (1 - xRatio) + tr * xRatio;
                        auto bottom = bl * (1 - xRatio) + br * xRatio;
                        auto weight = top * (1 - yRatio) + bottom * yRatio;

                        values[static_cast<int>(data[x])] += weight;
                    }
                }
            }
            else
            {
                for (auto y = 0; y < height; y++, data += stride)
                {
                    auto yRatio = y / height1;
                    for (auto x = 0; x < row; x += pixelSize)
                    {
                        auto xRatio = x / width1;
                        auto top = tl * (1 - xRatio) + tr * xRatio;
                        auto bottom = bl * (1 - xRatio) + br * xRatio;
                        auto weight = top * (1 - yRatio) + bottom * yRatio;

                        values[static_cast<int>(data[x])] += weight;
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
                    auto yRatio = y / height1;
                    for (auto x = 0; x < row; x += pixelSize)
                    {
                        if (mask[x] != max)
                        {
                            total--;
                            continue;
                        }
                        auto xRatio = x / width1;
                        auto top = tl * (1 - xRatio) + tr * xRatio;
                        auto bottom = bl * (1 - xRatio) + br * xRatio;
                        auto weight = top * (1 - yRatio) + bottom * yRatio;

                        const auto realColor = (data[x] - offset) / step;
                        const auto minColor = static_cast<unsigned int>(std::floor(realColor));
                        const auto diff = realColor - minColor;
                        values[minColor] += (1 - diff)*weight;
                        if (diff > 0)
                            values[minColor + 1] += diff*weight;
                    }
                }
            }
            else
            {
                for (auto y = 0; y < height; y++, data += stride)
                {
                    auto yRatio = y / height1;
                    for (auto x = 0; x < row; x += pixelSize)
                    {
                        auto xRatio = x / width1;
                        auto top = tl * (1 - xRatio) + tr * xRatio;
                        auto bottom = bl * (1 - xRatio) + br * xRatio;
                        auto weight = top * (1 - yRatio) + bottom * yRatio;

                        const auto realColor = (data[x] - offset) / step;
                        const auto minColor = static_cast<unsigned int>(std::floor(realColor));
                        const auto diff = realColor - minColor;
                        values[minColor] += (1 - diff)*weight;
                        if (diff > 0)
                            values[minColor + 1] += diff*weight;
                    }
                }
            }
        }
        return total;
    }

    template <typename T> int FillHistogram(FramePlane^ maskPlane, double* values, const double offset, const double step)
    {
        auto data = static_cast<T*>(pointer);
        const auto maskMax = byteDepth == 4 ? 1 : (1 << bitDepth) - 1;
        int total = width * height;
        if (std::abs(step - 1) < EPSILON && offset == 0 && byteDepth < 4)
        {
            if (maskPlane) {
                auto mask = static_cast<T*>(maskPlane->pointer);
                const auto maskStride = maskPlane->stride;
                for (auto y = 0; y < height; y++, data += stride, mask += maskStride)
                {
                    for (auto x = 0; x < width; x += pixelSize)
                    {
                        if (mask[x] < maskMax)
                        {
                            total--;
                            continue;
                        }
                        ++values[static_cast<int>(data[x])];
                    }
                }
            }
            else
            {
                for (auto y = 0; y < height; y++, data += stride)
                {
                    for (auto x = 0; x < width; x += pixelSize)
                    {
                        ++values[static_cast<int>(data[x])];
                    }
                }
            }
        }
        else
        {
            if (maskPlane) {
                auto mask = static_cast<T*>(maskPlane->pointer);
                const auto maskStride = maskPlane->stride;
                for (auto y = 0; y < height; y++, data += stride, mask += maskStride)
                {
                    for (auto x = 0; x < width; x += pixelSize)
                    {
                        if (mask[x] < maskMax)
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

    template <typename T> int FillGradientHistograms(
        FramePlane^ maskPlane,
        double* tlValues, double* trValues, double* brValues, double* blValues, 
        const double offset, const double step)
    {
        auto width1 = row - 1.0;
        auto height1 = height - 1.0;

        auto data = static_cast<T*>(pointer);

        const auto maskMax = byteDepth == 4 ? 1 : (1 << bitDepth) - 1;
        int total = width * height;
        if (std::abs(step - 1) < EPSILON && offset == 0 && byteDepth < 4)
        {
            if (maskPlane) {
                auto mask = static_cast<T*>(maskPlane -> pointer);
                const auto maskStride = maskPlane -> stride;
                for (auto y = 0; y < height; y++, data += stride, mask += maskStride)
                {
                    const auto yRatio = y / height1;
                    const auto yRatio0 = 1 - yRatio;
                    for (auto x = 0; x < width; x += pixelSize)
                    {
                        if (mask[x] < maskMax)
                        {
                            total--;
                            continue;
                        }
                        const auto xRatio = x / width1;
                        const auto xRatio0 = 1 - xRatio;
                        const unsigned int color = data[x];
                        tlValues[color] += xRatio0 * yRatio0;
                        trValues[color] += xRatio * yRatio0;
                        brValues[color] += xRatio * yRatio;
                        blValues[color] += xRatio0 * yRatio;
                    }
                }
            }
            else
            {
                for (auto y = 0; y < height; y++, data += stride)
                {
                    const auto yRatio = y / height1;
                    const auto yRatio0 = 1 - yRatio;
                    for (auto x = 0; x < width; x += pixelSize)
                    {
                        const auto xRatio = x / width1;
                        const auto xRatio0 = 1 - xRatio;
                        const unsigned int color = data[x];
                        tlValues[color] += xRatio0 * yRatio0;
                        trValues[color] += xRatio * yRatio0;
                        brValues[color] += xRatio * yRatio;
                        blValues[color] += xRatio0 * yRatio;
                    }
                }
            }
        }
        else
        {
            if (maskPlane) {
                auto mask = static_cast<T*>(maskPlane->pointer);
                const auto maskStride = maskPlane->stride;
                for (auto y = 0; y < height; y++, data += stride, mask += maskStride)
                {
                    const auto yRatio = y / height1;
                    const auto yRatio0 = 1 - yRatio;
                    for (auto x = 0; x < width; x += pixelSize)
                    {
                        if (mask[x] < maskMax)
                        {
                            total--;
                            continue;
                        }
                        const auto xRatio = x / width1;
                        const auto xRatio0 = 1 - xRatio;

                        const auto realColor = (data[x] - offset) / step;
                        const auto minColor = static_cast<unsigned int>(std::floor(realColor));
                        const auto diff = realColor - minColor;
                        const auto diff0 = 1 - diff;

                        const auto tl = xRatio0 * yRatio0;
                        const auto tr = xRatio * yRatio0;
                        const auto br = xRatio * yRatio;
                        const auto bl = xRatio0 * yRatio;

                        tlValues[minColor] += diff0 * tl;
                        trValues[minColor] += diff0 * tr;
                        brValues[minColor] += diff0 * br;
                        blValues[minColor] += diff0 * bl;
                        if (diff > 0)
                        {
                            const auto maxColor = minColor + 1;
                            tlValues[maxColor] += diff * tl;
                            trValues[maxColor] += diff * tr;
                            brValues[maxColor] += diff * br;
                            blValues[maxColor] += diff * bl;
                        }
                    }
                }
            }
            else
            {
                for (auto y = 0; y < height; y++, data += stride)
                {
                    const auto yRatio = y / height1;
                    const auto yRatio0 = 1 - yRatio;
                    for (auto x = 0; x < width; x += pixelSize)
                    {
                        const auto xRatio = x / width1;
                        const auto xRatio0 = 1 - xRatio;

                        const auto realColor = (data[x] - offset) / step;
                        const auto minColor = static_cast<unsigned int>(std::floor(realColor));
                        const auto diff = realColor - minColor;
                        const auto diff0 = 1 - diff;

                        const auto tl = xRatio0 * yRatio0;
                        const auto tr = xRatio * yRatio0;
                        const auto br = xRatio * yRatio;
                        const auto bl = xRatio0 * yRatio;

                        tlValues[minColor] += diff0 * tl;
                        trValues[minColor] += diff0 * tr;
                        brValues[minColor] += diff0 * br;
                        blValues[minColor] += diff0 * bl;
                        if (diff > 0)
                        {
                            const auto maxColor = minColor + 1;
                            tlValues[maxColor] += diff * tl;
                            trValues[maxColor] += diff * tr;
                            brValues[maxColor] += diff * br;
                            blValues[maxColor] += diff * bl;
                        }
                    }
                }
            }
        }
        return total / 4;
    }

    template <typename TInput, typename TOutput> void ApplyHistogram(FramePlane& input, IInterpolator^ interpolator, Nullable<int> seed)
    {
        auto inData = static_cast<TInput*>(input.pointer);
        auto outData = static_cast<TOutput*>(pointer);
        const auto inStride = input.stride;
        if (byteDepth == 4)
        {
            if (pixelSize == 1)
            {
                for (auto y = 0; y < height; y++, inData += inStride, outData += stride)
                {
                    for (auto x = 0; x < width; x++)
                    {
                        outData[x] = interpolator->Interpolate(inData[x]);
                    }
                }
            }
        	else
            {
                for (auto y = 0; y < height; y++, inData += inStride, outData += stride)
                {
                    for (auto x = 0; x < width; x += pixelSize)
                    {
                        outData[x] = interpolator->Interpolate(inData[x]);
                    }
                }
            }
        }
        else
        {
            auto random = seed.HasValue ? NativeRandom(seed.Value) : NativeRandom();
            for (auto y = 0; y < height; y++, inData += inStride, outData += stride)
            {
                for (auto x = 0; x < width; x += pixelSize)
                {
                    TInput& src = inData[x];
                    const auto interpolated = interpolator->Interpolate(src);
                    const auto floor = std::floor(interpolated);
                    const auto diff = interpolated - floor;
                    if (diff < EPSILON)
                    {
                        outData[x] = floor;
                    }
                    else
                    {
                        outData[x] = random.NextDouble() > diff ? floor : floor + 1;
                    }
                }
            }
        }
    }

    template <typename TInput, typename TOutput> void ApplyLut(FramePlane& input, Lut^ lut, Nullable<int> seed)
    {
        auto inData = static_cast<TInput*>(input.pointer);
        auto outData = static_cast<TOutput*>(pointer);
        const auto inStride = input.stride;
        const auto random = seed.HasValue ? new NativeRandom(seed.Value) : new NativeRandom();
        for (auto y = 0; y < height; y++, inData += inStride, outData += stride)
        {
            for (auto x = 0; x < width; x += pixelSize)
            {
                outData[x] = lut->Interpolate(inData[x], random);
            }
        }
        delete random;
    }
};
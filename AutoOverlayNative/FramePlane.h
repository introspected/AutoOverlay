#pragma once
#include "PlaneChannel.h"
using namespace AvsFilterNet;
using namespace AutoOverlay;

public value class FramePlane
{
public:
    void* pointer;
    int bitDepth;
    int byteDepth;
    int pixelSize;
    int width;
    int height;
    int pixelCount;
    int stride;

    FramePlane(PlaneChannel^ planeChannel, VideoFrame^ frame, bool read)
    {
        auto ptr = read ? frame->GetReadPtr(planeChannel->Plane) : frame->GetWritePtr(planeChannel->Plane);
        bitDepth = planeChannel->Depth;
        byteDepth = bitDepth / 8 + (bitDepth % 8 == 0 ? 0 : 1);
        pixelSize = planeChannel->PixelSize;
        pointer = static_cast<unsigned char*>(ptr.ToPointer()) + planeChannel->ChannelOffset * byteDepth;
        height = frame->GetHeight(planeChannel->Plane);
        width = frame->GetRowSize(planeChannel->Plane) / byteDepth;
        stride = frame->GetPitch(planeChannel->Plane) / byteDepth;
        pixelCount = width * height;
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
            plane->height = i == count - 1 ? height - minHeight * i : minHeight;
            plane->pixelCount = pixelCount;
            plane->stride = stride;
            planes->SetValue(plane, i);
        }
        return planes;
    }
};
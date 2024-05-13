#include "PlaneChannel.h"
using namespace AvsFilterNet;

namespace AutoOverlay
{
	YUVPlanes PlaneChannel::Plane::get()
	{
		return plane;
	}

	void PlaneChannel::Plane::set(YUVPlanes plane)
	{
		PlaneChannel::plane = plane;
	}

	YUVPlanes PlaneChannel::EffectivePlane::get()
	{
		return effectivePlane;
	}

	void PlaneChannel::EffectivePlane::set(YUVPlanes effectivePlane)
	{
		PlaneChannel::effectivePlane = effectivePlane;
	}

	int PlaneChannel::ChannelOffset::get()
	{
		return channelOffset;
	}

	void PlaneChannel::ChannelOffset::set(int channelOffset)
	{
		PlaneChannel::channelOffset = channelOffset;
	}

	int PlaneChannel::PixelSize::get()
	{
		return pixelSize;
	}

	void PlaneChannel::PixelSize::set(int pixelSize)
	{
		PlaneChannel::pixelSize = pixelSize;
	}

	int PlaneChannel::Depth::get()
	{
		return depth;
	}

	void PlaneChannel::Depth::set(int depth)
	{
		PlaneChannel::depth = depth;
	}

	PlaneChannel::PlaneChannel(YUVPlanes plane, YUVPlanes effectivePlane, int channelOffset, int pixelSize, int depth)
	{
		PlaneChannel::plane = plane;
		PlaneChannel::effectivePlane = effectivePlane;
		PlaneChannel::channelOffset = channelOffset;
		PlaneChannel::pixelSize = pixelSize;
		PlaneChannel::depth = depth;
	}
}

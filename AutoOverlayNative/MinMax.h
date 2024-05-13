#pragma once
#include "PlaneChannel.h"
#include <tuple>

namespace AutoOverlay
{
	public ref class MinMax
	{
	public:
		property double Min;
		property double Max;

		MinMax(int depth);
		MinMax(PlaneChannel^ planeChannel, VideoFrame^ frame);
	};
}


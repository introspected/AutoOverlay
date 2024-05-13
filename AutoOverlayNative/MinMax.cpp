#include "Stdafx.h"

namespace AutoOverlay
{
	MinMax::MinMax(const int depth)
	{
		Min = 0;
		Max = (1 << depth) - 1;
	}

	MinMax::MinMax(PlaneChannel^ planeChannel, VideoFrame^ frame)
	{
		const auto framePlane = new FramePlane(planeChannel, frame, true);

		std::tuple<double, double> minMax;
		switch (planeChannel->Depth)
		{
		case 8:
			minMax = NativeUtils::FindMinMax<unsigned char>(*framePlane);
			break;
		case 10:
		case 12:
		case 14:
		case 16:
			minMax = NativeUtils::FindMinMax<unsigned short>(*framePlane);
			break;
		case 32:
			minMax = NativeUtils::FindMinMax<float>(*framePlane);
			break;
		default: gcnew InvalidOperationException();
		}
		Min = std::get<0>(minMax);
		Max = std::get<1>(minMax);
	}
}
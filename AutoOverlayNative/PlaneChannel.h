#pragma once
using namespace AvsFilterNet;

namespace AutoOverlay
{
	public ref class PlaneChannel sealed
	{
		YUVPlanes plane;
		YUVPlanes effectivePlane;
		int channelOffset;
		int pixelSize;
		int depth;
	public:
		property YUVPlanes Plane
		{
			YUVPlanes get();
		private:
			void set(YUVPlanes plane);
		}
		property YUVPlanes EffectivePlane
		{
			YUVPlanes get();
		private:
			void set(YUVPlanes effectivePlane);
		}
		property int ChannelOffset
		{
			int get();
		private:
			void set(int channelOffset);
		}
		property int PixelSize
		{
			int get();
		private:
			void set(int pixelSize);
		}
		property int Depth
		{
			int get();
		private:
			void set(int depth);
		}
		PlaneChannel(YUVPlanes plane, YUVPlanes effectivePlane, int channelOffset, int pixelSize, int depth);
	};
}

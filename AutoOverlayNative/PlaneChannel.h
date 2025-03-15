#pragma once
using namespace AvsFilterNet;

namespace AutoOverlay
{
	public ref class PlaneChannel : System::IEquatable<PlaneChannel^>
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

		int GetHashCode() override
		{
			int seed = 0x2ECE6204;
			seed ^= (seed << 6) + (seed >> 2) + 0x19E078F4 + static_cast<int>(plane);
			seed ^= (seed << 6) + (seed >> 2) + 0x414E6FC3 + static_cast<int>(effectivePlane);
			seed ^= (seed << 6) + (seed >> 2) + 0x765EB621 + channelOffset;
			seed ^= (seed << 6) + (seed >> 2) + 0x4E02CA31 + pixelSize;
			seed ^= (seed << 6) + (seed >> 2) + 0x2CB76AFC + depth;
			return seed;
		}

		virtual bool Equals(PlaneChannel^ other)
		{
			return !ReferenceEquals(other, nullptr)
				&& plane == other->plane
				&& effectivePlane == other->effectivePlane
				&& channelOffset == other->channelOffset
				&& pixelSize == other->pixelSize
				&& depth == other->depth;
		}

		bool Equals(Object^ obj) override
		{
			return Equals(dynamic_cast<PlaneChannel^>(obj));
		}
	};
}

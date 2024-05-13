#pragma once

namespace AutoOverlay
{
	public ref class FastRandom sealed
	{
	private:
		uint32_t seed;

	public:
		FastRandom();
		FastRandom(int seed);

		int Next();
		int Next(int limit);
		double NextDouble();
	};
}
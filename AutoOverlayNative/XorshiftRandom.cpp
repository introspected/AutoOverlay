#include "Stdafx.h"

namespace AutoOverlay
{
	const double UINT_MAX_R = (double)std::numeric_limits<uint32_t>::max();

	XorshiftRandom::XorshiftRandom() : seed(1)
	{

	}

	XorshiftRandom::XorshiftRandom(int seed) : seed(seed)
	{

	}

	inline int XorshiftRandom::Next()
	{
		uint32_t x = seed;
		x ^= x << 13;
		x ^= x >> 17;
		x ^= x << 5;
		seed = x;
		return x;
	}

	inline double XorshiftRandom::NextDouble()
	{
		return (unsigned int)Next() / UINT_MAX_R;
	}
}
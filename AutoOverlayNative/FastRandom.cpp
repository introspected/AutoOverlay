#include "Stdafx.h"

namespace AutoOverlay
{
	FastRandom::FastRandom() : seed(std::rand())
	{

	}

	FastRandom::FastRandom(int seed) : seed(seed)
	{

	}

	inline int FastRandom::Next()
	{
		seed = (214013 * seed + 2531011);
		return (seed >> 16) & 0x7FFF;
	}

	inline int FastRandom::Next(int limit)
	{
		return Next() % limit;
	}

	inline double FastRandom::NextDouble()
	{
		return static_cast<unsigned int>(Next()) / 32768.0;
	}
}
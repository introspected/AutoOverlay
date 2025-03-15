#pragma once

#include "NativeRandom.h"

namespace AutoOverlay
{
	public ref class FastRandom sealed
	{
		NativeRandom* native;

	public:
		FastRandom() : native(new NativeRandom(std::rand()))
		{
			
		}

		FastRandom(int seed) : native(new NativeRandom(seed))
		{
			
		}

		int Next()
		{
			return native->Next();
		}

		int Next(int limit)
		{
			return native->Next(limit);
		}

		double NextDouble()
		{
			return native->NextDouble();
		}

		~FastRandom()
		{
			this->!FastRandom();
		}

		!FastRandom()
		{
			if (native)
			{
				delete native;
				native = nullptr;
			}
		}
	};
}

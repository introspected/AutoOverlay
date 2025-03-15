#pragma once

#include "IInterpolator.h"
#include "NativeInterpolator.h"

namespace AutoOverlay
{
	public ref class ManagedInterpolator sealed : public IInterpolator
	{
		NativeInterpolator* native;

	public:
		ManagedInterpolator(array<double>^ x, array<double>^ y)
		{
			native = new NativeInterpolator(x, y);
		}

		virtual double Interpolate(double t)
		{
			return native->Interpolate(t);
		}

		NativeInterpolator GetNative()
		{
			return *native;
		}

		~ManagedInterpolator()
		{
			this->!ManagedInterpolator();
		}

		!ManagedInterpolator()
		{
			if (native)
			{
				delete native;
				native = nullptr;
			}
		}
	};
}

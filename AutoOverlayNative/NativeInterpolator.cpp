#include "Stdafx.h"

NativeInterpolator::NativeInterpolator(array<double>^ x, array<double>^ y)
{
	pin_ptr<double> pinned = &x[0];
	xValues.assign(static_cast<double*>(pinned), static_cast<double*>(pinned) + x->Length);
	pinned = &y[0];
	yValues.assign(static_cast<double*>(pinned), static_cast<double*>(pinned) + y->Length);

	Init();
}

NativeInterpolator::NativeInterpolator(const std::vector<double>& x, const std::vector<double>& y) : xValues(x), yValues(y)
{
	Init();
}

void NativeInterpolator::Init()
{
	cValues.resize(xValues.size() - 1);

	std::ranges::sort(xValues);
	std::ranges::sort(yValues);

	for (auto i = 0; i < cValues.size(); i++) {
		cValues[i] = (yValues[i + 1] - yValues[i]) / (xValues[i + 1] - xValues[i]);
	}
	cLastIndex = cValues.size() - 1;
}

inline double NativeInterpolator::Interpolate(double t) const noexcept
{
	if (t <= xValues.front())
		return yValues.front();

	if (t >= xValues.back())
		return yValues.back();

	const auto it = std::ranges::upper_bound(xValues, t);
	int index = std::distance(xValues.begin(), it) - 1;
	return yValues[index] + (t - xValues[index]) * cValues[index];
}
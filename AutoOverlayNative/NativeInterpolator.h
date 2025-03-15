#pragma once

public class NativeInterpolator sealed
{
	std::vector<double> xValues;
	std::vector<double> yValues;
	std::vector<double> cValues;
	int cLastIndex;

	void Init();

public:
	NativeInterpolator(array<double>^ x, array<double>^ y);
	NativeInterpolator(const std::vector<double>& x, const std::vector<double>& y);

	inline double Interpolate(double t) const noexcept;
};
#pragma once

public value class CornerGradient sealed
{
public:
	double topLeft;
	double topRight;
	double bottomRight;
	double bottomLeft;

	static CornerGradient Of(double tl, double tr, double br, double bl)
	{
		auto gradient = new CornerGradient();
		gradient->topLeft = tl;
		gradient->topRight = tr;
		gradient->bottomRight = br;
		gradient->bottomLeft = bl;
		return *gradient;
	}

	CornerGradient Rotate()
	{
		auto gradient = new CornerGradient();
		gradient->topLeft = bottomLeft;
		gradient->topRight = topLeft;
		gradient->bottomRight = topRight;
		gradient->bottomLeft = bottomRight;
		return *gradient;
	}
};
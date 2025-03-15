#pragma once
#include <algorithm>
#include <vector>
#include <numeric>

#include "NativeInterpolator.h"

using namespace System::Collections::Generic;

public ref class Lut sealed
{
	std::vector<int>* fixedColors;
	std::vector<std::vector<int>>* dynamicColors;
	std::vector<std::vector<double>>* dynamicWeights;

public:
	Lut(array<double>^ sampleHistogram, array<double>^ referenceHistogram, const double dither, const double intensity, const double exclude)
	{
		auto length = sampleHistogram->Length;
		fixedColors = new std::vector(length, -1);
		dynamicColors = new std::vector<std::vector<int>>(length);
		dynamicWeights = new std::vector<std::vector<double>>(length);

		pin_ptr<double> samplePtr = &sampleHistogram[0];
		pin_ptr<double> refPtr = &referenceHistogram[0];
		const auto sample = static_cast<double*>(samplePtr);
		const auto reference = static_cast<double*>(refPtr);
		const auto refLength = referenceHistogram->Length;
		const auto maxColor = length - 1;
		const auto backIntensity = 1 - intensity;
		const auto refMaxColor = refLength - 1;

		double restPixels = 0, maxWeight = 0, sumWeight = 0;
		for (int newColor = 0, oldColor = 0; newColor < refLength; newColor++)
		{
			auto refCount = reference[newColor];
			while (refCount > 0)
			{
				const auto add = std::min(restPixels, refCount);
				auto& weights = (*dynamicWeights)[oldColor];
				if (add > 0 && sample[oldColor] > 0)
				{
					if (add > 0 && sample[oldColor] > exclude && reference[newColor] > exclude)
					{
						const auto targetColor = newColor * intensity + oldColor * backIntensity;
						const auto firstColor = static_cast<int>(targetColor);
						const auto secondColor = firstColor + 1;
						const auto secondWeight = targetColor - firstColor;
						const auto firstWeight = 1 - secondWeight;
						std::vector<int> targetColors;
						std::vector<double> targetWeights;
						if (secondWeight <= 0 || secondColor >= refMaxColor)
						{
							targetColors = { firstColor };
							targetWeights = { 1 };
						}
						else if (firstWeight <= 0)
						{
							targetColors = { secondColor };
							targetWeights = { 1 };
						}
						else 
						{
							targetColors = { firstColor, secondColor };
							targetWeights = { firstWeight, secondWeight };
						}

						for (auto i = 0; i < targetColors.size(); i++)
						{
							const auto targetColor = targetColors[i];
							const auto targetWeight = targetWeights[i];
							const auto weight = (add / sample[oldColor]) * targetWeight;
							if (weight >= dither && weight > maxWeight)
							{
								(*fixedColors)[oldColor] = targetColor;
								maxWeight = weight;
							}
							weights.push_back(weight);
							sumWeight += weight;
							(*dynamicColors)[oldColor].push_back(targetColor);
						}
					}
					refCount -= add;
					restPixels -= add;
				}
				else
				{
					if (!weights.empty()) {
						weights[weights.size() - 1] += 1 - sumWeight;
					}
					sumWeight = maxWeight = 0.0;
					if (oldColor == maxColor) {
						restPixels = 0;
						break;
					}
					restPixels = sample[++oldColor];
				}
			}
		}

		if ((*dynamicColors)[0].empty()) 
		{
			(*fixedColors)[0] = 0;
			(*dynamicColors)[0].assign(1, 0);
			(*dynamicWeights)[0].assign(1, 1);
		}
		if ((*dynamicColors)[maxColor].empty())
		{
			(*fixedColors)[maxColor] = refMaxColor;
			(*dynamicColors)[maxColor].assign(1, refMaxColor);
			(*dynamicWeights)[maxColor].assign(1, 1);
		}

		std::vector<double> sampleColors;
		std::vector<double> referenceColors;

		sampleColors.push_back(0);
		sampleColors.push_back(length - 1);
		referenceColors.push_back(0);
		referenceColors.push_back(refMaxColor);

		for (int color = 0; color < maxColor; color++)
		{
			auto& colors = (*dynamicColors)[color];
			auto& weights = (*dynamicWeights)[color];
			if (!colors.empty())
			{
				sampleColors.push_back(color);
				double refColor = 0;
				for (auto i = 0; i < colors.size(); i++) 
				{
					refColor += colors[i] * weights[i];
				}
				referenceColors.push_back(refColor);
			}
		}
		auto interpolator = NativeInterpolator(sampleColors, referenceColors);

		for (int color = 0; color < length; color++)
		{
			auto& colors = (*dynamicColors)[color];
			auto& weights = (*dynamicWeights)[color];
			if (colors.empty())
			{
				const auto interpolated = interpolator.Interpolate(color);
				const auto firstColor = static_cast<int>(interpolated);
				const auto secondColor = firstColor + 1;
				const auto secondWeight = interpolated - firstColor;
				auto firstWeight = 1 - secondWeight;

				if (secondWeight > 0 && secondColor < refLength)
				{
					colors.push_back(secondColor);
					weights.push_back(secondWeight);
				}

				colors.push_back(firstColor);
				weights.push_back(1);

				if (firstWeight > secondWeight && firstWeight >= dither)
				{
					(*fixedColors)[color] = firstColor;
				}
				else if (secondWeight >= dither)
				{
					(*fixedColors)[color] = secondColor;
				}
			}
			else
			{
				double weight = 0.0;
				for (auto i = 0; i < weights.size(); i++) {
					weight += weights[i];
					weights[i] = weight;
				}
				weights[weights.size() - 1] = 1;
			}
		}
	}

	~Lut()
	{
		this->!Lut();
	}

	!Lut()
	{
		if (fixedColors)
		{
			delete fixedColors;
			delete dynamicColors;
			delete dynamicWeights;
			fixedColors = nullptr;
			dynamicColors = nullptr;
			dynamicWeights = nullptr;
		}
	}

	int Interpolate(const int oldColor, NativeRandom* random)
	{
		const int& newColor = (*fixedColors)[oldColor];
		if (newColor == -1)
		{
			const auto weight = random->NextDouble();
			const auto& colors = (*dynamicColors)[oldColor];
			const auto& weights = (*dynamicWeights)[oldColor];

			const auto it = std::ranges::lower_bound(weights, weight);
			const auto index = it - weights.begin();
			return colors[index];
		}
		return newColor;
	}
};

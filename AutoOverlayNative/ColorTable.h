#pragma once
#include <vector>

using namespace System::Collections::Generic;

public ref class ColorTable sealed
{
	std::vector<int>* fixedColors;
	std::vector<std::vector<int>>* dynamicColors;
	std::vector<std::vector<double>>* dynamicWeights;

public:
	ColorTable(array<int>^ fixedMap, array<Dictionary<int, double>^>^ dynamicMap)
	{
		auto length = fixedMap->Length;
		fixedColors = new std::vector<int>(length);
		dynamicColors = new std::vector<std::vector<int>>(length);
		dynamicWeights = new std::vector<std::vector<double>>(length);

		pin_ptr<int> pinned = &fixedMap[0];
		fixedColors->assign(static_cast<int*>(pinned), static_cast<int*>(pinned) + length);

		for (int color = 0; color < length; color++)
		{
			if ((*fixedColors)[color] >= 0) continue;
			auto map = dynamicMap[color];
			auto& colors = (*dynamicColors)[color];
			colors.resize(map->Count);
			auto& weights = (*dynamicWeights)[color];
			weights.resize(map->Count);
			int i = 0;
			double prev = 0;
			for each (auto pair in map)
			{
				colors[i] = pair.Key;
				prev = weights[i++] = prev + pair.Value;
			}
			weights[i - 1] += 1 - prev;
		}
	}

	~ColorTable()
	{
		this->!ColorTable();
	}

	!ColorTable()
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

	template <typename TInputColor, typename TOutputColor> TOutputColor Map(TInputColor oldColor, NativeRandom* random)
	{
		int& newColor = (*fixedColors)[oldColor];
		if (newColor == -1)
		{
			auto weight = random->NextDouble();
			std::vector<int>& colors = (*dynamicColors)[oldColor];
			std::vector<double>& weights = (*dynamicWeights)[oldColor];

			auto it = std::ranges::lower_bound(weights, weight);
			return colors[it - weights.begin()];
		}
		return newColor;
	}
};

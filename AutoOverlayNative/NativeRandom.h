#pragma once

#include <array>
#include <limits>
#include <random>

#include "DeterministicEngine.h"

class NativeRandom final {
    std::array<uint64_t, 2> state;

    static constexpr double MAX_INV = 1.0 / std::numeric_limits<uint64_t>::max();

    static inline uint64_t splitmix64(uint64_t& seed) noexcept {
        uint64_t z = (seed += 0x9E3779B97f4A7C15ULL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9ULL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBULL;
        return z ^ (z >> 31);
    }

    static constexpr std::size_t NORMAL_TABLE_SIZE = 1024;
    static const std::vector<double>& LUT()
    {
        static const std::vector<double>& lut = createLUT();
        return lut;
    }

    static std::vector<double> createLUT() {
        DeterministicEngine engine(NORMAL_TABLE_SIZE);
        std::uniform_real_distribution<double> dist(0.0, 1.0);

        std::vector<double> table;
        table.reserve(NORMAL_TABLE_SIZE);

        for (unsigned int i = 0; i < NORMAL_TABLE_SIZE; ++i) {
            table.push_back(dist(engine));
        }
        return table;
    }

public:
    explicit NativeRandom(uint64_t seed = std::rand()) noexcept {
        state[0] = splitmix64(seed);
        state[1] = splitmix64(seed);
    }

    inline uint64_t operator()() noexcept {
        uint64_t s1 = state[0];
        uint64_t s0 = state[1];
        state[0] = s0;
        s1 ^= s1 << 23;
        state[1] = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
        return state[1] + s0;
    }

    inline int Next() noexcept {
        return static_cast<int>((*this)() >> 33);
    }

    inline int Next(int limit) noexcept {
        const uint64_t random = (*this)() >> 16;
        return static_cast<int>((random * limit) >> 48);
    }

    inline double NextDouble() noexcept {
        return operator()() * MAX_INV;
    }

    inline double NextNormal() noexcept {
        return LUT()[Next(NORMAL_TABLE_SIZE)];
    }
};

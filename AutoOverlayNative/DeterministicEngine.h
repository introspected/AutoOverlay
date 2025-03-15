#pragma once

struct DeterministicEngine {
    using result_type = unsigned int;
    unsigned int current;
    unsigned int N;

    DeterministicEngine(unsigned int N) : current(0), N(N) {}

    result_type operator()() {
        return current++;
    }

    static constexpr result_type min() {
        return 0;
    }

    result_type max() const {
        return N - 1;
    }
};

namespace AutoOverlay
{
	public ref class XorshiftRandom sealed
	{
	private:
		uint32_t seed;
	public:
		XorshiftRandom();
		XorshiftRandom(int seed);

		int Next();
		double NextDouble();
	};
}
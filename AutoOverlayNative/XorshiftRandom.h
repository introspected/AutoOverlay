namespace AutoOverlay
{
	public ref class XorshiftRandom sealed
	{
	private:
		unsigned int seed;
	public:
		XorshiftRandom();
		XorshiftRandom(int seed);

		int Next();
		double NextDouble();
	};
}
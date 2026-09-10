namespace Retag;

/// <summary>
/// Fixed-behaviour PRNG. Deliberately not System.Random: the fixtures must be
/// byte-identical across machines and runtimes, forever. xorshift64*.
/// </summary>
public sealed class Rng
{
    private ulong _s;

    public Rng(ulong seed) => _s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;

    public ulong Next()
    {
        _s ^= _s >> 12;
        _s ^= _s << 25;
        _s ^= _s >> 27;
        return _s * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>Uniform in [0, bound).</summary>
    public int Next(int bound) => bound <= 0 ? 0 : (int)(Next() % (ulong)bound);

    public int Next(int min, int max) => min + Next(max - min);

    public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];
}

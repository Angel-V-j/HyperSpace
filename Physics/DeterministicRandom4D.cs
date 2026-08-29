using System;

namespace HyperSpace.Physics;

/// <summary>
/// Small SplitMix64 generator whose sequence does not depend on System.Random versions.
/// </summary>
internal sealed class DeterministicRandom4D
{
    private ulong _state;

    public DeterministicRandom4D(int seed) => _state = unchecked((ulong)(long)seed);

    public double NextUnitDouble()
    {
        var bits = NextUInt64() >> 11;
        return bits * (1.0 / (1UL << 53));
    }

    public double NextDouble(double minimum, double maximum) =>
        minimum + ((maximum - minimum) * NextUnitDouble());

    private ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var value = _state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}

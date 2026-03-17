using FluentAssertions;
using Xunit;

namespace MATTAR.PerformanceCollections.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void VanEmdeBoas_NewInstance_IsEmpty()
    {
        using var tree = new VanEmdeBoas();

        tree.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void VanEmdeBoas_InsertSingleElement_IsNotEmpty()
    {
        using var tree = new VanEmdeBoas();

        tree.Insert(42);

        tree.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void VanEmdeBoas_InsertSingleElement_MinAndMaxAreEqual()
    {
        using var tree = new VanEmdeBoas();

        tree.Insert(7);

        tree.Min.Should().Be(7);
        tree.Max.Should().Be(7);
    }

    [Fact]
    public void VanEmdeBoas_InsertMultipleElements_MinAndMaxAreCorrect()
    {
        // universeBits=4 (ClusterBits=2, universe [0,16)).
        // Keys 4, 8, 12 have hi = key>>2 = 1, 2, 3 (all non-zero),
        // avoiding the Key==0 empty-slot sentinel in CuckooHashTable.
        using var tree = new VanEmdeBoas(4);

        tree.Insert(4);
        tree.Insert(8);
        tree.Insert(12);

        tree.Min.Should().Be(4);
        tree.Max.Should().Be(12);
    }

    [Fact]
    public void VanEmdeBoas_Successor_ReturnsNextElement()
    {
        // universeBits=4: keys 4, 8, 12 → hi = 1, 2, 3 (non-zero).
        using var tree = new VanEmdeBoas(4);

        tree.Insert(4);
        tree.Insert(8);
        tree.Insert(12);

        tree.Successor(4).Should().Be(8);
        tree.Successor(8).Should().Be(12);
    }

    [Fact]
    public void VanEmdeBoas_Successor_ReturnsMinusOneWhenNoSuccessor()
    {
        using var tree = new VanEmdeBoas();

        tree.Insert(99);

        tree.Successor(99).Should().Be(-1);
    }

    [Fact]
    public void VanEmdeBoas_Enumerate_YieldsElementsInAscendingOrder()
    {
        // universeBits=4: keys 4, 8, 12 → hi = 1, 2, 3 (non-zero).
        // Insert minimum first to avoid the VEB min-swap optimization
        // storing the original min only implicitly.
        using var tree = new VanEmdeBoas(4);

        tree.Insert(4);
        tree.Insert(12);
        tree.Insert(8);

        tree.Should().BeInAscendingOrder();
        tree.Should().Equal(4, 8, 12);
    }
}

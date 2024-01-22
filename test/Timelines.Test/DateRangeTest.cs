﻿using System.Collections.Immutable;

namespace Timelines.Test;

[TestClass]
public class DateRangeTest
{
    private static DateOnly D(int day) => new(2024, 1, day);
    private static DateRange DR(int start, int end) => new(D(start), D(end));
    private static DateRange DR(int start) => new(D(start));

    private static void AssertDatesAre(DateOnlyTimeline dut, params int[] dates)
    {
        // Set the max date to check to something past where we'll be testing.
        for (var i = 1; i < 20; i++)
            Assert.AreEqual(dates.Contains(i), dut.Contains(D(i)), $"Failed on {i}");
    }


    [TestMethod]
    public void DateRangeConstructorWithSingleValueSetsEndToMaxValue()
    {
        var dut = new DateRange(D(1));

        Assert.AreEqual(D(1), dut.Start);
        Assert.AreEqual(DateOnly.MaxValue, dut.End);
    }

    [TestMethod]
    public void DateRangeConstructorAllowsEqualValues()
    {
        var dut = new DateRange(D(1), D(2));

        Assert.AreEqual(D(1), dut.Start);
        Assert.AreEqual(D(2), dut.End);
    }

    [TestMethod]
    public void DateRangeConstructorAllowsEndAfterStart()
    {
        var dut = new DateRange(D(1), D(2));

        Assert.AreEqual(D(1), dut.Start);
        Assert.AreEqual(D(2), dut.End);
    }

    [TestMethod]
    public void DateRangeConstructorForbidsStartAfterEnd()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            new DateRange(D(2), D(1)));
    }

    [DataRow(1, 1, 1, true)]
    [DataRow(1, 1, 2, false)]
    [DataRow(1, 2, 1, true)]
    [DataRow(1, 2, 2, true)]
    [DataRow(1, 2, 3, false)]
    [DataRow(2, 2, 1, false)]
    [DataRow(2, 2, 2, true)]
    [DataRow(2, 4, 3, true)]
    [DataRow(2, 4, 4, true)]
    [DataRow(2, 4, 5, false)]
    [DataTestMethod]
    public void DateRangeContainsHandlesValues(
        int start, int end, int test, bool expected)
    {
        var dut = new DateRange(D(start), D(end));
        Assert.AreEqual(expected, dut.Contains(D(test)));
    }

    [TestMethod]
    public void IntersectionWithNullReturnsNull()
    {
        var dut = DR(1, 2);
        Assert.IsNull(dut.IntersectionWith(null));
    }

    [TestMethod]
    public void IntersectionWithDisjointRangesReturnsNull()
    {
        var dut = DR(1, 2);
        Assert.IsNull(dut.IntersectionWith(DR(3, 4)));
    }

    [TestMethod]
    public void IntersectionWithOverlappingRangesReturnsIntersection()
    {
        var dut = DR(1, 3);
        var result = dut.IntersectionWith(DR(2, 4));

        Assert.IsNotNull(result);
        Assert.AreEqual(D(2), result.Value.Start);
        Assert.AreEqual(D(3), result.Value.End);
    }

    [TestMethod]
    public void IntersectionWithOverlappingRangesReturnsIntersection2()
    {
        var dut = DR(1, 3);
        var result = dut.IntersectionWith(DR(2, 2));

        Assert.IsNotNull(result);
        Assert.AreEqual(D(2), result.Value.Start);
        Assert.AreEqual(D(2), result.Value.End);
    }

    [TestMethod]
    public void IntersectionWithOverlappingRangesReturnsIntersection3()
    {
        var dut = DR(2, 3);
        var result = dut.IntersectionWith(DR(1, 2));

        Assert.IsNotNull(result);
        Assert.AreEqual(D(2), result.Value.Start);
        Assert.AreEqual(D(2), result.Value.End);
    }

    [TestMethod]
    public void EqualsReturnsTrueForSameValues()
    {
        var dut = DR(1, 2);
        Assert.IsTrue(dut.Equals(DR(1, 2)));
    }

    [TestMethod]
    public void EqualsReturnsFalseForDifferentStart()
    {
        var dut = DR(1, 2);
        Assert.IsFalse(dut.Equals(DR(2, 2)));
    }

    [TestMethod]
    public void EqualsReturnsFalseForDifferentEnd()
    {
        var dut = DR(1, 2);
        Assert.IsFalse(dut.Equals(DR(1, 3)));
    }

    [TestMethod]
    public void EqualsReturnsFalseForDifferentStartAndEnd()
    {
        var dut = DR(1, 2);
        Assert.IsFalse(dut.Equals(DR(2, 3)));
    }

    [TestMethod]
    public void EqualsReturnsFalseForNull()
    {
        var dut = DR(1, 2);
        Assert.IsFalse(dut.Equals(null));
    }

    [TestMethod]
    public void EqualsReturnsFalseForDifferentType()
    {
        var dut = DR(1, 2);
        Assert.IsFalse(dut.Equals(1));
    }

    [TestMethod]
    public void GetHashCodeReturnsSameValueForSameValues()
    {
        var dut = DR(1, 2);
        Assert.AreEqual(dut.GetHashCode(), DR(1, 2).GetHashCode());
    }

    [TestMethod]
    public void GetHashCodeReturnsDifferentValueForDifferentStart()
    {
        var dut = DR(1, 2);
        Assert.AreNotEqual(dut.GetHashCode(), DR(2, 2).GetHashCode());
    }

    [TestMethod]
    public void GetHashCodeReturnsDifferentValueForDifferentEnd()
    {
        var dut = DR(1, 2);
        Assert.AreNotEqual(dut.GetHashCode(), DR(1, 3).GetHashCode());
    }

    [TestMethod]
    public void GetHashCodeReturnsDifferentValueForDifferentStartAndEnd()
    {
        var dut = DR(1, 2);
        Assert.AreNotEqual(dut.GetHashCode(), DR(2, 3).GetHashCode());
    }

    [TestMethod]
    public void ToStringReturnsExpectedValue()
    {
        var dut = DR(1, 2);
        Assert.AreEqual("20240101-20240102", dut.ToString());
    }

    [TestMethod]
    public void ToStringReturnsExpectedValueForSingleDay()
    {
        var dut = DR(1, 1);
        Assert.AreEqual("20240101-20240101", dut.ToString());
    }

    [TestMethod]
    public void ToStringReturnsExpectedValueForMaxValue()
    {
        var dut = DR(1);
        Assert.AreEqual("20240101-99991231", dut.ToString());
    }

    [TestMethod]
    public void TaggedDateRangeConstructorWithSingleValueSetsEndToMaxValue()
    {
        var dut = new DateRange<char>(D(1), 'A');

        Assert.AreEqual(D(1), dut.Start);
        Assert.AreEqual(DateOnly.MaxValue, dut.End);
        Assert.AreEqual('A', dut.Tag);
    }

    [TestMethod]
    public void TaggedDateRangeConstructorAllowsEqualValues()
    {
        var dut = new DateRange<char>(D(1), D(2), 'A');

        Assert.AreEqual(D(1), dut.Start);
        Assert.AreEqual(D(2), dut.End);
        Assert.AreEqual('A', dut.Tag);
    }

    [TestMethod]
    public void TaggedDateRangeConstructorAllowsEndAfterStart()
    {
        var dut = new DateRange<char>(D(1), D(2), 'A');

        Assert.AreEqual(D(1), dut.Start);
        Assert.AreEqual(D(2), dut.End);
        Assert.AreEqual('A', dut.Tag);
    }

    [TestMethod]
    public void TaggedDateRangeConstructorForbidsStartAfterEnd()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            new DateRange<char>(D(2), D(1), 'A'));
    }

    [DataRow(1, 1, 1, true)]
    [DataRow(1, 1, 2, false)]
    [DataRow(1, 2, 1, true)]
    [DataRow(1, 2, 2, true)]
    [DataRow(1, 2, 3, false)]
    [DataRow(2, 2, 1, false)]
    [DataRow(2, 2, 2, true)]
    [DataRow(2, 4, 3, true)]
    [DataRow(2, 4, 4, true)]
    [DataRow(2, 4, 5, false)]
    [DataTestMethod]
    public void TaggedDateRangeContainsHandlesValues(
        int start, int end, int test, bool expected)
    {
        var dut = new DateRange<char>(D(start), D(end), 'A');
        Assert.AreEqual(expected, dut.Contains(D(test)));
    }

    [TestMethod]
    public void TaggedToStringReturnsExpectedValue()
    {
        var dut = new DateRange<char>(D(1), D(2), 'A');
        Assert.AreEqual("A:20240101-20240102", dut.ToString());
    }

    [TestMethod]
    public void TaggedToStringReturnsExpectedValueForSingleDay()
    {
        var dut = new DateRange<char>(D(1), D(1), 'A');
        Assert.AreEqual("A:20240101-20240101", dut.ToString());
    }

    [TestMethod]
    public void TaggedToStringReturnsExpectedValueForMaxValue()
    {
        var dut = new DateRange<char>(D(1), DateOnly.MaxValue, 'A');
        Assert.AreEqual("A:20240101-99991231", dut.ToString());
    }
}

using NUnit.Framework;
using Core;

namespace Tests.EditMode
{
    public class RingIndexTests
    {
        [Test]
        public void Wrap_NormalisesAnyIndexIntoRange()
        {
            Assert.AreEqual(0, RingIndex.Wrap(0, 3));
            Assert.AreEqual(2, RingIndex.Wrap(2, 3));
            Assert.AreEqual(0, RingIndex.Wrap(3, 3));   // one past the end wraps to the start
            Assert.AreEqual(1, RingIndex.Wrap(4, 3));
        }

        [Test]
        public void Wrap_HandlesNegativeIndices()
        {
            Assert.AreEqual(2, RingIndex.Wrap(-1, 3));  // one before the start wraps to the end
            Assert.AreEqual(2, RingIndex.Wrap(-4, 3));  // even a full loop backwards
        }

        [Test]
        public void Next_StepsForwardAndWraps()
        {
            Assert.AreEqual(1, RingIndex.Next(0, 3, 1));
            Assert.AreEqual(0, RingIndex.Next(2, 3, 1));   // last → first (right-arrow wrap)
        }

        [Test]
        public void Next_StepsBackwardAndWraps()
        {
            Assert.AreEqual(2, RingIndex.Next(0, 3, -1));  // first → last (left-arrow wrap)
            Assert.AreEqual(0, RingIndex.Next(1, 3, -1));
        }

        [Test]
        public void SingleItemRing_AlwaysWrapsToZero()
        {
            Assert.AreEqual(0, RingIndex.Next(0, 1, 1));
            Assert.AreEqual(0, RingIndex.Next(0, 1, -1));
        }

        [Test]
        public void EmptyRing_ReturnsZeroWithoutDividingByZero()
        {
            // A future caller might not guard against count == 0; RingIndex must not throw.
            Assert.AreEqual(0, RingIndex.Wrap(5, 0));
            Assert.AreEqual(0, RingIndex.Next(0, 0, 1));
        }
    }
}
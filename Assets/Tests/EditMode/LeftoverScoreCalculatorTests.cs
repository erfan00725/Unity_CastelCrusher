using System.Collections.Generic;
using NUnit.Framework;
using Core;

namespace Tests.EditMode
{
    public class LeftoverScoreCalculatorTests
    {
        [Test]
        public void DefaultMultiplier_CountsLeftover()
        {
            var entries = new List<(int remaining, float multiplier)> { (3, 1f) };
            float result = LeftoverScoreCalculator.Compute(entries, 100f);
            Assert.AreEqual(300f, result);
        }

        [Test]
        public void ZeroMultiplier_ContributesNothing()
        {
            var entries = new List<(int remaining, float multiplier)>
            {
                (2, 0f),
                (1, 2f)
            };
            float result = LeftoverScoreCalculator.Compute(entries, 100f);
            Assert.AreEqual(200f, result);
        }

        [Test]
        public void EmptyEntries_ReturnZero()
        {
            float result = LeftoverScoreCalculator.Compute(new List<(int, float)>(), 100f);
            Assert.AreEqual(0f, result);
        }
    }

}
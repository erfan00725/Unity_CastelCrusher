using NUnit.Framework;
using Core;

namespace Tests.EditMode
{
    public class CarouselLayoutCalculatorTests
    {
        [Test]
        public void AvailableSideWidth_HalfRemainder()
        {
            Assert.AreEqual(450f, CarouselLayoutCalculator.AvailableSideWidth(1000f, 100f));
        }

        [Test]
        public void AvailableSideWidth_NeverNegative()
        {
            Assert.AreEqual(0f, CarouselLayoutCalculator.AvailableSideWidth(50f, 100f));
        }

        [Test]
        public void VisibleSideButtons_WideScreen_CapsAtMax()
        {
            Assert.AreEqual(3, CarouselLayoutCalculator.VisibleSideButtons(450f, 100f, 1, 3));
        }

        [Test]
        public void VisibleSideButtons_NarrowScreen_FloorsAtMin()
        {
            Assert.AreEqual(1, CarouselLayoutCalculator.VisibleSideButtons(40f, 100f, 1, 3));
        }

        [Test]
        public void VisibleSideButtons_ZeroButtonWidth_FloorsAtMin()
        {
            Assert.AreEqual(1, CarouselLayoutCalculator.VisibleSideButtons(100f, 0f, 1, 3));
        }
    }
}
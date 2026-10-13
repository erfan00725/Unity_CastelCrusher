using NUnit.Framework;
using Core;

namespace Tests.EditMode 
{
    public class CarouselWindowTests
    {
        [Test]
        public void ThreeTypes_AllShown()
        {
            int[] window = CarouselWindow.Window(1, 3, 3);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, window);
        }

        [Test]
        public void WindowWrapsAtStart()
        {
            int[] window = CarouselWindow.Window(0, 5, 1);
            CollectionAssert.AreEqual(new[] { 4, 0, 1 }, window);
        }

        [Test]
        public void WindowWrapsAtEnd()
        {
            int[] window = CarouselWindow.Window(4, 5, 1);
            CollectionAssert.AreEqual(new[] { 3, 4, 0 }, window);
        }

        [Test]
        public void CenterOfLargeWindow()
        {
            int[] window = CarouselWindow.Window(2, 10, 2);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, window);
        }

        [Test]
        public void Empty_ReturnsEmpty()
        {
            Assert.AreEqual(0, CarouselWindow.Window(0, 0, 2).Length);
        }
    }
}
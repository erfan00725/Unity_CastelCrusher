using NUnit.Framework;
using Core;

namespace Tests.EditMode
{
    public class CarouselWindowTests
    {
        // The window slot where the selected type must always sit. Mirrors
        // CarouselWindow.Window and the UI's own `middle` computation.
        private static int Middle(int[] window) => (window.Length - 1) / 2;

        [Test]
        public void SelectingLastType_RotatesItToCenter()
        {
            // 3 types fit entirely; selecting index 2 must still rotate the
            // row so it lands in the middle slot - not stay at the end.
            int[] window = CarouselWindow.Window(2, 3, 3);
            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, window);
            Assert.AreEqual(2, window[Middle(window)]);
        }

        [Test]
        public void SelectingFirstType_RotatesItToCenter()
        {
            int[] window = CarouselWindow.Window(0, 3, 3);
            CollectionAssert.AreEqual(new[] { 2, 0, 1 }, window);
            Assert.AreEqual(0, window[Middle(window)]);
        }

        [Test]
        public void SelectedType_IsMiddle_ForWrappedWindow()
        {
            int[] window = CarouselWindow.Window(3, 5, 1);
            CollectionAssert.AreEqual(new[] { 2, 3, 4 }, window);
            Assert.AreEqual(3, window[Middle(window)]);
        }

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

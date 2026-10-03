using UnityEngine;

namespace Core
{
    public static class CarouselLayoutCalculator
    {
        public static float AvailableSideWidth(float totalWidth, float centerButtonWidth)
        {
            return Mathf.Max(0f, (totalWidth - centerButtonWidth) * 0.5f);
        }

        public static int VisibleSideButtons(float availableSideWidth, float buttonWidth, int minVisible, int maxVisible)
        {
            if (buttonWidth <= 0f) return minVisible;
            int count = Mathf.FloorToInt(availableSideWidth / buttonWidth);
            return Mathf.Clamp(count, minVisible, maxVisible);
        }
    }
}
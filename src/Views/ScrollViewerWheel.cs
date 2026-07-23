using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace SPConverter.Views;

internal static class ScrollViewerWheel
{
    private const double WheelStep = 48;

    public static void ScrollByFixedStep(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || e.Delta == 0)
        {
            return;
        }

        double direction = e.Delta > 0 ? -1 : 1;
        double targetOffset = Math.Clamp(
            scrollViewer.VerticalOffset + direction * WheelStep,
            0,
            scrollViewer.ScrollableHeight);

        scrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
    }
}

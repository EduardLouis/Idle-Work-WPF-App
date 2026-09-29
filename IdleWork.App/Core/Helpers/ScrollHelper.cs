// [v0.2: ScrollHelper] Attached behavior to bubble mouse wheel events from nested ListViews to parent ScrollViewers
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace IdleWork.App.Core.Helpers
{
    public static class ScrollHelper
    {
        public static readonly DependencyProperty BubbleMouseWheelProperty =
            DependencyProperty.RegisterAttached(
                "BubbleMouseWheel",
                typeof(bool),
                typeof(ScrollHelper),
                new PropertyMetadata(false, OnBubbleMouseWheelChanged));

        public static bool GetBubbleMouseWheel(DependencyObject obj)
        {
            return (bool)obj.GetValue(BubbleMouseWheelProperty);
        }

        public static void SetBubbleMouseWheel(DependencyObject obj, bool value)
        {
            obj.SetValue(BubbleMouseWheelProperty, value);
        }

        private static void OnBubbleMouseWheelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                if ((bool)e.NewValue)
                {
                    element.PreviewMouseWheel += Element_PreviewMouseWheel;
                }
                else
                {
                    element.PreviewMouseWheel -= Element_PreviewMouseWheel;
                }
            }
        }

        private static void Element_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is UIElement element && !e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };

                // Find parent UIElement and raise the routed event
                var parent = System.Windows.Media.VisualTreeHelper.GetParent(element) as UIElement;
                parent?.RaiseEvent(eventArg);
            }
        }
    }
}

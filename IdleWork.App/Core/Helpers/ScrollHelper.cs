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
                // [v0.003: SmartScroll] Find if there is an ancestor ScrollViewer to bubble to
                var parentScrollViewer = FindAncestor<ScrollViewer>(element);
                if (parentScrollViewer == null)
                {
                    // No outer ScrollViewer exists; do NOT hijack mouse wheel events!
                    return;
                }

                // If element itself or a descendant is a ScrollViewer, check if it can scroll in this direction
                var innerScrollViewer = element as ScrollViewer ?? FindDescendant<ScrollViewer>(element);
                if (innerScrollViewer != null && innerScrollViewer.ScrollableHeight > 0)
                {
                    bool canScrollDown = e.Delta < 0 && innerScrollViewer.VerticalOffset < innerScrollViewer.ScrollableHeight;
                    bool canScrollUp = e.Delta > 0 && innerScrollViewer.VerticalOffset > 0;
                    if (canScrollDown || canScrollUp)
                    {
                        // Allow inner control to scroll naturally
                        return;
                    }
                }

                // Inner control is at boundary or cannot scroll; bubble to parent ScrollViewer
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                parentScrollViewer.RaiseEvent(eventArg);
            }
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
                if (current is T match)
                    return match;
            }
            return null;
        }

        private static T? FindDescendant<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                    return match;
                var sub = FindDescendant<T>(child);
                if (sub != null)
                    return sub;
            }
            return null;
        }
    }
}

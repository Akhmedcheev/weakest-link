using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WeakestLink.Views
{
    public static class TransformBehavior
    {
        public static readonly DependencyProperty TargetYProperty =
            DependencyProperty.RegisterAttached("TargetY", typeof(double), typeof(TransformBehavior),
                new PropertyMetadata(0.0, OnTargetYChanged));

        public static void SetTargetY(UIElement element, double value)
        {
            element.SetValue(TargetYProperty, value);
        }

        public static double GetTargetY(UIElement element)
        {
            return (double)element.GetValue(TargetYProperty);
        }

        private static void OnTargetYChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                var transform = element.RenderTransform as TranslateTransform;
                if (transform == null)
                {
                    if (element.RenderTransform is TransformGroup group)
                    {
                        foreach (var child in group.Children)
                        {
                            if (child is TranslateTransform t)
                                transform = t;
                        }
                    }
                    if (transform == null)
                    {
                        transform = new TranslateTransform();
                        element.RenderTransform = transform;
                    }
                }

                double target = (double)e.NewValue;
                var anim = new DoubleAnimation
                {
                    To = target,
                    Duration = TimeSpan.FromSeconds(0.4),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                transform.BeginAnimation(TranslateTransform.YProperty, anim);
            }
        }
    }
}

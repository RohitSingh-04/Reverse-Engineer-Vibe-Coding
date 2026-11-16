using System.Windows;
using System.Windows.Controls;

namespace AuraWallpaper.UI.Controls
{
    public partial class ScrollNavigator : UserControl
    {
        public static readonly DependencyProperty TargetScrollViewerProperty =
            DependencyProperty.Register(
                nameof(TargetScrollViewer),
                typeof(ScrollViewer),
                typeof(ScrollNavigator),
                new PropertyMetadata(null, OnTargetScrollViewerChanged));

        public ScrollViewer? TargetScrollViewer
        {
            get => (ScrollViewer?)GetValue(TargetScrollViewerProperty);
            set => SetValue(TargetScrollViewerProperty, value);
        }

        public ScrollNavigator()
        {
            InitializeComponent();
            Loaded += (s, e) => UpdateButtons();
        }

        private static void OnTargetScrollViewerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollNavigator nav)
            {
                if (e.OldValue is ScrollViewer oldViewer)
                {
                    oldViewer.ScrollChanged -= nav.OnScrollChanged;
                }
                if (e.NewValue is ScrollViewer newViewer)
                {
                    newViewer.ScrollChanged += nav.OnScrollChanged;
                    nav.UpdateButtons();
                }
            }
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateButtons();
        }

        public void UpdateButtons()
        {
            var sv = TargetScrollViewer;
            if (sv == null)
            {
                BtnScrollTop.Visibility = Visibility.Collapsed;
                BtnScrollBottom.Visibility = Visibility.Collapsed;
                return;
            }

            // Top button: show when scrolled down > 40px
            bool showTop = sv.VerticalOffset > 40;
            BtnScrollTop.Visibility = showTop ? Visibility.Visible : Visibility.Collapsed;

            // Bottom button: show when there is scrollable content and we are not near bottom (> 40px away)
            bool showBottom = sv.ScrollableHeight > 60 && (sv.VerticalOffset < sv.ScrollableHeight - 40);
            BtnScrollBottom.Visibility = showBottom ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnScrollTop_Click(object sender, RoutedEventArgs e)
        {
            TargetScrollViewer?.ScrollToTop();
        }

        private void BtnScrollBottom_Click(object sender, RoutedEventArgs e)
        {
            TargetScrollViewer?.ScrollToEnd();
        }
    }
}

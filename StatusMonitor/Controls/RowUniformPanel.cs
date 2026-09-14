using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace StatusMonitor.Controls
{
    /// <summary>
    /// Lays children into a fixed number of equal-width columns. Every row is as
    /// tall as its tallest child and each child is stretched to fill its cell, so
    /// rows line up cleanly (equal width + row-uniform height). Reports a finite
    /// total height, so it scrolls correctly inside a ScrollViewer (unlike an
    /// auto-row UniformGrid, which requests infinite height there).
    /// </summary>
    public class RowUniformPanel : Panel
    {
        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(nameof(Columns), typeof(int), typeof(RowUniformPanel),
                new FrameworkPropertyMetadata(3, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public static readonly DependencyProperty GapProperty =
            DependencyProperty.Register(nameof(Gap), typeof(double), typeof(RowUniformPanel),
                new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public static readonly DependencyProperty FillLastRowProperty =
            DependencyProperty.Register(nameof(FillLastRow), typeof(bool), typeof(RowUniformPanel),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public bool FillLastRow
        {
            get => (bool)GetValue(FillLastRowProperty);
            set => SetValue(FillLastRowProperty, value);
        }

        public int Columns
        {
            get => (int)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        public double Gap
        {
            get => (double)GetValue(GapProperty);
            set => SetValue(GapProperty, value);
        }

        private static int EffectiveColumns(RowUniformPanel p) => Math.Max(1, Math.Min(p.Columns, p.Children.Cast<UIElement>().Count(c => c.Visibility != Visibility.Collapsed)));

        private static double CellWidth(double width, int cols, double gap)
        {
            double w = (width - gap * (cols - 1)) / cols;
            return w > 0 ? w : 0;
        }

        // Row heights (max child height per row), computed from measured children.
        private static List<double> ComputeRowHeights(RowUniformPanel p, int cols)
        {
            var heights = new List<double>();
            var children = p.Children.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).ToList();
            double row = 0;
            int inRow = 0;
            for (int i = 0; i < children.Count; i++)
            {
                if (inRow == 0) row = 0;
                row = Math.Max(row, children[i].DesiredSize.Height);
                if (++inRow == cols) { heights.Add(row); inRow = 0; }
            }
            if (inRow > 0) heights.Add(row);
            return heights;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var children = Children.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).ToList();
            double width = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width;
            int cols = EffectiveColumns(this);
            double gap = Gap;


            double total = 0, row = 0;
            int inRow = 0;
            for (int i = 0; i < children.Count; i++)
            {
                int rowColumns = FillLastRow ? Math.Min(cols, children.Count - (i / cols) * cols) : cols;
                children[i].Measure(new Size(CellWidth(width, rowColumns, gap), availableSize.Height));
                if (inRow == 0) row = 0;
                row = Math.Max(row, children[i].DesiredSize.Height);
                if (++inRow == cols) { total += row + gap; inRow = 0; }
            }
            if (inRow > 0) total += row + gap;
            if (total > 0) total -= gap;

            return new Size(width, total);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var children = Children.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).ToList();
            double width = finalSize.Width;
            int cols = EffectiveColumns(this);
            double gap = Gap;


            var rowHeights = ComputeRowHeights(this, cols);

            double y = 0;
            int inRow = 0, rowIdx = 0;
            for (int i = 0; i < children.Count; i++)
            {
                double h = rowHeights[rowIdx];
                int rowColumns = FillLastRow ? Math.Min(cols, children.Count - rowIdx * cols) : cols;
                double rowCell = CellWidth(width, rowColumns, gap);
                double x = inRow * (rowCell + gap);
                children[i].Arrange(new Rect(x, y, rowCell, h));
                if (++inRow == cols) { y += h + gap; inRow = 0; rowIdx++; }
            }

            return finalSize;
        }
    }
}



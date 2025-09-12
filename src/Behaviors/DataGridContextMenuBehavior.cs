using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DPUnity.Wpf.DpDataGrid.Behaviors
{
    public static class DataGridContextMenuBehavior
    {
        public static readonly DependencyProperty RestrictToRowsProperty =
            DependencyProperty.RegisterAttached(
                "RestrictToRows",
                typeof(bool),
                typeof(DataGridContextMenuBehavior),
                new PropertyMetadata(false, OnRestrictToRowsChanged));

        public static bool GetRestrictToRows(DependencyObject obj)
        {
            return (bool)obj.GetValue(RestrictToRowsProperty);
        }

        public static void SetRestrictToRows(DependencyObject obj, bool value)
        {
            obj.SetValue(RestrictToRowsProperty, value);
        }

        private static void OnRestrictToRowsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid dataGrid)
            {
                if ((bool)e.NewValue)
                {
                    dataGrid.ContextMenuOpening += DataGrid_ContextMenuOpening;
                }
                else
                {
                    dataGrid.ContextMenuOpening -= DataGrid_ContextMenuOpening;
                }
            }
        }

        private static void DataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // Traverse visual tree to check if source is within a DataGridRow
            var source = e.OriginalSource as DependencyObject;
            while (source is not null and not DataGridRow)
            {
                source = VisualTreeHelper.GetParent(source);
            }

            // If not on a row (e.g., header, footer, empty area), prevent menu from opening
            if (source == null)
            {
                e.Handled = true;
            }
        }
    }
}

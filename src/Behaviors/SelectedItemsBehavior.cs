using System.Collections;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

using SystemDataGrid = System.Windows.Controls.DataGrid;

namespace DPUnity.Wpf.DpDataGrid.Behaviors
{
    public static class SelectedItemsBehavior
    {
        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "SelectedItems",
                typeof(IList),
                typeof(SelectedItemsBehavior),
                new PropertyMetadata(null, OnSelectedItemsChanged));
        public static IList GetSelectedItems(DependencyObject obj)
        {
            return (IList)obj.GetValue(SelectedItemsProperty);
        }
        public static void SetSelectedItems(DependencyObject obj, IList value)
        {
            obj.SetValue(SelectedItemsProperty, value);
        }
        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SystemDataGrid dataGrid)
            {
                dataGrid.SelectionChanged -= DataGrid_SelectionChanged;
                if (e.NewValue is IList newList)
                {
                    dataGrid.SelectionChanged += DataGrid_SelectionChanged;
                    dataGrid.SelectedItems.Clear();
                    foreach (var item in newList)
                    {
                        dataGrid.SelectedItems.Add(item);
                    }
                }
            }
        }
        private static void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is SystemDataGrid dataGrid && GetSelectedItems(dataGrid) is IList selectedItems)
            {
                selectedItems.Clear();
                foreach (var item in dataGrid.SelectedItems)
                {
                    selectedItems.Add(item);
                }
            }
        }
    }
}
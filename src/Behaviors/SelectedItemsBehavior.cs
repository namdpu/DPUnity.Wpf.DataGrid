using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;

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

        // Suppress immediate per-item selection sync during bulk operations
        public static readonly DependencyProperty SuppressSelectionSyncProperty =
            DependencyProperty.RegisterAttached(
                "SuppressSelectionSync",
                typeof(bool),
                typeof(SelectedItemsBehavior),
                new PropertyMetadata(false));
        public static IList GetSelectedItems(DependencyObject obj)
        {
            return (IList)obj.GetValue(SelectedItemsProperty);
        }
        public static void SetSelectedItems(DependencyObject obj, IList value)
        {
            obj.SetValue(SelectedItemsProperty, value);
        }
        public static bool GetSuppressSelectionSync(DependencyObject obj)
        {
            return (bool)obj.GetValue(SuppressSelectionSyncProperty);
        }
        public static void SetSuppressSelectionSync(DependencyObject obj, bool value)
        {
            obj.SetValue(SuppressSelectionSyncProperty, value);
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
                if (GetSuppressSelectionSync(dataGrid))
                {
                    // Skip immediate sync; a bulk sync will follow
                    return;
                }

                // Check if this is a FilterDataGrid with virtual selection
                if (dataGrid is FilterDataGrid filterGrid && filterGrid.IsInVirtualSelectionMode)
                {
                    // Use effective selection for virtual mode
                    selectedItems.Clear();
                    var effectiveItems = filterGrid.GetEffectiveSelectedItems();
                    foreach (var item in effectiveItems)
                    {
                        selectedItems.Add(item);
                    }
                }
                else
                {
                    // Normal selection sync
                    selectedItems.Clear();
                    foreach (var item in dataGrid.SelectedItems)
                    {
                        selectedItems.Add(item);
                    }
                }
            }
        }

        /// <summary>
        /// Bulk sync helper to copy DataGrid.SelectedItems to the bound SelectedItems list in batches.
        /// </summary>
        /// <param name="dataGrid">The DataGrid.</param>
        /// <param name="batchSize">Batch size; if 0 or less, copies in one pass.</param>
        public static async Task SyncSelectedItemsAsync(SystemDataGrid dataGrid, int batchSize)
        {
            if (dataGrid == null) return;
            if (GetSelectedItems(dataGrid) is not IList selectedItems) return;

            selectedItems.Clear();

            List<object> items;
            
            // Check if this is a FilterDataGrid with virtual selection
            if (dataGrid is FilterDataGrid filterGrid && filterGrid.IsInVirtualSelectionMode)
            {
                // Use effective selection for virtual mode
                items = filterGrid.GetEffectiveSelectedItems().Cast<object>().ToList();
            }
            else
            {
                // Normal selection
                items = dataGrid.SelectedItems?.Cast<object>()?.ToList() ?? new List<object>();
            }

            if (batchSize <= 0)
            {
                foreach (var item in items)
                {
                    selectedItems.Add(item);
                }
                return;
            }

            for (int i = 0; i < items.Count; i += batchSize)
            {
                var chunk = items.Skip(i).Take(batchSize);
                foreach (var item in chunk)
                {
                    selectedItems.Add(item);
                }
                // yield to UI thread to keep it responsive
                await Task.Yield();
            }
        }
    }
}
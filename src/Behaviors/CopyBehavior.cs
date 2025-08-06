using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

using SystemDataGrid = System.Windows.Controls.DataGrid;

namespace DPUnity.Wpf.DpDataGrid.Behaviors
{
    public static class CopyBehavior
    {
        #region Attached Properties

        public static readonly DependencyProperty UseHeaderProperty =
            DependencyProperty.RegisterAttached(
                "UseHeader",
                typeof(bool),
                typeof(CopyBehavior),
                new PropertyMetadata(false, OnCopyPropertyChanged));

        public static void SetUseHeader(DependencyObject element, bool value)
            => element.SetValue(UseHeaderProperty, value);

        public static bool GetUseHeader(DependencyObject element)
            => (bool)element.GetValue(UseHeaderProperty);

        public static readonly DependencyProperty CopyCommandProperty =
            DependencyProperty.RegisterAttached(
                "CopyCommand",
                typeof(ICommand),
                typeof(CopyBehavior),
                new PropertyMetadata(null, OnCopyPropertyChanged));

        public static void SetCopyCommand(DependencyObject element, ICommand value)
            => element.SetValue(CopyCommandProperty, value);

        public static ICommand GetCopyCommand(DependencyObject element)
            => (ICommand)element.GetValue(CopyCommandProperty);

        #endregion

        private static void OnCopyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not SystemDataGrid dataGrid) return;

            UpdateCommandBindings(dataGrid);
        }

        private static void UpdateCommandBindings(SystemDataGrid dataGrid)
        {
            bool useHeader = GetUseHeader(dataGrid);
            ICommand copyCommand = GetCopyCommand(dataGrid);

            // Remove any existing copy command binding
            var bindingToRemove = dataGrid.CommandBindings.OfType<CommandBinding>()
                .FirstOrDefault(cb => cb.Command == ApplicationCommands.Copy);
            if (bindingToRemove != null)
            {
                dataGrid.CommandBindings.Remove(bindingToRemove);
            }

            // Add binding if either UseHeader is true or a custom command is provided
            if (useHeader || copyCommand != null)
            {
                dataGrid.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopyExecuted, OnCanCopyExecute));
            }
        }

        private static void OnCanCopyExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (sender is not SystemDataGrid dataGrid) return;

            var customCommand = GetCopyCommand(dataGrid);
            if (customCommand != null)
            {
                var context = new ClipboardDataContext(dataGrid, dataGrid.SelectedItems.Cast<object>());
                e.CanExecute = customCommand.CanExecute(context);
            }
            else
            {
                e.CanExecute = dataGrid.SelectedItems.Count > 0;
            }
        }

        private static void OnCopyExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is not SystemDataGrid dataGrid) return;

            // Try to use custom command first
            var customCommand = GetCopyCommand(dataGrid);
            if (customCommand != null)
            {
                var context = new ClipboardDataContext(dataGrid, dataGrid.SelectedItems.Cast<object>());
                if (customCommand.CanExecute(context))
                {
                    customCommand.Execute(context);
                    e.Handled = true;
                    return;
                }
            }

            // Fall back to default behavior if no custom command or UseHeader is true
            if (GetUseHeader(dataGrid) && dataGrid.SelectedItems.Count > 0)
            {
                var clipboardContent = new StringBuilder();

                // Filter columns to include (skip selection column if present)
                var columnsToInclude = GetColumnsToInclude(dataGrid);

                // Add header row
                var headers = columnsToInclude.Select(GetColumnHeaderText).ToList();
                clipboardContent.AppendLine(string.Join("\t", headers));

                // Add data rows
                foreach (var item in dataGrid.SelectedItems)
                {
                    var rowData = columnsToInclude.Select(column => GetCellValue(item, column)?.ToString() ?? string.Empty).ToList();
                    clipboardContent.AppendLine(string.Join("\t", rowData));
                }

                // Copy to clipboard
                try
                {
                    Clipboard.SetText(clipboardContent.ToString());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Clipboard error: {ex.Message}");
                }

                e.Handled = true;
            }
        }

        // Existing helper methods remain unchanged
        private static string GetColumnHeaderText(DataGridColumn column)
        {
            if (column.Header != null && !string.IsNullOrWhiteSpace(column.Header.ToString()))
            {
                return column.Header.ToString();
            }

            if (!string.IsNullOrWhiteSpace(column.SortMemberPath))
            {
                return column.SortMemberPath;
            }

            if (column is DataGridBoundColumn boundColumn &&
                boundColumn.Binding is Binding binding &&
                !string.IsNullOrEmpty(binding.Path?.Path))
            {
                return binding.Path.Path;
            }

            if (column is DataGridComboBoxColumn comboColumn &&
                comboColumn.SelectedValueBinding is Binding comboBinding &&
                !string.IsNullOrEmpty(comboBinding.Path?.Path))
            {
                return comboBinding.Path.Path;
            }

            return string.Empty;
        }

        private static object GetCellValue(object item, DataGridColumn column)
        {
            if (column is DataGridBoundColumn boundColumn &&
                boundColumn.Binding is Binding binding)
            {
                return GetValueFromPath(item, binding.Path.Path);
            }

            if (column is DataGridComboBoxColumn comboColumn &&
                comboColumn.SelectedValueBinding is Binding comboBinding)
            {
                return GetValueFromPath(item, comboBinding.Path.Path);
            }

            if (column is DataGridTemplateColumn templateColumn)
            {
                if (!string.IsNullOrWhiteSpace(templateColumn.SortMemberPath))
                {
                    return GetValueFromPath(item, templateColumn.SortMemberPath);
                }

                var fieldNameProperty = templateColumn.GetType().GetProperty("FieldName");
                if (fieldNameProperty != null)
                {
                    var fieldName = fieldNameProperty.GetValue(templateColumn) as string;
                    if (!string.IsNullOrWhiteSpace(fieldName))
                    {
                        return GetValueFromPath(item, fieldName);
                    }
                }

                return GetValueFromTemplate(item, templateColumn);
            }

            if (!string.IsNullOrWhiteSpace(column.SortMemberPath))
            {
                return GetValueFromPath(item, column.SortMemberPath);
            }

            return null;
        }

        private static IEnumerable<DataGridColumn> GetColumnsToInclude(SystemDataGrid dataGrid)
        {
            var visibleColumns = dataGrid.Columns.Where(c => c.Visibility == Visibility.Visible);

            if (HasShowSelectionColumn(dataGrid) && IsShowSelectionColumnEnabled(dataGrid))
            {
                return visibleColumns.Skip(1);
            }

            var firstColumn = visibleColumns.FirstOrDefault();
            if (IsSelectionColumn(firstColumn))
            {
                return visibleColumns.Skip(1);
            }

            return visibleColumns;
        }

        private static bool IsSelectionColumn(DataGridColumn column)
        {
            if (column is DataGridTemplateColumn templateColumn)
            {
                return templateColumn.Header == null &&
                       templateColumn.Width.Value == 35 &&
                       !templateColumn.CanUserReorder &&
                       !templateColumn.CanUserResize;
            }
            return false;
        }

        private static bool HasShowSelectionColumn(SystemDataGrid dataGrid)
        {
            var property = dataGrid.GetType().GetProperty("ShowSelectionColumn");
            return property != null && property.PropertyType == typeof(bool);
        }

        private static bool IsShowSelectionColumnEnabled(SystemDataGrid dataGrid)
        {
            try
            {
                var property = dataGrid.GetType().GetProperty("ShowSelectionColumn");
                if (property != null)
                {
                    return (bool)property.GetValue(dataGrid);
                }
            }
            catch
            {
            }
            return false;
        }

        private static object GetValueFromPath(object item, string path)
        {
            if (item == null || string.IsNullOrEmpty(path)) return null;

            try
            {
                var obj = item;
                var properties = path.Split('.');

                foreach (var propName in properties)
                {
                    if (obj == null) return null;

                    var property = obj.GetType().GetProperty(propName);
                    if (property == null) return null;

                    obj = property.GetValue(obj);
                }

                return obj;
            }
            catch
            {
                return null;
            }
        }

        private static object GetValueFromTemplate(object item, DataGridTemplateColumn templateColumn)
        {
            try
            {
                var cellTemplate = templateColumn.CellTemplate;
                if (cellTemplate != null)
                {
                    var dummyElement = new ContentPresenter
                    {
                        Content = item,
                        ContentTemplate = cellTemplate
                    };

                    dummyElement.ApplyTemplate();

                    var textBlock = FindChildOfType<TextBlock>(dummyElement);
                    if (textBlock != null)
                    {
                        var binding = BindingOperations.GetBinding(textBlock, TextBlock.TextProperty);
                        if (binding != null && !string.IsNullOrEmpty(binding.Path?.Path))
                        {
                            return GetValueFromPath(item, binding.Path.Path);
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static T FindChildOfType<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T result)
                    return result;

                var childOfType = FindChildOfType<T>(child);
                if (childOfType != null)
                    return childOfType;
            }

            return null;
        }

        public class ClipboardDataContext
        {
            public SystemDataGrid SystemDataGrid { get; }
            public IEnumerable<object> SelectedItems { get; }

            public ClipboardDataContext(SystemDataGrid dataGrid, IEnumerable<object> selectedItems)
            {
                SystemDataGrid = dataGrid;
                SelectedItems = selectedItems;
            }
        }
    }
}
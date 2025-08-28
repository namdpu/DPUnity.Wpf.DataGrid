// Author     : Gilles Macabies
// Solution   : DataGridFilter
// Projet     : DataGridFilter
// File       : DataGridColumn.cs
// Created    : 09/11/2019

using DPUnity.Wpf.DpDataGrid.Converters;
using DPUnity.Wpf.UI.Controls.PackIcon;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

// ReSharper disable ConvertTypeCheckPatternToNullCheck
// ReSharper disable InvertIf
// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable CheckNamespace

namespace DPUnity.Wpf.DpDataGrid
{
    public sealed class DataGridCheckBoxColumn : System.Windows.Controls.DataGridCheckBoxColumn
    {
        #region Public Fields

        /// <summary>
        /// FieldName Dependency Property.
        /// </summary>
        public static readonly DependencyProperty FieldNameProperty =
            DependencyProperty.Register(nameof(FieldName), typeof(string), typeof(DataGridCheckBoxColumn),
                new PropertyMetadata(""));

        /// <summary>
        /// IsColumnFiltered Dependency Property.
        /// </summary>
        public static readonly DependencyProperty IsColumnFilteredProperty =
            DependencyProperty.Register(nameof(IsColumnFiltered), typeof(bool), typeof(DataGridCheckBoxColumn),
                new PropertyMetadata(false));

        #endregion Public Fields

        #region Public Properties

        public string FieldName
        {
            get => (string)GetValue(FieldNameProperty);
            set => SetValue(FieldNameProperty, value);
        }

        public bool IsColumnFiltered
        {
            get => (bool)GetValue(IsColumnFilteredProperty);
            set => SetValue(IsColumnFilteredProperty, value);
        }

        #endregion Public Properties

        #region Element Generation
        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem) => GenerateElement(cell, dataItem);

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            var checkbox = (CheckBox)base.GenerateElement(cell, dataItem);
            cell.HorizontalContentAlignment = HorizontalAlignment.Center;
            cell.VerticalContentAlignment = VerticalAlignment.Center;
            cell.Padding = new Thickness(0);

            cell.SetResourceReference(FrameworkElement.StyleProperty, "DP_DataGridCheckBoxCell");

            return checkbox;
        }
        #endregion Element Generation
    }
    public sealed class DataGridComboBoxColumn : System.Windows.Controls.DataGridComboBoxColumn
    {

        #region Public Classes

        public class ItemsSourceMembers
        {
            #region Public Properties

            public string DisplayMember { get; set; } = string.Empty;
            public string SelectedValue { get; set; } = string.Empty;

            #endregion Public Properties
        }

        #endregion Public Classes

        #region Public Fields

        /// <summary>
        /// FieldName Dependency Property.
        /// </summary>
        public static readonly DependencyProperty FieldNameProperty =
            DependencyProperty.Register(nameof(FieldName), typeof(string), typeof(DataGridComboBoxColumn),
                new PropertyMetadata(""));

        /// <summary>
        /// IsColumnFiltered Dependency Property.
        /// </summary>
        public static readonly DependencyProperty IsColumnFilteredProperty =
            DependencyProperty.Register(nameof(IsColumnFiltered), typeof(bool), typeof(DataGridComboBoxColumn),
                new PropertyMetadata(false));

        #endregion Public Fields

        #region Public Properties

        public List<ItemsSourceMembers>? ComboBoxItemsSource { get; set; }

        public string FieldName
        {
            get => (string)GetValue(FieldNameProperty);
            set => SetValue(FieldNameProperty, value);
        }

        public bool IsColumnFiltered
        {
            get => (bool)GetValue(IsColumnFilteredProperty);
            set => SetValue(IsColumnFilteredProperty, value);
        }

        public bool IsSingle { get; set; }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Updates the items source.
        /// </summary>
        public async void UpdateItemsSourceAsync()
        {
            if (ItemsSource == null) return;

            // Marshal the call back to the UI thread
            await Dispatcher.InvokeAsync(() =>
            {
                var itemsSource = ItemsSource;
                var itemsSourceMembers = itemsSource.Cast<object>().Select(x =>
                    new ItemsSourceMembers
                    {
                        SelectedValue = x.GetPropertyValue(SelectedValuePath)?.ToString() ?? string.Empty,
                        DisplayMember = x.GetPropertyValue(DisplayMemberPath)?.ToString() ?? string.Empty
                    }).ToList();

                ComboBoxItemsSource = itemsSourceMembers;
            });
        }

        #endregion Public Methods

        #region Protected Methods

        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            var element = base.GenerateEditingElement(cell, dataItem);
            element.SetResourceReference(FrameworkElement.StyleProperty, "ComboBox.Small");
            return element;
        }

        protected override void OnSelectedValueBindingChanged(BindingBase oldBinding, BindingBase newBinding)
        {
            base.OnSelectedValueBindingChanged(oldBinding, newBinding);
            UpdateItemsSourceAsync();
        }

        #endregion Protected Methods
    }

    public class DataGridNumericColumn : DataGridTextColumn
    {
        #region Private Fields

        private const bool DebugMode = false;
        private CultureInfo? culture;
        private Type? fieldType;
        private string? originalValue;
        private Regex? regex;
        private string? stringFormat;

        #endregion Private Fields

        #region Public Methods

        /// <summary>
        /// Determines if the field type is numeric and sets the appropriate regex pattern.
        /// </summary>
        public void BuildRegex()
        {
            Debug.WriteLineIf(DebugMode, $"BuildRegex : {fieldType}");

            if (culture == null || fieldType == null) return;

            var nfi = culture.NumberFormat;

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (Type.GetTypeCode(fieldType))
            {
                // signed integer types
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                    regex = new Regex($@"^{nfi.NegativeSign}?\d+$");
                    break;

                // unsigned integer types
                case TypeCode.Byte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    regex = new Regex(@"^\d+$");
                    break;

                // floating point types
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    var decimalSeparator = (stringFormat?.Contains("c") == true)
                        ? Regex.Escape(nfi.CurrencyDecimalSeparator)
                        : Regex.Escape(nfi.NumberDecimalSeparator);
                    regex = new Regex($@"^{nfi.NegativeSign}?(\d+({decimalSeparator}\d*)?|{decimalSeparator}\d*)?$");
                    break;

                // non-numeric types
                default:
                    Debug.WriteLineIf(DebugMode, "Unsupported fieldType");
                    regex = new Regex(@"[^\t\r\n]+");
                    break;
            }
        }

        #endregion Public Methods

        #region Protected Methods

        /// <summary>
        /// Cancels the cell edit.
        /// </summary>
        /// <param name="editingElement">The editing element.</param>
        /// <param name="uneditedValue">The unedited value.</param>
        protected override void CancelCellEdit(FrameworkElement editingElement, object uneditedValue)
        {
            Debug.WriteLineIf(DebugMode, $"CancelCellEdit : {uneditedValue}");
            base.CancelCellEdit(editingElement, uneditedValue);
        }

        /// <summary>
        /// Commits the cell edit.
        /// </summary>
        /// <param name="editingElement">The editing element.</param>
        /// <returns>True if the edit was committed successfully, otherwise false.</returns>
        protected override bool CommitCellEdit(FrameworkElement editingElement)
        {
            Debug.WriteLineIf(DebugMode, "CommitCellEdit");
            if (editingElement is TextBox tb)
            {
                if (string.IsNullOrEmpty(tb.Text))
                {
                    tb.Text = originalValue;
                }
            }

            return base.CommitCellEdit(editingElement);
        }

        /// <summary>
        /// Prepares the cell for editing.
        /// </summary>
        /// <param name="editingElement">The editing element.</param>
        /// <param name="e">The event arguments.</param>
        /// <returns>The original value of the cell.</returns>
        protected override object PrepareCellForEdit(FrameworkElement editingElement, RoutedEventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "PrepareCellForEdit");

            try
            {
                // Determine the column type if not already determined
                if (fieldType == null)
                {
                    var filterDataGrid = (FilterDataGrid)DataGridOwner;
                    var dataContext = editingElement.DataContext;
                    culture = new CultureInfo("en-US");
                    var propertyName = ((Binding)Binding).Path.Path;
                    stringFormat = string.IsNullOrEmpty(((Binding)Binding).StringFormat)
                        ? string.Empty
                        : ((Binding)Binding).StringFormat.ToLower();

                    var fieldProperty = dataContext.GetType().GetProperty(propertyName);
                    if (fieldProperty != null)
                    {
                        fieldType = Nullable.GetUnderlyingType(fieldProperty.PropertyType) ?? fieldProperty.PropertyType;
                        BuildRegex();
                    }
                    else
                    {
                        Debug.WriteLineIf(DebugMode, "fieldProperty is null");
                    }
                }

                // Subscribe to keyboard and paste events
                if (editingElement is TextBox edit)
                {
                    originalValue = edit.Text;
                    edit.PreviewTextInput += OnPreviewTextInput;
                    DataObject.AddPastingHandler(edit, OnPaste);

                    // Create a new binding with the desired StringFormat and culture
                    var newBinding = new Binding(((Binding)Binding).Path.Path)
                    {
                        // removes formatting(symbol) for cell editing(TextBox)
                        // original formatting remains active for display(TextBlock)
                        ConverterCulture = culture
                    };

                    edit.SetBinding(TextBox.TextProperty, newBinding);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLineIf(DebugMode, $"Exception in PrepareCellForEdit: {ex.Message}");
            }

            return base.PrepareCellForEdit(editingElement, e);
        }

        #region Generate Element
        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            var element = base.GenerateElement(cell, dataItem);
            if (element is TextBlock textBlock)
            {
                textBlock.TextAlignment = TextAlignment.Right;
                textBlock.Margin = new Thickness(0, 0, 5, 0);
            }
            return element;
        }

        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            var element = base.GenerateEditingElement(cell, dataItem);
            if (element is TextBox textBox)
            {
                textBox.TextAlignment = TextAlignment.Right;
                textBox.Margin = new Thickness(0, 0, 5, 0);
            }
            return element;
        }
        #endregion

        #endregion Protected Methods

        #region Private Methods

        /// <summary>
        /// Handles the paste event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "OnPaste");

            if (e.SourceDataObject.GetData(DataFormats.Text) is string pasteText && sender is TextBox textBox)
            {
                var newText = textBox.Text.Insert(textBox.SelectionStart, pasteText);

                if (regex != null && !regex.IsMatch(newText))
                {
                    e.CancelCommand();
                }
            }
        }

        /// <summary>
        /// Handles the PreviewTextInput event of the TextBox control.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "OnPreviewTextInput");

            if (sender is TextBox textBox)
            {
                var newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
                var isNumeric = regex?.IsMatch(newText) ?? false;

                Debug.WriteLineIf(DebugMode, $"originalValue : {originalValue,-15}" +
                                             $"originalText : {textBox.Text,-15}" +
                                             $"newText : {newText,-15}" +
                                             $"IsTextNumeric : {isNumeric}");

                e.Handled = !isNumeric;
            }
        }

        #endregion Private Methods
    }

    public sealed class DataGridTemplateColumn : System.Windows.Controls.DataGridTemplateColumn
    {

        #region Public Fields

        /// <summary>
        /// FieldName Dependency Property.
        /// </summary>
        public static readonly DependencyProperty FieldNameProperty =
            DependencyProperty.Register(nameof(FieldName), typeof(string), typeof(DataGridTemplateColumn),
                new PropertyMetadata(""));

        /// <summary>
        /// IsColumnFiltered Dependency Property.
        /// </summary>
        public static readonly DependencyProperty IsColumnFilteredProperty =
            DependencyProperty.Register(nameof(IsColumnFiltered), typeof(bool), typeof(DataGridTemplateColumn),
                new PropertyMetadata(false));

        #endregion Public Fields

        #region Public Properties

        public string FieldName
        {
            get => (string)GetValue(FieldNameProperty);
            set => SetValue(FieldNameProperty, value);
        }

        public bool IsColumnFiltered
        {
            get => (bool)GetValue(IsColumnFilteredProperty);
            set => SetValue(IsColumnFilteredProperty, value);
        }

        #endregion Public Properties
    }

    public class DataGridTextColumn : System.Windows.Controls.DataGridTextColumn
    {
        #region Public Fields

        /// <summary>
        /// FieldName Dependency Property.
        /// </summary>
        public static readonly DependencyProperty FieldNameProperty =
            DependencyProperty.Register(nameof(FieldName), typeof(string), typeof(DataGridTextColumn),
                new PropertyMetadata(""));

        /// <summary>
        /// IsColumnFiltered Dependency Property.
        /// </summary>
        public static readonly DependencyProperty IsColumnFilteredProperty =
            DependencyProperty.Register(nameof(IsColumnFiltered), typeof(bool), typeof(DataGridTextColumn),
                new PropertyMetadata(false));

        #endregion Public Fields

        #region Public Properties

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            var element = base.GenerateElement(cell, dataItem);
            element.SetResourceReference(FrameworkElement.StyleProperty, "DP_TextblockBase");
            return element;
        }

        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            var element = base.GenerateEditingElement(cell, dataItem);
            element.SetResourceReference(FrameworkElement.StyleProperty, "TextBox.Small");
            return element;
        }

        public string FieldName
        {
            get => (string)GetValue(FieldNameProperty);
            set => SetValue(FieldNameProperty, value);
        }

        public bool IsColumnFiltered
        {
            get => (bool)GetValue(IsColumnFilteredProperty);
            set => SetValue(IsColumnFilteredProperty, value);
        }

        #endregion Public Properties
    }

    public sealed class DataGridBoundColumn : System.Windows.Controls.DataGridBoundColumn
    {
        #region Public Fields

        /// <summary>
        /// FieldName Dependency Property.
        /// </summary>
        public static readonly DependencyProperty FieldNameProperty =
            DependencyProperty.Register(nameof(FieldName), typeof(string), typeof(DataGridTextColumn),
                new PropertyMetadata(""));

        /// <summary>
        /// IsColumnFiltered Dependency Property.
        /// </summary>
        public static readonly DependencyProperty IsColumnFilteredProperty =
            DependencyProperty.Register(nameof(IsColumnFiltered), typeof(bool), typeof(DataGridTextColumn),
                new PropertyMetadata(false));

        #endregion Public Fields

        #region Public Properties

        public string FieldName
        {
            get => (string)GetValue(FieldNameProperty);
            set => SetValue(FieldNameProperty, value);
        }

        public bool IsColumnFiltered
        {
            get => (bool)GetValue(IsColumnFilteredProperty);
            set => SetValue(IsColumnFilteredProperty, value);
        }

        #endregion Public Properties

        #region GenerateElement

        public string? TemplateName { get; set; }

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            Binding binding;

            ContentControl content = new ContentControl();

            if (!string.IsNullOrEmpty(TemplateName))
            {
                content.ContentTemplate = (DataTemplate)cell.FindResource(TemplateName);
            }

            if (Binding != null)
            {
                binding = new Binding(((Binding)Binding).Path.Path)
                {
                    Source = dataItem,
                    Mode = BindingMode.TwoWay,
                    NotifyOnSourceUpdated = true,
                    NotifyOnTargetUpdated = true,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };

                content.SetBinding(ContentControl.ContentProperty, binding);
            }

            return content;
        }

        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem) => GenerateElement(cell, dataItem);

        #endregion GenerateElement
    }

    public sealed class DataGridStatusColumn : System.Windows.Controls.DataGridBoundColumn
    {
        #region Public Fields
        /// <summary>
        /// FieldName Dependency Property.
        /// </summary>
        public static readonly DependencyProperty FieldNameProperty =
            DependencyProperty.Register(nameof(FieldName), typeof(string), typeof(DataGridStatusColumn),
                new PropertyMetadata("Status"));

        /// <summary>
        /// IsColumnFiltered Dependency Property.
        /// </summary>
        public static readonly DependencyProperty IsColumnFilteredProperty =
            DependencyProperty.Register(nameof(IsColumnFiltered), typeof(bool), typeof(DataGridStatusColumn),
                new PropertyMetadata(false));
        #endregion Public Fields

        #region Public Properties
        public string FieldName
        {
            get => (string)GetValue(FieldNameProperty);
            set => SetValue(FieldNameProperty, value);
        }

        public bool IsColumnFiltered
        {
            get => (bool)GetValue(IsColumnFilteredProperty);
            set => SetValue(IsColumnFilteredProperty, value);
        }
        public double IconWidth { get; set; } = 14;
        public double IconHeight { get; set; } = 14;
        #endregion Public Properties

        #region GenerateElement
        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            var element = CreateIcon();

            // Configure the cell
            if (cell != null)
            {
                cell.HorizontalContentAlignment = HorizontalAlignment.Center;
                cell.VerticalContentAlignment = VerticalAlignment.Center;
                cell.Padding = new Thickness(0);
            }

            return element;
        }

        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            var element = CreateIcon();

            // Configure the cell
            if (cell != null)
            {
                cell.HorizontalContentAlignment = HorizontalAlignment.Center;
                cell.VerticalContentAlignment = VerticalAlignment.Center;
                cell.Padding = new Thickness(0);
            }

            return element;
        }

        private FrameworkElement CreateIcon()
        {
            var container = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0)
            };

            var viewbox = new Viewbox
            {
                Width = IconWidth + 4, // Add a little extra space
                Height = IconHeight + 4,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Stretch = Stretch.Uniform
            };

            var icon = new PackIcon
            {
                Width = IconWidth,
                Height = IconHeight,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            if (Binding is Binding baseBinding)
            {
                var kindBinding = new Binding(baseBinding.Path.Path)
                {
                    Converter = new ItemStatusToIconKindConverter(),
                    Mode = BindingMode.OneWay
                };
                icon.SetBinding(PackIcon.KindProperty, kindBinding);

                // Thiết lập Foreground binding
                var brushBinding = new Binding(baseBinding.Path.Path)
                {
                    Converter = new ItemStatusToBrushConverter(),
                    Mode = BindingMode.OneWay
                };
                icon.SetBinding(Control.ForegroundProperty, brushBinding);
            }

            viewbox.Child = icon;
            container.Children.Add(viewbox);

            return container;
        }
        #endregion GenerateElement
    }

    public enum ItemStatus
    {
        None, Success, Warning, Error
    }

    public interface IHasStatus
    {
        ItemStatus Status { get; set; }
        string StatusMessage { get; set; }
    }

    public sealed class DataGridEnumDescriptionColumn : System.Windows.Controls.DataGridComboBoxColumn
    {

        #region Public Classes
        public class ItemsSourceMembers
        {
            #region Public Properties

            public string DisplayMember { get; set; } = string.Empty;
            public string SelectedValue { get; set; } = string.Empty;

            #endregion Public Properties
        }

        #endregion Public Classes

        #region Public Fields

        /// <summary>
        /// FieldName Dependency Property.
        /// </summary>
        public static readonly DependencyProperty FieldNameProperty =
            DependencyProperty.Register(nameof(FieldName), typeof(string), typeof(DataGridEnumDescriptionColumn),
                new PropertyMetadata(""));

        /// <summary>
        /// IsColumnFiltered Dependency Property.
        /// </summary>
        public static readonly DependencyProperty IsColumnFilteredProperty =
            DependencyProperty.Register(nameof(IsColumnFiltered), typeof(bool), typeof(DataGridEnumDescriptionColumn),
                new PropertyMetadata(false));

        #endregion Public Fields

        #region Public Properties

        public List<ItemsSourceMembers>? ComboBoxItemsSource { get; set; }

        public string FieldName
        {
            get => (string)GetValue(FieldNameProperty);
            set => SetValue(FieldNameProperty, value);
        }

        public bool IsColumnFiltered
        {
            get => (bool)GetValue(IsColumnFilteredProperty);
            set => SetValue(IsColumnFilteredProperty, value);
        }

        public bool IsSingle { get; set; }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Updates the items source.
        /// </summary>
        public async void UpdateItemsSourceAsync()
        {
            if (ItemsSource == null) return;

            // Marshal the call back to the UI thread
            await Dispatcher.InvokeAsync(() =>
            {
                var itemsSource = ItemsSource;
                var itemsSourceMembers = itemsSource.Cast<object>().Select(x =>
                    new ItemsSourceMembers
                    {
                        SelectedValue = x.GetPropertyValue(SelectedValuePath)?.ToString() ?? string.Empty,
                        DisplayMember = x.GetPropertyValue(DisplayMemberPath)?.ToString() ?? string.Empty
                    }).ToList();

                ComboBoxItemsSource = itemsSourceMembers;
            });
        }

        #endregion Public Methods

        #region Protected Methods

        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            var element = base.GenerateEditingElement(cell, dataItem);
            element.SetResourceReference(FrameworkElement.StyleProperty, "ComboBoxBaseStyle");
            return element;
        }

        protected override void OnSelectedValueBindingChanged(BindingBase oldBinding, BindingBase newBinding)
        {
            SelectedValuePath = "Key";
            DisplayMemberPath = "Value";

            base.OnSelectedValueBindingChanged(oldBinding, newBinding);
            UpdateItemsSourceAsync();
        }

        #endregion Protected Methods
    }
}
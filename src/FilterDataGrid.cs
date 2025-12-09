#region (c) 2022 Gilles Macabies All right reserved

// Author     : Gilles Macabies
// Solution   : FilterDataGrid
// Projet     : FilterDataGrid.Net
// File       : FilterDataGrid.cs
// Created    : 06/03/2022
//

#endregion

using DPUnity.Wpf.Controls.Controls.DialogService;
using DPUnity.Wpf.Controls.Controls.InputForms;
using DPUnity.Wpf.DpDataGrid.Converters;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace DPUnity.Wpf.DpDataGrid
{
    /// <summary>
    ///     Implementation of Datagrid
    /// </summary>
    public class FilterDataGrid : System.Windows.Controls.DataGrid, INotifyPropertyChanged
    {
        #region Constructors

        /// <summary>
        ///     FilterDataGrid constructor
        /// </summary>
        public FilterDataGrid()
        {
            Debug.WriteLineIf(DebugMode, "FilterDataGrid.Constructor");

            DefaultStyleKey = typeof(FilterDataGrid);

            var resourceDictionary = new ResourceDictionary
            {
                Source = new Uri("/DPUnity.Wpf.DpDataGrid;component/Themes/Generic.xaml", UriKind.Relative)
            };

            Resources.MergedDictionaries.Add(resourceDictionary);

            // initial popup size
            popUpSize = new Point
            {
                X = (double)TryFindResource("PopupWidth"),
                Y = (double)TryFindResource("PopupHeight")
            };

            CommandBindings.Add(new CommandBinding(ApplyFilter, ApplyFilterCommand, CanApplyFilter)); // Ok
            CommandBindings.Add(new CommandBinding(CancelFilter, CancelFilterCommand));
            CommandBindings.Add(new CommandBinding(ClearSearchBox, ClearSearchBoxClick));
            CommandBindings.Add(new CommandBinding(IsChecked, CheckedAllCommand));
            CommandBindings.Add(new CommandBinding(RemoveAllFilters, RemoveAllFilterCommand, CanRemoveAllFilter));
            CommandBindings.Add(new CommandBinding(RemoveFilter, RemoveFilterCommand, CanRemoveFilter));
            CommandBindings.Add(new CommandBinding(ShowFilter, ShowFilterCommand, CanShowFilter));
            _ = CommandBindings.Add(new CommandBinding(ShowFindReplace, ShowFindReplaceCommand, CanShowFindReplace));

            // Thêm KeyBinding cho Ctrl + H
            InputBindings.Add(new KeyBinding(ShowFindReplace, new KeyGesture(Key.H, ModifierKeys.Control)));

            Loaded += (s, e) => OnLoadFilterDataGrid(this, new DependencyPropertyChangedEventArgs());
            // Intercept Ctrl+A to optimize Select All on large datasets
            PreviewKeyDown += OnFilterDataGridPreviewKeyDown;

        }

        static FilterDataGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FilterDataGrid), new
                FrameworkPropertyMetadata(typeof(FilterDataGrid)));
        }

        #endregion Constructors

        #region Command

        public static readonly ICommand ApplyFilter = new RoutedCommand();
        public static readonly ICommand CancelFilter = new RoutedCommand();
        public static readonly ICommand ClearSearchBox = new RoutedCommand();
        public static readonly ICommand IsChecked = new RoutedCommand();
        public static readonly ICommand RemoveAllFilters = new RoutedCommand();
        public static readonly ICommand RemoveFilter = new RoutedCommand();
        public static readonly ICommand ShowFilter = new RoutedCommand();
        public static readonly ICommand ShowFindReplace = new RoutedCommand();

        #endregion Command

        #region Public DependencyProperty
        /// <summary>
        /// Threshold of item count after which Ctrl+A uses fast batched selection instead of selecting all at once.
        /// </summary>
        public static readonly DependencyProperty FastSelectAllThresholdProperty =
            DependencyProperty.Register("FastSelectAllThreshold",
                typeof(int),
                typeof(FilterDataGrid),
                new PropertyMetadata(5000));

        /// <summary>
        /// Batch size for fast Ctrl+A selection. Larger batches reduce overhead but may make UI less responsive.
        /// </summary>
        public static readonly DependencyProperty FastSelectAllBatchSizeProperty =
            DependencyProperty.Register("FastSelectAllBatchSize",
                typeof(int),
                typeof(FilterDataGrid),
                new PropertyMetadata(1000));

        /// <summary>
        /// Indicates if all filtered items are virtually selected (without setting IsSelected on individual items)
        /// </summary>
        public static readonly DependencyProperty IsAllSelectedProperty =
            DependencyProperty.Register("IsAllSelected",
                typeof(bool),
                typeof(FilterDataGrid),
                new PropertyMetadata(false, OnIsAllSelectedChanged));

        /// <summary>
        /// Threshold above which to use virtual selection instead of actual IsSelected property (default: 3000)
        /// </summary>
        public static readonly DependencyProperty VirtualSelectThresholdProperty =
            DependencyProperty.Register("VirtualSelectThreshold",
                typeof(int),
                typeof(FilterDataGrid),
                new PropertyMetadata(3000));

        /// <summary>
        ///     Excluded Fields (only AutoGeneratingColumn)
        /// </summary>
        public static readonly DependencyProperty ExcludeFieldsProperty =
            DependencyProperty.Register("ExcludeFields",
                typeof(string),
                typeof(FilterDataGrid),
                new PropertyMetadata(""));

        /// <summary>
        ///     Excluded Column (only AutoGeneratingColumn)
        /// </summary>
        public static readonly DependencyProperty ExcludeColumnsProperty =
            DependencyProperty.Register("ExcludeColumns",
                typeof(string),
                typeof(FilterDataGrid),
                new PropertyMetadata(""));

        /// <summary>
        ///     Date format displayed
        /// </summary>
        public static readonly DependencyProperty DateFormatStringProperty =
            DependencyProperty.Register("DateFormatString",
                typeof(string),
                typeof(FilterDataGrid),
                new PropertyMetadata("d"));

        /// <summary>
        ///     Language displayed
        /// </summary>
        public static readonly DependencyProperty FilterLanguageProperty =
            DependencyProperty.Register("FilterLanguage",
                typeof(Local),
                typeof(FilterDataGrid),
                new PropertyMetadata(Local.English));

        /// <summary>
        ///     Show status bar
        /// </summary>
        public static readonly DependencyProperty ShowStatusBarProperty =
            DependencyProperty.Register("ShowStatusBar",
                typeof(bool),
                typeof(FilterDataGrid),
                new PropertyMetadata(false));

        /// <summary>
        ///     Show Rows Count
        /// </summary>
        public static readonly DependencyProperty ShowRowsCountProperty =
            DependencyProperty.Register("ShowRowsCount",
                typeof(bool),
                typeof(FilterDataGrid),
                new PropertyMetadata(false));

        /// <summary>
        ///     Persistent filter
        /// </summary>
        public static readonly DependencyProperty PersistentFilterProperty =
            DependencyProperty.Register("PersistentFilter",
                typeof(bool),
                typeof(FilterDataGrid),
                new PropertyMetadata(false));

        /// <summary>
        ///     Show Select All Button
        /// </summary>
        public static readonly DependencyProperty ShowSelectAllButtonProperty =
            DependencyProperty.Register("ShowSelectAllButton",
                typeof(bool),
                typeof(FilterDataGrid),
                new PropertyMetadata(true));

        /// <summary>
        /// Name of the column to sum on group by
        /// </summary>
        public static readonly DependencyProperty ColumnSumOnGroupByProperty =
            DependencyProperty.Register("ColumnSumOnGroupBy",
                typeof(string),
                typeof(FilterDataGrid),
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// Name of the column to average on group by
        /// </summary>
        public static readonly DependencyProperty ColumnAverageOnGroupByProperty =
            DependencyProperty.Register("ColumnAverageOnGroupBy",
                typeof(string),
                typeof(FilterDataGrid),
                new PropertyMetadata(string.Empty));

        /// <summary>
        ///     Enable or disable Find and Replace feature
        /// </summary>
        public static readonly DependencyProperty EnableReplaceProperty =
            DependencyProperty.Register("EnableReplace",
                typeof(bool),
                typeof(FilterDataGrid),
                new PropertyMetadata(false));

        #endregion Public DependencyProperty

        #region Public Event

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler Sorted;

        #endregion Public Event

        #region Private Fields

        private const bool DebugMode = true;

        private string fileName = "persistentFilter.json";
        private Stopwatch stopWatchFilter = new();
        private DataGridColumnHeadersPresenter columnHeadersPresenter;
        private bool currentlyFiltering;
        private bool search;
        private Button button;

        private Cursor cursor;
        private int searchLength;
        private double minHeight;
        private double minWidth;
        private double sizableContentHeight;
        private double sizableContentWidth;
        private Grid sizableContentGrid;

        private List<string> excludedFields;
        private List<string> excludedColumns;
        private List<FilterItemDate> treeView;
        private List<FilterItem> listBoxItems;

        private Point popUpSize;
        private Popup popup;

        private string fieldName;
        private string lastFilter;
        private string searchText;
        private TextBox searchTextBox;
        private Thumb thumb;

        private TimeSpan elapsed;

        private Type collectionType;
        private Type fieldType;

        private bool startsWith;

        private readonly Dictionary<string, Predicate<object>> criteria = [];

        #endregion Private Fields

        #region Public Properties

        public string ColumnSumOnGroupBy
        {
            get => (string)GetValue(ColumnSumOnGroupByProperty);
            set => SetValue(ColumnSumOnGroupByProperty, value);
        }
        public string ColumnAverageOnGroupBy
        {
            get => (string)GetValue(ColumnAverageOnGroupByProperty);
            set => SetValue(ColumnAverageOnGroupByProperty, value);
        }

        /// <summary>
        ///     Excluded Fields (AutoGeneratingColumn)
        /// </summary>
        public string ExcludeFields
        {
            get => (string)GetValue(ExcludeFieldsProperty);
            set => SetValue(ExcludeFieldsProperty, value);
        }

        /// <summary>
        ///     Excluded Columns (AutoGeneratingColumn)
        /// </summary>
        public string ExcludeColumns
        {
            get => (string)GetValue(ExcludeColumnsProperty);
            set => SetValue(ExcludeColumnsProperty, value);
        }

        /// <summary>
        ///     The string begins with the specific character. Used in pop-up search box
        /// </summary>
        public bool StartsWith
        {
            get => startsWith;
            set
            {
                startsWith = value;
                OnPropertyChanged();

                // refresh filter
                if (!string.IsNullOrEmpty(searchText)) ItemCollectionView.Refresh();
            }
        }

        /// <summary>
        ///     Date format displayed
        /// </summary>
        public string DateFormatString
        {
            get => (string)GetValue(DateFormatStringProperty);
            set => SetValue(DateFormatStringProperty, value);
        }

        /// <summary>
        ///     Elapsed time
        /// </summary>
        public TimeSpan ElapsedTime
        {
            get => elapsed;
            set
            {
                elapsed = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        ///     Language
        /// </summary>
        public Local FilterLanguage
        {
            get => (Local)GetValue(FilterLanguageProperty);
            set => SetValue(FilterLanguageProperty, value);
        }

        /// <summary>
        ///     Display items count
        /// </summary>
        public int ItemsSourceCount { get; set; }

        /// <summary>
        ///     Show status bar
        /// </summary>
        public bool ShowStatusBar
        {
            get => (bool)GetValue(ShowStatusBarProperty);
            set => SetValue(ShowStatusBarProperty, value);
        }

        /// <summary>
        ///     Show rows count
        /// </summary>
        public bool ShowRowsCount
        {
            get => (bool)GetValue(ShowRowsCountProperty);
            set => SetValue(ShowRowsCountProperty, value);
        }

        /// <summary>
        ///     Instance of Loc
        /// </summary>
        public Loc Translate { get; set; }

        /// <summary>
        /// Tree View ItemsSource
        /// </summary>
        public List<FilterItemDate> TreeViewItems
        {
            get => treeView ?? [];
            set
            {
                treeView = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// ListBox ItemsSource
        /// </summary>
        public List<FilterItem> ListBoxItems
        {
            get => listBoxItems ?? [];
            set
            {
                listBoxItems = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Field Type
        /// </summary>
        public Type FieldType
        {
            get => fieldType;
            set
            {
                fieldType = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        ///     Persistent filter
        /// </summary>
        public bool PersistentFilter
        {
            get => (bool)GetValue(PersistentFilterProperty);
            set => SetValue(PersistentFilterProperty, value);
        }

        /// <summary>
        ///     Show Select All Button
        /// </summary>
        public bool ShowSelectAllButton
        {
            get => (bool)GetValue(ShowSelectAllButtonProperty);
            set => SetValue(ShowSelectAllButtonProperty, value);
        }

        /// <summary>
        ///     Enable or disable Find and Replace feature
        /// </summary>
        public bool EnableReplace
        {
            get => (bool)GetValue(EnableReplaceProperty);
            set => SetValue(EnableReplaceProperty, value);
        }

        /// <summary>
        /// Threshold of item count after which Ctrl+A uses fast batched selection instead of selecting all at once.
        /// Default 5000.
        /// </summary>
        public int FastSelectAllThreshold
        {
            get => (int)GetValue(FastSelectAllThresholdProperty);
            set => SetValue(FastSelectAllThresholdProperty, value);
        }

        /// <summary>
        /// Batch size for fast Ctrl+A selection. Default 1000.
        /// </summary>
        public int FastSelectAllBatchSize
        {
            get => (int)GetValue(FastSelectAllBatchSizeProperty);
            set => SetValue(FastSelectAllBatchSizeProperty, value);
        }

        /// <summary>
        /// Indicates if all filtered items are virtually selected
        /// </summary>
        public bool IsAllSelected
        {
            get => (bool)GetValue(IsAllSelectedProperty);
            set => SetValue(IsAllSelectedProperty, value);
        }

        /// <summary>
        /// Threshold above which to use virtual selection instead of actual IsSelected property. Default 3000.
        /// </summary>
        public int VirtualSelectThreshold
        {
            get => (int)GetValue(VirtualSelectThresholdProperty);
            set => SetValue(VirtualSelectThresholdProperty, value);
        }

        /// <summary>
        /// Indicates if currently in virtual selection mode
        /// </summary>
        public bool IsInVirtualSelectionMode { get; private set; } = false;

        /// <summary>
        /// Indicates if sorting is currently in progress
        /// </summary>
        public bool IsSorting
        {
            get => _isSorting;
            private set
            {
                if (_isSorting != value)
                {
                    _isSorting = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets the count of effectively selected items
        /// </summary>
        public int EffectiveSelectedItemsCount
        {
            get
            {
                if (IsInVirtualSelectionMode)
                {
                    var totalItems = CollectionViewSource?.Cast<object>().Count() ?? Items.Count;
                    return totalItems - _virtualSelectionExceptions.Count;
                }
                else
                {
                    return SelectedItems.Count;
                }
            }
        }

        #endregion Public Properties

        #region Private Properties

        private FilterCommon CurrentFilter { get; set; }
        private ICollectionView CollectionViewSource { get; set; }

        // Virtual selection support
        private HashSet<object> _virtualSelectionExceptions = [];
        private bool _isSorting = false;
        private ICollectionView ItemCollectionView { get; set; }
        private List<FilterCommon> GlobalFilterList { get; } = [];

        /// <summary>
        /// Popup filtered items (ListBox/TreeView)
        /// </summary>
        private IEnumerable<FilterItem> PopupViewItems =>
            ItemCollectionView?.OfType<FilterItem>().Where(c => c.Level != 0) ?? [];

        /// <summary>
        /// Popup source collection (ListBox/TreeView)
        /// </summary>
        private IEnumerable<FilterItem> SourcePopupViewItems =>
            ItemCollectionView?.SourceCollection.OfType<FilterItem>().Where(c => c.Level != 0) ??
            [];

        #endregion Private Properties

        #region Protected Methods

        // CALL ORDER :
        // Constructor
        // OnInitialized
        // OnItemsSourceChanged
        // OnLoaded

        /// <summary>
        ///     Initialize datagrid
        /// </summary>
        /// <param name="e"></param>
        protected override void OnInitialized(EventArgs e)
        {
            Debug.WriteLineIf(DebugMode, $"OnInitialized :{Name}");

            base.OnInitialized(e);

            try
            {
                // FilterLanguage : default : 0 (english)
                Translate = new Loc { Language = FilterLanguage };

                // fill excluded Fields list with values
                if (AutoGenerateColumns)
                {
                    excludedFields = [.. ExcludeFields.Split(',').Select(p => p.Trim())];
                    excludedColumns = [.. ExcludeColumns.Split(',').Select(p => p.Trim())];
                }
                // generating custom columns
                else if (collectionType != null) GeneratingCustomsColumn();

                // sorting event
                Sorted += OnSorted;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnInitialized : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Auto generated column, set templateHeader
        /// </summary>
        /// <param name="e"></param>
        protected override void OnAutoGeneratingColumn(DataGridAutoGeneratingColumnEventArgs e)
        {
            Debug.WriteLineIf(DebugMode, $"OnAutoGeneratingColumn : {e.PropertyName}");

            base.OnAutoGeneratingColumn(e);

            try
            {
                // ignore excluded columns
                if (excludedColumns.Any(
                        x => string.Equals(x, e.PropertyName, StringComparison.CurrentCultureIgnoreCase)))
                {
                    e.Cancel = true;
                    return;
                }

                // enable column sorting when user specified
                e.Column.CanUserSort = CanUserSortColumns;

                // return if the field is excluded
                if (excludedFields.Any(c =>
                        string.Equals(c, e.PropertyName, StringComparison.CurrentCultureIgnoreCase))) return;

                // template
                var template = (DataTemplate)TryFindResource("DataGridHeaderTemplate");

                // get type
                fieldType = Nullable.GetUnderlyingType(e.PropertyType) ?? e.PropertyType;

                // get type code
                var typeCode = Type.GetTypeCode(fieldType);

                if (fieldType.IsEnum)
                {
                    var column = new DataGridComboBoxColumn
                    {
                        ItemsSource = ((System.Windows.Controls.DataGridComboBoxColumn)e.Column).ItemsSource,
                        SelectedItemBinding = new Binding(e.PropertyName),
                        FieldName = e.PropertyName,
                        Header = e.Column.Header,
                        HeaderTemplate = template,
                        IsSingle = false, // eNum is not a unique value (unique identifier)
                        IsColumnFiltered = true
                    };

                    e.Column = column;
                }
                else if (typeCode == TypeCode.Boolean)
                {
                    var column = new DataGridCheckBoxColumn
                    {
                        Binding = new Binding(e.PropertyName) { ConverterCulture = Translate.Culture },
                        FieldName = e.PropertyName,
                        Header = e.Column.Header,
                        HeaderTemplate = template,
                        IsColumnFiltered = true
                    };

                    e.Column = column;
                }
                // TypeCode of numeric type, between 5 and 15
                else if ((int)typeCode is > 4 and < 16)
                {
                    var column = new DataGridNumericColumn()
                    {
                        Binding = new Binding(e.PropertyName) { ConverterCulture = Translate.Culture },
                        FieldName = e.PropertyName,
                        Header = e.Column.Header,
                        HeaderTemplate = template,
                        IsColumnFiltered = true
                    };

                    e.Column = column;
                }
                else
                {
                    var column = new DataGridTextColumn
                    {
                        Binding = new Binding(e.PropertyName) { ConverterCulture = Translate.Culture },
                        FieldName = e.PropertyName,
                        Header = e.Column.Header,
                        IsColumnFiltered = true
                    };

                    // apply the format string provided
                    if (typeCode == TypeCode.DateTime && !string.IsNullOrEmpty(DateFormatString))
                        column.Binding.StringFormat = DateFormatString;

                    // if the type does not belong to the "System" namespace, disable sorting
                    if (!fieldType.IsSystemType())
                    {
                        column.CanUserSort = false;

                        // if the type is a nested object (class), disable cell editing
                        column.IsReadOnly = fieldType.IsClass;
                    }
                    else
                    {
                        column.HeaderTemplate = template;
                    }

                    e.Column = column;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnAutoGeneratingColumn : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     The source of the Data grid items has been changed (refresh or on loading)
        /// </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            Debug.WriteLineIf(DebugMode, $"\nOnItemsSourceChanged Auto : {AutoGenerateColumns}");

            base.OnItemsSourceChanged(oldValue, newValue);

            try
            {
                // remove previous event : Contribution mcboothy
                if (oldValue is INotifyCollectionChanged collectionChanged)
                    collectionChanged.CollectionChanged -= ItemSourceCollectionChanged;

                if (newValue == null)
                {
                    RemoveFilters();

                    // remove custom HeaderTemplate
                    foreach (var col in Columns)
                    {
                        col.HeaderTemplate = null;
                    }
                    return;
                }

                if (oldValue != null)
                {
                    RemoveFilters();

                    // free previous resource
                    CollectionViewSource = System.Windows.Data.CollectionViewSource.GetDefaultView(new object());

                    // scroll to top on reload collection
                    var scrollViewer = GetTemplateChild("DG_ScrollViewer") as ScrollViewer;
                    scrollViewer?.ScrollToTop();
                }

                // add new event : Contribution mcboothy
                if (newValue is INotifyCollectionChanged changed)
                    changed.CollectionChanged += ItemSourceCollectionChanged;

                CollectionViewSource = System.Windows.Data.CollectionViewSource.GetDefaultView(ItemsSource);

                // set Filter, contribution : STEFAN HEIMEL
                if (CollectionViewSource.CanFilter) CollectionViewSource.Filter = Filter;

                ItemsSourceCount = Items.Count;
                ElapsedTime = new TimeSpan(0, 0, 0);

                OnPropertyChanged(nameof(ItemsSourceCount));
                OnPropertyChanged(nameof(GlobalFilterList));

                // get collection type
                // contribution : APFLKUACHA
                collectionType = ItemsSource is ICollectionView collectionView
                    ? collectionView.SourceCollection?.GetType().GenericTypeArguments.FirstOrDefault()
                    : ItemsSource?.GetType().GenericTypeArguments.FirstOrDefault();

                // set name of persistent filter json file
                // The name of the file is defined by the "Name" property of the FilterDatGrid, otherwise
                // the name of the source collection type is used
                if (collectionType != null)
                    fileName = !string.IsNullOrEmpty(Name) ? $"{Name}.json" : $"{collectionType?.Name}.json";

                // generating custom columns
                if (!AutoGenerateColumns && collectionType != null) GeneratingCustomsColumn();

                // re-evalutate the command's CanExecute.
                // when "IsReadOnly" is set to "False", "CanRemoveAllFilter" is not re-evaluated,
                // the "Remove All Filters" icon remains active
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnItemsSourceChanged : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Set the cursor to "Cursors.Wait" during a long sorting operation
        ///     https://stackoverflow.com/questions/8416961/how-can-i-be-notified-if-a-datagrid-column-is-sorted-and-not-sorting
        /// </summary>
        /// <param name="eventArgs"></param>
        protected override void OnSorting(DataGridSortingEventArgs eventArgs)
        {
            // Prevent multiple concurrent sorts
            if (IsSorting || currentlyFiltering || (popup?.IsOpen ?? false))
            {
                eventArgs.Handled = true;
                return;
            }

            IsSorting = true;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                // Get the column being sorted
                var column = eventArgs.Column;

                // Check if we can apply custom sorting
                if (CollectionViewSource != null && column is DataGridBoundColumn boundColumn)
                {
                    eventArgs.Handled = true;

                    // Get the property path for sorting
                    var sortPropertyName = column.SortMemberPath;
                    if (string.IsNullOrEmpty(sortPropertyName) && boundColumn.Binding is Binding binding)
                    {
                        sortPropertyName = binding.Path.Path;
                    }

                    if (!string.IsNullOrEmpty(sortPropertyName))
                    {
                        // Determine sort direction
                        var direction = column.SortDirection != ListSortDirection.Ascending
                            ? ListSortDirection.Ascending
                            : ListSortDirection.Descending;

                        // Apply custom sorting with natural number ordering
                        ApplyNaturalSort(sortPropertyName, direction);

                        // Update column sort direction
                        column.SortDirection = direction;

                        // Remove sort direction from other columns
                        foreach (var col in Columns)
                        {
                            if (col != column)
                            {
                                col.SortDirection = null;
                            }
                        }
                    }
                }
                else
                {
                    // Use default sorting for columns that don't support custom sorting
                    base.OnSorting(eventArgs);
                }

                Sorted?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                // Use async reset to ensure UI responsiveness
                _ = ResetSortingStateAsync();
            }
        }

        /// <summary>
        ///     Apply natural sort with numeric extraction
        /// </summary>
        /// <param name="propertyName">Property name to sort by</param>
        /// <param name="direction">Sort direction</param>
        private void ApplyNaturalSort(string propertyName, ListSortDirection direction)
        {
            try
            {
                if (CollectionViewSource == null) return;

                // Clear existing sort descriptions
                CollectionViewSource.SortDescriptions.Clear();

                // Create a custom comparer for natural sorting
                var comparer = new NaturalSortComparer(propertyName, direction);

                // Use LiveShaping if available for better performance
                if (CollectionViewSource is ICollectionViewLiveShaping liveShaping && liveShaping.CanChangeLiveSorting)
                {
                    liveShaping.LiveSortingProperties.Clear();
                    liveShaping.LiveSortingProperties.Add(propertyName);
                    liveShaping.IsLiveSorting = true;
                }

                // Apply custom sorting
                if (CollectionViewSource is ListCollectionView listView)
                {
                    listView.CustomSort = comparer;
                }
                else
                {
                    // Fallback to standard sort description
                    CollectionViewSource.SortDescriptions.Add(new SortDescription(propertyName, direction));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplyNaturalSort error: {ex.Message}");
                // Fallback to simple sort
                CollectionViewSource?.SortDescriptions.Add(new SortDescription(propertyName, direction));
            }
        }

        /// <summary>
        ///     Natural sort comparer that handles numeric values in strings
        /// </summary>
        private class NaturalSortComparer : IComparer
        {
            private readonly string _propertyName;
            private readonly ListSortDirection _direction;

            public NaturalSortComparer(string propertyName, ListSortDirection direction)
            {
                _propertyName = propertyName;
                _direction = direction;
            }

            public int Compare(object? x, object? y)
            {
                try
                {
                    if (x == null && y == null) return 0;
                    if (x == null) return _direction == ListSortDirection.Ascending ? -1 : 1;
                    if (y == null) return _direction == ListSortDirection.Ascending ? 1 : -1;

                    // Get property values
                    var xValue = x.GetPropertyValue(_propertyName);
                    var yValue = y.GetPropertyValue(_propertyName);

                    if (xValue == null && yValue == null) return 0;
                    if (xValue == null) return _direction == ListSortDirection.Ascending ? -1 : 1;
                    if (yValue == null) return _direction == ListSortDirection.Ascending ? 1 : -1;

                    // Convert to strings for comparison
                    var xString = xValue.ToString() ?? string.Empty;
                    var yString = yValue.ToString() ?? string.Empty;

                    // Try to extract numbers from the strings
                    var xNumber = Helpers.ExtractNumberFromName(xString);
                    var yNumber = Helpers.ExtractNumberFromName(yString);

                    // If both have numbers, compare numerically first
                    if (xNumber != 0 || yNumber != 0)
                    {
                        var numericComparison = xNumber.CompareTo(yNumber);
                        if (numericComparison != 0)
                        {
                            return _direction == ListSortDirection.Ascending ? numericComparison : -numericComparison;
                        }
                    }

                    // If numbers are equal or both zero, compare strings
                    var stringComparison = string.Compare(xString, yString, StringComparison.CurrentCultureIgnoreCase);
                    return _direction == ListSortDirection.Ascending ? stringComparison : -stringComparison;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"NaturalSortComparer.Compare error: {ex.Message}");
                    return 0;
                }
            }
        }

        private async Task ResetSortingStateAsync()
        {
            try
            {
                // Small delay to ensure sorting operation completes
                await Task.Delay(100);

                IsSorting = false;
                await ResetCursorAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ResetSortingStateAsync error: {ex.Message}");
                IsSorting = false;
            }
        }

        private async Task ResetCursorAsync()
        {
            await Dispatcher.BeginInvoke(() => { Mouse.OverrideCursor = null; },
                DispatcherPriority.ContextIdle);
        }

        /// <summary>
        ///     Adding Rows count
        /// </summary>
        /// <param name="e"></param>
        protected override void OnLoadingRow(DataGridRowEventArgs e)
        {
            if (ShowRowsCount)
            {
                var textBlock = new TextBlock
                {
                    Text = (e.Row.GetIndex() + 1).ToString(),
                    Padding = new Thickness(4, 0, 4, 0),
                    Margin = new Thickness(4, 0, 2, 0),
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                e.Row.Header = textBlock;
            }
            else
            {
                // Set empty content but maintain structure
                e.Row.Header = new TextBlock { Text = string.Empty };
            }

            // Update row selection visual for virtual selection mode
            if (IsInVirtualSelectionMode && e.Row.DataContext != null)
            {
                bool shouldBeSelected = !_virtualSelectionExceptions.Contains(e.Row.DataContext);
                // Just set the visual state, don't manipulate SelectedItems
                e.Row.IsSelected = shouldBeSelected;
            }

            base.OnLoadingRow(e);
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            // If in virtual selection mode and user is manually changing selection, exit virtual mode
            if (IsInVirtualSelectionMode && !_fastSelecting)
            {
                Debug.WriteLine($"Exiting virtual selection mode due to manual selection change. Removed: {e.RemovedItems.Count}, Added: {e.AddedItems.Count}");

                // Exit virtual mode and use normal DataGrid selection
                IsInVirtualSelectionMode = false;
                IsAllSelected = false;
                _virtualSelectionExceptions.Clear();

                // Force sync with bound collection
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    await Task.Delay(10); // Let selection settle
                    await DPUnity.Wpf.DpDataGrid.Behaviors.SelectedItemsBehavior.SyncSelectedItemsAsync(this, 0);
                }), DispatcherPriority.Background);
            }

            base.OnSelectionChanged(e);
        }

        #endregion Protected Methods

        #region Public Methods

        /// <summary>
        /// Access by the Host application to the method of loading active filters
        /// </summary>
        public void LoadPreset()
        {
            DeSerialize();
        }

        /// <summary>
        /// Access by the Host application to the method of saving active filters
        /// </summary>
        public void SavePreset()
        {
            Serialize();
        }

        /// <summary>
        ///     Remove All Filters
        /// </summary>
        public void RemoveFilters()
        {
            Debug.WriteLineIf(DebugMode, "RemoveFilters");

            ElapsedTime = new TimeSpan(0, 0, 0);

            try
            {
                foreach (var filterButton in GlobalFilterList.Select(filter => filter.FilterButton))
                {
                    FilterState.SetIsFiltered(filterButton, false);
                }

                // reset current filter
                CurrentFilter = null;
                criteria.Clear();
                GlobalFilterList.Clear();
                if (CollectionViewSource is IEditableCollectionView editable)
                {
                    if (editable.IsAddingNew) editable.CommitNew();
                    if (editable.IsEditingItem) editable.CommitEdit();
                }
                CollectionViewSource?.Refresh();

                // empty json file
                if (PersistentFilter) SavePreset();
            }
            catch (Exception ex)
            {
                Debug.WriteLineIf(DebugMode, $"RemoveFilters error : {ex.Message}");
                throw;
            }
        }

        #endregion Public Methods

        #region Private Methods

        private bool _fastSelecting;

        private async void OnFilterDataGridPreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (_fastSelecting)
                {
                    return;
                }

                // Optimize Ctrl+A (Select All)
                if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    // Only optimize for multi-select and large datasets
                    if (SelectionMode != DataGridSelectionMode.Single && Items != null && Items.Count >= FastSelectAllThreshold)
                    {
                        e.Handled = true;
                        _fastSelecting = true;

                        Mouse.OverrideCursor = Cursors.Wait;
                        try
                        {
                            // Use virtual selection for very large datasets
                            if (Items.Count >= VirtualSelectThreshold)
                            {
                                await VirtualSelectAllAsync();
                            }
                            else
                            {
                                // Suppress mirrored selection sync while DataGrid processes selection internally
                                DPUnity.Wpf.DpDataGrid.Behaviors.SelectedItemsBehavior.SetSuppressSelectionSync(this, true);

                                // Let DataGrid perform the selection
                                await Dispatcher.BeginInvoke(new Action(() => SelectAll()), DispatcherPriority.Normal);

                                // Yield to allow internal SelectedItems to settle
                                await Dispatcher.Yield(DispatcherPriority.Background);

                                // Bulk sync to bound SelectedItems (if any) in batches to keep UI responsive
                                int batch = Math.Max(0, FastSelectAllBatchSize);
                                await DPUnity.Wpf.DpDataGrid.Behaviors.SelectedItemsBehavior.SyncSelectedItemsAsync(this, batch);
                            }
                        }
                        finally
                        {
                            if (Items.Count < VirtualSelectThreshold)
                            {
                                DPUnity.Wpf.DpDataGrid.Behaviors.SelectedItemsBehavior.SetSuppressSelectionSync(this, false);
                            }
                            _fastSelecting = false;
                            ResetCursor();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fast SelectAll error: {ex.Message}");
            }
            finally
            {
                if (!e.Handled)
                {
                    base.OnPreviewKeyDown(e);
                }
            }
        }

        private void AdjustColumnWidth(DataGridColumn column)
        {
            double maxWidth = 0;

            // Lặp qua tất cả các item trong DataGrid để tìm chiều rộng lớn nhất
            foreach (var item in Items)
            {
                if (item != null && !CollectionView.NewItemPlaceholder.Equals(item)) // Bỏ qua placeholder
                {
                    if (ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row)
                    {
                        var cell = GetCell(row, column);
                        if (cell != null)
                        {
                            if (cell.Content is FrameworkElement content)
                            {
                                content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                                double cellWidth = content.DesiredSize.Width;
                                if (cellWidth > maxWidth)
                                {
                                    maxWidth = cellWidth;
                                }
                            }
                        }
                    }
                }
            }

            // Thêm padding và đặt chiều rộng cột
            maxWidth += 20; // Padding để tránh nội dung bị cắt
            column.Width = new DataGridLength(maxWidth);
            UpdateLayout(); // Cập nhật giao diện
        }

        // Phương thức tiện ích để lấy DataGridCell
        private DataGridCell GetCell(DataGridRow row, DataGridColumn column)
        {
            if (row == null || column == null) return null;

            var presenter = row.FindVisualChild<DataGridCellsPresenter>();
            if (presenter != null)
            {
                return presenter.ItemContainerGenerator.ContainerFromIndex(Columns.IndexOf(column)) as DataGridCell;
            }
            return null;
        }

        private void ColumnHeadersPresenter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Tìm header được double-click
            var header = VisualTreeHelpers.FindAncestor<DataGridColumnHeader>(e.OriginalSource as DependencyObject);
            if (header != null && header.Column != null)
            {
                AdjustColumnWidth(header.Column);
                e.Handled = true; // Ngăn sự kiện lan truyền thêm
            }
        }

        /// <summary>
        ///    Event handler for the "Loaded" event of the "FrameworkContentElement" class.
        /// </summary>
        /// <param name="filterDataGrid"></param>
        /// <param name="e"></param>
        private void OnLoadFilterDataGrid(FilterDataGrid filterDataGrid, DependencyPropertyChangedEventArgs e)
        {
            Debug.WriteLineIf(DebugMode, $"\tOnLoadFilterDataGrid {filterDataGrid?.Name}");

            base.OnApplyTemplate();

            if (GetTemplateChild("PART_ColumnHeadersPresenter") is DataGridColumnHeadersPresenter columnHeadersPresenter)
            {
                columnHeadersPresenter.MouseDoubleClick += ColumnHeadersPresenter_MouseDoubleClick;
            }
            if (filterDataGrid == null) return;

            filterDataGrid.SetupSelectionColumn();

            if (filterDataGrid.PersistentFilter)
                filterDataGrid.LoadPreset();
        }

        /// <summary>
        ///     Restore filters from json file
        ///     contribution : ericvdberge
        /// </summary>
        /// <param name="filterPreset">all the saved filters from a FilterDataGrid</param>
        private void OnFilterPresetChanged(List<FilterCommon> filterPreset)
        {
            Debug.WriteLineIf(DebugMode, "OnFilterPresetChanged");

            if (filterPreset == null || filterPreset.Count == 0) return;

            // Set cursor to wait
            Mouse.OverrideCursor = Cursors.Wait;

            // Remove all existing filters
            if (GlobalFilterList.Count > 0)
                RemoveFilters();

            // Reset previous elapsed time
            ElapsedTime = new TimeSpan(0, 0, 0);
            stopWatchFilter = Stopwatch.StartNew();

            try
            {
                foreach (var preset in filterPreset)
                {
                    // Get columns that match the preset field name and are filterable
                    var columns = Columns
                        .Where(c =>
                            c is DataGridTextColumn dtx && dtx.IsColumnFiltered && dtx.FieldName == preset.FieldName
                            || c is DataGridTemplateColumn dtp && dtp.IsColumnFiltered && dtp.FieldName == preset.FieldName
                            || c is DataGridCheckBoxColumn dck && dck.IsColumnFiltered && dck.FieldName == preset.FieldName
                            || c is DataGridNumericColumn dnm && dnm.IsColumnFiltered && dnm.FieldName == preset.FieldName
                            || c is DataGridComboBoxColumn cmb && cmb.IsColumnFiltered && cmb.FieldName == preset.FieldName
                            || c is DataGridEnumDescriptionColumn ded && ded.IsColumnFiltered && ded.FieldName == preset.FieldName
                            || c is DataGridStatusColumn dst && dst.IsColumnFiltered && dst.FieldName == preset.FieldName)
                        .ToList();

                    foreach (var col in columns)
                    {
                        // Get distinct values from the ItemsSource for the current column
                        var sourceObjectList = preset.FieldType == typeof(DateTime)
                            ? [.. Items.Cast<object>()
                                .Select(x => (object)((DateTime?)x.GetPropertyValue(preset.FieldName))?.Date)
                                .Distinct()]
                            : Items.Cast<object>()
                                .Select(x => x.GetPropertyValue(preset.FieldName))
                                .Distinct()
                                .ToList();

                        // Convert previously filtered items to the correct type
                        preset.PreviouslyFilteredItems = [.. preset.PreviouslyFilteredItems.Select(o => ConvertToType(o, preset.FieldType))];

                        // Get the items that are always present in the source collection
                        preset.FilteredItems = [.. sourceObjectList.Where(c => preset.PreviouslyFilteredItems.Contains(c))];

                        // if no items are filtered, continue to the next column
                        if (preset.FilteredItems.Count == 0)
                            continue;

                        preset.Translate = Translate;

                        var filterButton = VisualTreeHelpers.GetHeader(col, this)
                            ?.FindVisualChild<Button>("FilterButton");

                        preset.FilterButton = filterButton;

                        FilterState.SetIsFiltered(filterButton, true);

                        preset.AddFilter(criteria);

                        // Add current filter to GlobalFilterList
                        if (GlobalFilterList.All(f => f.FieldName != preset.FieldName))
                            GlobalFilterList.Add(preset);

                        // Set the current field name as the last filter name
                        lastFilter = preset.FieldName;
                    }
                }

                // Remove all predefined filters when there is no match with the source collection
                if (filterPreset.Count == 0)
                    RemoveFilters();

                // Save json file
                SavePreset();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnFilterPresetChanged : {ex.Message}");
                throw;
            }
            finally
            {
                // Apply filter
                CollectionViewSource.Refresh();

                stopWatchFilter.Stop();

                // Show elapsed time in UI
                ElapsedTime = stopWatchFilter.Elapsed;

                // Reset cursor
                ResetCursor();

                Debug.WriteLineIf(DebugMode, $"OnFilterPresetChanged Elapsed time : {ElapsedTime:mm\\:ss\\.ff}");
            }
        }

        /// <summary>
        /// Convert an object to the specified type.
        /// </summary>
        /// <param name="value">The object to convert.</param>
        /// <param name="type">The target type.</param>
        /// <returns>The converted object.</returns>
        private static object ConvertToType(object value, Type type)
        {
            try
            {
                if (type == typeof(DateTime))
                {
                    return DateTime.TryParse(value?.ToString(), out var dateTime) ? dateTime : (object)(DateTime?)null;
                }
                if (type.IsEnum)
                {
                    return Enum.Parse(type, value.ToString());
                }
                return Convert.ChangeType(value, type);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ConvertToType error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Serialize filters list
        /// </summary>
        private async void Serialize()
        {
            await Task.Run(() =>
            {
                var result = JsonConvert.Serialize(fileName, GlobalFilterList);
                Debug.WriteLineIf(DebugMode, $"Serialize : {result}");
            });
        }

        /// <summary>
        /// Deserialize json file
        /// </summary>
        private async void DeSerialize()
        {
            await Task.Run(() =>
            {
                var result = JsonConvert.Deserialize<List<FilterCommon>>(fileName);

                if (result == null) return;
                Dispatcher.BeginInvoke(() => { OnFilterPresetChanged(result); },
                    DispatcherPriority.Normal);

                Debug.WriteLineIf(DebugMode, $"DeSerialize : {result.Count}");
            });
        }

        /// <summary>
        /// Builds a tree structure from a collection of filter items.
        /// </summary>
        /// <param name="dates">The collection of filter items.</param>
        /// <returns>A list of FilterItemDate representing the tree structure.</returns>
        private async Task<List<FilterItemDate>> BuildTreeAsync(IEnumerable<FilterItem> dates)
        {
            var tree = new List<FilterItemDate>
            {
                new() {
                    Label = Translate.All, Level = 0, Initialize = true, FieldType = fieldType
                }
            };

            if (dates == null) return tree;

            try
            {
                var dateTimes = dates.Where(x => x.Level > 0).ToList();

                var years = dateTimes.GroupBy(
                    x => ((DateTime)x.Content).Year,
                    (key, group) => new FilterItemDate
                    {
                        Level = 1,
                        Content = key,
                        Label = key.ToString(Translate.Culture),
                        Initialize = true,
                        FieldType = fieldType,
                        Children = [.. group.GroupBy(
                                x => ((DateTime)x.Content).Month,
                                (monthKey, monthGroup) => new FilterItemDate
                                {
                                    Level = 2,
                                    Content = monthKey,
                                    Label = new DateTime(key, monthKey, 1).ToString("MMMM", Translate.Culture),
                                    Initialize = true,
                                    FieldType = fieldType,
                                    Children = [.. monthGroup.Select(x => new FilterItemDate
                                    {
                                        Level = 3,
                                        Content = ((DateTime)x.Content).Day,
                                        Label = ((DateTime)x.Content).ToString("dd", Translate.Culture),
                                        Initialize = true,
                                        FieldType = fieldType,
                                        Item = x
                                    })]
                                }
                            )]
                    }
                ).ToList();

                foreach (var year in years)
                {
                    foreach (var month in year.Children)
                    {
                        month.Parent = year;
                        foreach (var day in month.Children)
                        {
                            day.Parent = month;
                            // set the state of the "IsChecked" property based on the items already filtered (unchecked)
                            if (!day.Item.IsChecked)
                            {
                                // call the SetIsChecked method of the FilterItemDate class
                                day.IsChecked = false;
                                // reset with new state (isChanged == false)
                                day.Initialize = day.IsChecked;
                            }
                        }
                        // reset with new state
                        month.Initialize = month.IsChecked;
                    }
                    // reset with new state
                    year.Initialize = year.IsChecked;
                }

                tree.AddRange(years);

                if (dates.Any(x => x.Level == -1))
                {
                    var emptyItem = dates.First(x => x.Level == -1);
                    tree.Add(new FilterItemDate
                    {
                        Label = Translate.Empty,
                        Content = null,
                        Level = -1,
                        FieldType = fieldType,
                        Initialize = emptyItem.IsChecked,
                        Item = emptyItem,
                        Children = []
                    });
                }

                tree.First().Tree = tree;
                return await Task.FromResult(tree);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterCommon.BuildTree : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Handle Mousedown, contribution : WORDIBOI
        /// </summary>
        private readonly MouseButtonEventHandler onMousedown = (_, eArgs) => { eArgs.Handled = true; };

        /// <summary>
        ///     Generate custom columns that can be filtered
        /// </summary>
        private void GeneratingCustomsColumn()
        {
            Debug.WriteLineIf(DebugMode, "GeneratingCustomColumn");

            try
            {
                // get the columns that can be filtered
                // ReSharper disable MergeIntoPattern
                var columns = Columns
                    .Where(c => c is DataGridBoundColumn dbu && dbu.IsColumnFiltered
                                  || c is DataGridCheckBoxColumn dcb && dcb.IsColumnFiltered
                                  || c is DataGridComboBoxColumn dbx && dbx.IsColumnFiltered
                                  || c is DataGridEnumDescriptionColumn ded && ded.IsColumnFiltered
                                  || c is DataGridNumericColumn dnm && dnm.IsColumnFiltered
                                  || c is DataGridTemplateColumn dtp && dtp.IsColumnFiltered
                                  || c is DataGridTextColumn dtx && dtx.IsColumnFiltered
                                  || c is DataGridStatusColumn dst && dst.IsColumnFiltered
                    )
                    .Select(c => c)
                    .ToList();

                // set header template
                foreach (var col in columns)
                {
                    var columnType = col.GetType();

                    if (col.HeaderTemplate != null)
                    {
                        // Debug.WriteLineIf(DebugMode, "\tReset filter Button");

                        // reset filter Button
                        var buttonFilter = VisualTreeHelpers.GetHeader(col, this)
                            ?.FindVisualChild<Button>("FilterButton");

                        if (buttonFilter != null) FilterState.SetIsFiltered(buttonFilter, false);

                        // update the "ComboBoxItemsSource" custom property of "DataGridComboBoxColumn"
                        // this collection may change when loading a new source collection of the DataGrid.
                        if (columnType == typeof(DataGridComboBoxColumn))
                        {
                            var comboBoxColumn = (DataGridComboBoxColumn)col;
                            if (comboBoxColumn.IsSingle)
                            {
                                comboBoxColumn.UpdateItemsSourceAsync();
                            }
                        }
                        else if (columnType == typeof(DataGridEnumDescriptionColumn))
                        {
                            var enumDescriptionColumn = (DataGridEnumDescriptionColumn)col;
                            if (enumDescriptionColumn.IsSingle)
                            {
                                enumDescriptionColumn.UpdateItemsSourceAsync();
                            }
                        }
                    }
                    else
                    {
                        // Debug.WriteLineIf(DebugMode, "\tGenerate Columns");

                        fieldType = null;
                        var template = (DataTemplate)TryFindResource("DataGridHeaderTemplate");

                        if (columnType == typeof(DataGridTemplateColumn))
                        {
                            // DataGridTemplateColumn has no culture property
                            var column = (DataGridTemplateColumn)col;

                            if (string.IsNullOrEmpty(column.FieldName))
                                throw new ArgumentException("Value of \"FieldName\" property cannot be null.",
                                    nameof(DataGridTemplateColumn));
                            // template
                            column.HeaderTemplate = template;
                        }

                        if (columnType == typeof(DataGridBoundColumn))
                        {
                            var column = (DataGridBoundColumn)col;

                            column.FieldName = ((Binding)column.Binding).Path.Path;

                            // template
                            column.HeaderTemplate = template;

                            var fieldProperty = collectionType.GetProperty(((Binding)column.Binding).Path.Path);

                            // get type or underlying type if nullable
                            if (fieldProperty != null)
                                fieldType = Nullable.GetUnderlyingType(fieldProperty.PropertyType) ??
                                            fieldProperty.PropertyType;

                            // apply DateFormatString when StringFormat for column is not provided or empty
                            if (fieldType == typeof(DateTime) && !string.IsNullOrEmpty(DateFormatString))
                                if (string.IsNullOrEmpty(column.Binding.StringFormat))
                                    column.Binding.StringFormat = DateFormatString;

                            FieldType = fieldType;

                            // culture
                            if (((Binding)column.Binding).ConverterCulture == null)
                                ((Binding)column.Binding).ConverterCulture = Translate.Culture;
                        }

                        if (columnType == typeof(DataGridTextColumn))
                        {
                            var column = (DataGridTextColumn)col;

                            column.FieldName = ((Binding)column.Binding).Path.Path;

                            // template
                            column.HeaderTemplate = template;

                            var fieldProperty = collectionType.GetProperty(((Binding)column.Binding).Path.Path);

                            // get type or underlying type if nullable
                            if (fieldProperty != null)
                                fieldType = Nullable.GetUnderlyingType(fieldProperty.PropertyType) ??
                                            fieldProperty.PropertyType;

                            // apply DateFormatString when StringFormat for column is not provided or empty
                            if (fieldType == typeof(DateTime) && !string.IsNullOrEmpty(DateFormatString))
                                if (string.IsNullOrEmpty(column.Binding.StringFormat))
                                    column.Binding.StringFormat = DateFormatString;

                            FieldType = fieldType;

                            // culture
                            if (((Binding)column.Binding).ConverterCulture == null)
                                ((Binding)column.Binding).ConverterCulture = Translate.Culture;
                        }

                        if (columnType == typeof(DataGridCheckBoxColumn))
                        {
                            var column = (DataGridCheckBoxColumn)col;

                            column.FieldName = ((Binding)column.Binding).Path.Path;

                            // template
                            column.HeaderTemplate = template;

                            // culture
                            if (((Binding)column.Binding).ConverterCulture == null)
                                ((Binding)column.Binding).ConverterCulture = Translate.Culture;
                        }

                        if (columnType == typeof(DataGridComboBoxColumn))
                        {
                            var column = (DataGridComboBoxColumn)col;

                            if (column.ItemsSource == null) return;

                            var binding = (Binding)column.SelectedValueBinding ?? (Binding)column.SelectedItemBinding;

                            // check if binding is missing
                            if (binding != null)
                            {
                                column.FieldName = binding.Path.Path;

                                // template
                                column.HeaderTemplate = template;

                                var fieldProperty = collectionType.GetPropertyInfo(column.FieldName);

                                // get type or underlying type if nullable
                                if (fieldProperty != null)
                                    fieldType = Nullable.GetUnderlyingType(fieldProperty.PropertyType) ??
                                                fieldProperty.PropertyType;

                                // check if it is a unique id type and not nested object
                                column.IsSingle = fieldType.IsSystemType();

                                // culture
                                binding.ConverterCulture ??= Translate.Culture;
                            }
                            else
                            {
                                throw new ArgumentException(
                                    "Value of \"SelectedValueBinding\" property or \"SelectedItemBinding\" cannot be null.",
                                    nameof(DataGridComboBoxColumn));
                            }
                        }

                        if (columnType == typeof(DataGridEnumDescriptionColumn))
                        {
                            var column = (DataGridEnumDescriptionColumn)col;

                            if (column.ItemsSource == null) return;

                            var binding = (Binding)column.SelectedValueBinding ?? (Binding)column.SelectedItemBinding;

                            // check if binding is missing
                            if (binding != null)
                            {
                                column.FieldName = binding.Path.Path;

                                // template
                                column.HeaderTemplate = template;

                                var fieldProperty = collectionType.GetPropertyInfo(column.FieldName);

                                // get type or underlying type if nullable
                                if (fieldProperty != null)
                                    fieldType = Nullable.GetUnderlyingType(fieldProperty.PropertyType) ??
                                                fieldProperty.PropertyType;

                                // check if it is a unique id type and not nested object
                                column.IsSingle = fieldType.IsSystemType();

                                // culture
                                binding.ConverterCulture ??= Translate.Culture;
                            }
                            else
                            {
                                throw new ArgumentException(
                                    "Value of \"SelectedValueBinding\" property or \"SelectedItemBinding\" cannot be null.",
                                    nameof(DataGridEnumDescriptionColumn));
                            }
                        }

                        if (columnType == typeof(DataGridNumericColumn))
                        {
                            var column = (DataGridNumericColumn)col;

                            column.FieldName = ((Binding)column.Binding).Path.Path;

                            // template
                            column.HeaderTemplate = template;

                            // culture
                            if (((Binding)column.Binding).ConverterCulture == null)
                                ((Binding)column.Binding).ConverterCulture = Translate.Culture;
                        }
                        if (columnType == typeof(DataGridStatusColumn))
                        {
                            var column = (DataGridStatusColumn)col;

                            if (column.HeaderTemplate == null)
                            {
                                column.FieldName = ((Binding)column.Binding).Path.Path;

                                // template
                                column.HeaderTemplate = template;

                                // get field type
                                var fieldProperty = collectionType.GetPropertyInfo(column.FieldName);
                                if (fieldProperty != null)
                                    fieldType = Nullable.GetUnderlyingType(fieldProperty.PropertyType) ??
                                                fieldProperty.PropertyType;

                                // culture
                                if (((Binding)column.Binding).ConverterCulture == null)
                                    ((Binding)column.Binding).ConverterCulture = Translate.Culture;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GeneratingCustomColumn : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Reset the cursor at the end of the sort
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSorted(object sender, EventArgs e)
        {
            ResetCursor();
        }

        /// <summary>
        ///     Reset cursor
        /// </summary>
        private async void ResetCursor()
        {
            // reset cursor
            // Cast Action : compatibility Net4.8
            await Dispatcher.BeginInvoke(() => { Mouse.OverrideCursor = null; },
                DispatcherPriority.ContextIdle);
        }

        #region Virtual Selection

        private static void OnIsAllSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FilterDataGrid grid)
            {
                grid.OnIsAllSelectedChanged((bool)e.NewValue);
            }
        }

        private void OnIsAllSelectedChanged(bool isAllSelected)
        {
            if (isAllSelected)
            {
                _virtualSelectionExceptions.Clear();
                IsInVirtualSelectionMode = true;
            }
            else
            {
                _virtualSelectionExceptions.Clear();
                IsInVirtualSelectionMode = false;
            }

            // Refresh visual state of rows
            InvalidateArrange();
        }

        private async Task VirtualSelectAllAsync()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Debug.WriteLine($"[VirtualSelectAll] Starting - Items.Count: {Items.Count}, Threshold: {VirtualSelectThreshold}");

                // Enable virtual select all mode instantly
                IsAllSelected = true;
                IsInVirtualSelectionMode = true;
                _virtualSelectionExceptions.Clear();

                Debug.WriteLine($"[VirtualSelectAll] Virtual mode enabled in {stopwatch.ElapsedMilliseconds}ms");

                // Don't clear SelectedItems - instead populate it with visible items for visual feedback
                // Use a small batch to make selection appear fast but not freeze UI
                await PopulateVisibleRowSelection();

                Debug.WriteLine($"[VirtualSelectAll] Visible rows populated in {stopwatch.ElapsedMilliseconds}ms");

                // Notify UI that selection changed
                await Dispatcher.BeginInvoke(() =>
                {
                    OnPropertyChanged(nameof(SelectedItems));
                    OnPropertyChanged(nameof(EffectiveSelectedItemsCount));
                }, DispatcherPriority.Background);

                Debug.WriteLine($"[VirtualSelectAll] Completed in {stopwatch.ElapsedMilliseconds}ms - EffectiveSelectedItemsCount: {EffectiveSelectedItemsCount}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VirtualSelectAllAsync error: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        private async Task PopulateVisibleRowSelection()
        {
            try
            {
                // Select only currently visible rows for immediate visual feedback
                var containers = new List<DataGridRow>();
                for (int i = 0; i < Items.Count && containers.Count < 50; i++)
                {
                    if (ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow container)
                    {
                        containers.Add(container);
                    }
                }

                // Select these rows in small batches
                for (int i = 0; i < containers.Count; i += 10)
                {
                    for (int j = i; j < Math.Min(i + 10, containers.Count); j++)
                    {
                        var row = containers[j];
                        if (row.DataContext != null && !SelectedItems.Contains(row.DataContext))
                        {
                            SelectedItems.Add(row.DataContext);
                        }
                    }

                    // Yield every 10 items
                    if (i + 10 < containers.Count)
                    {
                        await Task.Yield();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PopulateVisibleRowSelection error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the effective selected items, considering virtual selection
        /// </summary>
        public IEnumerable GetEffectiveSelectedItems()
        {
            if (IsInVirtualSelectionMode)
            {
                // Return all filtered items except exceptions
                var allItems = new List<object>();

                if (CollectionViewSource != null)
                {
                    foreach (var item in CollectionViewSource)
                    {
                        if (!_virtualSelectionExceptions.Contains(item))
                        {
                            allItems.Add(item);
                        }
                    }
                }
                else
                {
                    foreach (var item in Items)
                    {
                        if (!_virtualSelectionExceptions.Contains(item))
                        {
                            allItems.Add(item);
                        }
                    }
                }

                return allItems;
            }
            else
            {
                return SelectedItems;
            }
        }

        /// <summary>
        /// Checks if an item is effectively selected, considering virtual selection
        /// </summary>
        public bool IsItemEffectivelySelected(object item)
        {
            if (IsInVirtualSelectionMode)
            {
                return !_virtualSelectionExceptions.Contains(item);
            }
            else
            {
                return SelectedItems.Contains(item);
            }
        }

        /// <summary>
        /// Clears virtual selection and returns to normal selection mode
        /// </summary>
        public void ClearVirtualSelection()
        {
            if (IsInVirtualSelectionMode)
            {
                IsInVirtualSelectionMode = false;
                IsAllSelected = false;
                _virtualSelectionExceptions.Clear();
                SelectedItems.Clear();

                OnPropertyChanged(nameof(EffectiveSelectedItemsCount));
                Debug.WriteLine("[VirtualSelectAll] Cleared virtual selection");
            }
        }

        #endregion Virtual Selection

        /// <summary>
        ///     Can Apply filter (popup Ok button)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CanApplyFilter(object sender, CanExecuteRoutedEventArgs e)
        {
            // CanExecute only when the popup is open
            if ((popup?.IsOpen ?? false) == false)
            {
                e.CanExecute = false;
            }
            else
            {
                if (search)
                    e.CanExecute = PopupViewItems.Any(f => f?.IsChecked == true);
                else
                    e.CanExecute = PopupViewItems.Any(f => f.IsChanged) &&
                                   PopupViewItems.Any(f => f?.IsChecked == true);
            }
        }

        /// <summary>
        ///     Cancel button, close popup
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CancelFilterCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (popup == null) return;
            popup.IsOpen = false; // raise EventArgs PopupClosed
        }

        /// <summary>
        /// Can remove all filter when GlobalFilterList.Count > 0
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CanRemoveAllFilter(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = GlobalFilterList.Count > 0;
        }

        /// <summary>
        ///     Can remove filter when current column (CurrentFilter) filtered
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CanRemoveFilter(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CurrentFilter?.IsFiltered ?? false;
        }

        /// <summary>
        ///     Can show filter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CanShowFilter(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CollectionViewSource?.CanFilter == true && (!popup?.IsOpen ?? true) && !currentlyFiltering;
        }

        /// <summary>
        ///     Check/uncheck all item when the action is (select all)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckedAllCommand(object sender, ExecutedRoutedEventArgs e)
        {
            var item = (FilterItem)e.Parameter;

            // only when the item[0] (select all) is checked or unchecked
            if (ItemCollectionView == null) return;

            if (item.Level == 0)
            {
                foreach (var obj in PopupViewItems.Where(f => f.IsChecked != item.IsChecked))
                {
                    obj.IsChecked = item.IsChecked;
                }
            }
            // check if first item select all checkbox (in case of bool?, first item is Unchecked)
            else if (ListBoxItems[0].Level == 0)
            {
                // update select all item status
                ListBoxItems[0].IsChecked = PopupViewItems.All(i => i.IsChecked);
            }
        }

        /// <summary>
        ///     Clear Search Box text
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="routedEventArgs"></param>
        private void ClearSearchBoxClick(object sender, RoutedEventArgs routedEventArgs)
        {
            search = false;
            searchTextBox.Text = string.Empty; // raises TextChangedEventArgs
        }

        /// <summary>
        ///     Aggregate list of predicate as filter
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        private bool Filter(object o)
        {
            return criteria.Values
                .Aggregate(true, (prevValue, predicate) => prevValue && predicate(o));
        }

        /// <summary>
        ///     OnPropertyChange
        /// </summary>
        /// <param name="propertyName"></param>
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        ///     On Resize Thumb Drag Completed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnResizeThumbDragCompleted(object sender, DragCompletedEventArgs e)
        {
            Cursor = cursor;
        }

        /// <summary>
        ///     Get delta on drag thumb
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnResizeThumbDragDelta(object sender, DragDeltaEventArgs e)
        {
            // initialize the first Actual size Width/Height
            if (sizableContentHeight <= 0)
            {
                sizableContentHeight = sizableContentGrid.ActualHeight;
                sizableContentWidth = sizableContentGrid.ActualWidth;
            }

            var yAdjust = sizableContentGrid.Height + e.VerticalChange;
            var xAdjust = sizableContentGrid.Width + e.HorizontalChange;

            //make sure not to resize to negative width or height
            xAdjust = sizableContentGrid.ActualWidth + xAdjust > minWidth ? xAdjust : minWidth;
            yAdjust = sizableContentGrid.ActualHeight + yAdjust > minHeight ? yAdjust : minHeight;

            xAdjust = xAdjust < minWidth ? minWidth : xAdjust;
            yAdjust = yAdjust < minHeight ? minHeight : yAdjust;

            // set size of grid
            sizableContentGrid.Width = xAdjust;
            sizableContentGrid.Height = yAdjust;
        }

        /// <summary>
        ///     On Resize Thumb DragStarted
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnResizeThumbDragStarted(object sender, DragStartedEventArgs e)
        {
            cursor = Cursor;
            Cursor = Cursors.SizeNWSE;
        }

        /// <summary>
        ///     Reset the size of popup to original size
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PopupClosed(object sender, EventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "PopupClosed");

            var pop = (Popup)sender;

            // free the resources if the popup is closed without filtering
            if (!currentlyFiltering)
            {
                CurrentFilter = null;
                ItemCollectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(new object());
                ResetCursor();
            }

            // free the resources, unsubscribe from event and re-enable columnHeadersPresenter
            pop.Closed -= PopupClosed;
            pop.MouseDown -= onMousedown;
            searchTextBox.TextChanged -= SearchTextBoxOnTextChanged;
            thumb.DragCompleted -= OnResizeThumbDragCompleted;
            thumb.DragDelta -= OnResizeThumbDragDelta;
            thumb.DragStarted -= OnResizeThumbDragStarted;

            sizableContentGrid.Width = sizableContentWidth;
            sizableContentGrid.Height = sizableContentHeight;
            Cursor = cursor;

            // once the popup is closed, this is no longer necessary
            ListBoxItems = [];
            TreeViewItems = [];

            // re-enable columnHeadersPresenter
            if (columnHeadersPresenter != null)
                columnHeadersPresenter.IsEnabled = true;
        }

        /// <summary>
        ///     Remove All Filter Command
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveAllFilterCommand(object sender, ExecutedRoutedEventArgs e)
        {
            RemoveFilters();
        }

        /// <summary>
        ///     Remove current filter
        /// </summary>
        private void RemoveCurrentFilter()
        {
            Debug.WriteLineIf(DebugMode, "RemoveCurrentFilter");

            if (CurrentFilter == null) return;

            popup.IsOpen = false; // raise PopupClosed event

            // reset button icon
            FilterState.SetIsFiltered(CurrentFilter.FilterButton, false);

            ElapsedTime = new TimeSpan(0, 0, 0);
            stopWatchFilter = Stopwatch.StartNew();

            Mouse.OverrideCursor = Cursors.Wait;

            if (CurrentFilter.IsFiltered && criteria.Remove(CurrentFilter.FieldName))
                CollectionViewSource.Refresh();

            if (GlobalFilterList.Contains(CurrentFilter))
                GlobalFilterList.Remove(CurrentFilter);

            // set the last filter applied
            lastFilter = GlobalFilterList.LastOrDefault()?.FieldName;

            CurrentFilter = null;
            ResetCursor();

            if (PersistentFilter)
                SavePreset();

            stopWatchFilter.Stop();
            ElapsedTime = stopWatchFilter.Elapsed;
        }

        /// <summary>
        ///     Remove Current Filter Command
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveFilterCommand(object sender, ExecutedRoutedEventArgs e)
        {
            RemoveCurrentFilter();
        }

        /// <summary>
        ///     Apply the filter to the items in the popup List/Treeview
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private bool SearchFilter(object obj)
        {
            var item = (FilterItem)obj;
            if (string.IsNullOrEmpty(searchText) || item == null || item.Level == 0) return true;

            var content = Convert.ToString(item.Content, Translate.Culture);

            // Contains
            if (!StartsWith)
                return Translate.Culture.CompareInfo.IndexOf(content ?? string.Empty, searchText,
                    CompareOptions.OrdinalIgnoreCase) >= 0;

            // StartsWith preserve RangeOverflow
            if (searchLength > item.ContentLength) return false;

            return Translate.Culture.CompareInfo.IndexOf(content ?? string.Empty, searchText, 0, searchLength,
                CompareOptions.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        ///     Search TextBox Text Changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SearchTextBoxOnTextChanged(object sender, TextChangedEventArgs e)
        {
            e.Handled = true;
            var textBox = (TextBox)sender;

            // fix TextChanged event fires twice I did not find another solution
            if (textBox == null || textBox.Text == searchText || ItemCollectionView == null) return;

            searchText = textBox.Text;

            searchLength = searchText.Length;

            search = !string.IsNullOrEmpty(searchText);

            // apply filter (call the SearchFilter method)
            ItemCollectionView.Refresh();

            if (CurrentFilter.FieldType != typeof(DateTime) || treeView == null) return;

            // rebuild treeView
            if (string.IsNullOrEmpty(searchText))
            {
                // populate the tree with items from the source list
                TreeViewItems = await BuildTreeAsync(SourcePopupViewItems);
            }
            else
            {
                // searchText is not empty
                // populate the tree only with items found by the search
                var items = PopupViewItems.Where(i => i.IsChecked).ToList();

                // if at least one element is not null, fill the tree, otherwise the tree contains only the element (select all).
                TreeViewItems = await BuildTreeAsync(items.Count != 0 ? items : null);
            }
        }

        /// <summary>
        ///     Open a pop-up window, Click on the header button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ShowFilterCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "\r\nShowFilterCommand");

            // reset previous elapsed time
            ElapsedTime = new TimeSpan(0, 0, 0);
            stopWatchFilter = Stopwatch.StartNew();

            // clear search text (!important)
            searchText = string.Empty;
            search = false;

            try
            {
                // filter button
                button = (Button)e.OriginalSource;

                if (Items.Count == 0 || button == null) return;

                // contribution : OTTOSSON
                // for the moment this functionality is not tested, I do not know if it can cause unexpected effects
                _ = CommitEdit(DataGridEditingUnit.Row, true);

                // navigate up to the current header and get column type
                var header = VisualTreeHelpers.FindAncestor<DataGridColumnHeader>(button);
                var headerColumn = header.Column;

                // then down to the current popup
                popup = VisualTreeHelpers.FindChild<Popup>(header, "FilterPopup");
                columnHeadersPresenter = VisualTreeHelpers.FindAncestor<DataGridColumnHeadersPresenter>(header);

                if (popup == null || columnHeadersPresenter == null) return;

                // disable columnHeadersPresenter while popup is open
                if (columnHeadersPresenter != null)
                    columnHeadersPresenter.IsEnabled = false;

                // popup handle event
                popup.Closed += PopupClosed;

                // disable popup background click-through, contribution : WORDIBOI
                popup.MouseDown += onMousedown;

                // resizable grid
                sizableContentGrid = VisualTreeHelpers.FindChild<Grid>(popup.Child, "SizableContentGrid");

                // search textbox
                searchTextBox = VisualTreeHelpers.FindChild<TextBox>(popup.Child, "SearchBox");
                searchTextBox.Text = string.Empty;
                searchTextBox.TextChanged += SearchTextBoxOnTextChanged;
                searchTextBox.Focusable = true;

                // thumb resize grip
                thumb = VisualTreeHelpers.FindChild<Thumb>(sizableContentGrid, "PopupThumb");

                // minimum size of Grid
                sizableContentHeight = 0;
                sizableContentWidth = 0;

                sizableContentGrid.Height = popUpSize.Y;
                sizableContentGrid.MinHeight = popUpSize.Y;

                minHeight = sizableContentGrid.MinHeight;
                minWidth = sizableContentGrid.MinWidth;

                // thumb handle event
                thumb.DragCompleted += OnResizeThumbDragCompleted;
                thumb.DragDelta += OnResizeThumbDragDelta;
                thumb.DragStarted += OnResizeThumbDragStarted;

                List<FilterItem> filterItemList = null;
                DataGridComboBoxColumn comboxColumn = null;
                DataGridEnumDescriptionColumn enumColumn = null;

                // get field name from binding Path
                // ReSharper disable once ConvertIfStatementToSwitchStatement
                if (headerColumn is DataGridTextColumn textColumn)
                {
                    fieldName = textColumn.FieldName;
                }
                if (headerColumn is DataGridBoundColumn templateBound)
                {
                    fieldName = templateBound.FieldName;
                }
                if (headerColumn is DataGridTemplateColumn templateColumn)
                {
                    fieldName = templateColumn.FieldName;
                }
                if (headerColumn is DataGridCheckBoxColumn checkBoxColumn)
                {
                    fieldName = checkBoxColumn.FieldName;
                }
                if (headerColumn is DataGridNumericColumn numericColumn)
                {
                    fieldName = numericColumn.FieldName;
                }
                if (headerColumn is DataGridComboBoxColumn comboBoxColumn)
                {
                    fieldName = comboBoxColumn.FieldName;
                    comboxColumn = comboBoxColumn;
                }
                if (headerColumn is DataGridEnumDescriptionColumn enumDescriptionColumn)
                {
                    fieldName = enumDescriptionColumn.FieldName;
                    enumColumn = enumDescriptionColumn;
                }
                if (headerColumn is DataGridStatusColumn statusColumn)
                {
                    fieldName = statusColumn.FieldName;
                }
                // invalid fieldName
                if (string.IsNullOrEmpty(fieldName)) return;

                // see Extensions helper for GetPropertyInfo
                var fieldProperty = collectionType.GetPropertyInfo(fieldName);

                // get type or underlying type if nullable
                if (fieldProperty != null)
                    FieldType = Nullable.GetUnderlyingType(fieldProperty.PropertyType) ?? fieldProperty.PropertyType;

                // If no filter, add filter to GlobalFilterList list
                CurrentFilter = GlobalFilterList.FirstOrDefault(f => f.FieldName == fieldName) ??
                                new FilterCommon
                                {
                                    FieldName = fieldName,
                                    FieldType = fieldType,
                                    Translate = Translate,
                                    FilterButton = button
                                };

                // set cursor
                Mouse.OverrideCursor = Cursors.Wait;

                // contribution : STEFAN HEIMEL
                await Dispatcher.InvokeAsync(() =>
                {
                    // list for all items values, filtered and unfiltered (previous filtered items)
                    List<object> sourceObjectList;

                    // remove NewItemPlaceholder added by DataGrid when user can add row
                    // !System.Windows.Data.CollectionView.NewItemPlaceholder.Equals(x): Explicitly excludes the new entry row.
                    // Testing on DataGridRow is an additional safety feature, but the most important thing is to exclude the NewItemPlaceholder.

                    // get the list of raw values of the current column
                    if (fieldType == typeof(DateTime))
                    {
                        // possible distinct values because time part is removed
                        sourceObjectList = [.. Items.Cast<object>()
                            .Where(x => x is not DataGridRow && !CollectionView.NewItemPlaceholder.Equals(x))
                            .Select(x => (object)((DateTime?)x.GetPropertyValue(fieldName))?.Date)
                            .Distinct()];
                    }
                    else
                    {
                        sourceObjectList = [.. Items.Cast<object>()
                            .Where(x => x is not DataGridRow && !CollectionView.NewItemPlaceholder.Equals(x))
                            .Select(x => x.GetPropertyValue(fieldName))
                            .Distinct()];
                    }

                    // adds the previous filtered items to the list of new items (CurrentFilter.PreviouslyFilteredItems)
                    if (lastFilter == CurrentFilter.FieldName)
                    {
                        sourceObjectList.AddRange(CurrentFilter?.PreviouslyFilteredItems ?? []);
                    }

                    // empty item flag
                    // if they exist, remove all null or empty string values from the list.
                    // content == null and content == "" are two different things but both labeled as (blank)
                    var emptyItem = sourceObjectList.RemoveAll(v => v == null || v.Equals(string.Empty)) > 0;

                    // TODO : AggregateException when user can add row

                    // sorting is a very slow operation, using ParallelQuery
                    sourceObjectList = sourceObjectList.AsParallel().OrderBy(x => x).ToList();

                    if (fieldType == typeof(bool))
                    {
                        filterItemList = new List<FilterItem>(sourceObjectList.Count + 1);
                    }
                    else
                    {
                        // add the first element (select all) at the top of list
                        filterItemList = new List<FilterItem>(sourceObjectList.Count + 2)
                        {
                            // contribution : damonpkuml
                            new() { Label = Translate.All, IsChecked = CurrentFilter?.PreviouslyFilteredItems.Count==0, Level = 0 }
                        };
                    }

                    // add all items (not null) to the filterItemList,
                    // the list of dates is calculated by BuildTree from this list
                    filterItemList.AddRange(sourceObjectList.Select(item => new FilterItem
                    {
                        Content = item,
                        ContentLength = item?.ToString().Length ?? 0,
                        FieldType = fieldType,
                        Label = GetLabel(item, fieldType),
                        Level = 1,
                        Initialize = CurrentFilter.PreviouslyFilteredItems?.Contains(item) == false
                    }));

                    // add a empty item(if exist) at the bottom of the list
                    if (emptyItem)
                    {
                        sourceObjectList.Insert(sourceObjectList.Count, null);

                        filterItemList.Add(new FilterItem
                        {
                            FieldType = fieldType,
                            Content = null,
                            Label = fieldType == typeof(bool) ? Translate.Indeterminate : Translate.Empty,
                            Level = -1,
                            Initialize = CurrentFilter?.PreviouslyFilteredItems?.Contains(null) == false
                        });
                    }

                    string GetLabel(object o, Type type)
                    {
                        if (comboxColumn != null)
                        {
                            // Try to get ItemsSource directly (handles both binding and static resources like proxy)
                            var items = comboxColumn.ItemsSource;

                            if (items != null &&
                                string.IsNullOrEmpty(comboxColumn.DisplayMemberPath) &&
                                string.IsNullOrEmpty(comboxColumn.SelectedValuePath))
                            {
                                // Simple collection where items are the values themselves
                                foreach (var item in items)
                                {
                                    if (item != null && item.Equals(o))
                                    {
                                        return item.ToString();
                                    }
                                }
                            }

                            // retrieve the label of the list previously reconstituted from "ItemsSource" of the combobox
                            if (comboxColumn.IsSingle == true)
                            {
                                return comboxColumn.ComboBoxItemsSource
                                    ?.FirstOrDefault(x => x.SelectedValue == o.ToString())?.DisplayMember;
                            }

                            var itemsSourceBinding = BindingOperations.GetBinding(comboxColumn, System.Windows.Controls.DataGridComboBoxColumn.ItemsSourceProperty);
                            bool isUsingEnumConverter = itemsSourceBinding?.Converter is EnumToKeyValueListConverter;

                            if (isUsingEnumConverter) // If using EnumToKeyValueListConverter => use description as field name
                            {
                                if (items != null)
                                {
                                    // Try to find a matching key-value pair where the key matches our value
                                    foreach (var item in items)
                                    {
                                        // Get the Key (enum value) using reflection
                                        var keyProperty = item.GetType().GetProperty(comboxColumn.SelectedValuePath);
                                        if (keyProperty != null)
                                        {
                                            var key = keyProperty.GetValue(item);
                                            // If we found the matching enum value
                                            if (key != null && key.ToString() == o.ToString())
                                            {
                                                // Get the Value (description) using reflection
                                                var valueProperty = item.GetType().GetProperty(comboxColumn.DisplayMemberPath);
                                                if (valueProperty != null)
                                                {
                                                    return valueProperty.GetValue(item)?.ToString();
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            // Handle ItemsSource collections with DisplayMemberPath (including proxy bindings)
                            else if (items != null && !string.IsNullOrEmpty(comboxColumn.DisplayMemberPath))
                            {
                                // Try to find display value from ItemsSource using DisplayMemberPath
                                foreach (var item in items)
                                {
                                    // Get the value that should match (SelectedValuePath or the item itself)
                                    var valueToMatch = o;
                                    if (!string.IsNullOrEmpty(comboxColumn.SelectedValuePath))
                                    {
                                        var valueProperty = item.GetType().GetProperty(comboxColumn.SelectedValuePath);
                                        if (valueProperty != null)
                                        {
                                            var itemValue = valueProperty.GetValue(item);
                                            if (itemValue != null && itemValue.Equals(valueToMatch))
                                            {
                                                // Found matching item, get display value
                                                var displayProperty = item.GetType().GetProperty(comboxColumn.DisplayMemberPath);
                                                if (displayProperty != null)
                                                {
                                                    return displayProperty.GetValue(item)?.ToString();
                                                }
                                            }
                                        }
                                    }
                                    else if (item.Equals(valueToMatch))
                                    {
                                        // No SelectedValuePath, item itself is the value
                                        var displayProperty = item.GetType().GetProperty(comboxColumn.DisplayMemberPath);
                                        if (displayProperty != null)
                                        {
                                            return displayProperty.GetValue(item)?.ToString();
                                        }
                                    }
                                }
                            }
                        }

                        if (enumColumn != null)
                        {
                            if (enumColumn.ComboBoxItemsSource != null)
                            {
                                return enumColumn.ComboBoxItemsSource
                                    ?.FirstOrDefault(x => x.SelectedValue == o.ToString())?.DisplayMember;
                            }

                            // For DataGridEnumDescriptionColumn, we know it uses "Key" and "Value" properties
                            var items = enumColumn.ItemsSource;
                            if (items != null)
                            {
                                foreach (var item in items)
                                {
                                    // Get the Key (enum value) using reflection
                                    var keyProperty = item.GetType().GetProperty("Key");
                                    if (keyProperty != null)
                                    {
                                        var key = keyProperty.GetValue(item);
                                        // If we found the matching enum value
                                        if (key != null && key.ToString() == o.ToString())
                                        {
                                            // Get the Value (description) using reflection
                                            var valueProperty = item.GetType().GetProperty("Value");
                                            if (valueProperty != null)
                                            {
                                                return valueProperty.GetValue(item)?.ToString();
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // label of other columns
                        return type != typeof(bool) ? o.ToString()
                            // translates boolean value label
                            : o != null && (bool)o ? Translate.IsTrue : Translate.IsFalse;
                    }
                }); // Dispatcher

                // ItemsSource (ListBow/TreeView)
                if (fieldType == typeof(DateTime))
                {
                    TreeViewItems = await BuildTreeAsync(filterItemList);
                }
                else
                {
                    ListBoxItems = filterItemList;
                }

                // Set ICollectionView for filtering in the pop-up window
                ItemCollectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(filterItemList);

                // set filter in popup
                if (ItemCollectionView.CanFilter) ItemCollectionView.Filter = SearchFilter;

                // set the placement and offset of the PopUp in relation to the header and the main window of the application
                // i.e (placement : bottom left or bottom right)
                PopupPlacement(sizableContentGrid, header);

                popup.UpdateLayout();

                // open popup
                popup.IsOpen = true;

                // set focus on searchTextBox
                searchTextBox.Focus();
                Keyboard.Focus(searchTextBox);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowFilterCommand error : {ex.Message}");
                throw;
            }
            finally
            {
                // reset cursor
                ResetCursor();

                stopWatchFilter.Stop();

                // show open popup elapsed time in UI
                ElapsedTime = stopWatchFilter.Elapsed;

                Debug.WriteLineIf(DebugMode,
                    $"ShowFilterCommand Elapsed time : {ElapsedTime:mm\\:ss\\.ff}");
            }
        }

        /// <summary>
        ///     Click OK Button when Popup is Open, apply filter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ApplyFilterCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "\r\nApplyFilterCommand");

            stopWatchFilter.Start();

            currentlyFiltering = true;
            popup.IsOpen = false; // raise PopupClosed event

            // set cursor wait
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                await Task.Run(() =>
                {
                    var previousFiltered = CurrentFilter.PreviouslyFilteredItems;
                    var blankIsChanged = new FilterItem();

                    if (search)
                    {
                        // in the search, the item (blank) is always unchecked
                        blankIsChanged.IsChecked = false;
                        blankIsChanged.IsChanged = !previousFiltered.Any(c => c != null && c.Equals(string.Empty));

                        // result of the research - only items that appear in search
                        var searchResult = PopupViewItems.ToList();
                        var searchResultContents = new HashSet<object>(searchResult.Select(c => c.Content));

                        // Get all source items (including those not visible in search)
                        var allSourceItems = SourcePopupViewItems.ToList();

                        // Items NOT in search result should be added to previousFiltered (hidden)
                        // This ensures that when user searches "1" and applies filter,
                        // items not containing "1" will also be filtered out
                        var itemsNotInSearch = allSourceItems.Where(c => !searchResultContents.Contains(c.Content)).ToList();
                        previousFiltered.UnionWith(itemsNotInSearch.Select(c => c.Content));

                        // Items checked in search results should be removed from previousFiltered (shown)
                        var checkedInSearch = searchResult.Where(c => c.IsChecked).ToList();
                        previousFiltered.ExceptWith(checkedInSearch.Select(c => c.Content));

                        // Items unchecked in search results should be added to previousFiltered (hidden)
                        var uncheckedInSearch = searchResult.Where(c => !c.IsChecked).ToList();
                        previousFiltered.UnionWith(uncheckedInSearch.Select(c => c.Content));
                    }
                    else
                    {
                        // changed popup items
                        var changedItems = PopupViewItems.Where(c => c.IsChanged).ToList();

                        var checkedItems = changedItems.Where(c => c.IsChecked);
                        var uncheckedItems = changedItems.Where(c => !c.IsChecked).ToList();

                        // previous item except unchecked items checked again
                        previousFiltered.ExceptWith(checkedItems.Select(c => c.Content));
                        previousFiltered.UnionWith(uncheckedItems.Select(c => c.Content));

                        blankIsChanged.IsChecked = changedItems.Any(c => c.Level == -1 && c.IsChecked);
                        blankIsChanged.IsChanged = changedItems.Any(c => c.Level == -1);
                    }

                    if (blankIsChanged.IsChanged && CurrentFilter.FieldType == typeof(string))
                    {
                        // two values: null and string.empty

                        // at this step, the null value is already added previously by the
                        // ShowFilterCommand method

                        switch (blankIsChanged.IsChecked)
                        {
                            // if (blank) item is unchecked, add string.Empty.
                            case false:
                                previousFiltered.Add(string.Empty);
                                break;

                            // if (blank) item is rechecked, remove string.Empty.
                            case true when previousFiltered.Any(c => c?.ToString() == string.Empty):
                                previousFiltered.RemoveWhere(item => item?.ToString() == string.Empty);
                                break;
                        }
                    }

                    // add a filter if it is not already added previously
                    if (!CurrentFilter.IsFiltered) CurrentFilter.AddFilter(criteria);

                    // add current filter to GlobalFilterList
                    if (GlobalFilterList.All(f => f.FieldName != CurrentFilter.FieldName))
                        GlobalFilterList.Add(CurrentFilter);

                    // set the current field name as the last filter name
                    lastFilter = CurrentFilter.FieldName;
                });

                // apply filter
                CollectionViewSource.Refresh();

                // set button icon (filtered or not)
                FilterState.SetIsFiltered(CurrentFilter.FilterButton, CurrentFilter?.IsFiltered ?? false);

                // remove the current filter if there is no items to filter
                if (CurrentFilter != null && CurrentFilter.PreviouslyFilteredItems.Count == 0)
                    RemoveCurrentFilter();
                else if (PersistentFilter) // call serialize (if persistent filter)
                    Serialize();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplyFilterCommand error : {ex.Message}");
                throw;
            }
            finally
            {
                // free resources (unsubscribe from the event and re-enable "columnHeadersPresenter"
                // is done in PopupClosed method)
                currentlyFiltering = false;
                CurrentFilter = null;
                ItemCollectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(new object());
                ResetCursor();

                stopWatchFilter.Stop();
                ElapsedTime = stopWatchFilter.Elapsed;

                Debug.WriteLineIf(DebugMode, $@"ApplyFilterCommand Elapsed time : {ElapsedTime:mm\:ss\.ff}");
            }
        }

        /// <summary>
        ///     PopUp placement and offset
        /// </summary>
        /// <param name="grid"></param>
        /// <param name="header"></param>
        private void PopupPlacement(FrameworkElement grid, FrameworkElement header)
        {
            try
            {
                popup.PlacementTarget = header;
                popup.HorizontalOffset = 0d;
                popup.VerticalOffset = -1d;
                popup.Placement = PlacementMode.Bottom;

                // get the host window of the datagrid, contribution : STEFAN HEIMEL
                var hostingWindow = Window.GetWindow(this);

                if (hostingWindow == null) return;

                const double border = 1d;

                // get the ContentPresenter from the hostingWindow
                var contentPresenter = VisualTreeHelpers.FindChild<ContentPresenter>(hostingWindow);

                var hostSize = new Point
                {
                    X = contentPresenter.ActualWidth,
                    Y = contentPresenter.ActualHeight
                };

                // get the X, Y position of the header
                var headerContentOrigin = header.TransformToVisual(contentPresenter).Transform(new Point(0, 0));
                var headerDataGridOrigin = header.TransformToVisual(this).Transform(new Point(0, 0));

                var headerSize = new Point { X = header.ActualWidth, Y = header.ActualHeight };
                var offset = popUpSize.X - headerSize.X + border;

                // the popup must stay in the DataGrid, move it to the left of the header, because it overflows on the right.
                if (headerDataGridOrigin.X + headerSize.X > popUpSize.X) popup.HorizontalOffset -= offset;

                // delta for max size popup
                var delta = new Point
                {
                    X = hostSize.X - (headerContentOrigin.X + headerSize.X),
                    Y = hostSize.Y - (headerContentOrigin.Y + headerSize.Y + popUpSize.Y)
                };

                // max size
                grid.MaxWidth = MaxSize(popUpSize.X + delta.X - border);
                grid.MaxHeight = MaxSize(popUpSize.Y + delta.Y - border);

                // remove offset
                // contributing to the fix : VASHBALDEUS
                if (popup.HorizontalOffset == 0)
                    grid.MaxWidth = MaxSize(Math.Abs(grid.MaxWidth - offset));

                if (!(delta.Y <= 0d)) return;

                // the height of popup is too large, reduce it, because it overflows down.
                grid.MaxHeight = MaxSize(popUpSize.Y - Math.Abs(delta.Y) - border);
                grid.Height = grid.MaxHeight;

                // contributing to the fix : VASHBALDEUS
                grid.MinHeight = grid.MaxHeight == 0 ? grid.MinHeight : grid.MaxHeight;

                // greater than or equal to 0.0
                static double MaxSize(double size)
                {
                    return size >= 0.0d ? size : 0.0d;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PopupPlacement error : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Check if Find and Replace command can execute
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CanShowFindReplace(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = EnableReplace && SelectedItems != null && SelectedItems.Count > 0;
        }

        /// <summary>
        ///     Show Find and Replace dialog when user presses Ctrl + H
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ShowFindReplaceCommand(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                // Kiểm tra xem tính năng Replace có được bật không
                if (!EnableReplace)
                {
                    return;
                }

                // Kiểm tra có ít nhất 1 dòng được chọn
                if (SelectedItems == null || SelectedItems.Count == 0)
                {
                    return;
                }

                List<DataGridColumn> replaceableColumns = [.. Columns.Where(c => !c.IsReadOnly && c is DataGridTextColumn && c is not DataGridNumericColumn)];

                if (replaceableColumns.Count == 0)
                {
                    return;
                }

                var replaceOutput = await DPInput.ShowDataGridReplaceInput(Name, replaceableColumns);

                int itemChangedCount = 0;
                bool hasChanges = false;

                foreach (var column in replaceOutput.Value.SelectedColumns)
                {
                    if (column is DataGridTextColumn textColumn)
                    {
                        var binding = textColumn.Binding as Binding;
                        var fieldName = binding?.Path.Path;
                        if (string.IsNullOrEmpty(fieldName))
                        {
                            continue;
                        }

                        foreach (var selectedItem in SelectedItems)
                        {
                            var property = selectedItem.GetType().GetProperty(fieldName);
                            if (property != null && property.PropertyType == typeof(string) && property.CanWrite)
                            {
                                var currentValue = property.GetValue(selectedItem) as string ?? string.Empty;
                                var newValue = currentValue.Replace(replaceOutput.Value.Find, replaceOutput.Value.ReplaceWith);

                                if (currentValue != newValue)
                                {
                                    property.SetValue(selectedItem, newValue);
                                    itemChangedCount++;
                                    hasChanges = true;

                                    if (selectedItem is INotifyPropertyChanged notifyPropertyChanged)
                                    {
                                        try
                                        {
                                            var propertyChangedField = notifyPropertyChanged.GetType()
                                                .GetField("PropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic);

                                            if (propertyChangedField?.GetValue(notifyPropertyChanged) is PropertyChangedEventHandler propertyChangedEvent)
                                            {
                                                propertyChangedEvent.Invoke(notifyPropertyChanged, new PropertyChangedEventArgs(fieldName));
                                            }
                                        }
                                        catch
                                        {
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (hasChanges)
                {
                    // Use Dispatcher to ensure UI update happens on UI thread
                    _ = Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // Force refresh the collection view first
                            CollectionViewSource?.Refresh();

                            // Update layout and invalidate visual
                            UpdateLayout();
                            InvalidateVisual();

                            // Force invalidate the visual tree to ensure all cells are refreshed
                            foreach (var item in SelectedItems)
                            {
                                if (ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row)
                                {
                                    row.InvalidateVisual();
                                    row.UpdateLayout();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error refreshing UI after replace: {ex.Message}");
                        }
                    }), DispatcherPriority.Render);
                    DPDialog.Info($"Đã thực hiện {itemChangedCount} thay đổi.");
                }
            }
            catch (Exception ex)
            {
                DPDialog.Error(ex.Message);
            }
        }

        /// <summary>
        ///     Renumber all rows when ItemsSource uses ObservableCollection
        ///     which implements INotifyCollectionChanged
        ///     Contribution : mcboothy
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ItemSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "ItemSourceCollectionChanged");

            ItemsSourceCount = Items.Count;
            OnPropertyChanged(nameof(ItemsSourceCount));

            if (!ShowRowsCount) return;
            // Renumber all rows - simple approach since width is controlled by XAML
            for (var i = 0; i < Items.Count; i++)
                if (ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow row && row.Header is TextBlock textBlock)
                    textBlock.Text = ShowRowsCount ? $"{i + 1}" : string.Empty;
        }

        #endregion Private Methods

        public static readonly DependencyProperty ShowSelectionColumnProperty =
            DependencyProperty.Register(nameof(ShowSelectionColumn), typeof(bool), typeof(FilterDataGrid),
                new PropertyMetadata(false, OnShowSelectionColumnChanged));

        public bool ShowSelectionColumn
        {
            get => (bool)GetValue(ShowSelectionColumnProperty);
            set => SetValue(ShowSelectionColumnProperty, value);
        }

        private static void OnShowSelectionColumnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FilterDataGrid grid && grid.IsLoaded)
            {
                grid.SetupSelectionColumn();
            }
        }

        private bool _existingSelectionColumn = false;

        private void SetupSelectionColumn()
        {
            if (ShowSelectionColumn)
            {
                if (_existingSelectionColumn)
                {
                    var existingColumn = Columns.FirstOrDefault(c => c is DataGridTemplateColumn column);
                    if (existingColumn != null)
                    {
                        Columns.Remove(existingColumn);
                    }
                }

                var selectionColumn = new DataGridTemplateColumn
                {
                    Header = null,
                    CanUserReorder = false,
                    CanUserResize = false,
                    Width = new DataGridLength(35),
                    CellTemplate = CreateSelectionTemplate()
                };

                Columns.Insert(0, selectionColumn);
                _existingSelectionColumn = true;
            }
        }

        private DataTemplate CreateSelectionTemplate()
        {
            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            gridFactory.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            gridFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Stretch);

            var checkBoxFactory = new FrameworkElementFactory(typeof(CheckBox));
            checkBoxFactory.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkBoxFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            checkBoxFactory.SetBinding(
                ToggleButton.IsCheckedProperty,
                new Binding("IsSelected")
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridRow), 1),
                    Mode = BindingMode.TwoWay
                });

            checkBoxFactory.AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(SelectionCheckBox_PreviewMouseLeftButtonDown));

            gridFactory.AppendChild(checkBoxFactory);

            return new DataTemplate { VisualTree = gridFactory };
        }
        private void SelectionCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is CheckBox checkBox)
            {
                var row = checkBox.TryFindParent<DataGridRow>();
                if (row != null)
                {
                    // Reverse selection logic because checkbox has not been checked yet
                    row.IsSelected = checkBox.IsChecked == false;

                    if (SelectionMode == DataGridSelectionMode.Single && checkBox.IsChecked == true)
                    {
                        foreach (var item in Items)
                        {
                            var otherRow = (DataGridRow)ItemContainerGenerator.ContainerFromItem(item);
                            if (otherRow != null && otherRow != row)
                            {
                                otherRow.IsSelected = false;
                            }
                        }
                    }

                    e.Handled = true;
                }
            }
            e.Handled = true;

        }
    }

    public enum GroupByFunction
    {
        Sum,
        Average,
        Min,
        Max,
        Count
    }
}



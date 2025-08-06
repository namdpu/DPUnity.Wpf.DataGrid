using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DPUnity.Wpf.DpDataGrid.Behaviors
{
    public static class PasteBehavior
    {
        public static readonly DependencyProperty PasteCommandProperty =
            DependencyProperty.RegisterAttached(
                "PasteCommand",
                typeof(ICommand),
                typeof(PasteBehavior),
                new PropertyMetadata(null, OnPasteCommandChanged));

        public static void SetPasteCommand(DependencyObject element, ICommand value)
            => element.SetValue(PasteCommandProperty, value);

        public static ICommand GetPasteCommand(DependencyObject element)
            => (ICommand)element.GetValue(PasteCommandProperty);

        private static void OnPasteCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid dataGrid) return;

            if (e.NewValue != null)
            {
                dataGrid.CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, OnPasteExecuted, OnCanPasteExecute));
            }
            else
            {
                var bindingToRemove = dataGrid.CommandBindings.OfType<CommandBinding>()
                    .FirstOrDefault(cb => cb.Command == ApplicationCommands.Paste);
                if (bindingToRemove != null)
                {
                    dataGrid.CommandBindings.Remove(bindingToRemove);
                }
            }
        }

        private static void OnCanPasteExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (sender is not DataGrid dataGrid) return;

            var command = GetPasteCommand(dataGrid);
            if (command != null)
            {
                // Create a parameter with clipboard data
                var dataObject = Clipboard.GetDataObject();
                var parameter = (dataGrid, dataObject);  // Create tuple with both elements
                e.CanExecute = command.CanExecute(parameter);
            }
            else
            {
                e.CanExecute = false;
            }
        }

        private static void OnPasteExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is not DataGrid dataGrid) return;

            var command = GetPasteCommand(dataGrid);
            if (command != null)
            {
                // Create a parameter with clipboard data
                var dataObject = Clipboard.GetDataObject();
                var parameter = (dataGrid, dataObject);  // Create tuple with both elements

                if (command.CanExecute(parameter))
                {
                    command.Execute(parameter);
                    e.Handled = true;
                }
            }
        }
    }
}
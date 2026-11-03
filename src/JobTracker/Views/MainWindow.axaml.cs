using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using JobTracker.ViewModels;

namespace JobTracker.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        MainViewModel? observed = null;
        DataContextChanged += (_, _) =>
        {
            if (observed is not null) observed.PropertyChanged -= OnScreenChanged;
            observed = DataContext as MainViewModel;
            if (observed is not null) observed.PropertyChanged += OnScreenChanged;
        };
        Closed += (_, _) => { if (observed is not null) observed.PropertyChanged -= OnScreenChanged; };
        void OnScreenChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsEditorOpen) && observed?.IsEditorOpen == true)
                Dispatcher.UIThread.Post(() => this.GetVisualDescendants().OfType<TextBox>()
                    .FirstOrDefault(t => t.Name == "CompanyInput")?.Focus(), DispatcherPriority.Loaded);
        }
        // Keyboard and focus are view concerns; saving and validation stay in the view model.
        KeyDown += (_, e) =>
        {
            if (DataContext is not MainViewModel vm) return;
            if (e.Key == Key.Escape && vm.IsEditorOpen) { vm.CancelCommand.Execute(null); e.Handled = true; }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                if (e.Key == Key.N && !vm.IsEditorOpen) { vm.NewCommand.Execute(null); e.Handled = true; }
                else if (e.Key == Key.S && vm.IsEditorOpen) { vm.SaveCommand.Execute(null); e.Handled = true; }
                else if (e.Key == Key.F && !vm.IsEditorOpen) { this.FindControl<TextBox>("SearchBox")?.Focus(); e.Handled = true; }
            }
        };
        Closing += (_, e) =>
        {
            if (DataContext is MainViewModel { Editor.IsDirty: true } vm)
            {
                e.Cancel = true;
                vm.CancelCommand.Execute(null);
            }
        };
    }
}

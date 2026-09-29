namespace PatarikAIOS;

public sealed class SupplierAnalysisWindow : Window
{
    private readonly string _supplier;
    private readonly Func<DateOnly, DateOnly, Task<SupplierAnalysisData>> _load;
    private readonly DatePicker _start;
    private readonly DatePicker _end;
    private readonly ContentControl _content = new();

    public SupplierAnalysisWindow(string supplier, DateOnly startDate, DateOnly endDate,
        Func<DateOnly, DateOnly, Task<SupplierAnalysisData>> load)
    {
        _supplier = supplier;
        _load = load;
        Title = $"Մատակարարի վերլուծություն — {supplier}";
        Width = 1120;
        Height = 760;
        MinWidth = 820;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _start = new DatePicker { SelectedDate = startDate.ToDateTime(TimeOnly.MinValue), Width = 150 };
        _end = new DatePicker { SelectedDate = endDate.ToDateTime(TimeOnly.MinValue), Width = 150, Margin = new Thickness(8, 0, 0, 0) };
        var refresh = new Button { Content = "Թարմացնել", Margin = new Thickness(10, 0, 0, 0), Padding = new Thickness(14, 5, 14, 5) };
        refresh.Click += async (_, _) => await RefreshAsync();

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 14) };
        controls.Children.Add(new TextBlock { Text = "Ժամանակահատված՝", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        controls.Children.Add(_start);
        controls.Children.Add(_end);
        controls.Children.Add(refresh);

        var root = new DockPanel { Margin = new Thickness(20) };
        DockPanel.SetDock(controls, Dock.Top);
        root.Children.Add(controls);
        root.Children.Add(_content);
        Content = root;
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_start.SelectedDate is null || _end.SelectedDate is null) return;
        var start = DateOnly.FromDateTime(_start.SelectedDate.Value);
        var end = DateOnly.FromDateTime(_end.SelectedDate.Value);
        if (end < start)
        {
            MessageBox.Show("Ավարտի ամսաթիվը չի կարող լինել սկզբից շուտ։", "Ժամանակահատված", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _content.Content = new TextBlock { Text = "Տվյալները բեռնվում են…", FontSize = 15 };
        try
        {
            _content.Content = Views.SupplierAnalysisCard(await _load(start, end));
        }
        catch (Exception exception)
        {
            _content.Content = new TextBlock { Text = $"Չհաջողվեց բեռնել տվյալները։\n{exception.Message}", TextWrapping = TextWrapping.Wrap };
        }
    }
}

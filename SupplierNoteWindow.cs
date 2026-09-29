namespace PatarikAIOS;

public sealed class SupplierNoteWindow : Window
{
    private readonly TextBox _note = new() { MinWidth = 390, MinHeight = 100, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
    public string? Result { get; private set; }

    public SupplierNoteWindow(DateOnly date, string supplier)
    {
        Title = "Մատակարարի նշում";
        Width = 500; Height = 310; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = $"{date:dd.MM.yyyy} · {supplier}", FontWeight = FontWeights.SemiBold, FontSize = 16 });
        panel.Children.Add(new TextBlock { Text = "Նշումը անմիջապես կուղարկվի աշխատակիցներին Telegram-ով։", Margin = new Thickness(0, 6, 0, 10), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(_note);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Չեղարկել", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => Close();
        var save = new Button { Content = "Պահպանել և ուղարկել", IsDefault = true, Background = new SolidColorBrush(Color.FromRgb(15, 118, 110)), Foreground = Brushes.White };
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_note.Text))
            {
                MessageBox.Show("Գրեք նշման տեքստը։", "Նշում", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Result = _note.Text.Trim(); DialogResult = true;
        };
        actions.Children.Add(cancel); actions.Children.Add(save); panel.Children.Add(actions);
        Content = panel;
    }
}

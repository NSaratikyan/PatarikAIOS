namespace PatarikAIOS;

public partial class MainWindow
{
    private void OpenAiManager_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot is null)
        {
            MessageBox.Show("Տվյալները դեռ բեռնված չեն։ Սեղմեք «Թարմացնել» և կրկին փորձեք։", "Patarik AI Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var window = new AgentChatWindow(_snapshot)
        {
            Owner = this
        };
        window.Show();
    }
}

namespace PatarikAIOS;

public partial class App : Application
{
    public static AppServices Services { get; } = new(new DemoDataProvider());
}

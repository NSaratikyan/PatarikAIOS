namespace PatarikAIOS;

public static class PaymentSourcePicker
{
    public static ComboBox Create(string? source = null) => new()
    {
        MinWidth=180,
        ItemsSource=new[]{"Չճշտված / արդեն հաշվառված", "0001 · Դրամարկղ", "0002 · Պահոց", "bank · Անկանխիկ"},
        SelectedIndex=source switch { "0001"=>1,"0002"=>2,"bank"=>3,_=>0 }
    };
    public static string? Value(ComboBox box) => box.SelectedIndex switch { 1=>"0001",2=>"0002",3=>"bank",_=>null };
}

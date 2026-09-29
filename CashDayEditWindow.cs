using System.Globalization;
namespace PatarikAIOS;

public sealed class CashDayEditWindow : Window
{
    public CashDayOverride? Result { get; private set; }
    public CashDayEditWindow(DateOnly date, CashDayValues auto, CashDayOverride? existing, decimal closing, decimal vaultClosing)
    {
        Title=$"Դրամական շարժի խմբագրում · {date:dd.MM.yyyy}"; Width=680; Height=720; WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel{Margin=new Thickness(24)};
        panel.Children.Add(new TextBlock{Text="Դատարկ դաշտը՝ ավտոմատ։ Լրացված թիվը փոխարինում է ավտոմատ գումարին, չի գումարվում դրան։",TextWrapping=TextWrapping.Wrap});
        var values=new decimal?[]{existing?.Sales,existing?.NonCash,existing?.OtherIn,existing?.Out,existing?.VaultIn,existing?.VaultOut,existing?.Closing,existing?.VaultClosing};
        var baselines=new[]{auto.Sales,auto.NonCash,auto.OtherIn,auto.Out,auto.VaultIn,auto.VaultOut,closing,vaultClosing};
        var labels=new[]{"Ամբողջ վաճառք / մուտք","Անկանխիկ","Այլ մուտք 0001 (այդ թվում փոխանցումներ)","0001 ելք՝ վճարումներ և այլ ելքեր","0002 պահոցի մուտք","0002 պահոցի ելք","0001 օրվա փակման մնացորդ","0002 օրվա փակման մնացորդ"};
        var inputs=new List<TextBox>();
        for(int i=0;i<labels.Length;i++)
        {
            panel.Children.Add(new TextBlock{Text=$"{labels[i]} · ավտոմատ՝ {baselines[i]:N2} ֏",Margin=new Thickness(0,10,0,3)});
            var box=new TextBox{Text=values[i]?.ToString(CultureInfo.InvariantCulture)??""};inputs.Add(box);panel.Children.Add(box);
        }
        var save=new Button{Content="Պահպանել և վերահաշվարկել",Margin=new Thickness(0,18,0,0)};
        save.Click+=(_,_)=>
        {
            var parsed=new decimal?[8];
            for(int i=0;i<8;i++) if(!string.IsNullOrWhiteSpace(inputs[i].Text))
            {
                if(!decimal.TryParse(inputs[i].Text.Replace(" ","").Replace(',','.'),NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var number) || (i<6 && number<0)) { MessageBox.Show("Ստուգեք թվերը։ Մուտքերը և ելքերը պետք է լինեն ոչ բացասական։"); return; }
                parsed[i]=number;
            }
            var candidate=new CashDayOverride(date,parsed[0],parsed[1],parsed[2],parsed[3],parsed[4],parsed[5],parsed[6],parsed[7],"Տնօրեն · ծրագիր",DateTime.Now);
            var effective=CashDayOverrideRules.Effective(auto,candidate);
            if(effective.NonCash>effective.Sales && effective.Sales>=0) { MessageBox.Show("Անկանխիկը գերազանցում է վաճառքը։ Նախ ճշտեք վաճառքի գումարը։"); return; }
            Result=candidate; DialogResult=true;
        };
        panel.Children.Add(save); Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    }
}

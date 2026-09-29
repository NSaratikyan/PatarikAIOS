using System.Windows.Controls;
using System.Windows.Media;

namespace PatarikAIOS;

public partial class MainWindow
{
    private void UpdatePresentationNavigation(string page)
    {
        OrderPeriodPanel.Visibility = page is "Suppliers" or "PurchasePlan" ? Visibility.Visible : Visibility.Collapsed;
        CurrentOrderTab.Background = page == "Suppliers" ? PresentationTheme.Blue : Brushes.White;
        CurrentOrderTab.Foreground = page == "Suppliers" ? Brushes.White : Brushes.SlateGray;
        NextOrderTab.Background = page == "PurchasePlan" ? PresentationTheme.Blue : Brushes.White;
        NextOrderTab.Foreground = page == "PurchasePlan" ? Brushes.White : Brushes.SlateGray;
        PageTitleText.Text = page switch
        {
            "Suppliers" => "Պատվերներ", "PurchasePlan" => "Վաղվա պատվերներ", "Finance" => "Ֆինանսներ",
            "Payments" => "Վճարումների պլան", "Approvals" => "Հաստատումներ", "Salaries" => "Աշխատավարձեր",
            "CashMovements" => "Դրամական շարժ", "Summary" => "Ժամանակահատվածի ամփոփում",
            "SupplierSales" => "Մատակարարների վերլուծություն", "Recommendations" => "Առաջարկներ և առաջադրանքներ",
            _ => "Օրվա ամփոփում"
        };
        foreach(var button in NavigationPanel.Children.OfType<Button>())
        {
            var selected = button.Tag as string == page;
            button.Background = selected ? PresentationTheme.Blue : Brushes.Transparent;
            button.Foreground = selected ? Brushes.White : new SolidColorBrush(Color.FromRgb(199,215,238));
            button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
            // Wrap long Armenian labels instead of clipping navigation actions.
            if (button.Content is string label) button.Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap };
        }
    }

    private IReadOnlyList<(DateOnly Date, decimal? Amount)> DashboardTrend()
    {
        var dates=Enumerable.Range(0,7).Select(i=>_selectedDate.AddDays(i-6)).ToList();
        if(App.Services.DataProvider is ExcelDataProvider)
        {
            // Read the already imported source. No additional API calls, store writes or inferred zeros.
            var state=_excelImportStore.Load();
            var covered=state.Batches.Where(b=>b.Kind=="sales").SelectMany(b=>b.Dates).ToHashSet();
            var totals=state.Batches.SelectMany(b=>b.Sales).Where(s=>dates.Contains(s.Date)).GroupBy(s=>s.Date).ToDictionary(g=>g.Key,g=>g.Sum(s=>s.Sales));
            return dates.Select(date=>(date,covered.Contains(date)?(decimal?)totals.GetValueOrDefault(date):null)).ToList();
        }
        return dates.Select(date => (date, date==_selectedDate && _snapshot!.Sales.SalesAvailable ? (decimal?)_snapshot.Sales.SalesAmount : date==_selectedDate.AddDays(-1) && _snapshot!.Sales.ComparisonAvailable ? _snapshot.Sales.PreviousSalesAmount : null)).ToList();
    }
}

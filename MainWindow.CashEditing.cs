namespace PatarikAIOS;

public partial class MainWindow
{
    private readonly CashDayOverrideStore _cashDayOverrideStore = new();
    private readonly Dictionary<DateOnly,decimal> _automaticNonCash = new();

    private IEnumerable<CashLedgerMovement> LocalPaymentMovements()
    {
        foreach(var p in _completedPayments.Where(x=>x.CashSource is not null))
            yield return new(p.PaidDate,p.CashSource!,null,p.Amount,p.SourceDocument??"LOCAL-PAY-"+p.PaidDate.ToString("yyyyMMdd")+p.Recipient,p.Recipient,p.Note,false);
        foreach(var p in _salaryPayments.Where(x=>x.CashSource is not null))
            yield return new(p.PaidDate,p.CashSource!,null,p.Amount,"SALARY-PAY-"+p.Id.ToString("N"),p.Employee,"Աշխատավարձ · "+p.Note,false);
    }

    private DateOnly CashOpeningStart(DateOnly date)
    {
        if(_availableFunds.OpeningMonth is {} initial) return new(initial.Year,initial.Month,1);
        var prior=EffectiveCashAdjustments().Where(x=>x.Date<=date).Select(x=>x.Date).ToList();
        return prior.Count>0 ? prior.Min() : new(date.Year,date.Month,1);
    }
    private List<CashDeskAdjustment> EffectiveCashAdjustments()
    {
        var combined=_cashDeskAdjustments.ToList();
        foreach(var edit in _cashDayOverrideStore.Load().Days)
        {
            if(edit.Closing is {} a) combined.Add(new(edit.Date,"0001",a,"Օրվա խմբագրում",edit.ChangedAt));
            if(edit.VaultClosing is {} b) combined.Add(new(edit.Date,"0002",b,"Օրվա խմբագրում",edit.ChangedAt));
        }
        return combined.OrderBy(x=>x.ChangedAt??DateTime.MinValue).ToList();
    }
    private Dictionary<DateOnly,CashDayValues> AutomaticCashDays(IReadOnlyList<CashLedgerMovement> raw)
    {
        var state=_excelImportStore.Load(); var edits=_cashDayOverrideStore.Load();
        var dates=raw.Select(x=>x.Date).Concat(edits.Days.Select(x=>x.Date)).Concat(state.NonCash.Select(x=>x.Date));
        if(App.Services.DataProvider is ExcelDataProvider) dates=dates.Concat(state.Batches.SelectMany(x=>x.Dates));
        var result=new Dictionary<DateOnly,CashDayValues>();
        foreach(var date in dates.Distinct())
        {
            var movements=raw.Where(x=>x.Date==date).ToList();
            var netSales=_cashDocuments.Where(x=>x.Date==date && x.Type=="ecr-cash-sales").Sum(x=>x.Amount);
            var salesIds=_cashDocuments.Where(x=>x.Date==date && (x.Type=="ecr-cash-sales" || x.Type.Contains("Վաճառք") || x.Type.Contains("sale",StringComparison.OrdinalIgnoreCase))).Select(x=>x.DocumentNumber).ToHashSet();
            var nonSales=movements.Where(x=>!salesIds.Contains(x.DocumentNumber)).ToList();
            var nonCash=App.Services.DataProvider is ExcelDataProvider ? state.NonCash.LastOrDefault(x=>x.Date==date)?.Amount??0 : _automaticNonCash.GetValueOrDefault(date);
            var gross=netSales+nonCash;
            if(App.Services.DataProvider is ExcelDataProvider)
            {
                var salesBatches=state.Batches.Where(x=>x.Kind=="sales" && x.Dates.Contains(date)).ToList();
                if(salesBatches.Count>0) gross=salesBatches.SelectMany(x=>x.Sales).Where(x=>x.Date==date).Sum(x=>x.Sales);
            }
            if(netSales==0 && !_cashDocuments.Any(x=>x.Date==date && x.Type=="ecr-cash-sales") && App.Services.DataProvider is not ExcelDataProvider)
                gross=movements.Where(x=>salesIds.Contains(x.DocumentNumber)).Sum(x=>x.TargetCashDesk=="0001"?x.Amount:-x.Amount)+nonCash;
            result[date]=new(gross,nonCash,nonSales.Where(x=>x.TargetCashDesk=="0001").Sum(x=>x.Amount),
                nonSales.Where(x=>x.SourceCashDesk=="0001").Sum(x=>x.Amount),movements.Where(x=>x.TargetCashDesk=="0002").Sum(x=>x.Amount),movements.Where(x=>x.SourceCashDesk=="0002").Sum(x=>x.Amount));
        }
        return result;
    }
    private async Task RefreshAutomaticNonCashAsync(DateOnly start,DateOnly end)
    {
        if(App.Services.DataProvider is ExcelDataProvider || App.Services.DataProvider is not IFundsMovementProvider provider) return;
        var dates=_cashDocuments.Where(x=>x.Date>=start && x.Date<=end && x.Type=="ecr-cash-sales").Select(x=>x.Date)
            .Concat(_cashDayOverrideStore.Load().Days.Where(x=>x.Date>=start && x.Date<=end).Select(x=>x.Date)).Distinct();
        foreach(var day in dates)
        {
            try { var value=await provider.GetNonCashSalesAsync(day,day); _automaticNonCash[day]=value.BankReport+value.AmeriabankPos099+value.Idram; }
            catch(Exception ex) { _cashSyncStatus="⚠ Անկանխիկի ավտոմատ թիվը չի թարմացվել․ "+ex.Message; }
        }
    }
    private async void EditCashDay(DateOnly date)
    {
        var raw=RawCashDeskMovements(); var automatic=AutomaticCashDays(raw).GetValueOrDefault(date)??new(0,0,0,0,0,0);
        var current=_cashDayOverrideStore.Load().Days.LastOrDefault(x=>x.Date==date);
        var opening=FundsForOpening(new(0,0)); var start=CashOpeningStart(date);
        var window=new CashDayEditWindow(date,automatic,current,CashDeskBalance("0001",opening.CashDesk,start,date),CashDeskBalance("0002",opening.CashVault,start,date)){Owner=this};
        if(window.ShowDialog()!=true || window.Result is null) return;
        try { _cashDayOverrideStore.Save(window.Result); _snapshot=null; await LoadAsync(_currentPage); }
        catch(Exception ex) { MessageBox.Show(ex.Message,"Չհաջողվեց պահպանել"); }
    }
    private async Task RegisterNonCashAsync(TelegramBotSettings settings,string text,string requestId,string author)
    {
        if(!CashDayOverrideRules.TryNonCashCommand(text,out var date,out var amount))
        { await TelegramBotClient.SendMessageAsync(settings,"Ձևաչափ՝ անկանխիկ 24.09.2026 35000\nԹիվը փոխարինում է օրվա նախորդ անկանխիկին։"); return; }
        var state=_cashDayOverrideStore.Load();
        var existing=state.Days.LastOrDefault(x=>x.Date==date)??new CashDayOverride(date);
        var automatic=AutomaticCashDays(RawCashDeskMovements()).GetValueOrDefault(date);
        if((existing.Sales??automatic?.Sales) is {} gross && gross>=0 && amount>gross)
        { await TelegramBotClient.SendMessageAsync(settings,"Չի պահպանվել․ անկանխիկը գերազանցում է հայտնի վաճառքը։ Նախ ճշտեք վաճառքի գումարը։"); return; }
        _cashDayOverrideStore.SaveNonCash(date,amount,author,requestId);
        _snapshot=null;
        await TelegramBotClient.SendMessageAsync(settings,$"✅ {date:dd.MM.yyyy} · Անկանխիկ՝ {amount:N0} ֏։ Օրվա նախկին թիվը փոխարինված է, ոչ թե գումարված։");
        if(_uiReady) await LoadAsync(_currentPage);
    }
    private async void RemoveSalaryEmployee(string employee)
    {
        var accruals=_salaryAccruals.Where(x=>string.Equals(x.Employee,employee,StringComparison.OrdinalIgnoreCase)).ToList();
        var payments=_salaryPayments.Where(x=>string.Equals(x.Employee,employee,StringComparison.OrdinalIgnoreCase)).ToList();
        if(MessageBox.Show($"Հեռացնե՞լ «{employee}»-ի տողը և նրա {accruals.Count} աշխատավարձի ու {payments.Count} վճարման գրառումները։ Գումարները կվերահաշվարկվեն։ Նախկին տվյալները կպահպանվեն պահուստում։","Աշխատողի հեռացում",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes) return;
        try
        {
            var keepAccruals=_salaryAccruals.Except(accruals).ToList(); var keepPayments=_salaryPayments.Except(payments).ToList();
            _salaryStore.SaveCorrection(keepAccruals,keepPayments);
            _salaryAccruals.Clear();_salaryAccruals.AddRange(keepAccruals);_salaryPayments.Clear();_salaryPayments.AddRange(keepPayments);
            _snapshot=null; await LoadAsync("Salaries");
        }
        catch(Exception ex) { MessageBox.Show(ex.Message,"Հեռացումը չի կատարվել"); }
    }
}

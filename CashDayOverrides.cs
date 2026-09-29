using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PatarikAIOS;

public sealed record CashDayValues(decimal Sales, decimal NonCash, decimal OtherIn, decimal Out, decimal VaultIn, decimal VaultOut);
public sealed record CashDayOverride(DateOnly Date, decimal? Sales = null, decimal? NonCash = null, decimal? OtherIn = null,
    decimal? Out = null, decimal? VaultIn = null, decimal? VaultOut = null, decimal? Closing = null, decimal? VaultClosing = null,
    string Author = "", DateTime ChangedAt = default);
public sealed record CashOverrideAudit(CashDayOverride? Before, CashDayOverride After, string RequestId);
public sealed class CashOverrideState
{
    public List<CashDayOverride> Days { get; set; } = [];
    public List<CashOverrideAudit> History { get; set; } = [];
}
public sealed class CashDayOverrideStore(string? path = null)
{
    private readonly string _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PatarikAIOS","cash-day-overrides.json");
    public CashOverrideState Load() => File.Exists(_path) ? JsonSerializer.Deserialize<CashOverrideState>(File.ReadAllText(_path)) ?? new() : new();
    public void Save(CashDayOverride value, string requestId = "")
        => Change(value.Date,_=>value,requestId);
    public void SaveNonCash(DateOnly date,decimal amount,string author,string requestId="")
        => Change(date,old=>(old??new CashDayOverride(date)) with { NonCash=amount,Author=author,ChangedAt=DateTime.Now },requestId);
    private void Change(DateOnly date,Func<CashDayOverride?,CashDayOverride> change,string requestId)
    {
        using var gate = new Mutex(false,@"Local\PatarikAIOS.CashOverrides");
        try { gate.WaitOne(); } catch (AbandonedMutexException) { }
        try
        {
            var state=Load(); if(requestId!="" && state.History.Any(x=>x.RequestId==requestId)) return;
            var prior=state.Days.LastOrDefault(x=>x.Date==date);
            var value=change(prior);
            state.History.Add(new(prior,value,requestId)); state.Days.RemoveAll(x=>x.Date==value.Date); state.Days.Add(value);
            AtomicJsonFile.Save(_path,state);
        }
        finally { gate.ReleaseMutex(); }
    }
}
public static class CashDayOverrideRules
{
    public static CashDayValues Effective(CashDayValues auto, CashDayOverride? edit) => edit is null ? auto :
        new(edit.Sales??auto.Sales,edit.NonCash??auto.NonCash,edit.OtherIn??auto.OtherIn,edit.Out??auto.Out,edit.VaultIn??auto.VaultIn,edit.VaultOut??auto.VaultOut);
    public static List<CashLedgerMovement> Apply(IEnumerable<CashLedgerMovement> source, IReadOnlyDictionary<DateOnly,CashDayValues> automatic, IEnumerable<CashDayOverride> edits)
    {
        var original=source.ToList(); var result=original.ToList(); var byDay=edits.ToDictionary(x=>x.Date);
        foreach(var pair in automatic)
        {
            var day=pair.Key; var value=Effective(pair.Value,byDay.GetValueOrDefault(day));
            var raw=original.Where(x=>x.Date==day).ToList();
            void Delta(string desk,bool incoming,decimal expected)
            {
                var actual=raw.Where(x=>incoming ? x.TargetCashDesk==desk : x.SourceCashDesk==desk).Sum(x=>x.Amount);
                var delta=expected-actual; if(delta==0) return;
                result.Add(new(day,incoming?"":desk,incoming?desk:null,delta,$"DAY-ADJUST-{day:yyyyMMdd}-{desk}-{incoming}","","Օրվա ամփոփ թվի ճշտում (ոչ նոր վճարում)",false));
            }
            Delta("0001",true,value.Sales-value.NonCash+value.OtherIn); Delta("0001",false,value.Out);
            Delta("0002",true,value.VaultIn); Delta("0002",false,value.VaultOut);
        }
        return result;
    }
    public static bool TryNonCashCommand(string text,out DateOnly date,out decimal amount)
    {
        date=default; amount=0;
        var match=Regex.Match(text.Trim(),@"^/?անկանխիկ\s+(\d{2}\.\d{2}\.\d{4})\s+([\d ]+(?:[.,]\d{1,2})?)\s*(?:դր(?:ամ)?|֏)?$",RegexOptions.IgnoreCase);
        return match.Success && DateOnly.TryParseExact(match.Groups[1].Value,"dd.MM.yyyy",CultureInfo.InvariantCulture,DateTimeStyles.None,out date) &&
            decimal.TryParse(match.Groups[2].Value.Replace(" ","").Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out amount) && amount>=0;
    }
}

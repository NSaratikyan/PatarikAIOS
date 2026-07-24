using System.IO;

namespace PatarikAIOS;

public partial class MainWindow : Window
{
    private DashboardSnapshot? _snapshot;
    private string _currentPage = "Dashboard";
    private readonly LocalPaymentChangeStore _paymentChangeStore = new();
    private readonly LocalCompletedPaymentStore _completedPaymentStore = new();
    private readonly RequiredPaymentStore _requiredPaymentStore = new();
    private readonly LocalDeliveryScheduleStore _deliveryScheduleStore = new();
    private readonly LocalSupplierWeekPlanStore _supplierWeekPlanStore = new();
    private readonly LocalRecurringSupplierDayRuleStore _recurringDayRuleStore = new();
    private readonly LocalSupplierScheduleOverrideStore _supplierScheduleOverrideStore = new();
    private readonly LocalRecurringSupplierMembershipRuleStore _recurringSupplierMembershipRuleStore = new();
    private readonly LocalPartnerDebtStore _partnerDebtStore = new();
    private readonly LocalCashDocumentStore _cashDocumentStore = new();
    private readonly HtsApiSettingsStore _htsApiSettingsStore = new();
    private readonly TelegramBotSettingsStore _telegramBotSettingsStore = new();
    private readonly EmployeeTelegramBotSettingsStore _employeeTelegramBotSettingsStore = new();
    private readonly EmployeeBotUserStore _employeeBotUserStore = new();
    private readonly EmployeeSupplierActionStore _employeeSupplierActionStore = new();
    private readonly EmployeePendingIssueStore _employeePendingIssueStore = new();
    private readonly PendingEmployeeOrderChangeStore _pendingEmployeeOrderChangeStore = new();
    private readonly EmployeeTaskStore _employeeTaskStore = new();
    private readonly EmployeeTaskActionStore _employeeTaskActionStore = new();
    private readonly EmployeePendingTaskIssueStore _employeePendingTaskIssueStore = new();
    private readonly EmployeeIssueStore _employeeIssueStore = new();
    private readonly EmployeePendingGeneralIssueStore _employeePendingGeneralIssueStore = new();
    private readonly OwnerPendingEmployeeIssueReplyStore _ownerPendingEmployeeIssueReplyStore = new();
    private readonly AvailableFundsStore _availableFundsStore = new();
    private readonly CashDeskAdjustmentStore _cashDeskAdjustmentStore = new();
    private readonly List<PaymentChangeDraft> _manualPaymentChanges;
    private readonly List<CompletedPayment> _completedPayments;
    private readonly List<RequiredPaymentTemplate> _requiredPayments;
    private List<SupplierDeliveryPattern> _deliveryPatterns;
    private List<SupplierWeekPlanRow> _supplierWeekRows;
    private List<RecurringSupplierDayRule> _recurringDayRules;
    private readonly List<SupplierDateScheduleOverride> _supplierScheduleOverrides;
    private readonly List<RecurringSupplierMembershipRule> _recurringSupplierMembershipRules;
    private readonly List<PartnerDebt> _partnerDebts;
    private readonly List<CashDocumentRecord> _cashDocuments;
    private AvailableFundsSettings _availableFunds;
    private readonly List<CashDeskAdjustment> _cashDeskAdjustments;
    private AvailableFundsBreakdown? _lastFunds;
    private System.Windows.Threading.DispatcherTimer? _telegramPollTimer;
    private System.Windows.Threading.DispatcherTimer? _employeeTelegramPollTimer;
    private bool _telegramPollInProgress;
    private bool _employeeTelegramPollInProgress;
    private bool _employeeMorningScheduleInProgress;
    private bool _employeeTaskScheduleInProgress;
    private bool _telegramScheduleInProgress;
    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private SummaryPeriod _summaryPeriod = SummaryPeriod.Month;
    private bool _uiReady;
    public MainWindow()
    {
        _manualPaymentChanges = _paymentChangeStore.Load().ToList();
        _completedPayments = _completedPaymentStore.Load();
        _requiredPayments = _requiredPaymentStore.LoadOrSeed();
        _deliveryPatterns = _deliveryScheduleStore.Load().ToList();
        _supplierWeekRows = _supplierWeekPlanStore.LoadOrSeed();
        _recurringDayRules = _recurringDayRuleStore.Load();
        _supplierScheduleOverrides = _supplierScheduleOverrideStore.Load();
        _recurringSupplierMembershipRules = _recurringSupplierMembershipRuleStore.Load();
        _partnerDebts = _partnerDebtStore.LoadOrSeed();
        _cashDocuments = _cashDocumentStore.Load();
        _availableFunds = _availableFundsStore.Load();
        _cashDeskAdjustments = _cashDeskAdjustmentStore.Load();
        InitializeComponent();
        ViewDatePicker.SelectedDate = _selectedDate.ToDateTime(TimeOnly.MinValue);
        ConfigureDataProvider();
        Loaded += async (_, _) =>
        {
            _uiReady = true;
            await LoadAsync("Dashboard");
            StartTelegramPolling();
            StartEmployeeTelegramPolling();
        };
    }

    private void ConfigureDataProvider()
    {
        var settings = _htsApiSettingsStore.Load();
        if (settings.IsConfigured)
        {
            App.Services.UseDataProvider(new HtsApiDataProvider(settings));
            DataSourceStatusText.Text = "Տվյալների աղբյուր՝ ՀԾ API";
        }
        else
        {
            App.Services.UseDataProvider(new DemoDataProvider());
            DataSourceStatusText.Text = "Տվյալների աղբյուր՝ Demo";
        }
    }

    private void StartTelegramPolling()
    {
        var settings = _telegramBotSettingsStore.Load();
        if (!settings.IsConfigured || string.IsNullOrWhiteSpace(settings.ChatId)) return;

        _telegramPollTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(25) };
        _telegramPollTimer.Tick -= TelegramPollTimer_Tick;
        _telegramPollTimer.Tick += TelegramPollTimer_Tick;
        _telegramPollTimer.Start();
        _ = ProcessTelegramMessagesAsync();
        _ = TrySendScheduledTelegramBriefsAsync();
    }

    private async void TelegramPollTimer_Tick(object? sender, EventArgs e)
    {
        await ProcessTelegramMessagesAsync();
        await TrySendScheduledTelegramBriefsAsync();
    }

    private void StartEmployeeTelegramPolling()
    {
        var settings = _employeeTelegramBotSettingsStore.Load();
        if (!settings.IsConfigured) return;
        _employeeTelegramPollTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(25) };
        _employeeTelegramPollTimer.Tick -= EmployeeTelegramPollTimer_Tick;
        _employeeTelegramPollTimer.Tick += EmployeeTelegramPollTimer_Tick;
        _employeeTelegramPollTimer.Start();
        _ = ProcessEmployeeTelegramMessagesAsync();
        _ = TrySendEmployeeMorningOrdersAsync();
        _ = TrySendEmployeeMorningTasksAsync();
        _ = TrySendEmployeeNextDayOrdersAsync();
        _ = TrySendEmployeeEveningReminderAsync();
    }

    private async void EmployeeTelegramPollTimer_Tick(object? sender, EventArgs e)
    {
        await ProcessEmployeeTelegramMessagesAsync();
        await TrySendEmployeeMorningOrdersAsync();
        await TrySendEmployeeMorningTasksAsync();
        await TrySendEmployeeNextDayOrdersAsync();
        await TrySendEmployeeEveningReminderAsync();
    }

    private async Task TrySendEmployeeMorningOrdersAsync()
    {
        if (_employeeMorningScheduleInProgress) return;
        var settings = _employeeTelegramBotSettingsStore.Load();
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (!settings.IsConfigured || settings.LastMorningOrdersDate == today || DateTime.Now.TimeOfDay < new TimeSpan(8, 0, 0)) return;
        _employeeMorningScheduleInProgress = true;
        try
        {
            foreach (var user in _employeeBotUserStore.Load())
                await SendEmployeeOrderListAsync(settings, user.ChatId, today);
            var latest = _employeeTelegramBotSettingsStore.Load();
            _employeeTelegramBotSettingsStore.Save(latest with { LastMorningOrdersDate = today });
        }
        catch
        {
            // The next polling cycle retries if the bot is temporarily unavailable.
        }
        finally { _employeeMorningScheduleInProgress = false; }
    }

    private async Task SendEmployeeOrderListAsync(EmployeeTelegramBotSettings settings, string chatId, DateOnly date, string workflow = "receipt")
    {
        var rows = PlannedSuppliersFor(date).OrderBy(x => x.Supplier).ToList();
        var isOrdering = workflow == "order";
        var message = new System.Text.StringBuilder();
        message.AppendLine(isOrdering ? $"📝 ՊԱՏՎԻՐԵԼ — {date:dd.MM.yyyy}" : $"📦 ԸՆԹԱՑԻԿ ՊԱՏՎԵՐ — {date:dd.MM.yyyy}");
        message.AppendLine(isOrdering ? "Վաղվա/նշված օրվա նախնական պատվերներն են՝ մենեջերին գրանցելու համար։" : "Այսօրվա սպասվող ապրանքների ցանկն է։ ");
        message.AppendLine($"📦 Այսօրվա պատվերներ — {date:dd.MM.yyyy}");
        message.AppendLine("Թվերը դրամով են։");
        message.AppendLine();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            message.AppendLine($"{i + 1}. {row.Supplier}");
            message.AppendLine($"   Պատվեր՝ {row.OrderAmount:N0} ֏ | Նոր վճարում՝ {row.PaymentAmount:N0} ֏ | Հին վճարում՝ {row.OldDebtPayment:N0} ֏");
        }
        if (rows.Count == 0) message.AppendLine("Այս օրվա համար մատակարար չկա։");
        message.AppendLine("Ցանկը նորից ստանալու համար գրեք՝ պատվերներ");
        message.AppendLine("Այլ օրվա համար՝ պատվերներ 27.07.2026");
        if (isOrdering) message.AppendLine("Փաստացի թվերը փոխելու համար սեղմեք «Գործողություն» → «Փաստացի թվեր»։");
        var dateCode = date.ToString("yyyyMMdd");
        var buttons = new IReadOnlyList<TelegramInlineButton>[]
        {
            new[] { new TelegramInlineButton("⚙️ Գործողություն", $"empactmenu:{workflow}:{dateCode}") }
        };
        await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(settings.BotToken, chatId), message.ToString(), buttons);
    }

    private async Task TrySendEmployeeMorningTasksAsync()
    {
        if (_employeeTaskScheduleInProgress) return;
        var settings = _employeeTelegramBotSettingsStore.Load();
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (!settings.IsConfigured || settings.LastMorningTasksDate == today || DateTime.Now.TimeOfDay < new TimeSpan(8, 0, 0)) return;
        _employeeTaskScheduleInProgress = true;
        try
        {
            foreach (var user in _employeeBotUserStore.Load())
                await SendEmployeeTasksAsync(settings, user.ChatId, today);
            var latest = _employeeTelegramBotSettingsStore.Load();
            _employeeTelegramBotSettingsStore.Save(latest with { LastMorningTasksDate = today });
        }
        catch { }
        finally { _employeeTaskScheduleInProgress = false; }
    }

    private async Task SendEmployeeTasksAsync(EmployeeTelegramBotSettings settings, string chatId, DateOnly date)
    {
        var tasks = _employeeTaskStore.Load().Where(x => x.Date == date).OrderBy(x => x.CreatedAt).ToList();
        var text = new System.Text.StringBuilder();
        text.AppendLine($"📋 Օրվա առաջադրանքներ — {date:dd.MM.yyyy}");
        if (tasks.Count == 0) text.AppendLine("Այս օրվա համար առաջադրանք չկա։");
        else
        {
            for (var i = 0; i < tasks.Count; i++) text.AppendLine($"{i + 1}. {tasks[i].Description}");
            text.AppendLine("\nԿարգավիճակ նշելու համար սեղմեք «Առաջադրանքի գործողություն»։");
        }
        var buttons = tasks.Count == 0 ? null : new IReadOnlyList<TelegramInlineButton>[]
        {
            new[] { new TelegramInlineButton("⚙️ Առաջադրանքի գործողություն", $"emptaskmenu:{date:yyyyMMdd}") }
        };
        await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(settings.BotToken, chatId), text.ToString(), buttons);
    }

    private async Task TrySendEmployeeNextDayOrdersAsync()
    {
        var settings = _employeeTelegramBotSettingsStore.Load();
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (!settings.IsConfigured || settings.LastNextDayPlanDate == today || DateTime.Now.TimeOfDay < new TimeSpan(8, 10, 0)) return;
        try
        {
            foreach (var user in _employeeBotUserStore.Load())
                await SendEmployeeOrderListAsync(settings, user.ChatId, today.AddDays(1), "order");
            var latest = _employeeTelegramBotSettingsStore.Load();
            _employeeTelegramBotSettingsStore.Save(latest with { LastNextDayPlanDate = today });
        }
        catch { }
    }

    private async Task TrySendEmployeeEveningReminderAsync()
    {
        var settings = _employeeTelegramBotSettingsStore.Load();
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (!settings.IsConfigured || settings.LastEveningReminderDate == today || DateTime.Now.TimeOfDay < new TimeSpan(20, 0, 0)) return;
        try
        {
            const string reminder = "🔔 20:00 հիշեցում\n\nԽնդրում ենք ստուգել այսօրվա ստացումները և վաղվա պատվերները։ Նշեք՝ պատվերը գրվել է, մենեջերը չի եկել, թե խնդիր կա։\n\nԱյսօրվա համար՝ ընթացիկ պատվեր\nՎաղվա պատվերների համար՝ պատվիրել";
            foreach (var user in _employeeBotUserStore.Load())
                await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(settings.BotToken, user.ChatId), reminder);
            var latest = _employeeTelegramBotSettingsStore.Load();
            _employeeTelegramBotSettingsStore.Save(latest with { LastEveningReminderDate = today });
        }
        catch { }
    }

    private async Task ProcessEmployeeActionCallbackAsync(EmployeeTelegramBotSettings settings, EmployeeTelegramIncomingMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.CallbackId)) return;
        await TelegramBotClient.AnswerCallbackAsync(new TelegramBotSettings(settings.BotToken, message.ChatId), message.CallbackId);
        var parts = message.Text.Split(':');
        if (parts.Length == 2 && parts[0] == "emptaskmenu" &&
            DateOnly.TryParseExact(parts[1], "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var taskDate))
        {
            var tasks = _employeeTaskStore.Load().Where(x => x.Date == taskDate).OrderBy(x => x.CreatedAt).ToList();
            var buttons = tasks.Select(task => (IReadOnlyList<TelegramInlineButton>)new[]
            {
                new TelegramInlineButton(task.Description.Length > 45 ? task.Description[..45] + "…" : task.Description, $"emptask:{task.Id}")
            }).ToList();
            await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(settings.BotToken, message.ChatId), "Ընտրեք առաջադրանքը։", buttons);
            return;
        }
        if (parts.Length == 2 && parts[0] == "emptask" && Guid.TryParse(parts[1], out var taskId))
        {
            var task = _employeeTaskStore.Load().FirstOrDefault(x => x.Id == taskId);
            if (task is null) return;
            var buttons = new IReadOnlyList<TelegramInlineButton>[]
            {
                new[]
                {
                    new TelegramInlineButton("✅ Կատարված", $"emptaskdone:{taskId}"),
                    new TelegramInlineButton("⚠️ Խնդիր", $"emptaskissue:{taskId}")
                }
            };
            await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(settings.BotToken, message.ChatId), $"Առաջադրանք՝ {task.Description}\nԸնտրեք կարգավիճակը։", buttons);
            return;
        }
        if (parts.Length == 2 && parts[0] == "emptaskdone" && Guid.TryParse(parts[1], out var doneTaskId))
        {
            SaveEmployeeTaskAction(doneTaskId, "Կատարված է", string.Empty, message);
            await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(settings.BotToken, message.ChatId), "✅ Առաջադրանքը նշվեց որպես կատարված։");
            return;
        }
        if (parts.Length == 2 && parts[0] == "emptaskissue" && Guid.TryParse(parts[1], out var issueTaskId))
        {
            var pending = _employeePendingTaskIssueStore.Load(); pending.RemoveAll(x => x.ChatId == message.ChatId); pending.Add(new EmployeePendingTaskIssue(message.ChatId, issueTaskId)); _employeePendingTaskIssueStore.Save(pending);
            await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(settings.BotToken, message.ChatId), "⚠️ Նկարագրեք առաջադրանքի խնդիրը մեկ հաղորդագրությամբ։");
            return;
        }
        if (parts.Length == 3 && parts[0] == "empactmenu" &&
            DateOnly.TryParseExact(parts[2], "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var menuDate))
        {
            var workflow = parts[1];
            var statusButtons = new IReadOnlyList<TelegramInlineButton>[]
            {
                new[]
                {
                    new TelegramInlineButton(workflow == "order" ? "✅ Պատվերը գրանցվել է" : "✅ Կատարված", $"empact:{workflow}:done:{menuDate:yyyyMMdd}"),
                    new TelegramInlineButton(workflow == "order" ? "🚚 Մենեջերը չի եկել" : "🚚 Չի եկել", $"empact:{workflow}:missing:{menuDate:yyyyMMdd}"),
                    new TelegramInlineButton("⚠️ Խնդիր", $"empact:{workflow}:issue:{menuDate:yyyyMMdd}"),
                    new TelegramInlineButton("✏️ Փաստացի թվեր", $"empact:{workflow}:actual:{menuDate:yyyyMMdd}")
                }
            };
            await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(settings.BotToken, message.ChatId), "Ընտրեք կարգավիճակը։", statusButtons);
            return;
        }
        if (parts.Length < 4 || !DateOnly.TryParseExact(parts[3], "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date)) return;

        if (parts[0] == "empact")
        {
            var workflow = parts[1];
            var action = parts[2];
            var rows = PlannedSuppliersFor(date).OrderBy(x => x.Supplier).ToList();
            var buttons = rows.Select((row, index) => (IReadOnlyList<TelegramInlineButton>)new[]
            {
                new TelegramInlineButton(row.Supplier, $"empsup:{workflow}:{action}:{date:yyyyMMdd}:{index}")
            }).ToList();
            var title = action == "done" ? "Կատարված" : action == "missing" ? "Չի եկել" : "Խնդիր";
            await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(settings.BotToken, message.ChatId),
                $"{title} — ընտրեք մատակարարին։", buttons);
            return;
        }

        if (parts[0] != "empsup" || parts.Length != 5 || !int.TryParse(parts[4], out var index)) return;
        var workflowForSupplier = parts[1];
        var status = parts[2];
        var suppliers = PlannedSuppliersFor(date).OrderBy(x => x.Supplier).ToList();
        if (index < 0 || index >= suppliers.Count) return;
        var supplier = suppliers[index].Supplier;
        var replySettings = new TelegramBotSettings(settings.BotToken, message.ChatId);
        if (status is "issue" or "actual")
        {
            var pending = _employeePendingIssueStore.Load();
            pending.RemoveAll(x => x.ChatId == message.ChatId);
            pending.Add(new EmployeePendingIssue(message.ChatId, date, supplier, workflowForSupplier, status));
            _employeePendingIssueStore.Save(pending);
            await TelegramBotClient.SendMessageAsync(replySettings, status == "actual"
                ? $"✏️ {supplier}\nԳրեք փաստացի թվերը այս ձևով՝ պատվեր/նոր վճարում/հին պարտքի վճարում\nՕրինակ՝ 5000/5000/2500"
                : $"⚠️ {supplier}\nՆկարագրեք խնդիրը մեկ հաղորդագրությամբ։ Օրինակ՝ «վճարում չի կատարվել», «ապրանքը հին էր», «մատակարարը ուշանալու է»։");
            return;
        }

        var statusText = status == "done" ? (workflowForSupplier == "order" ? "Պատվերը գրանցվել է" : "Կատարված է") : (workflowForSupplier == "order" ? "Մենեջերը չի եկել / պատվերը չի գրվել" : "Չի եկել");
        SaveEmployeeSupplierAction(date, supplier, statusText, string.Empty, message);
        await TelegramBotClient.SendMessageAsync(replySettings, $"✅ Գրանցվեց՝ {supplier} — {statusText}։");
    }

    private async Task RegisterEmployeeActualOrderAsync(EmployeePendingIssue pending, EmployeeTelegramIncomingMessage message, TelegramBotSettings replySettings)
    {
        if (!TryParseOrderAndPayment(message.Text, out var actualOrder, out var actualPayment, out var actualOldDebtPayment))
        {
            await TelegramBotClient.SendMessageAsync(replySettings, "Թվերը չհասկացա։ Գրեք այս ձևով՝ պատվեր/նոր վճարում/հին պարտքի վճարում։ Օրինակ՝ 5000/5000/2500");
            var items = _employeePendingIssueStore.Load();
            items.RemoveAll(x => x.ChatId == message.ChatId);
            items.Add(pending);
            _employeePendingIssueStore.Save(items);
            return;
        }
        var planned = PlannedSuppliersFor(pending.Date).FirstOrDefault(x => SupplierNamesMatch(x.Supplier, pending.Supplier));
        var plannedOrder = planned?.OrderAmount ?? 0m;
        var plannedPayment = planned?.PaymentAmount ?? 0m;
        var plannedOldDebtPayment = planned?.OldDebtPayment ?? 0m;
        if (actualOrder == plannedOrder && actualPayment == plannedPayment && actualOldDebtPayment == plannedOldDebtPayment)
        {
            SaveEmployeeSupplierAction(pending.Date, pending.Supplier, "Կատարված է", $"Փաստացի ստացվել է՝ պատվեր {actualOrder:N0} ֏, նոր վճարում {actualPayment:N0} ֏, հին պարտքի վճարում {actualOldDebtPayment:N0} ֏", message);
            await TelegramBotClient.SendMessageAsync(replySettings, "✅ Փաստացի թվերը համընկան նախնական պլանի հետ և գրանցվեցին։");
            return;
        }
        var change = new PendingEmployeeOrderChange(Guid.NewGuid(), pending.Date, pending.Supplier, plannedOrder, plannedPayment, actualOrder, actualPayment, message.ChatId, message.DisplayName, DateTime.Now, plannedOldDebtPayment, actualOldDebtPayment);
        var changes = _pendingEmployeeOrderChangeStore.Load(); changes.Add(change); _pendingEmployeeOrderChangeStore.Save(changes);
        await TelegramBotClient.SendMessageAsync(replySettings, "⏳ Շեղումը պահպանվեց և կուղարկվի տնօրենին 21:00-ի ընդհանուր հաստատումների ցանկով։ Անհրաժեշտության դեպքում տնօրենը կարող է շուտ ստանալ այն՝ «հաստատումներ» հրամանով։");
    }

    private static bool TryParseOrderAndPayment(string text, out decimal order, out decimal payment, out decimal oldDebtPayment)
    {
        order = payment = oldDebtPayment = 0m;
        var parts = text.Replace(" ", "").Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 3 && decimal.TryParse(parts[0], out order) && decimal.TryParse(parts[1], out payment) && decimal.TryParse(parts[2], out oldDebtPayment) && order >= 0m && payment >= 0m && oldDebtPayment >= 0m;
    }

    private async Task RegisterNewSupplierFromEmployeeAsync(EmployeeTelegramIncomingMessage message, TelegramBotSettings replySettings)
    {
        const string prefix = "ավելացնել մատակարար";
        var source = message.Text.Trim().TrimStart('/');
        var value = source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? source[prefix.Length..].Trim() : string.Empty;
        var date = TryTelegramDate(value, out var specifiedDate) ? specifiedDate : DateOnly.FromDateTime(DateTime.Today).AddDays(1);
        if (TryTelegramDate(value, out _))
        {
            var dateText = System.Text.RegularExpressions.Regex.Match(value, @"\d{1,2}[,./-]\d{1,2}(?:[,./-]\d{2,4})?").Value;
            value = value[dateText.Length..].Trim().TrimStart('-', '–').Trim();
        }
        var lastSpace = value.LastIndexOf(' ');
        if (lastSpace < 1 || !TryParseOrderAndPayment(value[(lastSpace + 1)..], out var order, out var payment, out var oldDebtPayment))
        {
            await TelegramBotClient.SendMessageAsync(replySettings, "Գրեք այս ձևով՝\nավելացնել մատակարար 29.07.2026 - Նոր մատակարար 5000/5000/2500\n\nՎերջի երեք թվերը՝ պատվեր/նոր վճարում/հին պարտքի վճարում։");
            return;
        }
        var supplier = value[..lastSpace].Trim();
        var changes = _pendingEmployeeOrderChangeStore.Load();
        changes.Add(new PendingEmployeeOrderChange(Guid.NewGuid(), date, supplier, 0m, 0m, order, payment, message.ChatId, message.DisplayName, DateTime.Now, 0m, oldDebtPayment));
        _pendingEmployeeOrderChangeStore.Save(changes);
        await TelegramBotClient.SendMessageAsync(replySettings, $"⏳ Նոր մատակարարը գրանցվեց հաստատման համար։\n{date:dd.MM.yyyy} · {supplier}\n{order:N0}/{payment:N0}/{oldDebtPayment:N0}");
    }

    private void SaveEmployeeSupplierAction(DateOnly date, string supplier, string status, string description, EmployeeTelegramIncomingMessage message)
    {
        var actions = _employeeSupplierActionStore.Load();
        if (status != "Խնդիր")
            actions.RemoveAll(x => x.Date == date && SupplierNamesMatch(x.Supplier, supplier) && x.Status != "Խնդիր");
        actions.Add(new EmployeeSupplierAction(date, supplier, status, description, message.ChatId, message.DisplayName, DateTime.Now));
        _employeeSupplierActionStore.Save(actions);
        RefreshRecommendationsIfOpen();
    }

    private void SaveEmployeeTaskAction(Guid taskId, string status, string description, EmployeeTelegramIncomingMessage message)
    {
        var actions = _employeeTaskActionStore.Load();
        actions.RemoveAll(x => x.TaskId == taskId && x.ChatId == message.ChatId);
        actions.Add(new EmployeeTaskAction(taskId, status, description, message.ChatId, message.DisplayName, DateTime.Now));
        _employeeTaskActionStore.Save(actions);
        RefreshRecommendationsIfOpen();
    }

    private void RefreshRecommendationsIfOpen()
    {
        if (_currentPage == "Recommendations") _ = LoadAsync("Recommendations");
    }

    private async Task ReportEmployeeIssueToOwnerAsync(EmployeeIssue issue)
    {
        RefreshRecommendationsIfOpen();
        var owner = _telegramBotSettingsStore.Load();
        if (!owner.IsConfigured || string.IsNullOrWhiteSpace(owner.ChatId)) return;
        var task = string.IsNullOrWhiteSpace(issue.TaskDescription) ? "Ընդհանուր խնդիր" : $"Առաջադրանք՝ {issue.TaskDescription}";
        var buttons = new IReadOnlyList<TelegramInlineButton>[]
        {
            new[] { new TelegramInlineButton("💬 Պատասխանել աշխատակցին", $"empissuereply:{issue.Id}") }
        };
        await TelegramBotClient.SendMessageAsync(owner,
            $"⚠️ Աշխատակցի խնդիր\n\nԱմսաթիվ՝ {issue.Date:dd.MM.yyyy}\n{task}\nՆկարագրություն՝ {issue.Description}\nՆշել է՝ {issue.EmployeeName}", buttons);
    }

    private void ShowSupplierEmployeeStatus(string supplier)
    {
        var actions = _employeeSupplierActionStore.Load()
            .Where(x => SupplierNamesMatch(x.Supplier, supplier))
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.ReportedAt)
            .ToList();
        var today = actions.FirstOrDefault(x => x.Date == _selectedDate);
        if (today is not null)
        {
            var text = string.IsNullOrWhiteSpace(today.Description) ? today.Status : today.Description;
            MessageBox.Show($"Մատակարար՝ {supplier}\nԱմսաթիվ՝ {_selectedDate:dd.MM.yyyy}\nԿարգավիճակ՝ {today.Status}\n\n{text}\n\nՆշել է՝ {today.ReportedByName} ({today.ReportedAt:HH:mm})", "Աշխատակցի նշում", MessageBoxButton.OK,
                today.Status == "Խնդիր" ? MessageBoxImage.Warning : MessageBoxImage.Information);
            return;
        }
        var history = actions.Where(x => x.Status is "Խնդիր" or "Չի եկել").Take(8).ToList();
        if (history.Count == 0)
        {
            MessageBox.Show($"{supplier}-ի համար {_selectedDate:dd.MM.yyyy}-ին աշխատակիցը դեռ նշում չի ուղարկել։", "Մատակարար", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var lines = history.Select(x => $"{x.Date:dd.MM.yyyy} · {x.Status} · {(string.IsNullOrWhiteSpace(x.Description) ? "Առանց նկարագրության" : x.Description)}");
        MessageBox.Show($"{supplier}\n\nԽնդիրների վերջին պատմությունը՝\n{string.Join("\n", lines)}", "Մատակարարի խնդիրների պատմություն", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private async Task ProcessEmployeeTelegramMessagesAsync()
    {
        if (_employeeTelegramPollInProgress) return;
        var settings = _employeeTelegramBotSettingsStore.Load();
        if (!settings.IsConfigured) return;
        _employeeTelegramPollInProgress = true;
        try
        {
            var messages = await TelegramBotClient.GetEmployeeMessagesAsync(settings);
            if (messages.Count == 0) return;
            var users = _employeeBotUserStore.Load();
            foreach (var message in messages)
            {
                var userIndex = users.FindIndex(x => x.ChatId == message.ChatId);
                var user = new EmployeeBotUser(message.ChatId, message.DisplayName, userIndex >= 0 ? users[userIndex].RegisteredAt : DateTime.Now, DateTime.Now);
                if (userIndex >= 0) users[userIndex] = user; else users.Add(user);

                var replySettings = new TelegramBotSettings(settings.BotToken, message.ChatId);
                if (!string.IsNullOrWhiteSpace(message.CallbackId))
                {
                    await ProcessEmployeeActionCallbackAsync(settings, message);
                }
                else if (message.Text.Equals("/start", StringComparison.OrdinalIgnoreCase))
                {
                    await TelegramBotClient.SendMessageAsync(replySettings,
                        "✅ Դուք գրանցվել եք Patarik-ի աշխատակիցների բոտում։\n\nԱյսօրվա ստացումները՝ գրեք «ընթացիկ պատվեր»։\nՎաղվա պատվերը մենեջերին գրելու համար՝ գրեք «պատվիրել»։\nՕրինակ՝ «պատվիրել 29.07.2026»։");
                    await SendEmployeeOrderListAsync(settings, message.ChatId, DateOnly.FromDateTime(DateTime.Today));
                }
                else if (message.Text.StartsWith("ընթացիկ պատվեր", StringComparison.OrdinalIgnoreCase) || message.Text.StartsWith("/ընթացիկ պատվեր", StringComparison.OrdinalIgnoreCase) || message.Text.StartsWith("պատվերներ", StringComparison.OrdinalIgnoreCase) || message.Text.StartsWith("/պատվերներ", StringComparison.OrdinalIgnoreCase))
                {
                    var date = TryTelegramDate(message.Text, out var requestedDate) ? requestedDate : DateOnly.FromDateTime(DateTime.Today);
                    await SendEmployeeOrderListAsync(settings, message.ChatId, date);
                }
                else if (message.Text.StartsWith("պատվիրել", StringComparison.OrdinalIgnoreCase) || message.Text.StartsWith("/պատվիրել", StringComparison.OrdinalIgnoreCase))
                {
                    var date = TryTelegramDate(message.Text, out var requestedDate) ? requestedDate : DateOnly.FromDateTime(DateTime.Today).AddDays(1);
                    await SendEmployeeOrderListAsync(settings, message.ChatId, date, "order");
                }
                else if (message.Text.StartsWith("ավելացնել մատակարար", StringComparison.OrdinalIgnoreCase) || message.Text.StartsWith("/ավելացնել մատակարար", StringComparison.OrdinalIgnoreCase))
                {
                    await RegisterNewSupplierFromEmployeeAsync(message, replySettings);
                }
                else if (message.Text.StartsWith("առաջադրանք", StringComparison.OrdinalIgnoreCase) || message.Text.StartsWith("/առաջադրանք", StringComparison.OrdinalIgnoreCase))
                {
                    var date = TryTelegramDate(message.Text, out var requestedDate) ? requestedDate : DateOnly.FromDateTime(DateTime.Today);
                    await SendEmployeeTasksAsync(settings, message.ChatId, date);
                }
                else if (message.Text.Equals("խնդիրներ", StringComparison.OrdinalIgnoreCase) || message.Text.Equals("/խնդիրներ", StringComparison.OrdinalIgnoreCase))
                {
                    var pending = _employeePendingGeneralIssueStore.Load(); pending.RemoveAll(x => x.ChatId == message.ChatId); pending.Add(new EmployeePendingGeneralIssue(message.ChatId)); _employeePendingGeneralIssueStore.Save(pending);
                    await TelegramBotClient.SendMessageAsync(replySettings, "⚠️ Նկարագրեք խնդիրը մեկ հաղորդագրությամբ։ Այն անմիջապես կուղարկվի տնօրենին։");
                }
                else if (_employeePendingTaskIssueStore.Load().FirstOrDefault(x => x.ChatId == message.ChatId) is { } pendingTaskIssue)
                {
                    var task = _employeeTaskStore.Load().FirstOrDefault(x => x.Id == pendingTaskIssue.TaskId);
                    _employeePendingTaskIssueStore.Save(_employeePendingTaskIssueStore.Load().Where(x => x.ChatId != message.ChatId));
                    if (task is not null)
                    {
                        SaveEmployeeTaskAction(task.Id, "Խնդիր", message.Text, message);
                        var issue = new EmployeeIssue(Guid.NewGuid(), task.Date, message.Text, message.ChatId, message.DisplayName, DateTime.Now, task.Description);
                        var issues = _employeeIssueStore.Load(); issues.Add(issue); _employeeIssueStore.Save(issues);
                        await ReportEmployeeIssueToOwnerAsync(issue);
                        await TelegramBotClient.SendMessageAsync(replySettings, "⚠️ Խնդիրը գրանցվեց և ուղարկվեց տնօրենին։");
                    }
                }
                else if (_employeePendingGeneralIssueStore.Load().Any(x => x.ChatId == message.ChatId))
                {
                    _employeePendingGeneralIssueStore.Save(_employeePendingGeneralIssueStore.Load().Where(x => x.ChatId != message.ChatId));
                    var issue = new EmployeeIssue(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), message.Text, message.ChatId, message.DisplayName, DateTime.Now);
                    var issues = _employeeIssueStore.Load(); issues.Add(issue); _employeeIssueStore.Save(issues);
                    await ReportEmployeeIssueToOwnerAsync(issue);
                    await TelegramBotClient.SendMessageAsync(replySettings, "⚠️ Խնդիրը գրանցվեց և ուղարկվեց տնօրենին։");
                }
                else if (_employeePendingIssueStore.Load().FirstOrDefault(x => x.ChatId == message.ChatId) is { } pendingIssue)
                {
                    var pending = _employeePendingIssueStore.Load();
                    pending.RemoveAll(x => x.ChatId == message.ChatId);
                    _employeePendingIssueStore.Save(pending);
                    if (pendingIssue.PendingAction == "actual")
                        await RegisterEmployeeActualOrderAsync(pendingIssue, message, replySettings);
                    else
                    {
                        SaveEmployeeSupplierAction(pendingIssue.Date, pendingIssue.Supplier, "Խնդիր", message.Text, message);
                        await TelegramBotClient.SendMessageAsync(replySettings, $"⚠️ Խնդիրը գրանցվեց՝ {pendingIssue.Supplier}։ Տնօրենի ամփոփման մեջ այն կներառվի։");
                    }
                }
                else
                {
                    await TelegramBotClient.SendMessageAsync(replySettings,
                        "Պատվերների ցանկի համար գրեք՝ պատվերներ");
                }
            }
            _employeeBotUserStore.Save(users);
            _employeeTelegramBotSettingsStore.Save(settings with { LastUpdateId = messages.Max(x => x.UpdateId) });
        }
        catch
        {
            // The employee bot will retry on the next polling cycle.
        }
        finally { _employeeTelegramPollInProgress = false; }
    }

    private async Task ProcessTelegramMessagesAsync()
    {
        if (_telegramPollInProgress) return;
        var settings = _telegramBotSettingsStore.Load();
        if (!settings.IsConfigured || string.IsNullOrWhiteSpace(settings.ChatId)) return;

        _telegramPollInProgress = true;
        try
        {
            var messages = await TelegramBotClient.GetNewMessagesAsync(settings);
            if (messages.Count == 0) return;

            foreach (var message in messages)
            {
                if (!string.IsNullOrWhiteSpace(message.CallbackId))
                {
                    await TelegramBotClient.AnswerCallbackAsync(settings, message.CallbackId);
                    await ProcessTelegramButtonAsync(settings, message.Text);
                    continue;
                }
                if (_ownerPendingEmployeeIssueReplyStore.Load().FirstOrDefault() is { } pendingReply)
                {
                    var issues = _employeeIssueStore.Load();
                    var issueIndex = issues.FindIndex(x => x.Id == pendingReply.IssueId);
                    _ownerPendingEmployeeIssueReplyStore.Save([]);
                    if (issueIndex >= 0)
                    {
                        var issue = issues[issueIndex] with { DirectorResponse = message.Text.Trim() };
                        issues[issueIndex] = issue; _employeeIssueStore.Save(issues);
                        var employeeSettings = _employeeTelegramBotSettingsStore.Load();
                        if (employeeSettings.IsConfigured)
                            await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(employeeSettings.BotToken, issue.ChatId), $"💬 Տնօրենի պատասխան\n\n{issue.DirectorResponse}");
                        await TelegramBotClient.SendMessageAsync(settings, "✅ Պատասխանը ուղարկվեց աշխատակցին։");
                    }
                    continue;
                }
                var command = message.Text.Trim().ToLowerInvariant();
                if (command is "սկսել" or "/սկսել" or "/start")
                    await SendTelegramDraftAsync(settings);
                else if (command.StartsWith("գլխավոր", StringComparison.OrdinalIgnoreCase) || command.StartsWith("/գլխավոր", StringComparison.OrdinalIgnoreCase))
                    await SendTelegramDashboardAsync(settings, TryTelegramDate(message.Text, out var dashboardDate) ? dashboardDate : DateOnly.FromDateTime(DateTime.Today));
                else if (command.StartsWith("առավոտ", StringComparison.OrdinalIgnoreCase) || command.StartsWith("/առավոտ", StringComparison.OrdinalIgnoreCase))
                    await SendTelegramMorningBriefAsync(settings, TryTelegramDate(message.Text, out var morningDate) ? morningDate : DateOnly.FromDateTime(DateTime.Today));
                else if (command.StartsWith("վճարումներ", StringComparison.OrdinalIgnoreCase) || command.StartsWith("/վճարումներ", StringComparison.OrdinalIgnoreCase))
                    await SendTelegramPaymentsAsync(settings, TryTelegramDate(message.Text, out var paymentsDate) ? paymentsDate : DateOnly.FromDateTime(DateTime.Today));
                else if (command.StartsWith("հաստատումներ", StringComparison.OrdinalIgnoreCase) || command.StartsWith("/հաստատումներ", StringComparison.OrdinalIgnoreCase))
                    await SendPendingConfirmationsAsync(settings);
                else if (command.StartsWith("առաջադրանք", StringComparison.OrdinalIgnoreCase) || command.StartsWith("/առաջադրանք", StringComparison.OrdinalIgnoreCase))
                    await AddEmployeeTaskFromTelegramAsync(settings, message.Text);
                else if ((message.Text.Contains('/') || message.Text.Contains("հեռացնել", StringComparison.OrdinalIgnoreCase)) && await TryRegisterTelegramBatchAsync(settings, message.Text.Trim()))
                {
                    // The revised plan is sent by TryRegisterTelegramBatchAsync.
                }
                else if (command.StartsWith("ցուցադրել", StringComparison.OrdinalIgnoreCase))
                    await ShowTelegramPlanForDateAsync(settings, message.Text.Trim());
                else if (command.StartsWith("ավարտ", StringComparison.OrdinalIgnoreCase))
                    await FinishTelegramPlanAsync(settings, message.Text.Trim());
                else if (command.StartsWith("գրանցել", StringComparison.OrdinalIgnoreCase))
                    await RegisterTelegramSupplierPlanAsync(settings, message.Text.Trim());
                else if (command.Contains("փոխել") && (command.Contains("պատվեր") || command.Contains("առաջարկ")))
                    await ChangeTelegramOrderAndSendDraftAsync(settings, message.Text.Trim());
                else if (message.Text.Trim().StartsWith("ավելացնել ", StringComparison.OrdinalIgnoreCase))
                    await AddTelegramSupplierAndSendDraftAsync(settings, message.Text.Trim()["ավելացնել ".Length..].Trim());
                else if (command.Contains("մատակարար") || command.Contains("պատվեր") || command.Contains("գումար"))
                    await SendTelegramDraftAsync(settings);
                else
                    await TelegramBotClient.SendMessageAsync(settings, "Գրեք «սկսել»՝ նախնական պլանը ստանալու համար, կամ «ավելացնել <մատակարարի անուն>»՝ նոր մատակարար ավելացնելու համար։");
            }

            var latestSettings = _telegramBotSettingsStore.Load();
            _telegramBotSettingsStore.Save(latestSettings with { LastUpdateId = messages.Max(x => x.UpdateId) });
        }
        catch
        {
            // A temporary Telegram outage must not interrupt the desktop application.
        }
        finally { _telegramPollInProgress = false; }
    }

    private async Task ProcessTelegramButtonAsync(TelegramBotSettings settings, string data)
    {
        if (data.Equals("emporderapproveall", StringComparison.OrdinalIgnoreCase))
        {
            var changes = _pendingEmployeeOrderChangeStore.Load().ToList();
            if (changes.Count == 0) { await TelegramBotClient.SendMessageAsync(settings, "Հաստատման սպասող փոփոխություններ չկան։"); return; }
            foreach (var change in changes) await ApproveEmployeeOrderChangeAsync(settings, change.Id, announceToOwner: false);
            await TelegramBotClient.SendMessageAsync(settings, $"✅ Հաստատվեց բոլոր փոփոխությունները՝ {changes.Count} հատ։");
            return;
        }
        if (data.StartsWith("empissuereply:", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(data["empissuereply:".Length..], out var issueId))
        {
            var pending = _ownerPendingEmployeeIssueReplyStore.Load(); pending.Clear(); pending.Add(new OwnerPendingEmployeeIssueReply(issueId)); _ownerPendingEmployeeIssueReplyStore.Save(pending);
            await TelegramBotClient.SendMessageAsync(settings, "Գրեք աշխատակցին ուղարկվող պատասխանը մեկ հաղորդագրությամբ։");
            return;
        }
        if (data.StartsWith("emporderapprove:", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(data["emporderapprove:".Length..], out var approvedId))
        {
            await ApproveEmployeeOrderChangeAsync(settings, approvedId);
            return;
        }
        if (data.StartsWith("emporderreject:", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(data["emporderreject:".Length..], out var rejectedId))
        {
            await RejectEmployeeOrderChangeAsync(settings, rejectedId);
            return;
        }
        var parts = data.Split(':', 2);
        if (parts.Length != 2 || !DateOnly.TryParseExact(parts[1], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date)) return;
        switch (parts[0])
        {
            case "show":
                _telegramBotSettingsStore.Save(_telegramBotSettingsStore.Load() with { EditingDate = date });
                await SendTelegramDraftAsync(settings, date);
                break;
            case "dashboard":
                await SendTelegramDashboardAsync(settings, date);
                break;
            case "morning":
                await SendTelegramMorningBriefAsync(settings, date);
                break;
            case "payments":
                await SendTelegramPaymentsAsync(settings, date);
                break;
            case "form":
                await TelegramBotClient.SendMessageAsync(settings,
                    $"Լրացրեք և ուղարկեք այս ձևը՝\n\nգրանցել\nամսաթիվ: {date:dd.MM.yyyy}\nմատակարար: \nպատվեր: \nվճարում: \nտեսակ: նոր");
                break;
            case "batch":
                await SendTelegramBatchEditFormAsync(settings, date);
                break;
            case "finish":
                await FinishTelegramPlanAsync(settings, $"ավարտ {date:dd.MM.yyyy}");
                break;
        }
    }

    private async Task ApproveEmployeeOrderChangeAsync(TelegramBotSettings ownerSettings, Guid changeId, bool announceToOwner = true)
    {
        var changes = _pendingEmployeeOrderChangeStore.Load();
        var change = changes.FirstOrDefault(x => x.Id == changeId);
        if (change is null) { await TelegramBotClient.SendMessageAsync(ownerSettings, "Այս փոփոխությունն արդեն մշակված է կամ չի գտնվել։"); return; }
        var index = _supplierWeekRows.FindIndex(x => x.Date == change.Date && SupplierNamesMatch(x.Supplier, change.Supplier));
        if (index >= 0)
        {
            var source = _supplierWeekRows[index];
            _supplierWeekRows[index] = source with { OrderAmount = change.ActualOrder, PaymentAmount = change.ActualPayment, OldDebtPayment = change.ActualOldDebtPayment };
        }
        else _supplierWeekRows.Add(new SupplierWeekPlanRow(change.Date, change.Supplier, change.ActualOrder, change.ActualPayment, change.ActualOldDebtPayment, DebtBeforeDate(change.Supplier, change.Date)));
        _supplierWeekPlanStore.Save(_supplierWeekRows);
        var actions = _employeeSupplierActionStore.Load();
        actions.Add(new EmployeeSupplierAction(change.Date, change.Supplier, "Կատարված է", $"Նախնական՝ {change.PlannedOrder:N0}/{change.PlannedPayment:N0}/{change.PlannedOldDebtPayment:N0}; փաստացի ստացվել է՝ {change.ActualOrder:N0}/{change.ActualPayment:N0}/{change.ActualOldDebtPayment:N0}", change.ReportedByChatId, change.ReportedByName, DateTime.Now));
        _employeeSupplierActionStore.Save(actions);
        changes.RemoveAll(x => x.Id == changeId); _pendingEmployeeOrderChangeStore.Save(changes);
        if (announceToOwner && ownerSettings.IsConfigured && !string.IsNullOrWhiteSpace(ownerSettings.ChatId))
            await TelegramBotClient.SendMessageAsync(ownerSettings, $"✅ Գրանցվեց՝ {change.Supplier}, {change.Date:dd.MM.yyyy}։");
        var employeeSettings = _employeeTelegramBotSettingsStore.Load();
        if (employeeSettings.IsConfigured)
            await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(employeeSettings.BotToken, change.ReportedByChatId), $"✅ Տնօրենը հաստատեց {change.Supplier}-ի պատվերի փոփոխությունը։");
        if (_currentPage is "Suppliers" or "Recommendations" or "Approvals") await LoadAsync(_currentPage);
    }

    private async Task RejectEmployeeOrderChangeAsync(TelegramBotSettings ownerSettings, Guid changeId)
    {
        var changes = _pendingEmployeeOrderChangeStore.Load();
        var change = changes.FirstOrDefault(x => x.Id == changeId);
        if (change is null) { await TelegramBotClient.SendMessageAsync(ownerSettings, "Այս փոփոխությունն արդեն մշակված է կամ չի գտնվել։"); return; }
        changes.RemoveAll(x => x.Id == changeId); _pendingEmployeeOrderChangeStore.Save(changes);
        if (ownerSettings.IsConfigured && !string.IsNullOrWhiteSpace(ownerSettings.ChatId))
            await TelegramBotClient.SendMessageAsync(ownerSettings, $"✕ Չհաստատվեց՝ {change.Supplier}, {change.Date:dd.MM.yyyy}։");
        var employeeSettings = _employeeTelegramBotSettingsStore.Load();
        if (employeeSettings.IsConfigured)
            await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(employeeSettings.BotToken, change.ReportedByChatId), $"ℹ️ Տնօրենը դեռ չի հաստատել {change.Supplier}-ի փոփոխությունը։");
        if (_currentPage == "Approvals") await LoadAsync("Approvals");
    }

    private async Task AddEmployeeTaskFromTelegramAsync(TelegramBotSettings settings, string message)
    {
        if (!TryTelegramDate(message, out var date))
        {
            await TelegramBotClient.SendMessageAsync(settings, "Գրեք այս ձևով՝\nառաջադրանք 29.07.2026 - գնապիտակի ճշտում");
            return;
        }
        var dash = message.IndexOf('-');
        if (dash < 0 || string.IsNullOrWhiteSpace(message[(dash + 1)..]))
        {
            await TelegramBotClient.SendMessageAsync(settings, "Նշեք նաև առաջադրանքի բնութագիրը։\nՕրինակ՝ առաջադրանք 29.07.2026 - գնապիտակի ճշտում");
            return;
        }
        var description = message[(dash + 1)..].Trim();
        var task = new EmployeeTask(Guid.NewGuid(), date, description, DateTime.Now);
        var tasks = _employeeTaskStore.Load(); tasks.Add(task); _employeeTaskStore.Save(tasks);
        await TelegramBotClient.SendMessageAsync(settings, $"✅ Առաջադրանքը ավելացվեց՝ {date:dd.MM.yyyy}\n{description}");

        if (date == DateOnly.FromDateTime(DateTime.Today))
        {
            var employeeSettings = _employeeTelegramBotSettingsStore.Load();
            if (employeeSettings.IsConfigured)
                foreach (var user in _employeeBotUserStore.Load())
                    await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(employeeSettings.BotToken, user.ChatId), $"📌 Դուք ստացել եք նոր առաջադրանք\n\n{description}\n\nՏեսնելու համար գրեք՝ առաջադրանք");
        }
    }

    private async Task SendPendingConfirmationsAsync(TelegramBotSettings settings)
    {
        var changes = _pendingEmployeeOrderChangeStore.Load().OrderBy(x => x.Date).ThenBy(x => x.Supplier).ToList();
        if (changes.Count == 0)
        {
            await TelegramBotClient.SendMessageAsync(settings, "✅ Հաստատման սպասող փոփոխություններ չկան։");
            return;
        }
        var text = new System.Text.StringBuilder();
        text.AppendLine($"🗂 Հաստատումների ցանկ — {changes.Count} հատ");
        foreach (var change in changes)
        {
            text.AppendLine();
            text.AppendLine($"{change.Date:dd.MM.yyyy} · {change.Supplier}");
            text.AppendLine($"Պլան՝ {change.PlannedOrder:N0}/{change.PlannedPayment:N0}/{change.PlannedOldDebtPayment:N0}");
            text.AppendLine($"Փաստացի՝ {change.ActualOrder:N0}/{change.ActualPayment:N0}/{change.ActualOldDebtPayment:N0}");
            text.AppendLine($"Աշխատակից՝ {change.ReportedByName}");
        }
        var buttons = new IReadOnlyList<TelegramInlineButton>[]
        {
            new[] { new TelegramInlineButton("✅ Հաստատել բոլորը", "emporderapproveall") }
        };
        await TelegramBotClient.SendMessageAsync(settings, text.ToString(), buttons);
    }

    private async Task SendEveningOperationsAsync(TelegramBotSettings settings, DateOnly date)
    {
        var received = PlannedSuppliersFor(date).OrderBy(x => x.Supplier).ToList();
        var tomorrow = PlannedSuppliersFor(date.AddDays(1)).OrderBy(x => x.Supplier).ToList();
        var actions = _employeeSupplierActionStore.Load();
        var confirmed = actions.Count(x => x.Date == date && x.Status == "Կատարված է");
        var problems = actions.Count(x => x.Date == date && x.Status is "Խնդիր" or "Չի եկել");
        var text = new System.Text.StringBuilder();
        text.AppendLine($"🌙 Երեկոյան օպերացիոն ամփոփում — {date:dd.MM.yyyy}");
        text.AppendLine();
        text.AppendLine($"Այսօրվա մատակարարումներ՝ {received.Count}");
        text.AppendLine($"Հաստատված ստացումներ՝ {confirmed}");
        text.AppendLine($"Խնդիրներ / չեկած՝ {problems}");
        text.AppendLine();
        text.AppendLine($"Վաղվա մատակարարումներ — {date.AddDays(1):dd.MM.yyyy}");
        foreach (var item in tomorrow) text.AppendLine($"• {item.Supplier} · պատվեր {item.OrderAmount:N0} ֏ · վճարում {item.PaymentAmount + item.OldDebtPayment:N0} ֏");
        await TelegramBotClient.SendMessageAsync(settings, text.ToString());
        await SendPendingConfirmationsAsync(settings);
    }

    private async Task TrySendScheduledTelegramBriefsAsync()
    {
        if (_telegramScheduleInProgress) return;
        var settings = _telegramBotSettingsStore.Load();
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (!settings.IsConfigured || string.IsNullOrWhiteSpace(settings.ChatId)) return;
        _telegramScheduleInProgress = true;
        try
        {
            var now = DateTime.Now.TimeOfDay;
            if (settings.LastMorningBriefDate != today && now >= new TimeSpan(7, 30, 0))
            {
                await SendTelegramMorningBriefAsync(settings, today);
                settings = settings with { LastMorningBriefDate = today };
            }
            if (settings.LastEveningDashboardDate != today && now >= new TimeSpan(23, 30, 0))
            {
                await SendTelegramDashboardAsync(settings, today);
                settings = settings with { LastEveningDashboardDate = today };
            }
            if (settings.LastEveningOperationsDate != today && now >= new TimeSpan(21, 0, 0))
            {
                await SendEveningOperationsAsync(settings, today);
                settings = settings with { LastEveningOperationsDate = today };
            }
            if (settings.LastDeliveryConfirmationDate != today && now >= new TimeSpan(22, 0, 0))
            {
                await SendTelegramDraftAsync(settings, today.AddDays(2));
                settings = settings with { LastDeliveryConfirmationDate = today };
            }
            var latestSettings = _telegramBotSettingsStore.Load();
            _telegramBotSettingsStore.Save(latestSettings with
            {
                LastMorningBriefDate = settings.LastMorningBriefDate,
                LastEveningDashboardDate = settings.LastEveningDashboardDate,
                LastEveningOperationsDate = settings.LastEveningOperationsDate,
                LastDeliveryConfirmationDate = settings.LastDeliveryConfirmationDate
            });
        }
        catch
        {
            // The next polling cycle will retry if Telegram or HTS is temporarily unavailable.
        }
        finally { _telegramScheduleInProgress = false; }
    }

    private async Task<DashboardSnapshot> TelegramSnapshotAsync(DateOnly date)
    {
        var snapshot = await App.Services.DataProvider.GetSnapshotAsync(date);
        snapshot = MergeImportedSuppliers(snapshot);
        return await ApplyAvailableFundsAsync(snapshot);
    }

    private async Task SendTelegramDashboardAsync(TelegramBotSettings settings, DateOnly date)
    {
        var snapshot = await TelegramSnapshotAsync(date);
        var funds = _lastFunds ?? FundsForOpening(snapshot.Cash);
        var suppliers = PlannedSuppliersFor(date);
        var plannedPayments = suppliers.Sum(x => x.PaymentAmount + x.OldDebtPayment) +
            snapshot.Payments.Where(x => x.DueDate == date).Sum(x => x.Amount) +
            _requiredPayments.Where(x => RequiredPaymentRules.AppliesOn(x, date)).Sum(x => x.Amount) +
            _manualPaymentChanges.Where(x => x.PlannedDate == date).Sum(x => x.Amount);
        var critical = snapshot.Recommendations.Where(x => x.Severity == Severity.Critical).ToList();

        var message = new System.Text.StringBuilder();
        message.AppendLine($"📊 Գլխավոր էջ — {date:dd.MM.yyyy}");
        message.AppendLine();
        message.AppendLine($"Հասանելի միջոցներ՝ {funds.Total:N0} ֏");
        message.AppendLine($"Կանխիկ՝ {funds.Cash:N0} ֏ · Բանկ՝ {funds.Bank:N0} ֏");
        message.AppendLine($"Այսօրվա վճարումներ՝ {plannedPayments:N0} ֏");
        message.AppendLine($"Այսօրվա վաճառք՝ {snapshot.Sales.SalesAmount:N0} ֏");
        message.AppendLine($"Շահույթ՝ {snapshot.Sales.Profit:N0} ֏");
        message.AppendLine($"Կտրոններ՝ {snapshot.Sales.ReceiptCount:N0} · Միջին չեկ՝ {snapshot.Sales.AverageReceipt:N0} ֏");
        message.AppendLine($"Կրիտիկական ռիսկեր՝ {critical.Count}");
        foreach (var risk in critical.Take(3)) message.AppendLine($"• {risk.Title}");
        message.AppendLine();
        message.AppendLine("Առավոտյան գործողությունների ցանկի համար գրեք՝ առավոտ");
        var dateCode = date.ToString("yyyy-MM-dd");
        var buttons = new IReadOnlyList<TelegramInlineButton>[]
        {
            new[] { new TelegramInlineButton("📊 Գլխավոր", $"dashboard:{dateCode}"), new TelegramInlineButton("🌅 Առավոտ", $"morning:{dateCode}") },
            new[] { new TelegramInlineButton("💳 Վճարումներ", $"payments:{dateCode}") }
        };
        await TelegramBotClient.SendMessageAsync(settings, message.ToString(), buttons);
    }

    private async Task SendTelegramMorningBriefAsync(TelegramBotSettings settings, DateOnly date)
    {
        var snapshot = await TelegramSnapshotAsync(date);
        var suppliers = PlannedSuppliersFor(date).OrderBy(x => x.Supplier).ToList();
        var otherPayments = new List<(string Name, decimal Amount, string Note)>();
        otherPayments.AddRange(snapshot.Payments.Where(x => x.DueDate == date)
            .Select(x => (Name: x.Supplier, Amount: x.Amount, Note: x.Reason)));
        otherPayments.AddRange(_requiredPayments.Where(x => RequiredPaymentRules.AppliesOn(x, date))
            .Select(x => (Name: x.Name, Amount: x.Amount, Note: x.Note)));
        otherPayments.AddRange(_manualPaymentChanges.Where(x => x.PlannedDate == date)
            .Select(x => (Name: x.Supplier, Amount: x.Amount, Note: x.Reason)));

        var message = new System.Text.StringBuilder();
        message.AppendLine($"🌅 Առավոտյան պլան — {date:dd.MM.yyyy}");
        message.AppendLine();
        message.AppendLine("```");
        message.AppendLine("Մատակարար      | Պատվ.  | Վճար.  | Հին");
        message.AppendLine("----------------|---------|---------|--------");
        foreach (var row in suppliers)
        {
            var name = TelegramTableSupplierName(row.Supplier);
            message.AppendLine($"{name,-15} | {TelegramTableAmount(row.OrderAmount),7} | {TelegramTableAmount(row.PaymentAmount),7} | {TelegramTableAmount(row.OldDebtPayment),6}");
        }
        message.AppendLine("```");
        message.AppendLine($"Մատակարարներին վճարում՝ {suppliers.Sum(x => x.PaymentAmount + x.OldDebtPayment):N0} ֏");
        message.AppendLine();
        message.AppendLine("Այլ վճարումներ");
        if (otherPayments.Count == 0) message.AppendLine("• Այսօր այլ պլանավորված վճարում չկա");
        foreach (var payment in otherPayments)
            message.AppendLine($"• {payment.Name}: {payment.Amount:N0} ֏{(string.IsNullOrWhiteSpace(payment.Note) ? string.Empty : $" — {payment.Note}")}");
        message.AppendLine($"Այլ վճարումների հանրագումար՝ {otherPayments.Sum(x => x.Amount):N0} ֏");
        var dateCode = date.ToString("yyyy-MM-dd");
        var buttons = new IReadOnlyList<TelegramInlineButton>[]
        {
            new[] { new TelegramInlineButton("📊 Գլխավոր", $"dashboard:{dateCode}"), new TelegramInlineButton("🌅 Առավոտ", $"morning:{dateCode}") },
            new[] { new TelegramInlineButton("💳 Վճարումներ", $"payments:{dateCode}") }
        };
        await TelegramBotClient.SendMessageAsync(settings, message.ToString(), buttons);
    }

    private async Task SendTelegramPaymentsAsync(TelegramBotSettings settings, DateOnly date)
    {
        var snapshot = await TelegramSnapshotAsync(date);
        var supplierPayments = PlannedSuppliersFor(date)
            .Where(x => x.PaymentAmount != 0m || x.OldDebtPayment != 0m)
            .OrderBy(x => x.Supplier).ToList();
        var otherPayments = new List<(string Category, string Name, decimal Amount, string Note)>();
        otherPayments.AddRange(snapshot.Payments.Where(x => x.DueDate == date)
            .Select(x => (Category: "Այլ", Name: x.Supplier, Amount: x.Amount, Note: x.Reason)));
        otherPayments.AddRange(_requiredPayments.Where(x => RequiredPaymentRules.AppliesOn(x, date))
            .Select(x => (Category: x.Category, Name: x.Name, Amount: x.Amount, Note: x.Note)));
        otherPayments.AddRange(_manualPaymentChanges.Where(x => x.PlannedDate == date)
            .Select(x => (Category: "Ձեռքով", Name: x.Supplier, Amount: x.Amount, Note: x.Reason)));

        var message = new System.Text.StringBuilder();
        message.AppendLine($"💳 Վճարումներ — {date:dd.MM.yyyy}");
        message.AppendLine();
        message.AppendLine("Մատակարարների վճարումներ");
        if (supplierPayments.Count == 0) message.AppendLine("• Այսօր մատակարարին պլանավորված վճարում չկա");
        foreach (var payment in supplierPayments)
            message.AppendLine($"• {payment.Supplier}: նոր՝ {payment.PaymentAmount:N0} ֏ · հին՝ {payment.OldDebtPayment:N0} ֏");
        message.AppendLine($"Մատակարարներին՝ {supplierPayments.Sum(x => x.PaymentAmount + x.OldDebtPayment):N0} ֏");
        message.AppendLine();
        message.AppendLine("Այլ վճարումներ");
        if (otherPayments.Count == 0) message.AppendLine("• Այսօր այլ պլանավորված վճարում չկա");
        foreach (var payment in otherPayments)
            message.AppendLine($"• {payment.Category} · {payment.Name}: {payment.Amount:N0} ֏{(string.IsNullOrWhiteSpace(payment.Note) ? string.Empty : $" — {payment.Note}")}");
        message.AppendLine($"Այլ վճարումներ՝ {otherPayments.Sum(x => x.Amount):N0} ֏");
        message.AppendLine($"Ընդհանուր պլանավորված վճարում՝ {(supplierPayments.Sum(x => x.PaymentAmount + x.OldDebtPayment) + otherPayments.Sum(x => x.Amount)):N0} ֏");
        var dateCode = date.ToString("yyyy-MM-dd");
        var buttons = new IReadOnlyList<TelegramInlineButton>[]
        {
            new[] { new TelegramInlineButton("📊 Գլխավոր", $"dashboard:{dateCode}"), new TelegramInlineButton("🌅 Առավոտ", $"morning:{dateCode}") },
            new[] { new TelegramInlineButton("💳 Վճարումներ", $"payments:{dateCode}") }
        };
        await TelegramBotClient.SendMessageAsync(settings, message.ToString(), buttons);
    }

    private async Task SendTelegramDraftAsync(TelegramBotSettings settings, DateOnly? requestedDate = null)
    {
        var planningDate = DateOnly.FromDateTime(DateTime.Today);
        var deliveryDate = requestedDate ?? planningDate.AddDays(2);
        var scheduled = PlannedSuppliersFor(deliveryDate);
        var supplierNames = scheduled.Select(x => x.Supplier).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var coverageDays = supplierNames.ToDictionary(x => x, x => DaysUntilNextDelivery(x, deliveryDate), StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<PurchaseProposal> proposals = [];
        if (App.Services.DataProvider is IPurchasePlanningProvider provider)
        {
            try { proposals = await provider.GetPurchaseProposalsAsync(planningDate, deliveryDate, supplierNames, coverageDays); }
            catch { /* Send the supplier schedule even if the optional stock report is unavailable. */ }
        }

        var supplierPayments = scheduled.Sum(x => x.PaymentAmount + x.OldDebtPayment);
        var otherPayments = _requiredPayments.Where(x => RequiredPaymentRules.AppliesOn(x, deliveryDate)).Sum(x => x.Amount) +
            _manualPaymentChanges.Where(x => x.PlannedDate == deliveryDate).Sum(x => x.Amount);
        var message = new System.Text.StringBuilder();
        message.AppendLine($"📋 Նախնական պլան — {deliveryDate:dd.MM.yyyy}");
        message.AppendLine($"Մատակարարներ՝ {supplierNames.Count}");
        var supplierOrderAmounts = supplierNames.Select(name => new
        {
            Supplier = name,
            Amount = scheduled.FirstOrDefault(x => SupplierNamesMatch(x.Supplier, name))?.OrderAmount is var manual && manual > 0m
                ? manual
                : proposals.Where(x => SupplierNamesMatch(x.Supplier, name)).Sum(x => x.Amount) is var calculated && calculated > 0m
                ? calculated
                : HistoricalSuggestedOrderAmount(name, deliveryDate)
        }).ToList();
        message.AppendLine();
        message.AppendLine("```");
        message.AppendLine("Մատակարար      | Պատվ.  | Վճար.  | Հին");
        message.AppendLine("----------------|---------|---------|--------");
        foreach (var supplier in supplierOrderAmounts.OrderBy(x => x.Supplier))
        {
            var planned = scheduled.FirstOrDefault(x => SupplierNamesMatch(x.Supplier, supplier.Supplier));
            var name = TelegramTableSupplierName(supplier.Supplier);
            message.AppendLine($"{name,-15} | {TelegramTableAmount(supplier.Amount),7} | {TelegramTableAmount(planned?.PaymentAmount ?? 0m),7} | {TelegramTableAmount(planned?.OldDebtPayment ?? 0m),6}");
        }
        message.AppendLine("```");
        if (otherPayments > 0m)
            message.AppendLine($"Այլ պարտադիր վճարներ՝ {otherPayments:N0} ֏");
        message.AppendLine($"Ընդհանուր պատվեր՝ {supplierOrderAmounts.Sum(x => x.Amount):N0} ֏");
        message.AppendLine($"Ընդհանուր վճարում՝ {(supplierPayments + otherPayments):N0} ֏");
        message.AppendLine("Գրանցման ձևում կարող եք ավելացնել՝ ամսաթիվ: 30.07.2026");
        var dateCode = deliveryDate.ToString("yyyy-MM-dd");
        var buttons = new IReadOnlyList<TelegramInlineButton>[]
        {
            new[] { new TelegramInlineButton("✏️ Փոփոխել ցանկը", $"batch:{dateCode}") },
            new[] { new TelegramInlineButton("💳 Վճարումներ", $"payments:{dateCode}") },
            new[] { new TelegramInlineButton("🔄 Թարմացնել պլանը", $"show:{dateCode}"), new TelegramInlineButton("✅ Հաստատել", $"finish:{dateCode}") }
        };
        await TelegramBotClient.SendMessageAsync(settings, message.ToString(), buttons);
    }

    private async Task ShowTelegramPlanForDateAsync(TelegramBotSettings settings, string instruction)
    {
        if (!TryTelegramDate(instruction, out var date))
        {
            await TelegramBotClient.SendMessageAsync(settings, "Գրեք ամսաթիվը, օրինակ՝ ցուցադրել 30.07.2026");
            return;
        }
        _telegramBotSettingsStore.Save(_telegramBotSettingsStore.Load() with { EditingDate = date });
        await SendTelegramDraftAsync(settings, date);
    }

    private async Task FinishTelegramPlanAsync(TelegramBotSettings settings, string instruction)
    {
        var date = TryTelegramDate(instruction, out var specified) ? specified : DateOnly.FromDateTime(DateTime.Today).AddDays(2);
        await TelegramBotClient.SendMessageAsync(settings, $"✅ {date:dd.MM.yyyy}-ի պլանի փոփոխությունները պահպանված են։ Ահա վերջնական տարբերակը։");
        await SendTelegramDraftAsync(settings, date);
    }

    private async Task SendTelegramBatchEditFormAsync(TelegramBotSettings settings, DateOnly date)
    {
        _telegramBotSettingsStore.Save(_telegramBotSettingsStore.Load() with { EditingDate = date });
        var rows = PlannedSuppliersFor(date).OrderBy(x => x.Supplier).ToList();
        var form = new System.Text.StringBuilder();
        form.AppendLine($"✏️ Փոփոխել ցանկը — {date:dd.MM.yyyy}");
        form.AppendLine("Յուրաքանչյուր տող՝ Մատակարար պատվեր/նոր վճարում/հին պարտքի վճարում");
        form.AppendLine("Թվերը փոխեք և ամբողջ ցանկը ուղարկեք բոտին։");
        form.AppendLine("Հեռացնելու համար գրեք՝ Մատակարար - հեռացնել");
        form.AppendLine();
        form.AppendLine($"ավելացնել {date:dd.MM.yyyy}");
        foreach (var row in rows)
            form.AppendLine($"{row.Supplier} {row.OrderAmount:0}/{row.PaymentAmount:0}/{row.OldDebtPayment:0}");
        await TelegramBotClient.SendMessageAsync(settings, form.ToString());
    }

    private async Task<bool> TryRegisterTelegramBatchAsync(TelegramBotSettings settings, string message)
    {
        var defaultDate = _telegramBotSettingsStore.Load().EditingDate ?? DateOnly.FromDateTime(DateTime.Today).AddDays(2);
        var currentDate = defaultDate;
        var deliveryDate = defaultDate;
        var changedRows = new List<(DateOnly Date, string Supplier, decimal Order, decimal Payment, decimal OldDebtPayment)>();
        var removedSuppliers = new List<(DateOnly Date, string Supplier)>();
        foreach (var line in message.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryParseTelegramBatchDateLine(line, out var specifiedDate))
                currentDate = specifiedDate;
            else if (TryParseTelegramRemovalLine(line, out var supplierToRemove))
                removedSuppliers.Add((currentDate, supplierToRemove));
            else if (TryParseTelegramBatchLine(line, out var parsed))
                changedRows.Add((currentDate, parsed.Supplier, parsed.Order, parsed.Payment, parsed.OldDebtPayment));
        }
        if (changedRows.Count == 0 && removedSuppliers.Count == 0) return false;

        foreach (var item in removedSuppliers)
        {
            var supplier = FindTelegramSupplier(item.Supplier, item.Date) ?? item.Supplier;
            _supplierScheduleOverrides.RemoveAll(x => x.Date == item.Date && SupplierNamesMatch(x.Supplier, supplier));
            _supplierScheduleOverrides.Add(new SupplierDateScheduleOverride(item.Date, supplier, false, "Telegram-ով հեռացված"));
            _supplierWeekRows.RemoveAll(x => x.Date == item.Date && SupplierNamesMatch(x.Supplier, supplier));
        }
        foreach (var item in changedRows)
        {
            var supplier = FindTelegramSupplier(item.Supplier, item.Date) ?? item.Supplier;
            if (!PlannedSuppliersFor(item.Date).Any(x => SupplierNamesMatch(x.Supplier, supplier)))
            {
                _supplierScheduleOverrides.RemoveAll(x => x.Date == item.Date && SupplierNamesMatch(x.Supplier, supplier));
                _supplierScheduleOverrides.Add(new SupplierDateScheduleOverride(item.Date, supplier, true, "Telegram խմբային փոփոխություն"));
            }

            var openingDebt = DebtBeforeDate(supplier, item.Date);
            var closingDebt = Math.Max(0m, openingDebt + item.Order - item.Payment - item.OldDebtPayment);
            var index = _supplierWeekRows.FindIndex(x => x.Date == item.Date && SupplierNamesMatch(x.Supplier, supplier));
            var row = new SupplierWeekPlanRow(item.Date, supplier, item.Order, item.Payment, item.OldDebtPayment, closingDebt);
            if (index < 0) _supplierWeekRows.Add(row); else _supplierWeekRows[index] = row;
        }
        _supplierScheduleOverrideStore.Save(_supplierScheduleOverrides);
        _supplierWeekPlanStore.Save(_supplierWeekRows);
        var affectedDates = changedRows.Select(x => x.Date).Concat(removedSuppliers.Select(x => x.Date)).Distinct().Order().ToList();
        foreach (var date in affectedDates.Where(x => x != deliveryDate))
            await SendTelegramDraftAsync(settings, date);
        deliveryDate = affectedDates.LastOrDefault(deliveryDate);
        await TelegramBotClient.SendMessageAsync(settings, $"✅ {deliveryDate:dd.MM.yyyy}-ի փոփոխությունները ընդունվել են։ Ստուգեք նոր գրաֆիկը և սեղմեք «Հաստատել» կամ կրկին «Փոփոխել ցանկը»։");
        await SendTelegramDraftAsync(settings, deliveryDate);
        return true;
    }

    private static bool TryParseTelegramBatchDateLine(string line, out DateOnly date)
    {
        date = default;
        var value = line.Trim();
        if (value.StartsWith("ավելացնել", StringComparison.OrdinalIgnoreCase))
            value = value["ավելացնել".Length..].Trim();
        var isDateOnly = System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{1,2}[,./-]\d{1,2}(?:[,./-]\d{2,4})?$");
        return isDateOnly && TryTelegramDate(value, out date);
    }

    private static bool TryParseTelegramRemovalLine(string line, out string supplier)
    {
        supplier = string.Empty;
        var marker = "- հեռացնել";
        if (!line.Trim().EndsWith(marker, StringComparison.OrdinalIgnoreCase)) return false;
        supplier = line.Trim()[..^marker.Length].Trim();
        return !string.IsNullOrWhiteSpace(supplier);
    }

    private static bool TryParseTelegramBatchLine(string line, out (string Supplier, decimal Order, decimal Payment, decimal OldDebtPayment) row)
    {
        row = default;
        var parts = line.Split('/');
        if (parts.Length != 3) return false;
        var first = parts[0].Trim();
        var lastSpace = first.LastIndexOf(' ');
        if (lastSpace <= 0) return false;
        var supplier = first[..lastSpace].Trim();
        var order = TelegramAmount(first[(lastSpace + 1)..]);
        if (string.IsNullOrWhiteSpace(supplier)) return false;
        row = (supplier, order, TelegramAmount(parts[1]), TelegramAmount(parts[2]));
        return true;
    }

    private async Task RegisterTelegramSupplierPlanAsync(TelegramBotSettings settings, string message)
    {
        string? supplier = null;
        DateOnly? specifiedDate = null;
        decimal order = 0m, payment = 0m;
        var isOldDebtPayment = false;
        foreach (var line in message.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var key = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();
            if (key.Contains("մատակարար")) supplier = value;
            else if (key.Contains("ամսաթիվ") && TryTelegramDate(value, out var parsedDate)) specifiedDate = parsedDate;
            else if (key.Contains("պատվեր")) order = TelegramAmount(value);
            else if (key.Contains("վճարում")) payment = TelegramAmount(value);
            else if (key.Contains("տեսակ")) isOldDebtPayment = value.Contains("հին", StringComparison.OrdinalIgnoreCase);
        }

        if (string.IsNullOrWhiteSpace(supplier))
        {
            await TelegramBotClient.SendMessageAsync(settings, "Գրեք այս ձևով՝\nգրանցել\nմատակարար: Չինար\nպատվեր: 5000\nվճարում: 2000\nտեսակ: հին");
            return;
        }

        var deliveryDate = specifiedDate ?? DateOnly.FromDateTime(DateTime.Today).AddDays(2);
        var planned = PlannedSuppliersFor(deliveryDate);
        var knownSupplier = FindTelegramSupplier(supplier, deliveryDate) ?? supplier.Trim();
        if (!planned.Any(x => SupplierNamesMatch(x.Supplier, knownSupplier)))
        {
            _supplierScheduleOverrides.RemoveAll(x => x.Date == deliveryDate && SupplierNamesMatch(x.Supplier, knownSupplier));
            _supplierScheduleOverrides.Add(new SupplierDateScheduleOverride(deliveryDate, knownSupplier, true, "Telegram գրանցում"));
            _supplierScheduleOverrideStore.Save(_supplierScheduleOverrides);
        }

        var index = _supplierWeekRows.FindIndex(x => x.Date == deliveryDate && SupplierNamesMatch(x.Supplier, knownSupplier));
        var openingDebt = index >= 0 && _supplierWeekRows[index].Debt != 0m
            ? _supplierWeekRows[index].Debt : DebtBeforeDate(knownSupplier, deliveryDate);
        var paymentForOrder = isOldDebtPayment ? 0m : payment;
        var oldDebtPayment = isOldDebtPayment ? payment : 0m;
        var closingDebt = Math.Max(0m, openingDebt + order - paymentForOrder - oldDebtPayment);
        var row = new SupplierWeekPlanRow(deliveryDate, knownSupplier, order, paymentForOrder, oldDebtPayment, closingDebt);
        if (index < 0) _supplierWeekRows.Add(row); else _supplierWeekRows[index] = row;
        _supplierWeekPlanStore.Save(_supplierWeekRows);

        var kind = isOldDebtPayment ? "հին պարտքի վճարում" : "նոր պատվերի վճարում";
        await TelegramBotClient.SendMessageAsync(settings, $"✅ Գրանցվեց՝ {knownSupplier}\nՊատվեր՝ {order:N0} ֏\nՎճարում՝ {payment:N0} ֏ ({kind})\nՊարտք՝ {closingDebt:N0} ֏");
        await SendTelegramDraftAsync(settings);
    }

    private static decimal TelegramAmount(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return decimal.TryParse(digits, out var amount) ? amount : 0m;
    }

    private static string TelegramTableSupplierName(string value) =>
        value.Length > 15 ? value[..14] + "…" : value;

    private static string TelegramTableAmount(decimal? amount) => amount.GetValueOrDefault().ToString("N0");

    private static bool TryTelegramDate(string text, out DateOnly date)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, @"\d{1,2}[,./-]\d{1,2}(?:[,./-]\d{2,4})?");
        if (!match.Success) { date = default; return false; }
        var parts = match.Value.Replace('/', '.').Replace('-', '.').Replace(',', '.').Split('.');
        if (parts.Length < 2 || !int.TryParse(parts[0], out var day) || !int.TryParse(parts[1], out var month)) { date = default; return false; }
        var year = DateTime.Today.Year;
        if (parts.Length == 3 && int.TryParse(parts[2], out var parsedYear)) year = parsedYear < 100 ? 2000 + parsedYear : parsedYear;
        try { date = new DateOnly(year, month, day); return true; }
        catch (ArgumentOutOfRangeException) { date = default; return false; }
    }

    private decimal HistoricalSuggestedOrderAmount(string supplier, DateOnly deliveryDate)
    {
        var pattern = _deliveryPatterns
            .Where(x => x.Weekday == deliveryDate.DayOfWeek && SupplierNamesMatch(x.Supplier, supplier))
            .OrderByDescending(x => x.DeliveryCount)
            .FirstOrDefault();
        return pattern?.SuggestedOrderAmount ?? 0m;
    }

    private async Task ChangeTelegramOrderAndSendDraftAsync(TelegramBotSettings settings, string instruction)
    {
        var numbers = System.Text.RegularExpressions.Regex.Matches(instruction, @"\d+(?:[.,]\d+)?")
            .Select(x => decimal.TryParse(x.Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0m)
            .Where(x => x >= 0m).ToList();
        var orderMarker = instruction.IndexOf("պատվեր", StringComparison.OrdinalIgnoreCase);
        if (numbers.Count == 0 || orderMarker <= 0)
        {
            await TelegramBotClient.SendMessageAsync(settings, "Գրեք օրինակ՝ Չինար պատվերի առաջարկը 5000 փոխել 2000");
            return;
        }

        var supplierText = instruction[..orderMarker].Trim();
        var deliveryDate = DateOnly.FromDateTime(DateTime.Today).AddDays(2);
        var supplier = FindTelegramSupplier(supplierText, deliveryDate);
        if (supplier is null)
        {
            await TelegramBotClient.SendMessageAsync(settings, $"«{supplierText}» մատակարարը չգտնվեց։ Նախ գրեք՝ ավելացնել {supplierText}");
            return;
        }

        var newAmount = numbers.Last();
        var index = _supplierWeekRows.FindIndex(x => x.Date == deliveryDate && SupplierNamesMatch(x.Supplier, supplier));
        if (index >= 0)
        {
            var row = _supplierWeekRows[index];
            _supplierWeekRows[index] = row with { OrderAmount = newAmount };
        }
        else
        {
            _supplierWeekRows.Add(new SupplierWeekPlanRow(deliveryDate, supplier, newAmount, 0m, 0m, DebtBeforeDate(supplier, deliveryDate)));
        }
        _supplierWeekPlanStore.Save(_supplierWeekRows);
        await TelegramBotClient.SendMessageAsync(settings, $"✅ {supplier} մատակարարի {deliveryDate:dd.MM.yyyy}-ի նախնական պատվերը փոխվեց՝ {newAmount:N0} ֏։");
        await SendTelegramDraftAsync(settings);
    }

    private string? FindTelegramSupplier(string input, DateOnly deliveryDate)
    {
        var candidates = PlannedSuppliersFor(deliveryDate).Select(x => x.Supplier)
            .Concat(SupplierWeekPlanSeed.AllSuppliers()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var requested = TelegramSupplierKey(input);
        return candidates.FirstOrDefault(candidate =>
        {
            var key = TelegramSupplierKey(candidate);
            return key == requested || (Math.Min(key.Length, requested.Length) >= 4 && (key.StartsWith(requested) || requested.StartsWith(key)));
        });
    }

    private static string TelegramSupplierKey(string value) =>
        new string(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()).TrimEnd('ի', 'ն', 'ը');

    private async Task AddTelegramSupplierAndSendDraftAsync(TelegramBotSettings settings, string supplier)
    {
        if (string.IsNullOrWhiteSpace(supplier))
        {
            await TelegramBotClient.SendMessageAsync(settings, "Գրեք մատակարարի անունը, օրինակ՝ ավելացնել Նոր մատակարար");
            return;
        }

        var deliveryDate = DateOnly.FromDateTime(DateTime.Today).AddDays(2);
        var existing = PlannedSuppliersFor(deliveryDate);
        if (!existing.Any(x => SupplierNamesMatch(x.Supplier, supplier)))
        {
            _supplierScheduleOverrides.RemoveAll(x => x.Date == deliveryDate && SupplierNamesMatch(x.Supplier, supplier));
            _supplierScheduleOverrides.Add(new SupplierDateScheduleOverride(deliveryDate, supplier, true, "Telegram-ով ավելացված"));
            _supplierScheduleOverrideStore.Save(_supplierScheduleOverrides);
            await TelegramBotClient.SendMessageAsync(settings, $"✅ «{supplier}»-ը ավելացվեց {deliveryDate:dd.MM.yyyy}-ի մատակարարների ցանկում։");
        }
        else
        {
            await TelegramBotClient.SendMessageAsync(settings, $"ℹ️ «{supplier}»-ն արդեն կա {deliveryDate:dd.MM.yyyy}-ի ցանկում։");
        }
        await SendTelegramDraftAsync(settings);
    }

    private async Task LoadAsync(string page)
    {
        _currentPage = page;
        UpdateTopActions(page);
        try
        {
            _snapshot ??= await App.Services.DataProvider.GetSnapshotAsync(_selectedDate);
        }
        catch (Exception exception)
        {
            // An unavailable report or missing API permission must never close the desktop app.
            // Keep the user working and make the cause visible instead.
            App.Services.UseDataProvider(new DemoDataProvider());
            DataSourceStatusText.Text = "Տվյալների աղբյուր՝ Demo (ՀԾ հարցման խնդիր)";
            _snapshot = await App.Services.DataProvider.GetSnapshotAsync(_selectedDate);
            MessageBox.Show(
                $"ՀԾ-ից ընտրված օրվա տվյալները չհաջողվեց բեռնել։\n\n{exception.Message}\n\nԾրագիրը բաց է մնացել փորձնական տվյալներով։ Ստուգեք ՀԾ API-ի հաշվետվությունների իրավասությունները, ապա սեղմեք «⚙ ՀԾ API» և նորից պահպանեք կարգավորումը։",
                "ՀԾ API տվյալների բեռնում", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        _snapshot = await ApplyAvailableFundsAsync(MergeImportedSuppliers(_snapshot));
        MergeApiSupplierMovements(_snapshot);
        SubtitleText.Text = $"{_snapshot.Date:dd.MM.yyyy} · Որոշումները պահանջում են ձեր հաստատումը";
        PageHost.Content = page switch
        {
            "Finance" => Views.Finance(_snapshot),
            "Suppliers" => Views.Suppliers(PlanForSelectedDate(), _snapshot.Suppliers, _partnerDebts, _employeeSupplierActionStore.Load(), SaveSupplierWeekRow, ShowSupplierEmployeeStatus),
            "PurchasePlan" => await PurchasePlanViewAsync(),
            "SupplierSales" => await SupplierSalesViewAsync(),
            "Payments" => Views.Payments(_snapshot, _completedPayments, _requiredPayments, PlanForSelectedDate(), _employeeSupplierActionStore.Load(), EditRequiredPayment, DeleteRequiredPayment),
            "Approvals" => Views.Approvals(_pendingEmployeeOrderChangeStore.Load(), ApprovePendingChangeFromDesktopAsync, RejectPendingChangeFromDesktopAsync, ApproveAllPendingChangesFromDesktopAsync),
            "DeliverySchedule" => Views.DeliverySchedule(_deliveryPatterns, UpdateSuggestedOrderAmount),
            "Recommendations" => Views.Recommendations(_snapshot, _employeeSupplierActionStore.Load(), _employeeTaskStore.Load(), _employeeTaskActionStore.Load(), _employeeIssueStore.Load()),
            "Summary" => await SummaryViewAsync(),
            _ => Views.Dashboard(_snapshot, _completedPayments, _requiredPayments, PlanForSelectedDate(), _employeeSupplierActionStore.Load(), CashSummaryForSelectedDate(), _lastFunds ?? FundsForOpening(_snapshot.Cash), _pendingEmployeeOrderChangeStore.Load().Count, OpenAvailableFunds, () => _ = LoadAsync("Payments"), () => _ = LoadAsync("Recommendations"), () => _ = LoadAsync("Approvals"))
        };
    }

    private async void ApprovePendingChangeFromDesktopAsync(Guid id)
    {
        var settings = _telegramBotSettingsStore.Load();
        await ApproveEmployeeOrderChangeAsync(settings, id, announceToOwner: false);
        await LoadAsync("Approvals");
    }

    private async void RejectPendingChangeFromDesktopAsync(Guid id)
    {
        var settings = _telegramBotSettingsStore.Load();
        await RejectEmployeeOrderChangeAsync(settings, id);
        await LoadAsync("Approvals");
    }

    private async void ApproveAllPendingChangesFromDesktopAsync()
    {
        var changes = _pendingEmployeeOrderChangeStore.Load();
        if (changes.Count == 0) return;
        if (MessageBox.Show($"Հաստատե՞լ բոլոր {changes.Count} փոփոխությունները։", "Հաստատումներ", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var settings = _telegramBotSettingsStore.Load();
        foreach (var change in changes) await ApproveEmployeeOrderChangeAsync(settings, change.Id, announceToOwner: false);
        await LoadAsync("Approvals");
    }

    private void UpdateTopActions(string page)
    {
        var dateVisible = page is "Dashboard" or "Finance" or "Suppliers" or "PurchasePlan" or "SupplierSales" or "Payments" or "Recommendations" or "Summary";
        DateLabel.Visibility = dateVisible ? Visibility.Visible : Visibility.Collapsed;
        ViewDatePicker.Visibility = dateVisible ? Visibility.Visible : Visibility.Collapsed;
        ShowDateButton.Visibility = dateVisible ? Visibility.Visible : Visibility.Collapsed;

        ApiButton.Visibility = page == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        TelegramButton.Visibility = page == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        EmployeeTelegramButton.Visibility = page == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        CompletedPaymentButton.Visibility = page is "Dashboard" or "Payments" ? Visibility.Visible : Visibility.Collapsed;
        RequiredPaymentButton.Visibility = page == "Payments" ? Visibility.Visible : Visibility.Collapsed;
        SupplierDayButton.Visibility = page == "Suppliers" ? Visibility.Visible : Visibility.Collapsed;
        SupplierMembershipButton.Visibility = page == "Suppliers" ? Visibility.Visible : Visibility.Collapsed;
        PaymentChangeButton.Visibility = page == "Payments" ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.Visibility = Visibility.Visible;
        CashImportButton.Visibility = Visibility.Collapsed;
        CashAdjustmentButton.Visibility = page is "Dashboard" or "Finance" ? Visibility.Visible : Visibility.Collapsed;
    }

    private enum SummaryPeriod { Month, Week, Day }

    private AvailableFundsBreakdown FundsForOpening(CashPosition fallback) => new(
        CashVault: _availableFunds.CashVault ?? 0m,
        CashDesk: _availableFunds.CashDesk ?? fallback.Cash,
        BankReport: _availableFunds.BankReport ?? fallback.Bank,
        AmeriabankPos099: _availableFunds.AmeriabankPos099 ?? 0m,
        Idram: _availableFunds.Idram ?? 0m);

    private async Task<DashboardSnapshot> ApplyAvailableFundsAsync(DashboardSnapshot source)
    {
        var funds = FundsForOpening(source.Cash);
        var start = new DateOnly(source.Date.Year, source.Date.Month, 1);
        var isConfiguredForMonth = _availableFunds.OpeningMonth is { } opening &&
            opening.Year == source.Date.Year && opening.Month == source.Date.Month;
        if (isConfiguredForMonth)
        {
            var cashDeskBalance = CashDeskBalance("0001", funds.CashDesk, start, source.Date);
            var vaultBalance = CashDeskBalance("0002", funds.CashVault, start, source.Date);
            BankSalesBreakdown bankMovement = BankSalesBreakdown.Empty;
            if (App.Services.DataProvider is IFundsMovementProvider provider)
            {
                try { bankMovement = await provider.GetNonCashSalesAsync(start, source.Date); }
                catch { /* The dashboard stays usable if the optional ECR report is not enabled. */ }
            }
            funds = funds with
            {
                CashDesk = cashDeskBalance,
                CashVault = vaultBalance,
                BankReport = funds.BankReport + bankMovement.BankReport,
                AmeriabankPos099 = funds.AmeriabankPos099 + bankMovement.AmeriabankPos099,
                Idram = funds.Idram + bankMovement.Idram
            };
        }
        _lastFunds = funds;
        return new DashboardSnapshot
        {
            Date = source.Date,
            Cash = new CashPosition(funds.Cash, funds.Bank),
            Suppliers = source.Suppliers,
            SupplierMovements = source.SupplierMovements,
            Payments = source.Payments,
            Forecast = source.Forecast,
            Sales = source.Sales,
            Recommendations = source.Recommendations,
            Tasks = source.Tasks
        };
    }

    private decimal CashDeskBalance(string cashDesk, decimal openingBalance, DateOnly start, DateOnly end)
    {
        var correction = _cashDeskAdjustments
            .Where(x => x.CashDesk == cashDesk && x.Date >= start && x.Date <= end)
            .OrderByDescending(x => x.Date)
            .FirstOrDefault();
        var balance = correction?.ClosingBalance ?? openingBalance;
        var movementStart = correction is null ? start : correction.Date.AddDays(1);
        foreach (var movement in CashDocumentImportService.CashDeskMovements(_cashDocuments)
                     .Where(x => x.Date >= movementStart && x.Date <= end))
        {
            if (movement.SourceCashDesk == cashDesk) balance -= movement.Amount;
            if (movement.TargetCashDesk == cashDesk) balance += movement.Amount;
        }
        return balance;
    }

    private async void OpenAvailableFunds()
    {
        if (_snapshot is null) return;
        // The editor always shows the fixed opening figures, not the calculated
        // balance of the currently selected day.
        var window = new AvailableFundsWindow(FundsForOpening(_snapshot.Cash), _selectedDate) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;
        _availableFunds = window.Result;
        _availableFundsStore.Save(_availableFunds);
        _snapshot = await ApplyAvailableFundsAsync(_snapshot);
        await LoadAsync("Dashboard");
    }

    private async void AddCashDeskAdjustment_Click(object sender, RoutedEventArgs e)
    {
        var window = new CashDeskAdjustmentWindow(_selectedDate) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;
        _cashDeskAdjustments.RemoveAll(x => x.Date == window.Result.Date && x.CashDesk == window.Result.CashDesk);
        _cashDeskAdjustments.Add(window.Result);
        _cashDeskAdjustmentStore.Save(_cashDeskAdjustments);
        _snapshot = null;
        await LoadAsync("Dashboard");
    }

    private async Task<UIElement> SummaryViewAsync()
    {
        var (start, end, label) = SummaryRange();
        BusinessSummary summary;
        if (App.Services.DataProvider is IBusinessSummaryProvider provider)
        {
            summary = await provider.GetBusinessSummaryAsync(start, end);
        }
        else
        {
            var snapshot = _snapshot ?? await App.Services.DataProvider.GetSnapshotAsync(_selectedDate);
            summary = new BusinessSummary(start, end, snapshot.Sales, [], [], 0m, 0m);
        }

        var paid = _completedPayments.Where(x => x.PaidDate >= start && x.PaidDate <= end).Sum(x => x.Amount);
        var supplied = summary.SuppliedAmount;
        summary = summary with { SupplierPayments = paid, DebtChange = supplied - paid };
        return Views.Summary(summary, label,
            () => SetSummaryPeriod(SummaryPeriod.Month),
            () => SetSummaryPeriod(SummaryPeriod.Week),
            () => SetSummaryPeriod(SummaryPeriod.Day));
    }

    private async Task<UIElement> PurchasePlanViewAsync()
    {
        var deliveryDate = _selectedDate.AddDays(1);
        var scheduled = PlannedSuppliersFor(deliveryDate);
        var coverageDays = scheduled.Select(x => x.Supplier).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x, x => DaysUntilNextDelivery(x, deliveryDate), StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<PurchaseProposal> proposals = [];
        if (App.Services.DataProvider is IPurchasePlanningProvider provider)
        {
            try
            {
                proposals = await provider.GetPurchaseProposalsAsync(_selectedDate, deliveryDate,
                    scheduled.Select(x => x.Supplier).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), coverageDays);
            }
            catch (Exception exception)
            {
                MessageBox.Show($"Չհաջողվեց ստանալ պահեստի մնացորդները։\n{exception.Message}", "Վաղվա պատվերներ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        return Views.PurchasePlan(_selectedDate, deliveryDate, scheduled, proposals);
    }

    private int DaysUntilNextDelivery(string supplier, DateOnly deliveryDate)
    {
        for (var days = 1; days <= 21; days++)
        {
            if (PlannedSuppliersFor(deliveryDate.AddDays(days))
                .Any(x => string.Equals(x.Supplier, supplier, StringComparison.OrdinalIgnoreCase)))
                return days;
        }
        return 7;
    }

    private async Task<UIElement> SupplierSalesViewAsync()
    {
        var start = StartOfWeek(_selectedDate);
        var end = _selectedDate;
        IReadOnlyList<SupplierSalesAnalysis> rows = [];
        if (App.Services.DataProvider is ISupplierSalesAnalysisProvider provider)
        {
            try { rows = await provider.GetSupplierSalesAnalysisAsync(start, end); }
            catch (Exception exception)
            {
                MessageBox.Show($"Չհաջողվեց ստանալ վաճառքի վերլուծությունը։\n{exception.Message}",
                    "Վաճառք՝ մատակարարներով", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        return Views.SupplierSalesAnalysis(start, end, rows);
    }

    private IReadOnlyList<SupplierWeekPlanRow> PlannedSuppliersFor(DateOnly date)
    {
        // Reuse the exact same weekly rules, one-time changes and recurring
        // changes used in the Suppliers screen without altering the selected UI date.
        var original = _selectedDate;
        try
        {
            _selectedDate = date;
            return PlanForSelectedDate();
        }
        finally { _selectedDate = original; }
    }

    private (DateOnly Start, DateOnly End, string Label) SummaryRange()
    {
        return _summaryPeriod switch
        {
            SummaryPeriod.Day => (_selectedDate, _selectedDate, _selectedDate.ToString("dd.MM.yyyy")),
            SummaryPeriod.Week => (StartOfWeek(_selectedDate), StartOfWeek(_selectedDate).AddDays(6), $"{StartOfWeek(_selectedDate):dd.MM.yyyy}–{StartOfWeek(_selectedDate).AddDays(6):dd.MM.yyyy}"),
            _ => (new DateOnly(_selectedDate.Year, _selectedDate.Month, 1), _selectedDate, $"{new DateOnly(_selectedDate.Year, _selectedDate.Month, 1):dd.MM.yyyy}–{_selectedDate:dd.MM.yyyy}")
        };
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    private void SetSummaryPeriod(SummaryPeriod period)
    {
        _summaryPeriod = period;
        _ = LoadAsync("Summary");
    }

    private async void Navigate_Click(object sender, RoutedEventArgs e) => await LoadAsync((string)((Button)sender).Tag);
    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _snapshot = await App.Services.DataProvider.GetSnapshotAsync(_selectedDate);
        _snapshot = await ApplyAvailableFundsAsync(MergeImportedSuppliers(_snapshot));
        MergeApiSupplierMovements(_snapshot);
        foreach (var change in _manualPaymentChanges) _snapshot = ApplyPaymentChange(_snapshot, change);
        await LoadAsync(_currentPage);
    }

    private async void ShowSelectedDate_Click(object sender, RoutedEventArgs e)
    {
        if (ViewDatePicker.SelectedDate is null) return;
        _selectedDate = DateOnly.FromDateTime(ViewDatePicker.SelectedDate.Value);
        _snapshot = null;
        await LoadAsync(_currentPage);
    }

    private async void ViewDatePicker_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady || ViewDatePicker.SelectedDate is null) return;
        _selectedDate = DateOnly.FromDateTime(ViewDatePicker.SelectedDate.Value);
        _snapshot = null;
        await LoadAsync(_currentPage);
    }

    private async void ConfigureHtsApi_Click(object sender, RoutedEventArgs e)
    {
        var window = new HtsApiSettingsWindow(_htsApiSettingsStore.Load()) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;

        _htsApiSettingsStore.Save(window.Result);
        ConfigureDataProvider();
        _snapshot = null;
        try
        {
            var result = await HtsApiDataProvider.TestConnectionAsync(window.Result);
            MessageBox.Show(result.Message, result.Success ? "ՀԾ API" : "Կապի խնդիր",
                MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Չհաջողվեց ստուգել ՀԾ API կապը։\n{exception.Message}", "Կապի խնդիր", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        await LoadAsync(_currentPage);
    }

    private async void ConfigureTelegram_Click(object sender, RoutedEventArgs e)
    {
        var window = new TelegramBotSettingsWindow(_telegramBotSettingsStore.Load()) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;

        try
        {
            // Keep the token locally even if the owner has not yet pressed /start.
            // The next Telegram click can then simply retry chat discovery.
            _telegramBotSettingsStore.Save(window.Result);
            var chatId = await TelegramBotClient.FindChatIdAsync(window.Result);
            if (string.IsNullOrWhiteSpace(chatId))
            {
                MessageBox.Show("Բոտին Telegram-ում ուղարկեք /start, ապա կրկին սեղմեք «Telegram» կոճակը։ Այս պահին անձնական չաթ չի գտնվել։",
                    "Telegram", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var settings = window.Result with { ChatId = chatId };
            _telegramBotSettingsStore.Save(settings);
            await TelegramBotClient.SendMessageAsync(settings, "✅ Patarik AI OS-ի Telegram կապը հաստատվեց։ Երեկոյան այստեղ կստանաք վաղվա պատվերների և վճարումների նախագիծը։");
            MessageBox.Show("Telegram կապը հաստատվեց։ Փորձնական հաղորդագրությունն ուղարկվել է ձեր բոտին։", "Telegram", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Չհաջողվեց միացնել Telegram-ը։\n{exception.Message}", "Telegram", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ConfigureEmployeeTelegram_Click(object sender, RoutedEventArgs e)
    {
        var window = new EmployeeTelegramBotSettingsWindow(_employeeTelegramBotSettingsStore.Load()) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;
        _employeeTelegramBotSettingsStore.Save(window.Result);
        StartEmployeeTelegramPolling();
        MessageBox.Show($"Աշխատակիցների բոտի կարգավորումները պահպանվել են։\n\nԲոտ՝ {window.Result.BotUsername}\n\nՀաջորդ քայլը՝ յուրաքանչյուր աշխատակից պետք է այս բոտին գրի /start, հետո նրանց կհանձնարարենք դերեր և պատվերներ։",
            "Աշխատակիցների բոտ", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void AddPaymentChange_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot is null) return;
        var window = new PaymentChangeWindow(_snapshot.Suppliers.Select(x => x.Name).ToList()) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;
        var draft = window.Result;
        _manualPaymentChanges.Add(draft);
        _paymentChangeStore.Save(_manualPaymentChanges);
        _snapshot = ApplyPaymentChange(_snapshot, draft);
        await LoadAsync("Suppliers");
    }

    private async void AddCompletedPayment_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot is null) return;
        var names = _snapshot.Suppliers.Select(x => x.Name)
            .Concat(_partnerDebts.Select(x => x.Supplier))
            .Concat(_snapshot.Payments.Select(x => x.Supplier))
            .Distinct().ToList();
        var window = new CompletedPaymentWindow(names, _selectedDate) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;
        _completedPayments.Add(window.Result);
        _completedPaymentStore.Save(_completedPayments);
        ApplyCompletedSupplierPayment(window.Result);
        await LoadAsync("Dashboard");
    }

    private async void AddRequiredPayment_Click(object sender, RoutedEventArgs e)
    {
        var window = new RequiredPaymentWindow(_selectedDate) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;
        _requiredPayments.Add(window.Result);
        _requiredPaymentStore.Save(_requiredPayments);
        await LoadAsync("Payments");
    }

    private async void ImportCashDocument_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Ընտրեք ՀԾ-ից արտահանված օրական դրամարկղային Excel ֆայլը",
            Filter = "Cash exports (*.xlsx;*.xml)|*.xlsx;*.xml|Excel files (*.xlsx)|*.xlsx|XML files (*.xml)|*.xml"
        };
        if (picker.ShowDialog() != true) return;
        try
        {
            var isXml = string.Equals(Path.GetExtension(picker.FileName), ".xml", StringComparison.OrdinalIgnoreCase);
            var imported = isXml ? CashXmlImportService.Import(picker.FileName).ToList() : CashDocumentImportService.Import(picker.FileName).ToList();
            await ProcessCashImportAsync(imported, replaceDay: !isXml);
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Ֆայլը չհաջողվեց ներմուծել։\n{exception.Message}", "Ներմուծման խնդիր", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task ProcessCashImportAsync(List<CashDocumentRecord> imported, bool replaceDay = true)
    {
        if (replaceDay) _cashDocumentStore.ReplaceDays(imported, _cashDocuments);
        else _cashDocumentStore.Upsert(imported, _cashDocuments);
        foreach (var payment in CashDocumentImportService.SupplierPaymentRows(imported))
        {
            if (_completedPayments.Any(x => x.SourceDocument == payment.DocumentNumber && x.PaidDate == payment.Date)) continue;
            var completed = new CompletedPayment(payment.Recipient, payment.Amount, payment.Date,
                $"ՀԾ դրամարկղային փաստաթուղթ {payment.DocumentNumber}", payment.DocumentNumber);
            _completedPayments.Add(completed);
            ApplyCompletedSupplierPayment(completed);
        }
        _completedPaymentStore.Save(_completedPayments);

        _selectedDate = imported.Max(x => x.Date);
        ViewDatePicker.SelectedDate = _selectedDate.ToDateTime(TimeOnly.MinValue);
        _snapshot = null;
        await LoadAsync("Dashboard");
        var summary = CashDocumentImportService.Summary(imported, _selectedDate);
        MessageBox.Show($"Ներմուծված է {summary.Date:dd.MM.yyyy}-ի {summary.DocumentCount} փաստաթուղթ։\n\nՎաճառք՝ {summary.Sales:N0} դրամ\nՄատակարարների վճարումներ՝ {summary.SupplierPayments:N0} դրամ\nԱյլ ծախսեր՝ {summary.OtherExpenses:N0} դրամ\nՊարտքի փոփոխություն՝ {summary.DebtChange:N0} դրամ",
            "Դրամարկղային տվյալները ներմուծված են", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void EditRequiredPayment(RequiredPaymentTemplate payment)
    {
        var window = new RequiredPaymentWindow(_selectedDate, payment) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;
        var index = _requiredPayments.FindIndex(x => x.Id == payment.Id);
        if (index >= 0) _requiredPayments[index] = window.Result;
        _requiredPaymentStore.Save(_requiredPayments);
        await LoadAsync("Payments");
    }

    private async void DeleteRequiredPayment(RequiredPaymentTemplate payment)
    {
        if (MessageBox.Show($"Հեռացնե՞լ «{payment.Name}» պարտադիր վճարումը գրաֆիկից։", "Հաստատում", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _requiredPayments.RemoveAll(x => x.Id == payment.Id);
        _requiredPaymentStore.Save(_requiredPayments);
        await LoadAsync("Payments");
    }

    private async void ImportWarehouse_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Title = "Ընտրեք ՀԾ-ից արտահանված ստացումների Excel ֆայլը", Filter = "Excel files (*.xlsx)|*.xlsx" };
        if (picker.ShowDialog() != true) return;
        try
        {
            var isXml = string.Equals(Path.GetExtension(picker.FileName), ".xml", StringComparison.OrdinalIgnoreCase);
            var records = isXml ? CashXmlImportService.Import(picker.FileName).ToList() : CashDocumentImportService.Import(picker.FileName).ToList();
            await ProcessCashImportAsync(records, replaceDay: !isXml);
            return;
        }
        catch (InvalidOperationException) { }
        try
        {
            _deliveryPatterns = WarehouseImportService.Import(picker.FileName).ToList();
            _deliveryScheduleStore.Save(_deliveryPatterns);
            await LoadAsync("DeliverySchedule");
            MessageBox.Show($"Ներմուծվել է {_deliveryPatterns.Count} մատակարար-օրային կանոն։", "Ներմուծումը հաջող է", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Ֆայլը չհաջողվեց ներմուծել։\n{exception.Message}", "Ներմուծման սխալ", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ChangeDeliveryPlan_Click(object sender, RoutedEventArgs e)
    {
        if (_deliveryPatterns.Count == 0)
        {
            MessageBox.Show("Նախ ներմուծեք ստացումների Excel ֆայլը։", "Տվյալներ չկան", MessageBoxButton.OK, MessageBoxImage.Information); return;
        }
        var window = new DeliveryPlanChangeWindow(_deliveryPatterns) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;
        var draft = window.Result;
        var index = _deliveryPatterns.FindIndex(x => x.Supplier == draft.Supplier && x.Weekday == draft.Weekday);
        if (index < 0)
        {
            _deliveryPatterns.Add(new SupplierDeliveryPattern(draft.Weekday, draft.Supplier, 0, 0m, draft.Amount, draft.Instruction));
        }
        else
        {
            _deliveryPatterns[index] = _deliveryPatterns[index] with { SuggestedOrderAmount = draft.Amount, OwnerInstruction = draft.Instruction };
        }
        _deliveryScheduleStore.Save(_deliveryPatterns);
        await LoadAsync("DeliverySchedule");
    }

    private async void ChangeDeliveryDay_Click(object sender, RoutedEventArgs e)
    {
        if (_deliveryPatterns.Count == 0) { MessageBox.Show("Մատակարարումների տվյալներ չկան։", "Տվյալներ չկան", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var window = new DeliveryDayChangeWindow(_deliveryPatterns) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;
        var change = window.Result;
        var index = _deliveryPatterns.FindIndex(x => x.Supplier == change.Supplier && x.Weekday == change.CurrentDay && !x.IsOneTime);
        if (index < 0) return;
        var current = _deliveryPatterns[index];
        if (change.IsOneTime)
        {
            _deliveryPatterns.Add(current with { Weekday = change.NewDay, DeliveryCount = 0, AverageOrderAmount = 0, OwnerInstruction = $"Միայն այս շաբաթ՝ {change.OneTimeDate:dd.MM.yyyy}. {change.Instruction}", IsOneTime = true });
        }
        else
        {
            _deliveryPatterns[index] = current with { Weekday = change.NewDay, OwnerInstruction = change.Instruction, IsOneTime = false };
        }
        _deliveryScheduleStore.Save(_deliveryPatterns);
        await LoadAsync("DeliverySchedule");
    }

    private void UpdateSuggestedOrderAmount(SupplierDeliveryPattern pattern, decimal amount)
    {
        var index = _deliveryPatterns.FindIndex(x => x.Supplier == pattern.Supplier && x.Weekday == pattern.Weekday && x.IsOneTime == pattern.IsOneTime && x.OwnerInstruction == pattern.OwnerInstruction);
        if (index < 0) return;
        _deliveryPatterns[index] = _deliveryPatterns[index] with { SuggestedOrderAmount = amount };
        _deliveryScheduleStore.Save(_deliveryPatterns);
    }

    private async void SaveSupplierWeekRow(SupplierWeekPlanRow row, decimal order, decimal payment, decimal oldDebtPayment, decimal debt)
    {
        try
        {
        var index = _supplierWeekRows.FindIndex(x => x.Date == row.Date && SupplierNamesMatch(x.Supplier, row.Supplier));
        // The debt field represents the closing balance for the selected day.  Only the change
        // made by the owner is applied, so pressing Save a second time cannot reduce it twice.
        var movementChange = (order - row.OrderAmount) - (payment - row.PaymentAmount) - (oldDebtPayment - row.OldDebtPayment);
        var closingDebt = Math.Max(0m, debt + movementChange);
        var updated = row with { OrderAmount = order, PaymentAmount = payment, OldDebtPayment = oldDebtPayment, Debt = closingDebt };
        if (index < 0) _supplierWeekRows.Add(updated);
        else _supplierWeekRows[index] = updated;
        _supplierWeekPlanStore.Save(_supplierWeekRows);
        await LoadAsync("Suppliers");
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Փոփոխությունը չպահպանվեց։\n{exception.Message}", "Պահպանման խնդիր", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private IReadOnlyList<SupplierWeekPlanRow> PlanForSelectedDate()
    {
        // The initial Monday–Sunday list is a reusable weekly template. This
        // makes every day from 01.07.2026 to 31.12.2026 immediately viewable.
        var inPlanningPeriod = _selectedDate >= new DateOnly(2026, 7, 1) && _selectedDate <= new DateOnly(2026, 12, 31);
        var rows = inPlanningPeriod
            ? SupplierWeekPlanSeed.ForDate(_selectedDate)
            : [];

        // Saved figures (order/payment/debt) are layered over the schedule,
        // so changing a date never loses the financial history for that date.
        foreach (var saved in _supplierWeekRows.Where(x => x.Date == _selectedDate))
        {
            var existing = rows.FindIndex(x => SupplierNamesMatch(x.Supplier, saved.Supplier));
            if (existing >= 0) rows[existing] = saved;
            else rows.Add(saved);
        }

        // A recurring change keeps the historical weekly template intact.
        // From its effective date onward it removes the old weekday and creates
        // the supplier on the new weekday.  One-time moves remain only in their
        // exact calendar-date row above.
        foreach (var rule in _recurringDayRules.Where(x => _selectedDate >= x.EffectiveFrom))
        {
            if (_selectedDate.DayOfWeek == rule.PreviousDay)
                rows.RemoveAll(x => SupplierNamesMatch(x.Supplier, rule.Supplier));

            if (_selectedDate.DayOfWeek == rule.NewDay && !rows.Any(x => SupplierNamesMatch(x.Supplier, rule.Supplier)))
                rows.Add(new SupplierWeekPlanRow(_selectedDate, rule.Supplier, 0m, 0m, 0m, DebtBeforeDate(rule.Supplier, _selectedDate)));
        }

        // Add/remove rules selected by the owner. A future rule applies to the
        // same weekday from its start date onward; a date override applies once.
        foreach (var rule in _recurringSupplierMembershipRules
                     .Where(x => x.Day == _selectedDate.DayOfWeek && x.EffectiveFrom <= _selectedDate)
                     .GroupBy(x => NormalizeSupplierName(x.Supplier))
                     .Select(x => x.OrderByDescending(y => y.EffectiveFrom).First()))
            ApplySupplierMembership(rows, rule.Supplier, rule.IsIncluded);

        foreach (var rule in _supplierScheduleOverrides.Where(x => x.Date == _selectedDate))
            ApplySupplierMembership(rows, rule.Supplier, rule.IsIncluded);

        // A payment saved today becomes the opening debt when the same supplier is viewed later.
        // The saved historical row itself is never changed, so earlier dates keep their own value.
        return rows.Select(row => row.Debt != 0m || row.HasActualDebt
            ? row
            : row with { Debt = DebtBeforeDate(row.Supplier, _selectedDate) }).ToList();
    }

    private void ApplySupplierMembership(List<SupplierWeekPlanRow> rows, string supplier, bool isIncluded)
    {
        var index = rows.FindIndex(x => SupplierNamesMatch(x.Supplier, supplier));
        if (isIncluded && index < 0)
            rows.Add(new SupplierWeekPlanRow(_selectedDate, supplier, 0m, 0m, 0m, DebtBeforeDate(supplier, _selectedDate)));
        else if (!isIncluded && index >= 0)
            rows.RemoveAt(index);
    }

    private static bool HasRecordedActivity(SupplierWeekPlanRow row) =>
        row.OrderAmount != 0m || row.PaymentAmount != 0m || row.OldDebtPayment != 0m || row.Debt != 0m;

    private void ApplyCompletedSupplierPayment(CompletedPayment payment)
    {
        if (!IsKnownSupplier(payment.Recipient)) return;

        var index = _supplierWeekRows.FindIndex(x => x.Date == payment.PaidDate && SupplierNamesMatch(x.Supplier, payment.Recipient));
        if (index >= 0)
        {
            var row = _supplierWeekRows[index];
            var debt = row.Debt != 0m ? row.Debt : DebtBeforeDate(row.Supplier, payment.PaidDate);
            _supplierWeekRows[index] = row with
            {
                Debt = Math.Max(0m, debt - payment.Amount),
                OldDebtPayment = row.OldDebtPayment + payment.Amount
            };
        }
        else
        {
            var openingDebt = DebtBeforeDate(payment.Recipient, payment.PaidDate);
            _supplierWeekRows.Add(new SupplierWeekPlanRow(payment.PaidDate, payment.Recipient, 0m, 0m,
                payment.Amount, Math.Max(0m, openingDebt - payment.Amount)));
        }
        _supplierWeekPlanStore.Save(_supplierWeekRows);
    }

    private decimal DebtBeforeDate(string supplier, DateOnly date)
    {
        var previous = _supplierWeekRows
            .Where(x => x.Date < date && SupplierNamesMatch(x.Supplier, supplier) && (x.Debt != 0m || x.HasActualDebt))
            .OrderByDescending(x => x.Date).FirstOrDefault();
        if (previous is not null) return previous.Debt;

        var imported = _partnerDebts.FirstOrDefault(x => SupplierNamesMatch(x.Supplier, supplier));
        if (imported is not null) return imported.Amount;
        return _snapshot?.Suppliers.FirstOrDefault(x => SupplierNamesMatch(x.Name, supplier))?.Debt ?? 0m;
    }

    private CashDailySummary? CashSummaryForSelectedDate() =>
        _cashDocuments.Any(x => x.Date == _selectedDate)
            ? CashDocumentImportService.Summary(_cashDocuments, _selectedDate)
            : null;

    private bool IsKnownSupplier(string name) =>
        _supplierWeekRows.Any(x => SupplierNamesMatch(x.Supplier, name)) ||
        _partnerDebts.Any(x => SupplierNamesMatch(x.Supplier, name)) ||
        (_snapshot?.Suppliers.Any(x => SupplierNamesMatch(x.Name, name)) ?? false);

    private static bool SupplierNamesMatch(string first, string second)
    {
        var a = NormalizeSupplierName(first); var b = NormalizeSupplierName(second);
        if (a == b) return true;
        if (a == "մենթոս") a = "մենթոսսլավգրուպ";
        if (b == "մենթոս") b = "մենթոսսլավգրուպ";
        return a == b || (Math.Min(a.Length, b.Length) >= 7 && (a.Contains(b) || b.Contains(a)));
    }

    private static string NormalizeSupplierName(string value) =>
        new string(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray())
            .Replace("սպը", "").Replace("փբը", "").Replace("հձ", "");

    private async void ChangeSupplierWeekDay_Click(object sender, RoutedEventArgs e)
    {
        var window = new SupplierWeekDayChangeWindow(PlanForSelectedDate()) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;
        var change = window.Result;
        var index = _supplierWeekRows.FindIndex(x => x.Date == change.Source.Date && x.Supplier == change.Source.Supplier);
        if (change.RecursInFuture)
        {
            _recurringDayRules.RemoveAll(x => x.Supplier == change.Source.Supplier && x.PreviousDay == change.Source.Date.DayOfWeek);
            _recurringDayRules.Add(new RecurringSupplierDayRule(change.Source.Supplier, change.Source.Date.DayOfWeek, change.NewDate.DayOfWeek, change.NewDate, change.Note));
            _recurringDayRuleStore.Save(_recurringDayRules);
        }
        else
        {
            // A one-time move is represented explicitly as “not on the old
            // date / present on the new date”; the normal weekly template
            // stays untouched for all later weeks.
            _supplierScheduleOverrides.RemoveAll(x =>
                (x.Date == change.Source.Date || x.Date == change.NewDate) && SupplierNamesMatch(x.Supplier, change.Source.Supplier));
            _supplierScheduleOverrides.Add(new SupplierDateScheduleOverride(change.Source.Date, change.Source.Supplier, false, change.Note));
            _supplierScheduleOverrides.Add(new SupplierDateScheduleOverride(change.NewDate, change.Source.Supplier, true, change.Note));
            _supplierScheduleOverrideStore.Save(_supplierScheduleOverrides);
        }
        _supplierWeekPlanStore.Save(_supplierWeekRows);
        await LoadAsync("Suppliers");
    }

    private async void EditSupplierScheduleMembership_Click(object sender, RoutedEventArgs e)
    {
        var suppliers = SupplierWeekPlanSeed.AllSuppliers()
            .Concat(_supplierWeekRows.Select(x => x.Supplier))
            .Concat(_snapshot?.Suppliers.Select(x => x.Name) ?? [])
            .Concat(_partnerDebts.Select(x => x.Supplier));
        var window = new SupplierScheduleMembershipWindow(suppliers, _selectedDate) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;
        var change = window.Result;

        if (change.RecursInFuture)
        {
            // The newest decision for this supplier/day takes precedence.
            _recurringSupplierMembershipRules.RemoveAll(x =>
                SupplierNamesMatch(x.Supplier, change.Supplier) && x.Day == change.Date.DayOfWeek && x.EffectiveFrom == change.Date);
            _recurringSupplierMembershipRules.Add(new RecurringSupplierMembershipRule(change.Supplier, change.Date.DayOfWeek, change.IsIncluded, change.Date, change.Note));
            _recurringSupplierMembershipRuleStore.Save(_recurringSupplierMembershipRules);
        }
        else
        {
            _supplierScheduleOverrides.RemoveAll(x => x.Date == change.Date && SupplierNamesMatch(x.Supplier, change.Supplier));
            _supplierScheduleOverrides.Add(new SupplierDateScheduleOverride(change.Date, change.Supplier, change.IsIncluded, change.Note));
            _supplierScheduleOverrideStore.Save(_supplierScheduleOverrides);
        }
        await LoadAsync("Suppliers");
    }

    private void MergeApiSupplierMovements(DashboardSnapshot source)
    {
        // API rows are the factual daily movements. They replace only the
        // financial columns of the same supplier/date; the weekly schedule and
        // owner-defined day changes remain intact.
        if (App.Services.DataProvider is not HtsApiDataProvider || source.SupplierMovements.Count == 0) return;
        var changed = false;
        foreach (var movement in source.SupplierMovements.Where(x => x.Date == source.Date))
        {
            var index = _supplierWeekRows.FindIndex(x => x.Date == movement.Date && SupplierNamesMatch(x.Supplier, movement.Supplier));
            var row = new SupplierWeekPlanRow(movement.Date, movement.Supplier, movement.OrderAmount,
                movement.PaymentForOrder, movement.OldDebtPayment, movement.ClosingDebt, true);
            if (index < 0) _supplierWeekRows.Add(row);
            else _supplierWeekRows[index] = row;
            changed = true;
        }
        if (changed) _supplierWeekPlanStore.Save(_supplierWeekRows);
    }

    private void TryAutoImportWarehouseData()
    {
        if (_deliveryPatterns.Count > 0) return;
        try
        {
            var source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "WarehouseDocument.xlsx");
            if (!File.Exists(source)) return;
            _deliveryPatterns = WarehouseImportService.Import(source).ToList();
            _deliveryScheduleStore.Save(_deliveryPatterns);
        }
        catch { /* User can import the source manually if it was moved or is malformed. */ }
    }

    private DashboardSnapshot MergeImportedSuppliers(DashboardSnapshot source)
    {
        if (_deliveryPatterns.Count == 0) return source;
        var suppliers = source.Suppliers.ToList();
        var names = suppliers.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var group in _deliveryPatterns.Where(x => !x.IsOneTime).GroupBy(x => x.Supplier))
        {
            if (!names.Add(group.Key)) continue;
            var primaryDay = group.OrderByDescending(x => x.DeliveryCount).ThenBy(x => x.Weekday == DayOfWeek.Sunday ? 7 : (int)x.Weekday).First().Weekday;
            suppliers.Add(new Supplier(group.Key, "WarehouseDocument.xlsx", 0m, NextWeekday(source.Date, primaryDay), 50, "Կարգավորել", "Ներմուծված է ստացումների պատմությունից"));
        }
        return new DashboardSnapshot
        {
            Date = source.Date, Cash = source.Cash, Suppliers = suppliers.OrderBy(x => x.Name).ToList(),
            SupplierMovements = source.SupplierMovements, Payments = source.Payments, Forecast = source.Forecast, Sales = source.Sales,
            Recommendations = source.Recommendations, Tasks = source.Tasks
        };
    }

    private static DateOnly NextWeekday(DateOnly date, DayOfWeek day)
    {
        var offset = ((int)day - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(offset == 0 ? 7 : offset);
    }

    private static DashboardSnapshot ApplyPaymentChange(DashboardSnapshot source, PaymentChangeDraft draft)
    {
        var plan = new PlannedPayment(draft.Supplier, draft.Amount, draft.PlannedDate, draft.IsMandatory, draft.Reason);
        var latestDebt = source.SupplierMovements.Where(x => x.Supplier.Equals(draft.Supplier, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Date).Select(x => x.ClosingDebt).FirstOrDefault();
        if (latestDebt == 0)
            latestDebt = source.Suppliers.FirstOrDefault(x => x.Name.Equals(draft.Supplier, StringComparison.OrdinalIgnoreCase))?.Debt ?? 0m;
        var movement = new SupplierDailyMovement(draft.PlannedDate, draft.Supplier, "Ձեռքով ավելացված", latestDebt,
            0m, draft.IsOldDebtPayment ? 0m : draft.Amount, draft.IsOldDebtPayment ? draft.Amount : 0m,
            draft.IsOldDebtPayment ? draft.PlannedDate.ToString("dd.MM") : null, draft.Reason, true);
        return new DashboardSnapshot
        {
            Date = source.Date, Cash = source.Cash, Suppliers = source.Suppliers,
            SupplierMovements = source.SupplierMovements.Append(movement).OrderBy(x => x.Date).ToList(),
            Payments = source.Payments.Append(plan).OrderBy(x => x.DueDate).ToList(),
            Forecast = source.Forecast, Sales = source.Sales, Recommendations = source.Recommendations, Tasks = source.Tasks
        };
    }
}

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
    private readonly SupplierStatusChangeStore _supplierStatusChangeStore = new();
    private readonly EmployeePendingIssueStore _employeePendingIssueStore = new();
    private readonly EmployeeSupplierSelectionStore _employeeSupplierSelectionStore = new();
    private readonly SupplierNoteStore _supplierNoteStore = new();
    private readonly PendingEmployeeOrderChangeStore _pendingEmployeeOrderChangeStore = new();
    private readonly EmployeeTaskStore _employeeTaskStore = new();
    private readonly EmployeeTaskActionStore _employeeTaskActionStore = new();
    private readonly EmployeePendingTaskIssueStore _employeePendingTaskIssueStore = new();
    private readonly EmployeeIssueStore _employeeIssueStore = new();
    private readonly EmployeePendingGeneralIssueStore _employeePendingGeneralIssueStore = new();
    private readonly OwnerPendingEmployeeIssueReplyStore _ownerPendingEmployeeIssueReplyStore = new();
    private readonly AvailableFundsStore _availableFundsStore = new();
    private readonly CashDeskAdjustmentStore _cashDeskAdjustmentStore = new();
    private readonly LocalPurchaseProposalStore _purchaseProposalStore = new();
    private readonly FundsTransactionStore _fundsTransactionStore = new();
    private readonly SupplierDebtHistoryStore _supplierDebtHistoryStore = new();
    private readonly CashFlowPolicyStore _cashFlowPolicyStore = new();
    private readonly SalaryStore _salaryStore = new();
    private readonly PendingSalaryEmployeeStore _pendingSalaryEmployeeStore = new();
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
    private readonly List<FundsTransaction> _fundsTransactions;
    private readonly List<SupplierDebtChange> _supplierDebtHistory;
    private readonly List<SalaryAccrual> _salaryAccruals;
    private readonly List<SalaryPayment> _salaryPayments;
    private readonly List<PendingSalaryEmployee> _pendingSalaryEmployees;
    private AvailableFundsBreakdown? _lastFunds;
    private System.Windows.Threading.DispatcherTimer? _telegramPollTimer;
    private System.Windows.Threading.DispatcherTimer? _employeeTelegramPollTimer;
    private bool _telegramPollInProgress;
    private bool _employeeTelegramPollInProgress;
    private bool _employeeMorningScheduleInProgress;
    private bool _employeeTaskScheduleInProgress;
    private bool _employeeNextDayScheduleInProgress;
    private bool _employeeEveningScheduleInProgress;
    private bool _telegramScheduleInProgress;
    private string? _cashSyncStatus;
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
        _fundsTransactions = _fundsTransactionStore.Load();
        _supplierDebtHistory = _supplierDebtHistoryStore.Load();
        _salaryAccruals = _salaryStore.LoadAccruals();
        _salaryPayments = _salaryStore.LoadPayments();
        _pendingSalaryEmployees = _pendingSalaryEmployeeStore.Load();
        InitializeComponent();
        PresentationTheme.Apply(this);
        ViewDatePicker.SelectedDate = _selectedDate.ToDateTime(TimeOnly.MinValue);
        ConfigureDataProvider();
        Loaded += async (_, _) =>
        {
            _uiReady = true;
            await LoadAsync("Dashboard");
            // Exactly one process is allowed to handle Telegram polling and timed
            // messages.  Normally this is the Automation Host. If it is not
            // running, an open desktop application safely takes over instead.
            if (((App)Application.Current).TryAcquireTelegramAutomationLease())
            {
                StartTelegramPolling();
                StartEmployeeTelegramPolling();
            }
        };
    }

    private void ConfigureDataProvider()
    {
        if (_excelImportStore.Load().Enabled)
        {
            App.Services.UseDataProvider(new ExcelDataProvider(_excelImportStore));
            DataSourceStatusText.Text = "Տվյալների աղբյուր՝ ներմուծված Excel (ոչ առցանց)";
            return;
        }
        var settings = _htsApiSettingsStore.Load();
        if (settings.IsConfigured)
        {
            App.Services.UseDataProvider(new HtsApiDataProvider(settings));
            DataSourceStatusText.Text = "Տվյալների աղբյուր՝ ՀԾ API";
        }
        else
        {
            App.Services.UseDataProvider(new EmptyDataProvider());
            DataSourceStatusText.Text = "Տվյալների աղբյուր՝ ՀԾ API-ն դեռ միացված չէ";
        }
        _cashDocuments.Clear(); _cashDocuments.AddRange(_cashDocumentStore.Load());
        _cashSyncStatus = null;
    }

    private void StartTelegramPolling()
    {
        if (!((App)Application.Current).TryAcquireTelegramAutomationLease()) return;

        // Keep checking settings even when the bot is connected after startup.
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
        if (!((App)Application.Current).TryAcquireTelegramAutomationLease()) return;
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
                await TrySendEmployeeNotificationAsync($"Morning orders for {user.ChatId}", () => SendEmployeeOrderListAsync(settings, user.ChatId, today));
            var latest = _employeeTelegramBotSettingsStore.Load();
            _employeeTelegramBotSettingsStore.Save(latest with { LastMorningOrdersDate = today });
        }
        catch
        {
            // The next polling cycle retries if the bot is temporarily unavailable.
        }
        finally { _employeeMorningScheduleInProgress = false; }
    }

    private async Task SendEmployeeOrderListAsync(EmployeeTelegramBotSettings settings, string chatId, DateOnly date, string workflow = "receipt", bool sendEmptyMessage = false)
    {
        // Telegram is an action list, not a copy of the complete delivery calendar.
        // Suppliers with no order, no payment and no reported exception remain visible
        // in the Windows schedule, but they do not create unnecessary bot messages.
        var rows = PlannedSuppliersFor(date)
            .Where(row => HasTelegramSupplierAction(row, date))
            .OrderBy(x => x.Supplier).ToList();
        var isOrdering = workflow == "order";
        if (rows.Count == 0)
        {
            // Automatic reminders remain quiet when there is nothing to do,
            // but a person who explicitly asks must always get an answer.
            if (sendEmptyMessage)
            {
                var label = isOrdering ? "պատվեր" : "մատակարարական գործողություն";
                await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(settings.BotToken, chatId),
                    $"ℹ️ {date:dd.MM.yyyy}-ի համար հաստատված {label} դեռ չկա։\n\n" +
                    "Երբ տնօրենը կամ մենեջերը պլանավորի պատվեր/վճարում, այն այստեղ կերևա։");
            }
            return;
        }
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
            var latestNote = _supplierNoteStore.Load()
                .Where(note => note.Date == date && SupplierNamesMatch(note.Supplier, row.Supplier))
                .OrderByDescending(note => note.CreatedAt).FirstOrDefault();
            if (latestNote is not null) message.AppendLine($"   📌 Նշում՝ {latestNote.Text}");
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

    private bool HasTelegramSupplierAction(SupplierWeekPlanRow row, DateOnly date)
    {
        if (row.OrderAmount != 0m || row.PaymentAmount != 0m || row.OldDebtPayment != 0m)
            return true;

        // A receipt, non-arrival or employee problem must remain visible even
        // if the financial amounts are zero.
        return _employeeSupplierActionStore.Load().Any(action =>
                   action.Date == date && SupplierNamesMatch(action.Supplier, row.Supplier))
            || _supplierNoteStore.Load().Any(note =>
                   note.Date == date && SupplierNamesMatch(note.Supplier, row.Supplier));
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
                await TrySendEmployeeNotificationAsync($"Morning tasks for {user.ChatId}", () => SendEmployeeTasksAsync(settings, user.ChatId, today));
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
        if (_employeeNextDayScheduleInProgress) return;
        var settings = _employeeTelegramBotSettingsStore.Load();
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (!settings.IsConfigured || settings.LastNextDayPlanDate == today || DateTime.Now.TimeOfDay < new TimeSpan(8, 10, 0)) return;
        _employeeNextDayScheduleInProgress = true;
        try
        {
            foreach (var user in _employeeBotUserStore.Load())
                await TrySendEmployeeNotificationAsync($"Next day orders for {user.ChatId}", () => SendEmployeeOrderListAsync(settings, user.ChatId, today.AddDays(1), "order"));
            var latest = _employeeTelegramBotSettingsStore.Load();
            _employeeTelegramBotSettingsStore.Save(latest with { LastNextDayPlanDate = today });
        }
        catch (Exception exception) { RuntimeDiagnostics.Log("Next day employee orders", exception); }
        finally { _employeeNextDayScheduleInProgress = false; }
    }

    private async Task TrySendEmployeeEveningReminderAsync()
    {
        if (_employeeEveningScheduleInProgress) return;
        var settings = _employeeTelegramBotSettingsStore.Load();
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (!settings.IsConfigured || settings.LastEveningReminderDate == today || DateTime.Now.TimeOfDay < new TimeSpan(20, 0, 0)) return;
        _employeeEveningScheduleInProgress = true;
        try
        {
            const string reminder = "🔔 20:00 հիշեցում\n\nԽնդրում ենք ստուգել այսօրվա ստացումները և վաղվա պատվերները։ Նշեք՝ պատվերը գրվել է, մենեջերը չի եկել, թե խնդիր կա։\n\nԱյսօրվա համար՝ ընթացիկ պատվեր\nՎաղվա պատվերների համար՝ պատվիրել";
            foreach (var user in _employeeBotUserStore.Load())
                await TrySendEmployeeNotificationAsync($"Evening reminder for {user.ChatId}", () => TelegramBotClient.SendMessageAsync(new TelegramBotSettings(settings.BotToken, user.ChatId), reminder));
            var latest = _employeeTelegramBotSettingsStore.Load();
            _employeeTelegramBotSettingsStore.Save(latest with { LastEveningReminderDate = today });
        }
        catch (Exception exception) { RuntimeDiagnostics.Log("Employee evening reminder", exception); }
        finally { _employeeEveningScheduleInProgress = false; }
    }

    private static async Task TrySendEmployeeNotificationAsync(string operation, Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception exception)
        {
            RuntimeDiagnostics.Log(operation, exception);
        }
    }

    private async Task ProcessEmployeeActionCallbackAsync(EmployeeTelegramBotSettings settings, EmployeeTelegramIncomingMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.CallbackId)) return;
        await TelegramBotClient.AnswerCallbackAsync(new TelegramBotSettings(settings.BotToken, message.ChatId), message.CallbackId);
        var parts = message.Text.Split(':');
        if (parts.Length == 2 && parts[0] == "empsel" && Guid.TryParse(parts[1], out var selectionId))
        {
            var selections = _employeeSupplierSelectionStore.Load();
            var selection = selections.FirstOrDefault(x => x.Id == selectionId && x.ChatId == message.ChatId);
            if (selection is null)
            {
                await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(settings.BotToken, message.ChatId),
                    "Այս ընտրությունը հնացել է։ Կրկին սեղմեք «Գործողություն» և ընտրեք մատակարարին։ ");
                return;
            }

            // One button must be used only once. Old buttons therefore cannot
            // accidentally update a different supplier after the plan changes.
            selections.RemoveAll(x => x.Id == selectionId || x.CreatedAt < DateTime.Now.AddDays(-7));
            _employeeSupplierSelectionStore.Save(selections);
            var replySettings = new TelegramBotSettings(settings.BotToken, message.ChatId);
            if (selection.Action is "issue" or "actual")
            {
                var pending = _employeePendingIssueStore.Load();
                pending.RemoveAll(x => x.ChatId == message.ChatId);
                pending.Add(new EmployeePendingIssue(message.ChatId, selection.Date, selection.Supplier, selection.Workflow, selection.Action));
                _employeePendingIssueStore.Save(pending);
                await TelegramBotClient.SendMessageAsync(replySettings, selection.Action == "actual"
                    ? $"✏️ {selection.Supplier}\nԳրեք փաստացի թվերը այս ձևով՝ պատվեր/նոր վճարում/հին պարտքի վճարում\nՕրինակ՝ 5000/5000/2500"
                    : $"⚠️ {selection.Supplier}\nՆկարագրեք խնդիրը մեկ հաղորդագրությամբ։ Օրինակ՝ «վճարում չի կատարվել», «ապրանքը հին էր», «մատակարարը ուշանալու է»։");
                return;
            }

            var statusText = selection.Action == "done"
                ? (selection.Workflow == "order" ? "Պատվերը գրանցվել է" : "Կատարված է")
                : (selection.Workflow == "order" ? "Մենեջերը չի եկել / պատվերը չի գրվել" : "Չի եկել");
            SaveEmployeeSupplierAction(selection.Date, selection.Supplier, statusText, string.Empty, message);
            await TelegramBotClient.SendMessageAsync(replySettings, $"✅ Գրանցվեց՝ {selection.Supplier} — {statusText}։");
            return;
        }
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
            // The inline button index must use exactly the same action-only
            // collection that is later used to resolve empsup callbacks.
            // Previously the menu showed all suppliers but the callback read
            // from a filtered list, so some buttons selected another supplier
            // or appeared to do nothing.
            var rows = PlannedSuppliersFor(date)
                .Where(row => HasTelegramSupplierAction(row, date))
                .OrderBy(x => x.Supplier)
                .ToList();
            if (rows.Count == 0)
            {
                await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(settings.BotToken, message.ChatId),
                    "Այս օրվա համար գործողություն պահանջող մատակարար չկա։");
                return;
            }
            var selections = _employeeSupplierSelectionStore.Load()
                .Where(x => x.CreatedAt >= DateTime.Now.AddDays(-7)).ToList();
            var buttons = new List<IReadOnlyList<TelegramInlineButton>>();
            foreach (var row in rows)
            {
                var selection = new EmployeeSupplierSelection(Guid.NewGuid(), message.ChatId, date, row.Supplier, workflow, action, DateTime.Now);
                selections.Add(selection);
                buttons.Add(new[] { new TelegramInlineButton(row.Supplier, $"empsel:{selection.Id:N}") });
            }
            _employeeSupplierSelectionStore.Save(selections);
            var title = action == "done" ? "Կատարված" : action == "missing" ? "Չի եկել" : "Խնդիր";
            await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(settings.BotToken, message.ChatId),
                $"{title} — ընտրեք մատակարարին։", buttons);
            return;
        }

        // Legacy buttons from already-sent messages used a list index. They
        // are intentionally rejected because that index may now refer to a
        // different supplier. Employees should open a fresh action menu.
        if (parts[0] == "empsup")
        {
            await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(settings.BotToken, message.ChatId),
                "Այս հին կոճակը անվտանգ չէ։ Կրկին սեղմեք «Գործողություն» և ընտրեք մատակարարին։ ");
            return;
        }
        return;
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
        var canonical = ExactSupplier(supplier);
        var changes = _pendingEmployeeOrderChangeStore.Load();
        var baseline = canonical is null ? null : PlannedSuppliersFor(date).FirstOrDefault(x => x.Supplier == canonical);
        var change = new PendingEmployeeOrderChange(Guid.NewGuid(), date, canonical ?? supplier,
            baseline?.OrderAmount ?? 0m, baseline?.PaymentAmount ?? 0m, order, payment, message.ChatId, message.DisplayName,
            DateTime.Now, baseline?.OldDebtPayment ?? 0m, oldDebtPayment, canonical is null, supplier,
            canonical is null ? SupplierNameSuggestions.Find(supplier, KnownSupplierNames()) : []);
        changes.Add(change);
        _pendingEmployeeOrderChangeStore.Save(changes);
        await TelegramBotClient.SendMessageAsync(replySettings, $"⏳ Նոր մատակարարը գրանցվեց հաստատման համար։\n{date:dd.MM.yyyy} · {supplier}\n{order:N0}/{payment:N0}/{oldDebtPayment:N0}");
        var owner = _telegramBotSettingsStore.Load();
        if (owner.IsConfigured && !string.IsNullOrWhiteSpace(owner.ChatId))
            await TrySendEmployeeNotificationAsync("Supplier review for director", () => SendPendingChangeAsync(owner, change));
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
        var debtChanges = _supplierDebtHistory
            .Where(x => SupplierNamesMatch(x.Supplier, supplier))
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.ChangedAt)
            .Take(8)
            .ToList();
        var actions = _employeeSupplierActionStore.Load()
            .Where(x => SupplierNamesMatch(x.Supplier, supplier))
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.ReportedAt)
            .ToList();
        var today = actions.FirstOrDefault(x => x.Date == _selectedDate);
        if (today is not null)
        {
            var text = string.IsNullOrWhiteSpace(today.Description) ? today.Status : today.Description;
            var debtText = debtChanges.Count == 0 ? string.Empty : "\n\nՊարտքի վերջին փոփոխություն՝ " +
                $"{debtChanges[0].PreviousDebt:N0} ֏ → {debtChanges[0].NewDebt:N0} ֏ ({debtChanges[0].ChangedAt:dd.MM.yyyy HH:mm})";
            MessageBox.Show($"Մատակարար՝ {supplier}\nԱմսաթիվ՝ {_selectedDate:dd.MM.yyyy}\nԿարգավիճակ՝ {today.Status}\n\n{text}{debtText}\n\nՆշել է՝ {today.ReportedByName} ({today.ReportedAt:HH:mm})", "Աշխատակցի նշում", MessageBoxButton.OK,
                today.Status == "Խնդիր" ? MessageBoxImage.Warning : MessageBoxImage.Information);
            return;
        }
        var history = actions.Where(x => x.Status is "Խնդիր" or "Չի եկել").Take(8).ToList();
        if (history.Count == 0 && debtChanges.Count == 0)
        {
            MessageBox.Show($"{supplier}-ի համար {_selectedDate:dd.MM.yyyy}-ին աշխատակիցը դեռ նշում չի ուղարկել։", "Մատակարար", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var lines = history.Select(x => $"{x.Date:dd.MM.yyyy} · {x.Status} · {(string.IsNullOrWhiteSpace(x.Description) ? "Առանց նկարագրության" : x.Description)}").ToList();
        if (debtChanges.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Պարտքի փոփոխությունների պատմություն՝");
            lines.AddRange(debtChanges.Select(x => $"{x.EffectiveDate:dd.MM.yyyy} · {x.PreviousDebt:N0} ֏ → {x.NewDebt:N0} ֏ · {x.Reason}"));
        }
        MessageBox.Show($"{supplier}\n\n{string.Join("\n", lines)}", "Մատակարարի պատմություն", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void ShowSupplierDebtHistory(string supplier)
    {
        var history = _supplierDebtHistory
            .Where(x => SupplierNamesMatch(x.Supplier, supplier))
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.ChangedAt)
            .ToList();
        if (history.Count == 0)
        {
            MessageBox.Show($"{supplier}-ի համար դեռ պարտքի փոփոխություն չի գրանցվել։\n\nՊարտքի թիվը փոխեք և սեղմեք «Պահպանել»։", "Պարտքի պատմություն", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var lines = history.Select(x =>
            $"{x.EffectiveDate:dd.MM.yyyy} · {x.ChangedAt:HH:mm}\n{x.PreviousDebt:N0} ֏ → {x.NewDebt:N0} ֏\n{x.Reason}");
        MessageBox.Show($"Մատակարար՝ {supplier}\n\n{string.Join("\n\n", lines)}", "Պարտքի փոփոխությունների պատմություն", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void EditSupplierStatus(SupplierWeekPlanRow row, EmployeeSupplierAction? currentAction)
    {
        var previousStatus = currentAction?.Status ?? "Սպասվում է";
        var window = new SupplierStatusWindow(row.Date, row.Supplier, previousStatus) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;

        var history = _supplierStatusChangeStore.Load();
        history.Add(window.Result);
        _supplierStatusChangeStore.Save(history);
        await LoadAsync(_currentPage == "PurchasePlan" ? "PurchasePlan" : "Suppliers");
    }

    private async void AddSupplierNoteFromDesktop(SupplierWeekPlanRow row)
    {
        var window = new SupplierNoteWindow(row.Date, row.Supplier) { Owner = this };
        if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(window.Result)) return;

        var note = new SupplierNote(Guid.NewGuid(), row.Date, row.Supplier, window.Result, "Տնօրեն", true, DateTime.Now);
        var notes = _supplierNoteStore.Load();
        notes.Add(note);
        _supplierNoteStore.Save(notes);
        await NotifyEmployeesOfDirectorNoteAsync(note);
        await LoadAsync(_currentPage == "PurchasePlan" ? "PurchasePlan" : "Suppliers");
    }

    private void ShowSupplierAnalysis(SupplierWeekPlanRow row)
    {
        var start = new DateOnly(_selectedDate.Year, _selectedDate.Month, 1);
        var window = new SupplierAnalysisWindow(row.Supplier, start, _selectedDate,
            (from, to) => LoadSupplierAnalysisAsync(row.Supplier, from, to)) { Owner = this };
        window.ShowDialog();
    }

    private async Task<SupplierAnalysisData> LoadSupplierAnalysisAsync(string supplier, DateOnly startDate, DateOnly endDate)
    {
        IReadOnlyList<SupplierActivityLine> activity = [];
        var fromHts = false;
        var warnings = new List<string>();
        if (App.Services.DataProvider is ISupplierActivityProvider activityProvider)
        {
            try
            {
                activity = await activityProvider.GetSupplierActivityAsync(supplier, startDate, endDate);
                fromHts = App.Services.DataProvider is not EmptyDataProvider;
            }
            catch (Exception ex) { warnings.Add("Ստացումներ/վճարումներ՝ " + ex.Message); }
        }

        // The app remains useful before the API returns document history: show
        // confirmed/manual plan entries instead of inventing figures.
        if (!fromHts)
        {
            activity = _supplierWeekRows
                .Where(row => row.Date >= startDate && row.Date <= endDate && SupplierNamesMatch(row.Supplier, supplier))
                .Where(row => IsSupplierReceiptConfirmed(row, _employeeSupplierActionStore.Load(), _supplierStatusChangeStore.Load()))
                .GroupBy(row => row.Date)
                .Select(group => new SupplierActivityLine(group.Key,
                    group.Sum(row => row.OrderAmount),
                    group.Sum(row => row.PaymentAmount),
                    group.Sum(row => row.OldDebtPayment),
                    "Ծրագրի պլան", "Ծրագրում գրանցված պատվեր/վճարում"))
                .OrderBy(line => line.Date)
                .ToList();
        }

        SupplierSalesAnalysis? sales = null;
        if (App.Services.DataProvider is ISupplierSalesAnalysisProvider salesProvider)
        {
            try
            {
                var allSales = await salesProvider.GetSupplierSalesAnalysisAsync(startDate, endDate);
                sales = allSales.FirstOrDefault(item => SupplierNamesMatch(item.Supplier, supplier));
                if (sales is null) warnings.Add("Մատակարարին կապակցված վաճառքի տողեր չկան։ Ստուգեք ՀԾ-ի խմբաքանակի մատակարարը։ Սա չի նշանակում զրոյական վաճառք։");
            }
            catch (Exception ex) { warnings.Add("Վաճառքներ՝ " + ex.Message); }
        }

        var apiDebt = _snapshot?.Suppliers.FirstOrDefault(item => SupplierNamesMatch(item.Name, supplier))?.Debt;
        var importedDebt = _partnerDebts.Where(item => SupplierNamesMatch(item.Supplier, supplier)).OrderByDescending(item => item.AsOfDate).FirstOrDefault()?.Amount;
        var plannedDebt = PlanForSelectedDate().Where(item => SupplierNamesMatch(item.Supplier, supplier)).Select(item => item.Debt).FirstOrDefault();
        var currentDebt = apiDebt ?? importedDebt ?? plannedDebt;
        return new SupplierAnalysisData(supplier, startDate, endDate, currentDebt, activity, sales, fromHts, string.Join("\n", warnings));
    }

    private async Task NotifyEmployeesOfDirectorNoteAsync(SupplierNote note)
    {
        var employeeSettings = _employeeTelegramBotSettingsStore.Load();
        if (!employeeSettings.IsConfigured) return;

        var text = $"📌 Տնօրենի նոր նշում\n\nՕր՝ {note.Date:dd.MM.yyyy}\nՄատակարար՝ {note.Supplier}\nՆշում՝ {note.Text}";
        foreach (var chatId in _employeeBotUserStore.Load().Select(user => user.ChatId).Distinct())
        {
            try
            {
                await TelegramBotClient.SendMessageAsync(new TelegramBotSettings(employeeSettings.BotToken, chatId), text);
            }
            catch (Exception exception)
            {
                RuntimeDiagnostics.Log("Director supplier note notification", exception);
            }
        }
    }

    private static bool TryParseSupplierNote(string source, DateOnly defaultDate, out DateOnly date, out string supplier, out string text)
    {
        date = defaultDate;
        supplier = string.Empty;
        text = string.Empty;
        var value = source.Trim().TrimStart('/');
        if (!value.StartsWith("նշում", StringComparison.OrdinalIgnoreCase)) return false;
        value = value["նշում".Length..].Trim();

        if (TryTelegramDate(value, out var requestedDate))
        {
            date = requestedDate;
            var match = System.Text.RegularExpressions.Regex.Match(value, @"\d{1,2}[,./-]\d{1,2}(?:[,./-]\d{2,4})?");
            if (match.Success) value = value.Remove(match.Index, match.Length).Trim();
        }

        var separators = new[] { " - ", " — ", " – " };
        var separator = separators.FirstOrDefault(value.Contains);
        if (separator is null) return false;
        var index = value.IndexOf(separator, StringComparison.Ordinal);
        supplier = value[..index].Trim();
        text = value[(index + separator.Length)..].Trim();
        return !string.IsNullOrWhiteSpace(supplier) && !string.IsNullOrWhiteSpace(text);
    }

    private async Task RegisterSupplierNoteFromEmployeeAsync(EmployeeTelegramIncomingMessage message, TelegramBotSettings replySettings)
    {
        if (!TryParseSupplierNote(message.Text, DateOnly.FromDateTime(DateTime.Today), out var date, out var supplier, out var text))
        {
            await TelegramBotClient.SendMessageAsync(replySettings,
                "Գրեք այս ձևով՝\nնշում 23.08.2026 Դավիդով - մենեջերը խնդրել է զանգել կեսօրից հետո");
            return;
        }

        var note = new SupplierNote(Guid.NewGuid(), date, supplier, text, message.DisplayName, false, DateTime.Now);
        var notes = _supplierNoteStore.Load();
        notes.Add(note);
        _supplierNoteStore.Save(notes);

        var owner = _telegramBotSettingsStore.Load();
        if (owner.IsConfigured && !string.IsNullOrWhiteSpace(owner.ChatId))
            await TelegramBotClient.SendMessageAsync(owner,
                $"📌 Աշխատակցի նշում\n\nՕր՝ {date:dd.MM.yyyy}\nՄատակարար՝ {supplier}\nՆշում՝ {text}\nԳրել է՝ {message.DisplayName}");

        await TelegramBotClient.SendMessageAsync(replySettings, $"✅ Նշումը պահպանվեց՝ {date:dd.MM.yyyy} · {supplier}");
        if (_currentPage == "Suppliers") await LoadAsync("Suppliers");
    }

    private async Task RegisterSupplierNoteFromOwnerTelegramAsync(TelegramBotSettings settings, string message)
    {
        if (!TryParseSupplierNote(message, DateOnly.FromDateTime(DateTime.Today), out var date, out var supplier, out var text))
        {
            await TelegramBotClient.SendMessageAsync(settings,
                "Գրեք այս ձևով՝\nնշում 23.08.2026 Դավիդով - զանգել, համաձայնեցնել փոխանցումը");
            return;
        }

        var note = new SupplierNote(Guid.NewGuid(), date, supplier, text, "Տնօրեն", true, DateTime.Now);
        var notes = _supplierNoteStore.Load();
        notes.Add(note);
        _supplierNoteStore.Save(notes);
        await NotifyEmployeesOfDirectorNoteAsync(note);
        await TelegramBotClient.SendMessageAsync(settings, $"✅ Նշումը պահպանվեց և ուղարկվեց աշխատակիցներին՝ {date:dd.MM.yyyy} · {supplier}");
        if (_currentPage == "Suppliers") await LoadAsync("Suppliers");
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
                try
                {
                var userIndex = users.FindIndex(x => x.ChatId == message.ChatId);
                var user = new EmployeeBotUser(message.ChatId, message.DisplayName, userIndex >= 0 ? users[userIndex].RegisteredAt : DateTime.Now, DateTime.Now);
                if (userIndex >= 0) users[userIndex] = user; else users.Add(user);

                var replySettings = new TelegramBotSettings(settings.BotToken, message.ChatId);
                if (!string.IsNullOrWhiteSpace(message.CallbackId))
                {
                    await ProcessEmployeeActionCallbackAsync(settings, message);
                }
                else if (message.Text.TrimStart('/').StartsWith("անկանխիկ", StringComparison.OrdinalIgnoreCase))
                {
                    await RegisterNonCashAsync(replySettings,message.Text,"employee-noncash-"+message.UpdateId,message.DisplayName);
                }
                else if (message.Text.StartsWith("աշխատավարձ", StringComparison.OrdinalIgnoreCase))
                {
                    await RegisterSalaryFromEmployeeTelegramAsync(message, replySettings);
                }
                else if (message.Text.StartsWith("նշում", StringComparison.OrdinalIgnoreCase) || message.Text.StartsWith("/նշում", StringComparison.OrdinalIgnoreCase))
                {
                    await RegisterSupplierNoteFromEmployeeAsync(message, replySettings);
                }
                else if (message.Text.Equals("/start", StringComparison.OrdinalIgnoreCase))
                {
                    await TelegramBotClient.SendMessageAsync(replySettings,
                        "✅ Դուք գրանցվել եք Patarik-ի աշխատակիցների բոտում։\n\nԱյսօրվա ստացումները՝ գրեք «ընթացիկ պատվեր»։\nՎաղվա պատվերը մենեջերին գրելու համար՝ գրեք «պատվիրել»։\nՕրինակ՝ «պատվիրել 29.07.2026»։");
                    await SendEmployeeOrderListAsync(settings, message.ChatId, DateOnly.FromDateTime(DateTime.Today), sendEmptyMessage: true);
                }
                else if (message.Text.StartsWith("ընթացիկ պատվեր", StringComparison.OrdinalIgnoreCase) || message.Text.StartsWith("/ընթացիկ պատվեր", StringComparison.OrdinalIgnoreCase) || message.Text.StartsWith("պատվերներ", StringComparison.OrdinalIgnoreCase) || message.Text.StartsWith("/պատվերներ", StringComparison.OrdinalIgnoreCase))
                {
                    var date = TryTelegramDate(message.Text, out var requestedDate) ? requestedDate : DateOnly.FromDateTime(DateTime.Today);
                    await SendEmployeeOrderListAsync(settings, message.ChatId, date, sendEmptyMessage: true);
                }
                else if (message.Text.StartsWith("պատվիրել", StringComparison.OrdinalIgnoreCase) || message.Text.StartsWith("/պատվիրել", StringComparison.OrdinalIgnoreCase))
                {
                    var date = TryTelegramDate(message.Text, out var requestedDate) ? requestedDate : DateOnly.FromDateTime(DateTime.Today).AddDays(1);
                    await SendEmployeeOrderListAsync(settings, message.ChatId, date, "order", sendEmptyMessage: true);
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
                catch (Exception exception)
                {
                    // One inaccessible employee chat must never block every
                    // other employee or leave the same update stuck forever.
                    RuntimeDiagnostics.Log($"Employee Telegram message {message.UpdateId} for {message.ChatId}", exception);
                }
                finally
                {
                    MarkEmployeeUpdateProcessed(message.UpdateId);
                }
            }
            _employeeBotUserStore.Save(users);
        }
        catch (Exception exception)
        {
            RuntimeDiagnostics.Log("Employee Telegram polling", exception);
        }
        finally { _employeeTelegramPollInProgress = false; }
    }

    private void MarkEmployeeUpdateProcessed(long updateId)
    {
        var current = _employeeTelegramBotSettingsStore.Load();
        if (current.LastUpdateId is { } last && last >= updateId) return;
        _employeeTelegramBotSettingsStore.Save(current with { LastUpdateId = updateId });
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

            await TelegramUpdateProcessor.ProcessAsync(messages, message => message.UpdateId, async message =>
            {
                if (!string.IsNullOrWhiteSpace(message.CallbackId))
                {
                    await TelegramBotClient.AnswerCallbackAsync(settings, message.CallbackId);
                    await ProcessTelegramButtonAsync(settings, message.Text);
                    return;
                }
                if (message.Text.TrimStart('/').StartsWith("անկանխիկ", StringComparison.OrdinalIgnoreCase))
                {
                    await RegisterNonCashAsync(settings,message.Text,"owner-noncash-"+message.UpdateId,"Տնօրեն · Telegram"); return;
                }
                if (await HandleSupplierCorrectionTextAsync(settings, message.Text)) return;
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
                    return;
                }
                var command = message.Text.Trim().ToLowerInvariant();
                if (command.StartsWith("վճարում", StringComparison.OrdinalIgnoreCase) ||
                    command.StartsWith("/վճարում", StringComparison.OrdinalIgnoreCase) ||
                    LooksLikeTransferCommand(command))
                {
                    await RegisterFundsTransactionFromTelegramAsync(settings, message.Text.Trim());
                    return;
                }
                if (command.StartsWith("աշխատավարձ", StringComparison.OrdinalIgnoreCase))
                {
                    await RegisterSalaryFromTelegramAsync(settings, message.Text.Trim());
                    return;
                }
                if (command.StartsWith("նշում", StringComparison.OrdinalIgnoreCase) || command.StartsWith("/նշում", StringComparison.OrdinalIgnoreCase))
                {
                    await RegisterSupplierNoteFromOwnerTelegramAsync(settings, message.Text.Trim());
                    return;
                }
                if (command.StartsWith("ընթացիկ վերլուծություն", StringComparison.OrdinalIgnoreCase) || command.StartsWith("/ընթացիկ վերլուծություն", StringComparison.OrdinalIgnoreCase))
                {
                    await SendTelegramCurrentDayAnalysisAsync(settings, TryTelegramDate(message.Text, out var currentAnalysisDate) ? currentAnalysisDate : DateOnly.FromDateTime(DateTime.Today));
                    return;
                }
                if (command.StartsWith("ընթացիկ դրություն", StringComparison.OrdinalIgnoreCase) || command.StartsWith("/ընթացիկ դրություն", StringComparison.OrdinalIgnoreCase))
                {
                    await SendTelegramCurrentStatusAsync(settings, TryTelegramDate(message.Text, out var currentStatusDate) ? currentStatusDate : DateOnly.FromDateTime(DateTime.Today));
                    return;
                }
                if (command.StartsWith("վերլուծություն", StringComparison.OrdinalIgnoreCase) || command.StartsWith("/վերլուծություն", StringComparison.OrdinalIgnoreCase))
                {
                    await SendTelegramCashFlowOpinionAsync(settings, TryTelegramDate(message.Text, out var analysisDate) ? analysisDate : DateOnly.FromDateTime(DateTime.Today));
                    return;
                }
                if (command is "սկսել" or "/սկսել" or "/start")
                    await SendTelegramDraftAsync(settings);
                else if (command.StartsWith("առաջարկ", StringComparison.OrdinalIgnoreCase) || command.StartsWith("/առաջարկ", StringComparison.OrdinalIgnoreCase))
                    await SendTelegramDraftAsync(settings, TryTelegramDate(message.Text, out var proposalDate) ? proposalDate : DateOnly.FromDateTime(DateTime.Today).AddDays(2));
                else if (command.StartsWith("գլխավոր", StringComparison.OrdinalIgnoreCase) || command.StartsWith("/գլխավոր", StringComparison.OrdinalIgnoreCase))
                    await SendTelegramDashboardAsync(settings, TryTelegramDate(message.Text, out var dashboardDate) ? dashboardDate : DateOnly.FromDateTime(DateTime.Today));
                else if (command.StartsWith("առավոտ", StringComparison.OrdinalIgnoreCase) || command.StartsWith("/առավոտ", StringComparison.OrdinalIgnoreCase))
                    await SendTelegramMorningBriefAsync(settings, TryTelegramDate(message.Text, out var morningDate) ? morningDate : DateOnly.FromDateTime(DateTime.Today));
                else if (command.StartsWith("վճարումներ", StringComparison.OrdinalIgnoreCase) || command.StartsWith("/վճարումներ", StringComparison.OrdinalIgnoreCase))
                    await SendTelegramPaymentsAsync(settings, TryTelegramDate(message.Text, out var paymentsDate) ? paymentsDate : DateOnly.FromDateTime(DateTime.Today));
                else if (command.StartsWith("կարծիք", StringComparison.OrdinalIgnoreCase) || command.StartsWith("/կարծիք", StringComparison.OrdinalIgnoreCase))
                    await SendTelegramCashFlowOpinionAsync(settings, TryTelegramDate(message.Text, out var opinionDate) ? opinionDate : DateOnly.FromDateTime(DateTime.Today));
                else if (command.StartsWith("հաստատումներ", StringComparison.OrdinalIgnoreCase) || command.StartsWith("/հաստատումներ", StringComparison.OrdinalIgnoreCase))
                    await SendPendingConfirmationsAsync(settings);
                else if (command.StartsWith("առաջադրանք", StringComparison.OrdinalIgnoreCase) || command.StartsWith("/առաջադրանք", StringComparison.OrdinalIgnoreCase))
                    await AddEmployeeTaskFromTelegramAsync(settings, message);
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
            }, updateId =>
            {
                var current = _telegramBotSettingsStore.Load();
                if (!TelegramBotIdentity.IsSameBot(current.BotToken, settings.BotToken)) return;
                if (current.LastUpdateId is { } last && last >= updateId) return;
                _telegramBotSettingsStore.Save(current with { LastUpdateId = updateId });
            }, (message, exception) => RuntimeDiagnostics.Log($"Owner Telegram message {message.UpdateId}", exception));
        }
        catch (Exception exception)
        {
            RuntimeDiagnostics.Log("Owner Telegram polling", exception);
        }
        finally { _telegramPollInProgress = false; }
    }

    /// <summary>
    /// Owner payment syntax:
    /// վճարում բանկ 10500 Ապարան թան
    /// վճարում դրամարկղ 0002 10000 կոմունալ
    /// վճարում բանկ 100000 կանխիկացում դրամարկղ 0001
    /// The first value always describes where the money left from.
    /// </summary>
    private async Task RegisterFundsTransactionFromTelegramAsync(TelegramBotSettings settings, string text)
    {
        if (!TryParseFundsTransaction(text, out var transaction, out var error))
        {
            await TelegramBotClient.SendMessageAsync(settings,
                $"Չհասկացա վճարումը։ {error}\n\nՕրինակներ՝\n• վճարում բանկ 10500 Ապարան թան\n• վճարում դրամարկղ 0002 10000 կոմունալ\n• վճարում բանկ 100000 կանխիկացում դրամարկղ 0001");
            return;
        }

        _fundsTransactions.Add(transaction);
        _fundsTransactionStore.Save(_fundsTransactions);
        if (TryGetSupplierPayment(transaction, out var supplier, out var isOldDebtPayment))
            ApplyManualSupplierPayment(transaction, supplier, isOldDebtPayment);
        if (_snapshot is not null) await LoadAsync(_currentPage);

        var target = string.IsNullOrWhiteSpace(transaction.TargetCashDesk)
            ? string.Empty
            : $" → դրամարկղ {transaction.TargetCashDesk}";
        await TelegramBotClient.SendMessageAsync(settings,
            $"✅ Վճարումը գրանցվեց\nԱղբյուր՝ {FundsSourceLabel(transaction.Source)}{target}\nԳումար՝ {transaction.Amount:N0} ֏\nՆպատակ՝ {transaction.Purpose}");
    }

    private static bool TryParseFundsTransaction(string text, out FundsTransaction transaction, out string error)
    {
        transaction = default!;
        error = string.Empty;
        var value = text.Trim().TrimStart('/');
        if (TryParseInternalTransfer(value, out transaction, out error)) return true;
        if (value.StartsWith("վճարում", StringComparison.OrdinalIgnoreCase)) value = value["վճարում".Length..].Trim();
        var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 3) { error = "Պետք է նշեք աղբյուրը, գումարը և նպատակը։"; return false; }

        var source = string.Empty;
        var index = 0;
        if (tokens[0].Equals("բանկ", StringComparison.OrdinalIgnoreCase))
        {
            source = "bank"; index = 1;
        }
        else if (tokens[0].Equals("դրամարկղ", StringComparison.OrdinalIgnoreCase))
        {
            if (tokens.Length < 4) { error = "Դրամարկղի կոդը, գումարը և նպատակը պարտադիր են։"; return false; }
            source = NormalizeCashDesk(tokens[1]); index = 2;
        }
        else { error = "Առաջին բառը պետք է լինի «բանկ» կամ «դրամարկղ»։"; return false; }

        if (!decimal.TryParse(tokens[index].Replace(",", string.Empty), out var amount) || amount <= 0m)
        {
            error = "Գումարը ճիշտ ձևով նշեք։"; return false;
        }
        index++;
        var tail = string.Join(' ', tokens[index..]);
        string? targetCashDesk = null;
        const string targetPrefix = "դրամարկղ ";
        var targetIndex = tail.LastIndexOf(targetPrefix, StringComparison.OrdinalIgnoreCase);
        if (source == "bank" && targetIndex >= 0)
        {
            var code = tail[(targetIndex + targetPrefix.Length)..].Trim();
            targetCashDesk = NormalizeCashDesk(code);
            tail = tail[..targetIndex].Trim();
        }
        if (string.IsNullOrWhiteSpace(tail)) { error = "Վճարման նպատակը նշեք։"; return false; }
        var category = targetCashDesk is not null ? "Կանխիկացում" : InferPaymentCategory(tail);
        transaction = new FundsTransaction(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), source, amount, tail, category, targetCashDesk, DateTime.Now);
        return true;
    }

    private static bool LooksLikeTransferCommand(string command)
    {
        var tokens = command.TrimStart('/').Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Length >= 4 && tokens[1].Equals("ելք", StringComparison.OrdinalIgnoreCase) &&
               (tokens[0].Equals("բանկ", StringComparison.OrdinalIgnoreCase) || NormalizeCashDesk(tokens[0]) is "0001" or "0002");
    }

    /// <summary>
    /// Standard transfer syntax: [source] ելք [amount] [target].
    /// It is a single internal transfer, never an expense or income.
    /// Examples: 0001 ելք 6500 0002; 0002 ելք 5800 0001; բանկ ելք 100000 0001.
    /// </summary>
    private static bool TryParseInternalTransfer(string value, out FundsTransaction transaction, out string error)
    {
        transaction = default!;
        error = string.Empty;
        var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length != 4 || !tokens[1].Equals("ելք", StringComparison.OrdinalIgnoreCase)) return false;

        var source = tokens[0].Equals("բանկ", StringComparison.OrdinalIgnoreCase) ? "bank" : NormalizeCashDesk(tokens[0]);
        var target = tokens[3].Equals("բանկ", StringComparison.OrdinalIgnoreCase) ? "bank" : NormalizeCashDesk(tokens[3]);
        if (source is not ("bank" or "0001" or "0002") || target is not ("bank" or "0001" or "0002"))
        {
            error = "Աղբյուրը և նպատակակետը պետք է լինեն բանկ, 0001 կամ 0002։";
            return false;
        }
        if (source == target)
        {
            error = "Փոխանցման աղբյուրը և նպատակակետը չեն կարող նույնը լինել։";
            return false;
        }
        if (!decimal.TryParse(tokens[2].Replace(",", string.Empty), out var amount) || amount <= 0m)
        {
            error = "Գումարը ճիշտ նշեք։";
            return false;
        }

        transaction = new FundsTransaction(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), source, amount,
            $"Ներքին փոխանցում՝ {FundsSourceLabel(source)} → {FundsSourceLabel(target)}", "Ներքին փոխանցում", target, DateTime.Now);
        return true;
    }

    private static string NormalizeCashDesk(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits switch { "1" or "01" or "001" or "0001" => "0001", "2" or "02" or "002" or "0002" => "0002", _ => digits.PadLeft(4, '0') };
    }

    private static string InferPaymentCategory(string purpose)
    {
        if (purpose.StartsWith("մատակարար ", StringComparison.OrdinalIgnoreCase)) return "Մատակարարի վճարում";
        if (purpose.Contains("կոմունալ", StringComparison.OrdinalIgnoreCase)) return "Կոմունալ";
        if (purpose.Contains("աշխատավարձ", StringComparison.OrdinalIgnoreCase)) return "Աշխատավարձ";
        if (purpose.Contains("վարձ", StringComparison.OrdinalIgnoreCase)) return "Վարձավճար";
        return "Այլ վճարում";
    }

    private static string FundsSourceLabel(string source) => source == "bank" ? "Բանկ" : $"Դրամարկղ {source}";

    /// <summary>
    /// Direct supplier-payment format for a manually selected cash desk:
    /// վճարում դրամարկղ 0002 12000 մատակարար Չինար հին
    /// վճարում դրամարկղ 0002 12000 մատակարար Չինար նոր
    /// The final word is optional; without it, the amount is recorded as a
    /// new-order payment.
    /// </summary>
    private static bool TryGetSupplierPayment(FundsTransaction transaction, out string supplier, out bool isOldDebtPayment)
    {
        supplier = string.Empty;
        isOldDebtPayment = false;
        const string prefix = "մատակարար ";
        if (!transaction.Purpose.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var value = transaction.Purpose[prefix.Length..].Trim();
        if (value.EndsWith(" հին", StringComparison.OrdinalIgnoreCase))
        {
            isOldDebtPayment = true;
            value = value[..^" հին".Length].Trim();
        }
        else if (value.EndsWith(" նոր", StringComparison.OrdinalIgnoreCase))
            value = value[..^" նոր".Length].Trim();
        if (string.IsNullOrWhiteSpace(value)) return false;
        supplier = value;
        return true;
    }

    private void ApplyManualSupplierPayment(FundsTransaction transaction, string supplier, bool isOldDebtPayment)
    {
        var index = _supplierWeekRows.FindIndex(x => x.Date == transaction.Date && SupplierNamesMatch(x.Supplier, supplier));
        var openingDebt = index >= 0
            ? (_supplierWeekRows[index].Debt != 0m ? _supplierWeekRows[index].Debt : DebtBeforeDate(supplier, transaction.Date))
            : DebtBeforeDate(supplier, transaction.Date);
        var closingDebt = Math.Max(0m, openingDebt - transaction.Amount);
        if (index >= 0)
        {
            var row = _supplierWeekRows[index];
            _supplierWeekRows[index] = row with
            {
                PaymentAmount = row.PaymentAmount + (isOldDebtPayment ? 0m : transaction.Amount),
                OldDebtPayment = row.OldDebtPayment + (isOldDebtPayment ? transaction.Amount : 0m),
                Debt = closingDebt
            };
        }
        else
        {
            _supplierWeekRows.Add(new SupplierWeekPlanRow(transaction.Date, supplier,
                0m, isOldDebtPayment ? 0m : transaction.Amount,
                isOldDebtPayment ? transaction.Amount : 0m, closingDebt));
        }
        _supplierWeekPlanStore.Save(_supplierWeekRows);
        _supplierDebtHistory.Add(new SupplierDebtChange(Guid.NewGuid(), transaction.Date, supplier,
            openingDebt, closingDebt,
            $"Ձեռքով գրանցված {(isOldDebtPayment ? "հին պարտքի" : "նոր պատվերի")} վճարում՝ {transaction.Amount:N0} ֏ ({FundsSourceLabel(transaction.Source)})",
            DateTime.Now, "Տնօրեն"));
        _supplierDebtHistoryStore.Save(_supplierDebtHistory);
    }

    private async Task ProcessTelegramButtonAsync(TelegramBotSettings settings, string data)
    {
        if (await HandleSupplierReviewButtonAsync(settings, data)) return;
        if (data.StartsWith("empday:", StringComparison.Ordinal) && DateOnly.TryParseExact(data[7..], "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var approvalDate))
        {
            var daily = _pendingEmployeeOrderChangeStore.Load().Where(x => x.Date == approvalDate && !x.RequiresSupplierReview).ToList();
            foreach (var change in daily) await ApproveEmployeeOrderChangeAsync(settings, change.Id, announceToOwner: false);
            await TelegramBotClient.SendMessageAsync(settings, $"✅ {approvalDate:dd.MM.yyyy}․ հաստատված է {daily.Count} փոփոխություն։ Անվան ճշտման սպասողները մնացել են առանձին։");
            return;
        }
        if (data.Equals("emporderapproveall", StringComparison.OrdinalIgnoreCase))
        {
            await SendPendingConfirmationsAsync(settings);
            return;
        }
        if (data.StartsWith("empissuereply:", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(data["empissuereply:".Length..], out var issueId))
        {
            var state = _operationsStateStore.Load(); state.PendingSupplierCorrection = null; _operationsStateStore.Save(state);
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
            case "currentanalysis":
                await SendTelegramCurrentDayAnalysisAsync(settings, date);
                break;
            case "currentstatus":
                await SendTelegramCurrentStatusAsync(settings, date);
                break;
            case "currentdetail":
                await SendTelegramCurrentStatusDetailsAsync(settings, date);
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
        if (change.RequiresSupplierReview)
        {
            if (announceToOwner) await SendSupplierReviewAsync(ownerSettings, change);
            else MessageBox.Show("Նախ ճշտեք մատակարարի անունը։", "Հաստատում");
            return;
        }
        var index = _supplierWeekRows.FindIndex(x => x.Date == change.Date && string.Equals(x.Supplier, change.Supplier, StringComparison.OrdinalIgnoreCase));
        var debtBeforeApproval = index >= 0 ? _supplierWeekRows[index].Debt : DebtBeforeDate(change.Supplier, change.Date);
        if (index >= 0)
        {
            var source = _supplierWeekRows[index];
            _supplierWeekRows[index] = source with { OrderAmount = change.ActualOrder, PaymentAmount = change.ActualPayment, OldDebtPayment = change.ActualOldDebtPayment,
                Debt = source.Debt + (change.ActualOrder - source.OrderAmount) - (change.ActualPayment - source.PaymentAmount) - (change.ActualOldDebtPayment - source.OldDebtPayment) };
        }
        else _supplierWeekRows.Add(new SupplierWeekPlanRow(change.Date, change.Supplier, change.ActualOrder, change.ActualPayment, change.ActualOldDebtPayment,
            DebtBeforeDate(change.Supplier, change.Date) + change.ActualOrder - change.ActualPayment - change.ActualOldDebtPayment));
        _supplierWeekPlanStore.Save(_supplierWeekRows);
        var actions = _employeeSupplierActionStore.Load();
        var approvedDebt = _supplierWeekRows.First(x => x.Date == change.Date && string.Equals(x.Supplier, change.Supplier, StringComparison.OrdinalIgnoreCase)).Debt;
        if (approvedDebt != debtBeforeApproval)
        {
            _supplierDebtHistory.Add(new SupplierDebtChange(Guid.NewGuid(), change.Date, change.Supplier,
                debtBeforeApproval, approvedDebt, "Աշխատակցի փաստացի տվյալների հաստատում", DateTime.Now, "Տնօրեն"));
            _supplierDebtHistoryStore.Save(_supplierDebtHistory);
        }
        actions.Add(new EmployeeSupplierAction(change.Date, change.Supplier, "Կատարված է", $"Նախնական՝ {change.PlannedOrder:N0}/{change.PlannedPayment:N0}/{change.PlannedOldDebtPayment:N0}; փաստացի ստացվել է՝ {change.ActualOrder:N0}/{change.ActualPayment:N0}/{change.ActualOldDebtPayment:N0}", change.ReportedByChatId, change.ReportedByName, DateTime.Now));
        _employeeSupplierActionStore.Save(actions);
        changes.RemoveAll(x => x.Id == changeId); _pendingEmployeeOrderChangeStore.Save(changes);
        if (announceToOwner && ownerSettings.IsConfigured && !string.IsNullOrWhiteSpace(ownerSettings.ChatId))
            await TelegramBotClient.SendMessageAsync(ownerSettings, $"✅ Գրանցվեց՝ {change.Supplier}, {change.Date:dd.MM.yyyy}։");
        var employeeSettings = _employeeTelegramBotSettingsStore.Load();
        if (employeeSettings.IsConfigured)
            await TrySendEmployeeNotificationAsync("Supplier approval", () => TelegramBotClient.SendMessageAsync(new TelegramBotSettings(employeeSettings.BotToken, change.ReportedByChatId), $"✅ Տնօրենը հաստատեց {change.Date:dd.MM.yyyy} · {change.Supplier}\nՊատվեր/վճարում/հին պարտքի վճարում՝ {change.ActualOrder:N0}/{change.ActualPayment:N0}/{change.ActualOldDebtPayment:N0}"));
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

    private async Task AddEmployeeTaskFromTelegramAsync(TelegramBotSettings settings, TelegramIncomingMessage incoming)
    {
        var message = incoming.Text;
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
        var (_, added) = _employeeTaskStore.AddFromTelegram(date, description,
            TelegramBotIdentity.BotId(settings.BotToken), incoming.ChatId, incoming.UpdateId);
        if (!added) return;
        await TrySendEmployeeNotificationAsync("Director task confirmation", () =>
            TelegramBotClient.SendMessageAsync(settings, $"✅ Առաջադրանքը ավելացվեց՝ {date:dd.MM.yyyy}\n{description}"));

        if (date == DateOnly.FromDateTime(DateTime.Today))
        {
            var employeeSettings = _employeeTelegramBotSettingsStore.Load();
            if (employeeSettings.IsConfigured)
                foreach (var user in _employeeBotUserStore.Load())
                    await TrySendEmployeeNotificationAsync($"New task for {user.ChatId}", () =>
                        TelegramBotClient.SendMessageAsync(new TelegramBotSettings(employeeSettings.BotToken, user.ChatId), $"📌 Դուք ստացել եք նոր առաջադրանք\n\n{description}\n\nՏեսնելու համար գրեք՝ առաջադրանք"));
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
        foreach (var day in changes.GroupBy(x => x.Date))
        {
            await TelegramBotClient.SendMessageAsync(settings, $"🗂 Հաստատումներ — {day.Key:dd.MM.yyyy} · {day.Count()} հատ",
                [[new TelegramInlineButton("Հաստատել այս օրվա ճշտվածները", $"empday:{day.Key:yyyyMMdd}")]]);
            foreach (var change in day) await SendPendingChangeAsync(settings, change);
        }
    }

    private async Task SendEveningOperationsAsync(TelegramBotSettings settings, DateOnly date)
    {
        var today = PlannedSuppliersFor(date).Where(row => HasTelegramSupplierAction(row, date)).OrderBy(x => x.Supplier).ToList();
        var tomorrow = PlannedSuppliersFor(date.AddDays(1)).Where(row => HasTelegramSupplierAction(row, date.AddDays(1))).OrderBy(x => x.Supplier).ToList();
        var actions = _employeeSupplierActionStore.Load();
        var ownerChanges = _supplierStatusChangeStore.Load();
        var currentActions = actions.Where(x => x.Date == date).GroupBy(x => NormalizeSupplierName(x.Supplier))
            .Select(x => x.OrderByDescending(a => a.ReportedAt).First()).ToList();
        var currentStatuses = currentActions.Select(x => new { x.Supplier, x.Status, x.Description, Time = x.ReportedAt })
            .Concat(ownerChanges.Where(x => x.Date == date).Select(x => new { x.Supplier, Status = x.NewStatus, Description = x.Note ?? "", Time = x.ChangedAt }))
            .GroupBy(x => NormalizeSupplierName(x.Supplier)).Select(x => x.OrderByDescending(a => a.Time).First()).ToList();

        var text = new System.Text.StringBuilder($"🌙 Այսօրվա ամփոփում — {date:dd.MM.yyyy}\n");
        foreach (var item in today)
        {
            var confirmed = IsSupplierReceiptConfirmed(item, actions, ownerChanges);
            text.AppendLine($"• {item.Supplier} · պատվեր {item.OrderAmount:N0} ֏ · {(confirmed ? "վճարված" : "նախատեսված վճարում")} {item.PaymentAmount + item.OldDebtPayment:N0} ֏ · {(confirmed ? "հաստատված" : "չհաստատված")}");
        }
        if (today.Count == 0) text.AppendLine("Գործողություն պահանջող մատակարարում չկա։");
        await SendDailySectionAsync(settings, date, "today", text.ToString());

        if (tomorrow.Count > 0)
        {
            text = new System.Text.StringBuilder($"📅 Վաղվա պլան — {date.AddDays(1):dd.MM.yyyy}\n");
            foreach (var item in tomorrow) text.AppendLine($"• {item.Supplier} · պատվեր {item.OrderAmount:N0} ֏ · նախատեսված վճարում {item.PaymentAmount + item.OldDebtPayment:N0} ֏");
            await SendDailySectionAsync(settings, date, "tomorrow", text.ToString());
        }

        var absent = currentStatuses.Where(x => x.Status == "Չի եկել").ToList();
        var pending = today.Where(x => !IsSupplierReceiptConfirmed(x, actions, ownerChanges) &&
            !absent.Any(a => SupplierNamesMatch(a.Supplier, x.Supplier))).ToList();
        if (absent.Count > 0 || pending.Count > 0)
            await SendDailySectionAsync(settings, date, "absent",
                $"🚚 Չեկած / չհաստատված — {date:dd.MM.yyyy}\n" +
                string.Join("\n", absent.Select(x => $"• Չի եկել՝ {x.Supplier} · {x.Description}")) +
                (pending.Count == 0 ? "" : "\nՍտացումը դեռ հաստատված չէ (չի նշանակում՝ չի եկել)․\n" + string.Join("\n", pending.Select(x => "• " + x.Supplier))));

        var notes = _supplierNoteStore.Load().Where(x => x.Date == date).OrderBy(x => x.CreatedAt).ToList();
        if (notes.Count > 0)
            await SendDailySectionAsync(settings, date, "notes", $"📝 Նշումներ — {date:dd.MM.yyyy}\n" +
                string.Join("\n", notes.Select(x => $"• {x.Supplier} · {x.Text} · {x.Author}")));

        var issues = _employeeIssueStore.Load().Where(x => x.Date == date).ToList();
        var supplierIssues = currentStatuses.Where(x => x.Status == "Խնդիր").ToList();
        if (issues.Count > 0 || supplierIssues.Count > 0)
            await SendDailySectionAsync(settings, date, "issues", $"⚠ Գրանցված խնդիրներ — {date:dd.MM.yyyy}\n" +
                string.Join("\n", supplierIssues.Select(x => $"• {x.Supplier} · {x.Description}")
                    .Concat(issues.Select(x => "• " + x.Description))));

        foreach (var group in _pendingEmployeeOrderChangeStore.Load().GroupBy(x => x.Date).OrderBy(x => x.Key))
        {
            await SendDailySectionAsync(settings, date, $"pending-{group.Key:yyyyMMdd}",
                $"⏳ Հաստատման սպասող — {group.Key:dd.MM.yyyy} · {group.Count()} փոփոխություն\nՄանրամասները և հաստատման կոճակները՝ «հաստատումներ» հրամանով։");
        }
    }

    private async Task SendDailySectionAsync(TelegramBotSettings settings, DateOnly date, string section, string text)
    {
        var index = 0;
        foreach (var part in TelegramTextSections.Split(text))
        {
            var key = $"{settings.BotToken.Split(':')[0]}:{settings.ChatId}:{date:yyyyMMdd}:{section}:{index++}";
            if (_operationsStateStore.Load().DeliveredSections.Contains(key)) continue;
            await TelegramBotClient.SendMessageAsync(settings, part);
            var state = _operationsStateStore.Load();
            state.DeliveredSections.Add(key);
            _operationsStateStore.Save(state);
        }
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
                _telegramBotSettingsStore.Save(_telegramBotSettingsStore.Load() with { LastMorningBriefDate = today });
            }
            if (settings.LastEveningDashboardDate != today && now >= new TimeSpan(23, 30, 0))
            {
                await SendTelegramDashboardAsync(settings, today);
                settings = settings with { LastEveningDashboardDate = today };
                _telegramBotSettingsStore.Save(_telegramBotSettingsStore.Load() with { LastEveningDashboardDate = today });
            }
            if (settings.LastCashFlowOpinionDate != today && now >= new TimeSpan(23, 40, 0))
            {
                await SendTelegramCashFlowOpinionAsync(settings, today);
                settings = settings with { LastCashFlowOpinionDate = today };
                _telegramBotSettingsStore.Save(_telegramBotSettingsStore.Load() with { LastCashFlowOpinionDate = today });
            }
            if (settings.LastEveningOperationsDate != today && now >= new TimeSpan(21, 0, 0))
            {
                await SendEveningOperationsAsync(settings, today);
                settings = settings with { LastEveningOperationsDate = today };
                _telegramBotSettingsStore.Save(_telegramBotSettingsStore.Load() with { LastEveningOperationsDate = today });
            }
            if (settings.LastDeliveryConfirmationDate != today && now >= new TimeSpan(22, 0, 0))
            {
                await SendTelegramDraftAsync(settings, today.AddDays(2));
                settings = settings with { LastDeliveryConfirmationDate = today };
                _telegramBotSettingsStore.Save(_telegramBotSettingsStore.Load() with { LastDeliveryConfirmationDate = today });
            }
            var latestSettings = _telegramBotSettingsStore.Load();
            _telegramBotSettingsStore.Save(latestSettings with
            {
                LastMorningBriefDate = settings.LastMorningBriefDate,
                LastEveningDashboardDate = settings.LastEveningDashboardDate,
                LastEveningOperationsDate = settings.LastEveningOperationsDate,
                LastDeliveryConfirmationDate = settings.LastDeliveryConfirmationDate,
                LastCashFlowOpinionDate = settings.LastCashFlowOpinionDate
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
        await SyncCashDocumentsAsync(date);
        var snapshot = await App.Services.DataProvider.GetSnapshotAsync(date);
        snapshot = MergeImportedSuppliers(snapshot);
        snapshot = await ApplyAvailableFundsAsync(snapshot);
        return DecisionEngine.Evaluate(snapshot, PlannedSuppliersFor(date), _requiredPayments);
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
        var unplannedReceipts = snapshot.SupplierMovements
            .Where(x => x.Date == date && x.OrderAmount > 0m && !suppliers.Any(plan => SupplierNamesMatch(plan.Supplier, x.Supplier)))
            .OrderBy(x => x.Supplier)
            .ToList();

        var message = new System.Text.StringBuilder();
        message.AppendLine($"📊 Գլխավոր էջ — {date:dd.MM.yyyy}");
        message.AppendLine();
        message.AppendLine($"Հասանելի միջոցներ՝ {funds.Total:N0} ֏");
        message.AppendLine($"Կանխիկ՝ {funds.Cash:N0} ֏ · Բանկ՝ {funds.Bank:N0} ֏");
        message.AppendLine($"Այսօրվա վճարումներ՝ {plannedPayments:N0} ֏");
        message.AppendLine($"Այսօրվա վաճառք՝ {snapshot.Sales.SalesDisplay}");
        message.AppendLine($"Շահույթ (վաճառք − ինքնարժեք)՝ {snapshot.Sales.ProfitDisplay}");
        message.AppendLine($"Կտրոններ՝ {snapshot.Sales.ReceiptCount:N0} · Միջին չեկ՝ {snapshot.Sales.AverageReceipt:N0} ֏");
        message.AppendLine($"Կրիտիկական ռիսկեր՝ {critical.Count}");
        foreach (var risk in critical.Take(3)) message.AppendLine($"• {risk.Title}");
        if (unplannedReceipts.Count > 0)
        {
            message.AppendLine();
            message.AppendLine("⚠ Չպլանավորված մատակարարումներ");
            foreach (var receipt in unplannedReceipts)
                message.AppendLine($"• {receipt.Supplier} — ստացում՝ {receipt.OrderAmount:N0} ֏");
        }
        message.AppendLine();
        message.AppendLine("Առավոտյան գործողությունների ցանկի համար գրեք՝ առավոտ");
        var dateCode = date.ToString("yyyy-MM-dd");
        var buttons = new IReadOnlyList<TelegramInlineButton>[]
        {
            new[] { new TelegramInlineButton("📊 Գլխավոր", $"dashboard:{dateCode}"), new TelegramInlineButton("🌅 Առավոտ", $"morning:{dateCode}") },
            new[] { new TelegramInlineButton("💳 Վճարումներ", $"payments:{dateCode}"), new TelegramInlineButton("📍 Ընթացիկ դրություն", $"currentstatus:{dateCode}") },
            new[] { new TelegramInlineButton("📈 Խորը վերլուծություն", $"currentanalysis:{dateCode}") }
        };
        await TelegramBotClient.SendMessageAsync(settings, message.ToString(), buttons);
    }

    private async Task RegisterSalaryFromEmployeeTelegramAsync(EmployeeTelegramIncomingMessage message, TelegramBotSettings replySettings)
    {
        if (SalaryTelegramParser.TryParseBatch(message.Text.Trim(), DateOnly.FromDateTime(DateTime.Today), out var batchDate, out var batchLines))
        {
            await RegisterSalaryBatchAsync(replySettings, batchDate, batchLines, message.DisplayName);
            return;
        }
        if (!SalaryTelegramParser.TryParse(message.Text.Trim(), DateOnly.FromDateTime(DateTime.Today), out var date, out var employee, out var amount, out var note))
        {
            await TelegramBotClient.SendMessageAsync(replySettings, "Գրեք այս ձևով՝\nաշխատավարձ / Աշխատողի անուն / 12000 / նշում\n\nԿամ մի քանի հոգու համար՝\nաշխատավարձ 02.09.2026\nԼուսինե / 8500\nՆարինե / 9000");
            return;
        }
        var knownEmployees = _salaryAccruals.Select(x => x.Employee).Concat(_salaryPayments.Select(x => x.Employee));
        var matched = SalaryEmployeeMatcher.FindKnown(employee, knownEmployees);
        var ownerSettings = _telegramBotSettingsStore.Load();
        if (matched is null)
        {
            _pendingSalaryEmployees.Add(new PendingSalaryEmployee(Guid.NewGuid(), new SalaryAccrual(Guid.NewGuid(), date, employee, amount, note, DateTime.Now), DateTime.Now));
            _pendingSalaryEmployeeStore.Save(_pendingSalaryEmployees);
            await TelegramBotClient.SendMessageAsync(replySettings, $"🟡 «{employee}»-ի աշխատավարձը ուղարկվել է տնօրենի հաստատման։");
            if (ownerSettings.IsConfigured && !string.IsNullOrWhiteSpace(ownerSettings.ChatId))
                await TelegramBotClient.SendMessageAsync(ownerSettings, $"🟡 Աշխատակից {message.DisplayName}-ը ավելացրել է նոր աշխատող՝ {employee}։\n{date:dd.MM.yyyy} · {amount:N0} ֏\nՀաստատեք «Աշխատավարձեր» բաժնից։");
            return;
        }
        var repeated = _salaryAccruals.Count(x => x.Date == date && string.Equals(x.Employee, matched, StringComparison.OrdinalIgnoreCase));
        _salaryAccruals.Add(new SalaryAccrual(Guid.NewGuid(), date, matched, amount, note, DateTime.Now));
        _salaryStore.SaveAccruals(_salaryAccruals);
        await TelegramBotClient.SendMessageAsync(replySettings, $"✅ Գրանցվեց՝ {matched}, {date:dd.MM.yyyy}, {amount:N0} ֏։");
        if (ownerSettings.IsConfigured && !string.IsNullOrWhiteSpace(ownerSettings.ChatId) && repeated > 0)
            await TelegramBotClient.SendMessageAsync(ownerSettings, $"⚠️ Կրկնվող աշխատավարձային գրառում\nԱշխատող՝ {matched}\nՕր՝ {date:dd.MM.yyyy}\nՆոր գումար՝ {amount:N0} ֏\nՆույն օրվա նախկին գրառումներ՝ {repeated}։ Ստուգեք «Աշխատավարձեր» բաժնից։");
        if (_currentPage == "Salaries") await LoadAsync("Salaries");
    }

    private async Task RegisterSalaryFromTelegramAsync(TelegramBotSettings settings, string message)
    {
        if (SalaryTelegramParser.TryParseBatch(message, DateOnly.FromDateTime(DateTime.Today), out var batchDate, out var batchLines))
        {
            await RegisterSalaryBatchAsync(settings, batchDate, batchLines, "Տնօրեն");
            return;
        }
        if (!SalaryTelegramParser.TryParse(message, DateOnly.FromDateTime(DateTime.Today), out var date, out var employee, out var amount, out var note))
        {
            await TelegramBotClient.SendMessageAsync(settings, "Չհաջողվեց կարդալ աշխատավարձի տվյալը։ Գրեք այս ձևով՝\n\nաշխատավարձ / Աշխատողի անուն / 12000 / նշում\n\nԿամ մի քանի հոգու համար՝\nաշխատավարձ 02.09.2026\nԼուսինե / 8500\nՆարինե / 9000");
            return;
        }
        var knownEmployees = _salaryAccruals.Select(x => x.Employee).Concat(_salaryPayments.Select(x => x.Employee));
        var matchedEmployee = SalaryEmployeeMatcher.FindKnown(employee, knownEmployees);
        if (matchedEmployee is null && knownEmployees.Any())
        {
            var proposed = new SalaryAccrual(Guid.NewGuid(), date, employee, amount, note, DateTime.Now);
            _pendingSalaryEmployees.Add(new PendingSalaryEmployee(Guid.NewGuid(), proposed, DateTime.Now));
            _pendingSalaryEmployeeStore.Save(_pendingSalaryEmployees);
            await TelegramBotClient.SendMessageAsync(settings, $"🟡 Նոր աշխատող «{employee}» — {amount:N0} ֏։ Տվյալը սպասում է տնօրենի հաստատմանը և դեռ չի ներառվել աշխատավարձային հաշվարկում։");
            if (_currentPage == "Salaries") await LoadAsync("Salaries");
            return;
        }
        if (matchedEmployee is not null) employee = matchedEmployee;
        var repeated = _salaryAccruals.Count(x => x.Date == date && string.Equals(x.Employee, employee, StringComparison.OrdinalIgnoreCase));
        _salaryAccruals.Add(new SalaryAccrual(Guid.NewGuid(), date, employee, amount, note, DateTime.Now));
        _salaryStore.SaveAccruals(_salaryAccruals);
        var weekStart = SalaryRules.WeekStart(date);
        var currentTotal = SalaryRules.AccruedForWeek(_salaryAccruals, weekStart, employee);
        await TelegramBotClient.SendMessageAsync(settings, $"✅ Գրանցվեց։\n\nԱշխատող՝ {employee}\nՕր՝ {date:dd.MM.yyyy}\nՕրական աշխատավարձ՝ {amount:N0} ֏\nԱյս շաբաթ գեներացված՝ {currentTotal:N0} ֏");
        if (repeated > 0)
            await TelegramBotClient.SendMessageAsync(settings, $"⚠️ Նույն աշխատողի համար {date:dd.MM.yyyy}-ին արդեն կար {repeated} գրառում։ Այս նոր {amount:N0} ֏ գումարն ավելացվել է առանձին տողով։ Ստուգեք «Աշխատավարձեր» բաժնից։");
        if (_currentPage == "Salaries") await LoadAsync("Salaries");
    }

    private async Task RegisterSalaryBatchAsync(TelegramBotSettings replySettings, DateOnly date, IReadOnlyList<SalaryBatchLine> lines, string reporter)
    {
        var knownEmployees = _salaryAccruals.Select(x => x.Employee).Concat(_salaryPayments.Select(x => x.Employee)).ToList();
        var registered = new List<(string Employee, decimal Amount)>();
        var pending = new List<(string Employee, decimal Amount)>();
        var repeated = new List<string>();

        foreach (var line in lines)
        {
            var matched = SalaryEmployeeMatcher.FindKnown(line.Employee, knownEmployees);
            if (matched is null && knownEmployees.Any())
            {
                _pendingSalaryEmployees.Add(new PendingSalaryEmployee(Guid.NewGuid(), new SalaryAccrual(Guid.NewGuid(), date, line.Employee, line.Amount, line.Note, DateTime.Now), DateTime.Now));
                pending.Add((line.Employee, line.Amount));
                continue;
            }

            var employee = matched ?? line.Employee;
            var previous = _salaryAccruals.Count(x => x.Date == date && string.Equals(x.Employee, employee, StringComparison.OrdinalIgnoreCase));
            _salaryAccruals.Add(new SalaryAccrual(Guid.NewGuid(), date, employee, line.Amount, line.Note, DateTime.Now));
            if (!knownEmployees.Contains(employee, StringComparer.OrdinalIgnoreCase)) knownEmployees.Add(employee);
            registered.Add((employee, line.Amount));
            if (previous > 0) repeated.Add(employee);
        }

        if (registered.Count > 0) _salaryStore.SaveAccruals(_salaryAccruals);
        if (pending.Count > 0) _pendingSalaryEmployeeStore.Save(_pendingSalaryEmployees);

        var text = new System.Text.StringBuilder();
        text.AppendLine($"✅ Աշխատավարձերի ցանկ — {date:dd.MM.yyyy}");
        foreach (var item in registered) text.AppendLine($"• {item.Employee} — {item.Amount:N0} ֏");
        if (registered.Count > 0) text.AppendLine($"Ընդամենը գրանցվեց՝ {registered.Sum(x => x.Amount):N0} ֏");
        if (pending.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("🟡 Տնօրենի հաստատման սպասող նոր աշխատողներ՝");
            foreach (var item in pending) text.AppendLine($"• {item.Employee} — {item.Amount:N0} ֏");
        }
        if (repeated.Count > 0)
            text.AppendLine($"⚠️ Կրկնվող գրառում՝ {string.Join(", ", repeated.Distinct(StringComparer.OrdinalIgnoreCase))}։ Ստուգեք «Աշխատավարձեր» բաժնից։");
        await TelegramBotClient.SendMessageAsync(replySettings, text.ToString());

        var owner = _telegramBotSettingsStore.Load();
        if (pending.Count > 0 && owner.IsConfigured && !string.IsNullOrWhiteSpace(owner.ChatId) && owner.ChatId != replySettings.ChatId)
        {
            var pendingText = string.Join("\n", pending.Select(x => $"• {x.Employee} — {x.Amount:N0} ֏"));
            await TelegramBotClient.SendMessageAsync(owner, $"🟡 {reporter}-ը ավելացրել է նոր աշխատողներ։\nՕր՝ {date:dd.MM.yyyy}\n{pendingText}\n\nՀաստատեք «Աշխատավարձեր» բաժնից։");
        }
        if (_currentPage == "Salaries") await LoadAsync("Salaries");
    }

    /// <summary>
    /// A point-in-time view of the selected operational day.  Unlike the
    /// seven-day opinion this uses today's actual sales and separates what is
    /// completed from what remains actionable before the day is closed.
    /// </summary>
    private async Task SendTelegramCurrentDayAnalysisAsync(TelegramBotSettings settings, DateOnly date)
    {
        var snapshot = await TelegramSnapshotAsync(date);
        var plan = PlannedSuppliersFor(date);
        var employeeActions = _employeeSupplierActionStore.Load();
        var ownerChanges = _supplierStatusChangeStore.Load();
        var confirmed = plan.Where(x => IsSupplierReceiptConfirmed(x, employeeActions, ownerChanges)).ToList();
        var pending = plan.Where(x => !IsSupplierReceiptConfirmed(x, employeeActions, ownerChanges))
            .Where(x => x.OrderAmount != 0m || x.PaymentAmount != 0m || x.OldDebtPayment != 0m)
            .ToList();

        var plannedSupplierPayments = plan.Sum(x => x.PaymentAmount + x.OldDebtPayment);
        // Prefer factual payment records. A confirmed supplier line is only a
        // fallback when no actual payment has been registered for that supplier.
        // This keeps partial payments and manually chosen 0002/bank payments
        // correct in the point-in-time report.
        var supplierNamesForDay = plan.Select(x => x.Supplier).ToList();
        var actualSupplierPayments = AllActualPayments()
            .Where(x => x.PaidDate == date)
            .Where(x => supplierNamesForDay.Any(name => SupplierNamesMatch(name, x.Recipient)))
            .ToList();
        var confirmedFallback = confirmed
            .Where(row => !actualSupplierPayments.Any(payment => SupplierNamesMatch(payment.Recipient, row.Supplier)))
            .Sum(row => row.PaymentAmount + row.OldDebtPayment);
        var completedSupplierPayments = actualSupplierPayments.Sum(x => x.Amount) + confirmedFallback;
        var remainingSupplierPayments = plan.Sum(row =>
        {
            var factual = actualSupplierPayments
                .Where(payment => SupplierNamesMatch(payment.Recipient, row.Supplier))
                .Sum(payment => payment.Amount);
            var fallback = factual == 0m && confirmed.Any(confirmedRow => SupplierNamesMatch(confirmedRow.Supplier, row.Supplier))
                ? row.PaymentAmount + row.OldDebtPayment
                : 0m;
            return Math.Max(0m, row.PaymentAmount + row.OldDebtPayment - factual - fallback);
        });
        var plannedOther = _requiredPayments.Where(x => RequiredPaymentRules.AppliesOn(x, date)).Sum(x => x.Amount)
            + _manualPaymentChanges.Where(x => x.PlannedDate == date).Sum(x => x.Amount)
            + snapshot.Payments.Where(x => x.DueDate == date && !plan.Any(row => SupplierNamesMatch(row.Supplier, x.Supplier))).Sum(x => x.Amount)
            + PlannedSundayPayroll(date);
        var supplierNames = plan.Select(x => x.Supplier).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var completedOther = AllActualPayments()
            .Where(x => x.PaidDate == date && !supplierNames.Contains(x.Recipient))
            .Sum(x => x.Amount)
            + _salaryPayments.Where(x => x.PaidDate == date).Sum(x => x.Amount);
        var expectedRemaining = Math.Max(0m, remainingSupplierPayments) + Math.Max(0m, plannedOther - completedOther);
        var funds = _lastFunds ?? FundsForOpening(snapshot.Cash);

        var text = new System.Text.StringBuilder();
        text.AppendLine($"📍 Ընթացիկ վերլուծություն — {date:dd.MM.yyyy}");
        text.AppendLine($"Այս պահի վաճառք՝ {snapshot.Sales.SalesDisplay}");
        text.AppendLine($"Հասանելի միջոցներ՝ {funds.Total:N0} ֏ (կանխիկ՝ {funds.Cash:N0} ֏, բանկ՝ {funds.Bank:N0} ֏)");
        text.AppendLine();
        text.AppendLine($"Պլանավորված ծախսեր՝ {plannedSupplierPayments + plannedOther:N0} ֏");
        text.AppendLine($"Կատարված ծախսեր՝ {completedSupplierPayments + completedOther:N0} ֏");
        text.AppendLine($"Մնացած սպասվող ծախսեր՝ {expectedRemaining:N0} ֏");
        text.AppendLine($"Մնացած պլանային վճարումներից հետո մնացորդ՝ {funds.Total - expectedRemaining:N0} ֏");

        if (pending.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Սպասվող մատակարարներ / գործողություններ՝");
            foreach (var row in pending)
            {
                var payment = row.PaymentAmount + row.OldDebtPayment;
                text.AppendLine($"• {row.Supplier} — պատվեր {row.OrderAmount:N0} ֏, վճարում {payment:N0} ֏");
            }
        }
        else text.AppendLine("✅ Մատակարարների մասով մնացած գործողություն չկա։");

        if (funds.Total < expectedRemaining)
            text.AppendLine("⚠️ Առկա միջոցներն ու այս պահի վաճառքը բավարար չեն մնացած պլանավորված ծախսերի համար։ Վճարումները վերանայեք մինչև օրվա ավարտը։");
        else text.AppendLine("✅ Այս պահի տվյալներով մնացած պլանավորված ծախսերը կատարելի են։");

        await TelegramBotClient.SendMessageAsync(settings, text.ToString());
    }

    /// <summary>
    /// Short, point-in-time owner status.  Unlike the longer cash-flow opinion,
    /// this message contains only the operational facts needed to decide what
    /// still has to be done today.  A second button exposes the row-level data.
    /// </summary>
    private async Task SendTelegramCurrentStatusAsync(TelegramBotSettings settings, DateOnly date)
    {
        var snapshot = await TelegramSnapshotAsync(date);
        var plan = PlannedSuppliersFor(date);
        var employeeActions = _employeeSupplierActionStore.Load();
        var ownerChanges = _supplierStatusChangeStore.Load();
        var confirmed = plan.Where(x => IsSupplierReceiptConfirmed(x, employeeActions, ownerChanges)).ToList();
        var supplierNames = plan.Select(x => x.Supplier).ToList();
        var actualPayments = AllActualPayments().Where(x => x.PaidDate == date).ToList();
        var actualSupplierPayments = actualPayments
            .Where(x => supplierNames.Any(name => SupplierNamesMatch(name, x.Recipient))).ToList();

        var plannedSupplierPayments = plan.Sum(x => x.PaymentAmount + x.OldDebtPayment);
        var confirmedFallback = confirmed
            .Where(row => !actualSupplierPayments.Any(payment => SupplierNamesMatch(payment.Recipient, row.Supplier)))
            .Sum(row => row.PaymentAmount + row.OldDebtPayment);
        var completedSupplierPayments = actualSupplierPayments.Sum(x => x.Amount) + confirmedFallback;

        var plannedOther = _requiredPayments.Where(x => RequiredPaymentRules.AppliesOn(x, date)).Sum(x => x.Amount)
            + _manualPaymentChanges.Where(x => x.PlannedDate == date).Sum(x => x.Amount)
            + snapshot.Payments.Where(x => x.DueDate == date && !supplierNames.Any(name => SupplierNamesMatch(name, x.Supplier))).Sum(x => x.Amount)
            + PlannedSundayPayroll(date);
        var completedOther = actualPayments
            .Where(x => !supplierNames.Any(name => SupplierNamesMatch(name, x.Recipient))).Sum(x => x.Amount);

        BankSalesBreakdown nonCash = BankSalesBreakdown.Empty;
        var nonCashAvailable = false;
        if (App.Services.DataProvider is IFundsMovementProvider fundsProvider)
        {
            try { nonCash = await fundsProvider.GetNonCashSalesAsync(date, date); nonCashAvailable = App.Services.DataProvider is not EmptyDataProvider; }
            catch { /* Current status remains useful even if the optional ECR breakdown is temporarily unavailable. */ }
        }
        var nonCashSales = nonCash.BankReport + nonCash.AmeriabankPos099 + nonCash.Idram;
        var cashSaleRow = _cashDocuments.FirstOrDefault(x => x.Date == date && x.Type == "ecr-cash-sales");
        var plannedTotal = plannedSupplierPayments + plannedOther;
        var completedTotal = completedSupplierPayments + completedOther;
        var remaining = plan.Sum(row => PaymentReconciliation.Remaining(row.PaymentAmount + row.OldDebtPayment,
            actualSupplierPayments.Where(x => SupplierNamesMatch(x.Recipient, row.Supplier)).Sum(x => x.Amount)))
            + PaymentReconciliation.Remaining(plannedOther, completedOther);
        var paidSuppliers = plan.Count(row => actualSupplierPayments.Any(payment => SupplierNamesMatch(payment.Recipient, row.Supplier)) ||
            (confirmed.Any(done => SupplierNamesMatch(done.Supplier, row.Supplier)) && row.PaymentAmount + row.OldDebtPayment > 0m));

        var text = new System.Text.StringBuilder();
        text.AppendLine($"📍 Ընթացիկ դրություն — {date:dd.MM.yyyy}");
        text.AppendLine($"Վաճառք՝ {snapshot.Sales.SalesDisplay}");
        text.AppendLine($"Կանխիկ վաճառք՝ {(cashSaleRow is null ? "Տվյալ չկա" : $"{cashSaleRow.Amount:N0} ֏")} · անկանխիկ՝ {(nonCashAvailable ? $"{nonCashSales:N0} ֏" : "Տվյալ չկա")}");
        if (_cashSyncStatus?.Contains("⚠") == true) text.AppendLine(_cashSyncStatus);
        text.AppendLine();
        text.AppendLine($"Օրվա պատվերներ՝ {plan.Sum(x => x.OrderAmount):N0} ֏ ({plan.Count} մատակարար)");
        text.AppendLine($"Այլ վճարումներ՝ {plannedOther:N0} ֏");
        text.AppendLine($"Եկած/հաստատված մատակարարներ՝ {confirmed.Count}/{plan.Count}");
        text.AppendLine($"Կատարված վճարումներ՝ {completedTotal:N0} ֏ ({paidSuppliers} մատակարար)");
        text.AppendLine($"Մնացած վճարման՝ {remaining:N0} ֏");

        if (nonCashSales > snapshot.Sales.SalesAmount)
            text.AppendLine("ℹ️ Անկանխիկ վաճառքի տվյալը ՀԾ վճարման հաշվետվությունից է և վերանայման կարիք ունի, քանի որ այն գերազանցում է օրվա ընդհանուր վաճառքը։ ");
        if (remaining == 0m) text.AppendLine("✅ Այս պահի պլանավորված վճարումները կատարված են։ ");

        var dateCode = date.ToString("yyyy-MM-dd");
        var buttons = new IReadOnlyList<TelegramInlineButton>[]
        {
            new[] { new TelegramInlineButton("🔎 Մանրամասն", $"currentdetail:{dateCode}") },
            new[] { new TelegramInlineButton("💳 Վճարումներ", $"payments:{dateCode}"), new TelegramInlineButton("📈 Խորը վերլուծություն", $"currentanalysis:{dateCode}") }
        };
        await TelegramBotClient.SendMessageAsync(settings, text.ToString(), buttons);
    }

    private async Task SendTelegramCurrentStatusDetailsAsync(TelegramBotSettings settings, DateOnly date)
    {
        var snapshot = await TelegramSnapshotAsync(date);
        var plan = PlannedSuppliersFor(date);
        var employeeActions = _employeeSupplierActionStore.Load();
        var ownerChanges = _supplierStatusChangeStore.Load();
        var actualPayments = AllActualPayments().Where(x => x.PaidDate == date).ToList();
        var supplierNames = plan.Select(x => x.Supplier).ToList();
        var text = new System.Text.StringBuilder();
        text.AppendLine($"🔎 Մանրամասն — {date:dd.MM.yyyy}");
        text.AppendLine();
        text.AppendLine("Մատակարարներ");
        var rows = plan.Where(x => x.OrderAmount != 0m || x.PaymentAmount != 0m || x.OldDebtPayment != 0m ||
                IsSupplierReceiptConfirmed(x, employeeActions, ownerChanges))
            .OrderBy(x => x.Supplier).ToList();
        if (rows.Count == 0) text.AppendLine("• Գործողություն ունեցող մատակարար չկա։ ");
        foreach (var row in rows)
        {
            var actual = actualPayments.Where(x => SupplierNamesMatch(x.Recipient, row.Supplier)).Sum(x => x.Amount);
            var received = IsSupplierReceiptConfirmed(row, employeeActions, ownerChanges) ? "եկել է" : "սպասվում է";
            text.AppendLine($"• {row.Supplier} — պատվեր {row.OrderAmount:N0} ֏ | վճարում {row.PaymentAmount:N0} ֏ | հին {row.OldDebtPayment:N0} ֏ | {received}{(actual > 0m ? $" | փաստացի՝ {actual:N0} ֏" : string.Empty)}");
        }

        var other = new List<(string Category, string Name, decimal Amount, string Note)>();
        other.AddRange(snapshot.Payments.Where(x => x.DueDate == date && !supplierNames.Any(name => SupplierNamesMatch(name, x.Supplier)))
            .Select(x => ("Այլ", x.Supplier, x.Amount, x.Reason)));
        other.AddRange(_requiredPayments.Where(x => RequiredPaymentRules.AppliesOn(x, date))
            .Select(x => (x.Category, x.Name, x.Amount, x.Note)));
        other.AddRange(_manualPaymentChanges.Where(x => x.PlannedDate == date)
            .Select(x => ("Ձեռքով", x.Supplier, x.Amount, x.Reason)));
        var payroll = PlannedSundayPayroll(date);
        if (payroll > 0m) other.Add(("Աշխատավարձ", "Շաբաթվա աշխատավարձեր", payroll, ""));
        text.AppendLine();
        text.AppendLine("Այլ վճարումներ");
        if (other.Count == 0) text.AppendLine("• Այլ պլանավորված վճարում չկա։ ");
        foreach (var item in other)
            text.AppendLine($"• {item.Category} · {item.Name} — {item.Amount:N0} ֏{(string.IsNullOrWhiteSpace(item.Note) ? string.Empty : $" ({item.Note})")}");

        await TelegramBotClient.SendMessageAsync(settings, text.ToString());
    }

    private async Task SendTelegramCashFlowOpinionAsync(TelegramBotSettings settings, DateOnly startDate)
    {
        var policy = _cashFlowPolicyStore.LoadOrCreate();
        var snapshot = await TelegramSnapshotAsync(startDate);
        var funds = await CalculateFundsAsync(startDate.AddDays(-1));
        var historicalSales = 0m;
        var hasHistory = false;
        if (App.Services.DataProvider is IBusinessSummaryProvider provider)
        {
            try
            {
                var history = await provider.GetBusinessSummaryAsync(startDate.AddDays(-7), startDate.AddDays(-1));
                historicalSales = history.Sales.SalesAmount / 7m;
                hasHistory = history.Sales.SalesAvailable;
            }
            catch { /* The message will explicitly say that a reliable sales forecast is unavailable. */ }
        }

        decimal MandatoryPayments(DateOnly day) =>
            _requiredPayments.Where(x => RequiredPaymentRules.AppliesOn(x, day)).Sum(x => x.Amount)
            + _manualPaymentChanges.Where(x => x.PlannedDate == day).Sum(x => x.Amount)
            + snapshot.Payments.Where(x => x.DueDate == day && x.IsMandatory).Sum(x => x.Amount)
            + PlannedSundayPayroll(day);

        var actualToday = snapshot.Sales.SalesAvailable && startDate == DateOnly.FromDateTime(DateTime.Today) ? snapshot.Sales.SalesAmount : (decimal?)null;
        var analysis = CashFlowPlanner.Build(startDate, funds.Total, historicalSales, hasHistory, actualToday, policy.MinimumReserve,
            day => PlannedSuppliersFor(day), MandatoryPayments);

        var text = new System.Text.StringBuilder();
        text.AppendLine($"💬 Դրամական հոսքի կարծիք — {startDate:dd.MM.yyyy}–{startDate.AddDays(6):dd.MM.yyyy}");
        text.AppendLine($"Մեկնարկային հասանելի միջոցներ՝ {analysis.OpeningBalance:N0} ֏");
        if (hasHistory)
            text.AppendLine($"Վաճառքի կանխատեսման հիմք՝ նախորդ 7 օրվա միջին՝ {analysis.HistoricalDailySales:N0} ֏ / օր");
        else
            text.AppendLine("⚠ Վաճառքի 7-օրյա փաստացի պատմությունը բավարար չէ։ Կանխատեսվող վաճառքը 0 է ցուցադրված, ոչ թե ենթադրված թիվ։");
        text.AppendLine();
        text.AppendLine($"Շաբաթվա կանխատեսվող վաճառք՝ {analysis.WeekExpectedSales:N0} ֏");
        text.AppendLine($"Շաբաթվա մատակարարների վճարումներ՝ {analysis.WeekSupplierPayments:N0} ֏");
        text.AppendLine($"Շաբաթվա պարտադիր/այլ վճարումներ՝ {analysis.WeekMandatoryPayments:N0} ֏");
        text.AppendLine($"Պլանային պատվերներ՝ {analysis.WeekOrders:N0} ֏ (սա պարտքի/պաշարի ցուցանիշ է, ոչ թե ամբողջությամբ կանխիկ ելք)");
        text.AppendLine($"Շաբաթվա կանխատեսվող վերջի մնացորդ՝ {analysis.WeekClosingBalance:N0} ֏");

        foreach (var day in analysis.Days)
            text.AppendLine($"• {day.Date:dd.MM}: վաճառք {day.ExpectedSales:N0} ֏ | վճարումներ {day.PlannedPayments:N0} ֏ | մնացորդ {day.ClosingBalance:N0} ֏");

        text.AppendLine($"Պաշտպանական նվազագույն մնացորդ՝ {policy.MinimumReserve:N0} ֏");
        text.AppendLine("Չտեղափոխվող վճարումներ՝ կոմունալ, վարձավճար, Տոբակ, Նեվիս, Վինկո, Ֆիլիպ Մորիս, Մարիաննա, Ալյուր։");

        if (!hasHistory)
        {
            text.AppendLine();
            text.AppendLine("Խորհուրդ՝ մինչև վաճառքի պատմությունը ՀԾ-ից ամբողջությամբ հասանելի լինի, խոշոր վճարումները հաստատեք ֆինանսական պատասխանատուի հետ։");
        }
        else if (analysis.DeficitDays.Count == 0)
        {
            text.AppendLine();
            text.AppendLine("✅ Ըստ ընթացիկ պլանի շաբաթվա դրամական հոսքը հավասարակշռված է։");
        }
        else if (analysis.WeekClosingBalance >= policy.MinimumReserve)
        {
            var firstDeficit = analysis.DeficitDays[0];
            var flexible = PlannedSuppliersFor(firstDeficit.Date)
                .Where(x => x.PaymentAmount + x.OldDebtPayment > 0m)
                .Where(x => !policy.NonMovableSuppliers.Any(rule => CashFlowSupplierRuleMatches(rule, x.Supplier)))
                .OrderBy(x => snapshot.Suppliers.FirstOrDefault(s => SupplierNamesMatch(s.Name, x.Supplier))?.PriorityScore ?? 50)
                .ToList();
            text.AppendLine();
            text.AppendLine($"🟡 {firstDeficit.Date:dd.MM}-ին կանխատեսվող մնացորդը պաշտպանական շեմից պակաս է {policy.MinimumReserve - firstDeficit.ClosingBalance:N0} ֏-ով։ Շաբաթվա վերջում շեմը վերականգնվում է։");
            if (flexible.Count == 0)
                text.AppendLine("Այս օրվա համար տեղափոխելի մատակարարային վճարում չկա․ անհրաժեշտ է տնօրենի որոշում կամ ֆինանսական մասնագետի կարծիք։ ");
            else
                text.AppendLine("Առաջարկվող ճկուն վճարները՝ " + string.Join(", ", flexible.Take(3).Select(x => x.Supplier)) + "։");
            text.AppendLine($"🟡 {firstDeficit.Date:dd.MM}-ին օրվա դրամական պակասը՝ {Math.Abs(firstDeficit.ClosingBalance):N0} ֏, բայց շաբաթվա վերջում գումարը բավարար է։");
            text.AppendLine("Առաջարկ՝ պարտադիր վճարումները չտեղափոխել։ Նախ դիտարկել միայն մատակարարների ճկուն վճարումները՝");
            foreach (var row in flexible.Take(3))
                text.AppendLine($"• {row.Supplier} — մինչև {row.PaymentAmount + row.OldDebtPayment:N0} ֏, միայն մատակարարի հետ համաձայնեցնելուց հետո՝ 1 օր տեղափոխել։");
        }
        else
        {
            text.AppendLine();
            text.AppendLine($"🔴 Շաբաթվա ավարտին պաշտպանական շեմից պակաս է {policy.MinimumReserve - analysis.WeekClosingBalance:N0} ֏։ Չփոխել կոմունալի, վարձավճարի և պաշտպանված մատակարարների վճարումները։");
            text.AppendLine($"🔴 Շաբաթվա կանխատեսվող պակասը՝ {Math.Abs(analysis.WeekClosingBalance):N0} ֏։ Միայն վճարումների տեղափոխումը բավարար չէ։");
            text.AppendLine("Առաջարկ՝ չհաստատել ոչ պարտադիր գնումներ, մատակարարների հետ վերանայել ժամկետները և դիմել հաշվապահ/ֆինանսական մասնագետի՝ վճարումների ու վաճառքի պլանը հաստատելու համար։");
        }
        await TelegramBotClient.SendMessageAsync(settings, text.ToString());
    }

    private static bool CashFlowSupplierRuleMatches(string rule, string supplier)
    {
        var a = new string(rule.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        var b = new string(supplier.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        return a.Length > 0 && b.Length > 0 && (a.Contains(b) || b.Contains(a));
    }

    private async Task SendTelegramMorningBriefAsync(TelegramBotSettings settings, DateOnly date)
    {
        var snapshot = await TelegramSnapshotAsync(date);
        var suppliers = PlannedSuppliersFor(date)
            .Where(row => HasTelegramSupplierAction(row, date)).OrderBy(x => x.Supplier).ToList();
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
        var plannedSalary = PlannedSundayPayroll(date);
        if (plannedSalary > 0m)
            otherPayments.Add(("Աշխատավարձ", "Շաբաթվա աշխատավարձեր", plannedSalary, "Կիրակի օրվա վճարման ենթակա մնացորդ"));
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

        // The product-level calculation is retained by delivery date. If HTS is
        // temporarily unavailable later, the owner still receives the last plan
        // instead of an empty draft.
        if (proposals.Count > 0) _purchaseProposalStore.Save(deliveryDate, proposals);
        else proposals = _purchaseProposalStore.Load(deliveryDate);

        var supplierPayments = scheduled.Sum(x => x.PaymentAmount + x.OldDebtPayment);
        var otherPayments = _requiredPayments.Where(x => RequiredPaymentRules.AppliesOn(x, deliveryDate)).Sum(x => x.Amount) +
            _manualPaymentChanges.Where(x => x.PlannedDate == deliveryDate).Sum(x => x.Amount);
        var message = new System.Text.StringBuilder();
        message.AppendLine("🤖 AI առաջարկվող պատվերներ՝ վաճառք + պաշար + հաջորդ մատակարարման օր հաշվարկով");
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
        supplierOrderAmounts = supplierOrderAmounts
            .Where(item => item.Amount != 0m ||
                scheduled.Any(row => SupplierNamesMatch(row.Supplier, item.Supplier) && HasTelegramSupplierAction(row, deliveryDate)))
            .ToList();
        if (supplierOrderAmounts.Count == 0 && otherPayments == 0m)
            return;
        message.AppendLine();
        message.AppendLine("```");
        message.AppendLine("Մատակարար      | Առաջարկ | Վճար.  | Հին");
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
        UpdatePresentationNavigation(page);
        UpdateTopActions(page);
        try
        {
            if (_snapshot is null || !_snapshot.Sales.SalesAvailable)
                _snapshot = await App.Services.DataProvider.GetSnapshotAsync(_selectedDate);
            if (App.Services.DataProvider is ExcelDataProvider || page is "CashMovements" or "Dashboard" or "Finance")
                await TrySyncCashDocumentsForSelectedMonthAsync();
        }
        catch (Exception exception)
        {
            // An unavailable report or missing API permission must never close the desktop app.
            // Keep the user working and make the cause visible instead.
            DataSourceStatusText.Text = "Տվյալների աղբյուր՝ ՀԾ հարցման խնդիր. ցուցադրվում են միայն պահպանված փաստացի տվյալները";
            _snapshot = await new EmptyDataProvider().GetSnapshotAsync(_selectedDate);
            MessageBox.Show(
                $"ՀԾ-ից ընտրված օրվա տվյալները չհաջողվեց բեռնել։\n\n{exception.Message}\n\nՑուցադրվում են միայն պահպանված տվյալները։ Հաջորդ թարմացման ժամանակ կապը նորից կփորձարկվի։",
                "ՀԾ API տվյալների բեռնում", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        _snapshot = await ApplyAvailableFundsAsync(MergeImportedSuppliers(_snapshot));
        _snapshot = DecisionEngine.Evaluate(_snapshot, PlanForSelectedDate(), _requiredPayments);
        MergeApiSupplierMovements(_snapshot);
        SubtitleText.Text = $"{_snapshot.Date:dd.MM.yyyy} · Որոշումները պահանջում են ձեր հաստատումը";
        if (!string.IsNullOrWhiteSpace(_cashSyncStatus)) DataSourceStatusText.Text = _cashSyncStatus;
        PageHost.Content = page switch
        {
            "Finance" => Views.Finance(_snapshot, await BuildWeeklyFinancialPlanAsync(_selectedDate), _cashFlowPolicyStore.LoadOrCreate(), ConfigureCashFlowPolicy),
            "Suppliers" => await SupplierEditorViewAsync(_selectedDate),
            "PurchasePlan" => await PurchasePlanViewAsync(),
            "SupplierSales" => await SupplierSalesViewAsync(),
            "CashMovements" => Views.CashMovements(_selectedDate, CashLedgerForSelectedMonth(FundsForOpening(_snapshot.Cash)), CashMovementsForSelectedDate(), _cashSyncStatus, EditCashDay),
            "Salaries" => Views.Salaries(_selectedDate, _salaryAccruals, _salaryPayments, _pendingSalaryEmployees, AddSalaryAccrual, AddSalaryPayment, OpenSalaryEmployee, ApprovePendingSalaryEmployee, RejectPendingSalaryEmployee, RemoveSalaryEmployee),
            "Payments" => Views.Payments(_snapshot, AllActualPayments(), _requiredPayments, PlanForSelectedDate(), _employeeSupplierActionStore.Load(), _salaryPayments, PlannedSundayPayroll(_selectedDate), EditRequiredPayment, DeleteRequiredPayment),
            "Approvals" => Views.Approvals(_pendingEmployeeOrderChangeStore.Load(), ApprovePendingChangeFromDesktopAsync, RejectPendingChangeFromDesktopAsync, ApproveAllPendingChangesFromDesktopAsync, ResolveSupplierFromDesktop, KnownSupplierNames()),
            "DeliverySchedule" => Views.DeliverySchedule(_deliveryPatterns, UpdateSuggestedOrderAmount),
            "Recommendations" => Views.Recommendations(_snapshot, _employeeSupplierActionStore.Load(), _employeeTaskStore.Load(), _employeeTaskActionStore.Load(), _employeeIssueStore.Load()),
            "Summary" => await SummaryViewAsync(),
            _ => Views.Dashboard(_snapshot, AllActualPayments(), _requiredPayments, PlanForSelectedDate(), _employeeSupplierActionStore.Load(), CashSummaryForSelectedDate(), _lastFunds ?? FundsForOpening(_snapshot.Cash), _pendingEmployeeOrderChangeStore.Load().Count, OpenAvailableFunds, () => _ = LoadAsync("Payments"), () => _ = LoadAsync("Recommendations"), () => _ = LoadAsync("Approvals"), DashboardTrend())
        };
        if (PageHost.Content is DependencyObject presentation) PresentationTheme.Apply(presentation);
    }

    // Cash movements are operational records.  They are refreshed from HTS
    // separately from the dashboard reports so a missing cash-report right
    // never makes the whole desktop application fall back to demo data.
    private Task TrySyncCashDocumentsForSelectedMonthAsync() => SyncCashDocumentsAsync(_selectedDate);

    private async Task SyncCashDocumentsAsync(DateOnly date)
    {
        if (App.Services.DataProvider is not ICashDocumentProvider provider) return;
        if (provider is ExcelDataProvider)
        {
            var state = _excelImportStore.Load();
            var allDates = state.Batches.SelectMany(x => x.Dates).ToList();
            var start = allDates.Count == 0 ? date : allDates.Min();
            var end = allDates.Count == 0 ? date : allDates.Max();
            var records = await provider.GetCashDocumentsAsync(start,end);
            _cashDocuments.Clear(); _cashDocuments.AddRange(records);
            var manualNonCashDays=_cashDayOverrideStore.Load().Days.Where(x=>x.NonCash.HasValue).Select(x=>x.Date).ToHashSet();
            var missing = state.Batches.Where(x => x.Kind is "cash" or "sales").SelectMany(x => x.Dates)
                .Where(d => d >= start && d <= date && !state.NonCash.Any(n => n.Date == d) && !manualNonCashDays.Contains(d)).Distinct().Order().ToList();
            _cashSyncStatus = "Excel ռեժիմ․ վերջին ներմուծված տվյալներ։" + (missing.Count == 0 ? "" :
                "\n⚠ Մուտքը վերցված է ամբողջ վաճառքով։ Անկանխիկը դեռ նշված չէ (ժամանակավորապես 0)․ " + string.Join(", ",missing.Select(d => d.ToString("dd.MM"))));
            if (_fundsTransactions.Any(t => TryGetSupplierPayment(t,out var supplier,out _) &&
                CashDocumentImportService.SupplierPaymentRows(records).Any(p=>p.Date==t.Date && p.Amount==t.Amount && SupplierNamesMatch(p.Recipient,supplier))))
                _cashSyncStatus += "\n⚠ Կա նույն մատակարարի նույն գումարով ձեռքով վճարում և ներմուծված փաստաթուղթ․ հնարավոր կրկնումը ճշտեք։ Դրանք ինքնաբերաբար չեն միացվել։";
            return;
        }

        var firstDay = _availableFunds.OpeningMonth is { } openingDate && openingDate <= date
            ? new DateOnly(openingDate.Year, openingDate.Month, 1) : new DateOnly(date.Year, date.Month, 1);
        try
        {
            var documents = (await provider.GetCashDocumentsAsync(firstDay, date)).ToList();
            if (documents.Count == 0)
            {
                _cashSyncStatus = "⚠ ՀԾ-ից տվյալ ժամանակահատվածի դրամարկղային օրդեր չվերադարձավ։ Պահպանված կամ ձեռքով ներմուծված տվյալներն են ցուցադրվում։";
                return;
            }

            // Upsert preserves Excel/XML imports when HTS returns only a
            // partial document set, while still adding every live document.
            _cashDocumentStore.Upsert(documents, _cashDocuments);
            _cashSyncStatus = $"✓ ՀԾ-ից թարմացվել է {documents.Count:N0} դրամարկղային գրանցում ({firstDay:dd.MM}–{date:dd.MM.yyyy})։";
            if (provider is HtsApiDataProvider hts && hts.LastCashWarning is { } warning)
                _cashSyncStatus += "\n⚠ Տվյալները մասնակի են․ " + warning;
            var incomeOrders = _cashDocuments.Where(x => x.Date >= firstDay && x.Date <= date &&
                (x.Type.Contains("cashinput", StringComparison.OrdinalIgnoreCase) || x.Type.Contains("Մուտքի", StringComparison.OrdinalIgnoreCase)) &&
                x.Information.Contains("հասույթ", StringComparison.OrdinalIgnoreCase) &&
                _cashDocuments.Any(s => s.Date == x.Date && s.Type == "ecr-cash-sales" && s.Amount != 0m)).ToList();
            if (incomeOrders.Count > 0)
                _cashSyncStatus += "\n⚠ ՀԴՄ կանխիկի հետ կա նաև հասույթի մուտքի օրդեր․ համադրեք փաստաթղթերը, հնարավոր է նույն գումարի կրկնակի մուտք։";
            if (_fundsTransactions.Any(t => t.Date >= firstDay && t.Date <= date && TryGetSupplierPayment(t, out var name, out _) &&
                CashDocumentImportService.SupplierPaymentRows(_cashDocuments).Any(p => p.Date == t.Date && p.Amount == t.Amount && SupplierNamesMatch(p.Recipient, name))))
                _cashSyncStatus += "\n⚠ Նույն մատակարարի համար կա հավասար ձեռքով վճարում և ՀԾ փաստաթուղթ․ ստուգեք՝ նույն վճարո՞ւմն է, թե երկու առանձին վճարում։";
        }
        catch (Exception exception)
        {
            // Keep the last successfully imported cash ledger available.
            // The user can still work even if this optional HTS report is
            // temporarily unavailable or needs a separate permission.
            _cashSyncStatus = $"⚠ ՀԾ դրամարկղային ներմուծումը չստացվեց․ {exception.Message}";
        }
    }

    private async void ApprovePendingChangeFromDesktopAsync(Guid id)
    {
        var all = _pendingEmployeeOrderChangeStore.Load();
        var target = all.FirstOrDefault(x => x.Id == id);
        if (target is not null && ApprovalWarnings.Duplicate(target,all) &&
            MessageBox.Show("Այս գրանցումն ունի հավանական կրկնում։ Ստուգե՞լ եք և ցանկանում եք հաստատել հենց այս տողը։","Հավանական կրկնում",MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
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
        var all = _pendingEmployeeOrderChangeStore.Load();
        var changes = all.Where(x => !ApprovalWarnings.Unknown(x,KnownSupplierNames()) && !ApprovalWarnings.Duplicate(x,all)).ToList();
        if (changes.Count == 0) return;
        if (MessageBox.Show($"Հաստատե՞լ բոլոր {changes.Count} փոփոխությունները։", "Հաստատումներ", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var settings = _telegramBotSettingsStore.Load();
        foreach (var change in changes) await ApproveEmployeeOrderChangeAsync(settings, change.Id, announceToOwner: false);
        await LoadAsync("Approvals");
    }

    private void UpdateTopActions(string page)
    {
        var dateVisible = page is "Dashboard" or "Finance" or "Suppliers" or "PurchasePlan" or "SupplierSales" or "CashMovements" or "Salaries" or "Payments" or "Recommendations" or "Summary";
        DateLabel.Visibility = dateVisible ? Visibility.Visible : Visibility.Collapsed;
        ViewDatePicker.Visibility = dateVisible ? Visibility.Visible : Visibility.Collapsed;
        ShowDateButton.Visibility = dateVisible ? Visibility.Visible : Visibility.Collapsed;

        ApiButton.Visibility = page == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        TelegramButton.Visibility = page == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        EmployeeTelegramButton.Visibility = page == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        CompletedPaymentButton.Visibility = page is "Dashboard" or "Payments" ? Visibility.Visible : Visibility.Collapsed;
        FundsTransactionButton.Visibility = page is "Dashboard" or "Finance" or "Payments" or "CashMovements" ? Visibility.Visible : Visibility.Collapsed;
        CashFlowPolicyButton.Visibility = page == "Finance" ? Visibility.Visible : Visibility.Collapsed;
        RequiredPaymentButton.Visibility = page == "Payments" ? Visibility.Visible : Visibility.Collapsed;
        SupplierDayButton.Visibility = page == "Suppliers" ? Visibility.Visible : Visibility.Collapsed;
        SupplierMembershipButton.Visibility = page == "Suppliers" ? Visibility.Visible : Visibility.Collapsed;
        PaymentChangeButton.Visibility = page == "Payments" ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.Visibility = Visibility.Visible;
        CashImportButton.Visibility = Visibility.Collapsed;
        CashAdjustmentButton.Visibility = page is "Dashboard" or "Finance" or "CashMovements" ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task<WeeklyFinancialPlan> BuildWeeklyFinancialPlanAsync(DateOnly startDate)
    {
        var policy = _cashFlowPolicyStore.LoadOrCreate();
        var snapshot = _snapshot ?? await App.Services.DataProvider.GetSnapshotAsync(startDate);
        var historicalDailySales = 0m;
        var hasSalesHistory = false;
        if (App.Services.DataProvider is IBusinessSummaryProvider provider)
        {
            try
            {
                var history = await provider.GetBusinessSummaryAsync(startDate.AddDays(-7), startDate.AddDays(-1));
                historicalDailySales = history.Sales.SalesAmount / 7m;
                hasSalesHistory = history.Sales.SalesAvailable;
            }
            catch { /* An unavailable optional report must not block the planning page. */ }
        }

        var baselineWeekSales = policy.WeeklySalesBaselineOverride ?? (hasSalesHistory ? historicalDailySales * 7m : 0m);
        if(policy.WeeklySalesBaselineOverride is null)
        {
            var automatic=AutomaticCashDays(RawCashDeskMovements());
            foreach(var edit in _cashDayOverrideStore.Load().Days.Where(x=>x.Date>=startDate.AddDays(-7) && x.Date<startDate && x.Sales.HasValue))
            {
                decimal original=automatic.GetValueOrDefault(edit.Date)?.Sales??0m;
                if(App.Services.DataProvider is not ExcelDataProvider && App.Services.DataProvider is IBusinessSummaryProvider dailyProvider)
                {
                    try { var report=await dailyProvider.GetBusinessSummaryAsync(edit.Date,edit.Date); if(!report.Sales.SalesAvailable) continue; original=report.Sales.SalesAmount; }
                    catch { continue; }
                }
                baselineWeekSales+=edit.Sales!.Value-original;
            }
        }
        var baselineDailySales = baselineWeekSales / 7m;
        decimal FixedDue(DateOnly date) =>
            _requiredPayments.Where(x => RequiredPaymentRules.AppliesOn(x, date)).Sum(x => x.Amount)
            + _manualPaymentChanges.Where(x => x.PlannedDate == date).Sum(x => x.Amount)
            + snapshot.Payments.Where(x => x.DueDate == date && x.IsMandatory).Sum(x => x.Amount);
        decimal SalaryDue(DateOnly date) => PlannedSundayPayroll(date);
        decimal Mandatory(DateOnly date) => FixedDue(date) + SalaryDue(date);

        // For today, actual sales replace the baseline; the other days keep
        // the previous-week sales baseline until their actual figures arrive.
        var actualToday = snapshot.Sales.SalesAvailable && startDate == DateOnly.FromDateTime(DateTime.Today) ? snapshot.Sales.SalesAmount : (decimal?)null;
        if(startDate==DateOnly.FromDateTime(DateTime.Today)) actualToday=_cashDayOverrideStore.Load().Days.LastOrDefault(x=>x.Date==startDate)?.Sales??actualToday;
        var openingFunds = await CalculateFundsAsync(startDate.AddDays(-1));
        var cashFlow = CashFlowPlanner.Build(startDate, openingFunds.Total,
            baselineDailySales, baselineWeekSales > 0m, actualToday, policy.MinimumReserve,
            day => PlannedSuppliersFor(day), Mandatory);

        var daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);
        decimal FixedReserve(DateOnly day)
        {
            var currentMonth = _requiredPayments.Where(x => RequiredPaymentRules.AppliesInMonth(x, day)).ToList();
            if (policy.FixedCostAllocation == FixedCostAllocationMode.EvenlyAcrossMonth)
                return currentMonth.Sum(x => x.Amount) / daysInMonth;
            return currentMonth.Where(x => x.PaymentDay >= day.Day).Sum(x => x.Amount / Math.Max(1, x.PaymentDay - day.Day + 1));
        }
        decimal SalaryAccrual(DateOnly day) => _salaryAccruals.Where(x => x.Date == day).Sum(x => x.Amount);

        return WeeklyFinancialPlanBuilder.Build(cashFlow, baselineWeekSales,
            cashFlow.Days.Sum(x => FixedDue(x.Date)), cashFlow.Days.Sum(x => SalaryDue(x.Date)),
            cashFlow.WeekSupplierPayments, FixedReserve, SalaryAccrual);
    }

    private void ConfigureCashFlowPolicy()
    {
        var window = new CashFlowPolicyWindow(_cashFlowPolicyStore.LoadOrCreate()) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;
        _cashFlowPolicyStore.Save(window.Result);
        _ = LoadAsync("Finance");
    }

    private void ConfigureCashFlowPolicy_Click(object sender, RoutedEventArgs e) => ConfigureCashFlowPolicy();

    private enum SummaryPeriod { Month, Week, Day }

    private AvailableFundsBreakdown FundsForOpening(CashPosition fallback) => new(
        CashVault: _availableFunds.CashVault ?? 0m,
        CashDesk: _availableFunds.CashDesk ?? fallback.Cash,
        BankReport: _availableFunds.BankReport ?? fallback.Bank,
        AmeriabankPos099: _availableFunds.AmeriabankPos099 ?? 0m,
        Idram: _availableFunds.Idram ?? 0m);

    private async Task<AvailableFundsBreakdown> CalculateFundsAsync(DateOnly asOfDate)
    {
        var funds = FundsForOpening(new CashPosition(0m, 0m));
        var start = CashOpeningStart(asOfDate);
        await RefreshAutomaticNonCashAsync(start,asOfDate);
        var isConfiguredForMonth = start <= asOfDate;
        if (isConfiguredForMonth)
        {
            var cashDeskBalance = CashDeskBalance("0001", funds.CashDesk, start, asOfDate);
            var vaultBalance = CashDeskBalance("0002", funds.CashVault, start, asOfDate);
            var bankCorrection = _cashDeskAdjustments.AsEnumerable().Reverse()
                .Where(x => x.CashDesk == "bank" && x.Date >= start && x.Date <= asOfDate)
                .OrderByDescending(x => x.Date).FirstOrDefault();
            var bankStart = bankCorrection?.Date.AddDays(1) ?? start;
            BankSalesBreakdown bankMovement = BankSalesBreakdown.Empty;
            if (bankStart <= asOfDate && App.Services.DataProvider is IFundsMovementProvider provider)
            {
                try { bankMovement = await provider.GetNonCashSalesAsync(bankStart, asOfDate); }
                catch (Exception ex) { _cashSyncStatus = "⚠ Անկանխիկ մուտքերը չեն թարմացվել․ " + ex.Message; }
            }
            var cashAuto=AutomaticCashDays(RawCashDeskMovements());
            var nonCashDelta=_cashDayOverrideStore.Load().Days.Where(x=>x.Date>=bankStart && x.Date<=asOfDate && x.NonCash.HasValue)
                .Sum(x=>x.NonCash!.Value-(cashAuto.GetValueOrDefault(x.Date)?.NonCash??0));
            funds = funds with
            {
                CashDesk = cashDeskBalance,
                CashVault = vaultBalance,
                // Owner-entered bank payments and cash withdrawals reduce the
                // non-cash balance immediately.  A withdrawal is added to the
                // target cash desk by AllCashDeskMovements(), so it is not lost.
                BankReport = (bankCorrection?.ClosingBalance ?? funds.BankReport) + bankMovement.BankReport + nonCashDelta - ManualBankOutflows(bankStart, asOfDate) + ManualBankInflows(bankStart, asOfDate),
                AmeriabankPos099 = (bankCorrection is null ? funds.AmeriabankPos099 : 0m) + bankMovement.AmeriabankPos099,
                Idram = (bankCorrection is null ? funds.Idram : 0m) + bankMovement.Idram
            };
        }
        return funds;
    }

    private async Task<DashboardSnapshot> ApplyAvailableFundsAsync(DashboardSnapshot source)
    {
        var funds = await CalculateFundsAsync(source.Date);
        _lastFunds = funds;
        var warnings = new List<string>();
        if (!string.IsNullOrWhiteSpace(source.Sales.DataWarning)) warnings.AddRange(source.Sales.DataWarning.Split('\n'));
        if (_cashSyncStatus?.Contains("⚠") == true) warnings.AddRange(_cashSyncStatus.Split('\n'));
        if (_availableFunds.OpeningMonth is null) warnings.Add("⚠ Մեկնարկային մնացորդների ամսաթիվը հաստատված չէ․ կարգավորեք հասանելի միջոցները։");
        if(_salaryPayments.Any(x=>x.PaidDate<=source.Date && x.CashSource is null)) warnings.Add("⚠ Կան աշխատավարձի հին վճարումներ՝ առանց դրամական աղբյուրի։ Աշխատողներ → Խմբագրել․ ընտրեք աղբյուրը միայն եթե ելքն արդեն ներմուծված չէ։");
        return new DashboardSnapshot
        {
            Date = source.Date,
            Cash = new CashPosition(funds.Cash, funds.Bank),
            Suppliers = source.Suppliers,
            SupplierMovements = source.SupplierMovements,
            Payments = source.Payments,
            Forecast = source.Forecast,
            Sales = source.Sales with { DataWarning = warnings.Count == 0 ? null : string.Join("\n", warnings.Distinct()) },
            Recommendations = source.Recommendations,
            Tasks = source.Tasks
        };
    }

    private decimal CashDeskBalance(string cashDesk, decimal openingBalance, DateOnly start, DateOnly end)
        => CashBalanceCalculator.Balance(cashDesk, openingBalance, start, end, EffectiveCashAdjustments(), AllCashDeskMovements());

    /// <summary>
    /// The cash ledger has three sources: imported HTS cash documents,
    /// confirmed supplier receipts (always paid from 0001 by the agreed
    /// business rule), and owner-entered cash/bank transactions.
    /// </summary>
    private IReadOnlyList<CashLedgerMovement> AllCashDeskMovements()
    {
        var raw=RawCashDeskMovements();
        return CashDayOverrideRules.Apply(raw,AutomaticCashDays(raw),_cashDayOverrideStore.Load().Days);
    }

    private IReadOnlyList<CashLedgerMovement> RawCashDeskMovements()
    {
        var imported = CashDocumentImportService.CashDeskMovements(_cashDocuments);
        var supplierPayments = ConfirmedSupplierCashMovements();
        var manual = _fundsTransactions
            .Where(x => x.Source is "0001" or "0002" || !string.IsNullOrWhiteSpace(x.TargetCashDesk))
            .Select(x => new CashLedgerMovement(
                x.Date,
                x.Source is "0001" or "0002" ? x.Source : string.Empty,
                x.TargetCashDesk,
                x.Amount,
                $"OWNER-{x.Id:N}",
                x.Purpose,
                x.Category,
                !string.IsNullOrWhiteSpace(x.TargetCashDesk)));

        var local=LocalPaymentMovements();
        return imported.Concat(supplierPayments).Concat(manual).Concat(local.Where(x=>x.SourceCashDesk is "0001" or "0002"))
            .OrderBy(x => x.Date).ThenBy(x => x.DocumentNumber).ToList();
    }

    private IReadOnlyList<CashLedgerMovement> ConfirmedSupplierCashMovements()
    {
        var employeeActions = _employeeSupplierActionStore.Load();
        var ownerChanges = _supplierStatusChangeStore.Load();
        var importedCashPayments = CashDocumentImportService.SupplierPaymentRows(_cashDocuments).ToList();

        return _supplierWeekRows
            .Where(row => row.PaymentAmount + row.OldDebtPayment > 0m)
            .Where(row => IsSupplierReceiptConfirmed(row, employeeActions, ownerChanges))
            .Select(row =>
            {
                var expected = row.PaymentAmount + row.OldDebtPayment;
                var imported = importedCashPayments
                    .Where(x => x.Date == row.Date && SupplierNamesMatch(x.Recipient, row.Supplier))
                    .Sum(x => x.Amount);
                var manual = _fundsTransactions.Where(x => x.Date == row.Date &&
                    TryGetSupplierPayment(x, out var supplier, out _) && SupplierNamesMatch(supplier, row.Supplier)).Sum(x => x.Amount);
                manual+=_completedPayments.Where(x=>x.CashSource is not null && x.PaidDate==row.Date && SupplierNamesMatch(x.Recipient,row.Supplier)).Sum(x=>x.Amount);
                return new { Row = row, Remaining = PaymentReconciliation.Unrecorded(expected, imported, manual) };
            })
            .Where(x => x.Remaining > 0m)
            .Select(x => new CashLedgerMovement(
                x.Row.Date, "0001", null, x.Remaining,
                $"SUPPLIER-CONFIRMED-{x.Row.Date:yyyyMMdd}-{NormalizeSupplierName(x.Row.Supplier)}",
                x.Row.Supplier, "Հաստատված վճարման՝ այլ աղբյուրով չհաշվառված մասը", false))
            .ToList();
    }

    private static bool IsSupplierReceiptConfirmed(
        SupplierWeekPlanRow row,
        IReadOnlyList<EmployeeSupplierAction> employeeActions,
        IReadOnlyList<SupplierStatusChange> ownerChanges)
    {
        var employee = employeeActions
            .Where(x => x.Date == row.Date && SupplierNamesMatch(x.Supplier, row.Supplier))
            .OrderByDescending(x => x.ReportedAt).FirstOrDefault();
        var owner = ownerChanges
            .Where(x => x.Date == row.Date && SupplierNamesMatch(x.Supplier, row.Supplier))
            .OrderByDescending(x => x.ChangedAt).FirstOrDefault();
        var status = owner is not null && (employee is null || owner.ChangedAt >= employee.ReportedAt)
            ? owner.NewStatus : employee?.Status;
        return status is "Կատարված է" or "Հաստատված" or "Հաստատված է";
    }

    private decimal ManualBankOutflows(DateOnly start, DateOnly end) =>
        _fundsTransactions.Where(x => x.Source == "bank" && x.Date >= start && x.Date <= end).Sum(x => x.Amount)
        + LocalPaymentMovements().Where(x=>x.SourceCashDesk=="bank" && x.Date>=start && x.Date<=end).Sum(x=>x.Amount);

    private decimal ManualBankInflows(DateOnly start, DateOnly end) =>
        _fundsTransactions.Where(x => x.TargetCashDesk == "bank" && x.Date >= start && x.Date <= end).Sum(x => x.Amount);

    private IReadOnlyList<CompletedPayment> AllActualPayments()
    {
        // A bank-to-cash transfer is not an expense and must not appear as a
        // completed payment. Every other manually entered transaction is an
        // actual payment and therefore appears in the Payments page.
        var ownerPayments = _fundsTransactions
            .Where(x => string.IsNullOrWhiteSpace(x.TargetCashDesk))
            .Select(x =>
            {
                var recipient = TryGetSupplierPayment(x, out var supplier, out _) ? supplier : x.Purpose;
                return new CompletedPayment(recipient, x.Amount, x.Date,
                    $"{FundsSourceLabel(x.Source)} · {x.Category}", $"OWNER-{x.Id:N}");
            });
        var cashPayments = AllCashDeskMovements()
            .Where(x => !x.IsInternalTransfer && !string.IsNullOrWhiteSpace(x.SourceCashDesk) &&
                !x.DocumentNumber.StartsWith("LOCAL-PAY-",StringComparison.Ordinal) && !x.DocumentNumber.StartsWith("SALARY-PAY-",StringComparison.Ordinal) &&
                !x.DocumentNumber.StartsWith("DAY-ADJUST-", StringComparison.Ordinal) && !x.DocumentNumber.StartsWith("OWNER-", StringComparison.Ordinal) && !x.DocumentNumber.StartsWith("HTS-CASH-SALES-", StringComparison.Ordinal))
            .Select(x => new CompletedPayment(string.IsNullOrWhiteSpace(x.Partner) ? x.ContractOrReason : x.Partner,
                x.Amount, x.Date, x.ContractOrReason, x.DocumentNumber));
        // Prefer current source documents over older imported snapshots of the same payment.
        var covered = App.Services.DataProvider is ExcelDataProvider
            ? _excelImportStore.Load().Batches.Where(x => x.Kind == "cash").SelectMany(x => x.Dates).ToHashSet() : [];
        var prior = _completedPayments.Where(x => !(covered.Contains(x.PaidDate) && x.Note.StartsWith("ՀԾ դրամարկղային փաստաթուղթ")));
        return cashPayments.Concat(prior).Concat(ownerPayments)
            .GroupBy(x => (x.PaidDate, Reference: x.SourceDocument ?? Guid.NewGuid().ToString(), Recipient: NormalizeSupplierName(x.Recipient)))
            .Select(x => x.First()).ToList();
    }

    private IReadOnlyList<CashLedgerMovement> CashMovementsForSelectedDate() =>
        AllCashDeskMovements()
            .Where(x => x.Date == _selectedDate)
            .OrderBy(x => x.DocumentNumber)
            .ToList();

    private IReadOnlyList<CashDayLedger> CashLedgerForSelectedMonth(AvailableFundsBreakdown openingFunds)
    {
        var start = new DateOnly(_selectedDate.Year, _selectedDate.Month, 1);
        var movements = AllCashDeskMovements()
            .Where(x => x.Date >= start && x.Date <= _selectedDate).ToList();
        var openingStart = CashOpeningStart(_selectedDate);
        var effectiveAdjustments=EffectiveCashAdjustments();
        var automatic=AutomaticCashDays(RawCashDeskMovements()); var overrides=_cashDayOverrideStore.Load().Days;
        var cashDesk = CashDeskBalance("0001", openingFunds.CashDesk, openingStart, start.AddDays(-1));
        var vault = CashDeskBalance("0002", openingFunds.CashVault, openingStart, start.AddDays(-1));
        var rows = new List<CashDayLedger>();
        for (var date = start; date <= _selectedDate; date = date.AddDays(1))
        {
            var correction0001 = effectiveAdjustments.LastOrDefault(x => x.Date == date && x.CashDesk == "0001");
            var correction0002 = effectiveAdjustments.LastOrDefault(x => x.Date == date && x.CashDesk == "0002");
            var day = movements.Where(x => x.Date == date).ToList();
            var in0001 = day.Where(x => x.TargetCashDesk == "0001").Sum(x => x.Amount);
            var out0001 = day.Where(x => x.SourceCashDesk == "0001").Sum(x => x.Amount);
            var in0002 = day.Where(x => x.TargetCashDesk == "0002").Sum(x => x.Amount);
            var out0002 = day.Where(x => x.SourceCashDesk == "0002").Sum(x => x.Amount);
            if (correction0001 is not null) { cashDesk = correction0001.ClosingBalance; }
            else cashDesk += in0001 - out0001;
            if (correction0002 is not null) { vault = correction0002.ClosingBalance; }
            else vault += in0002 - out0002;
            var edit=overrides.LastOrDefault(x=>x.Date==date);
            var values=CashDayOverrideRules.Effective(automatic.GetValueOrDefault(date)??new(0,0,0,0,0,0),edit);
            rows.Add(new CashDayLedger(date, in0001, out0001, cashDesk, in0002, out0002, vault) { GrossSales=values.Sales,NonCash=values.NonCash,OtherCashIn=values.OtherIn,IsManual=edit is not null && new[]{edit.Sales,edit.NonCash,edit.OtherIn,edit.Out,edit.VaultIn,edit.VaultOut,edit.Closing,edit.VaultClosing}.Any(x=>x.HasValue) });
        }
        return rows;
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
        var window = new CashDeskAdjustmentWindow(_selectedDate, _cashDeskAdjustments) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;
        try { _cashDeskAdjustmentStore.Save(_cashDeskAdjustments.Append(window.Result).ToList()); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Մնացորդը չի պահպանվել"); return; }
        _cashDeskAdjustments.Add(window.Result);
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

        var paid = AllActualPayments().Where(x => x.PaidDate >= start && x.PaidDate <= end).Sum(x => x.Amount);
        var supplied = summary.SuppliedAmount;
        summary = summary with { SupplierPayments = paid, DebtChange = supplied - paid };
        return Views.Summary(summary, label,
            () => SetSummaryPeriod(SummaryPeriod.Month),
            () => SetSummaryPeriod(SummaryPeriod.Week),
            () => SetSummaryPeriod(SummaryPeriod.Day));
    }

    private async Task<UIElement> PurchasePlanViewAsync()
    {
        return await SupplierEditorViewAsync(_selectedDate.AddDays(1));
    }

    private async Task<UIElement> SupplierEditorViewAsync(DateOnly date)
        => Views.Suppliers(PlannedSuppliersFor(date), _snapshot!.Suppliers, _partnerDebts,
            _employeeSupplierActionStore.Load(), _supplierStatusChangeStore.Load(), _supplierNoteStore.Load(),
            await BuildWeeklyFinancialPlanAsync(date), SaveSupplierWeekRow, ShowSupplierEmployeeStatus,
            ShowSupplierDebtHistory, EditSupplierStatus, AddSupplierNoteFromDesktop, ShowSupplierAnalysis,
            SaveAllSupplierRowsAsync, _supplierInputDrafts, _supplierSaveStatus);

    private async Task<IReadOnlyList<PurchaseProposal>> GetOrBuildPurchaseProposalsAsync(DateOnly planningDate, DateOnly deliveryDate, IReadOnlyList<SupplierWeekPlanRow> scheduled, bool showErrors = false)
    {
        var supplierNames = scheduled.Select(x => x.Supplier).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (App.Services.DataProvider is not IPurchasePlanningProvider provider || supplierNames.Count == 0)
            return _purchaseProposalStore.Load(deliveryDate);

        var coverageDays = supplierNames.ToDictionary(x => x, x => DaysUntilNextDelivery(x, deliveryDate), StringComparer.OrdinalIgnoreCase);
        try
        {
            var proposals = await provider.GetPurchaseProposalsAsync(planningDate, deliveryDate, supplierNames, coverageDays);
            if (proposals.Count > 0) _purchaseProposalStore.Save(deliveryDate, proposals);
            return proposals.Count > 0 ? proposals : _purchaseProposalStore.Load(deliveryDate);
        }
        catch (Exception exception)
        {
            if (showErrors)
                MessageBox.Show($"ՀԾ-ից պաշարների կամ վաճառքի տվյալները չեն ստացվել։\n{exception.Message}\n\nՑուցադրվում է վերջին պահպանված նախնական պատվերը, եթե առկա է։", "Նախնական պատվեր", MessageBoxButton.OK, MessageBoxImage.Warning);
            return _purchaseProposalStore.Load(deliveryDate);
        }
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
        _snapshot = DecisionEngine.Evaluate(_snapshot, PlanForSelectedDate(), _requiredPayments);
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
            var configured = _telegramBotSettingsStore.Configure(window.Result.BotToken);
            var chatId = configured.ChatId ?? await TelegramBotClient.FindChatIdAsync(configured);
            if (string.IsNullOrWhiteSpace(chatId))
            {
                MessageBox.Show("Բոտին Telegram-ում ուղարկեք /start, ապա կրկին սեղմեք «Telegram» կոճակը։ Այս պահին անձնական չաթ չի գտնվել։",
                    "Telegram", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var settings = _telegramBotSettingsStore.Load() with { ChatId = chatId };
            _telegramBotSettingsStore.Save(settings);
            StartTelegramPolling();
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
        _employeeTelegramBotSettingsStore.Configure(window.Result.BotUsername, window.Result.BotToken);
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

    private async void AddFundsTransaction_Click(object sender, RoutedEventArgs e)
    {
        var supplierNames = (_snapshot?.Suppliers.Select(x => x.Name) ?? [])
            .Concat(_partnerDebts.Select(x => x.Supplier))
            .Concat(SupplierWeekPlanSeed.AllSuppliers())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var window = new FundsTransactionWindow(_selectedDate, supplierNames) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;
        var entry = window.Result;
        var isTransfer = entry.Kind == "Ներքին փոխանցում";
        var isSupplier = entry.Kind == "Մատակարարի վճարում";
        var purpose = isTransfer
            ? $"Ներքին փոխանցում՝ {FundsSourceLabel(entry.Source)} → {FundsSourceLabel(entry.Target!)}"
            : isSupplier
                ? $"մատակարար {entry.RecipientOrPurpose} {(entry.IsOldDebtPayment ? "հին" : "նոր")}" 
                : entry.RecipientOrPurpose;
        var category = isTransfer ? "Ներքին փոխանցում" : isSupplier ? "Մատակարարի վճարում" : InferPaymentCategory(entry.RecipientOrPurpose);
        var transaction = new FundsTransaction(Guid.NewGuid(), entry.Date, entry.Source, entry.Amount, purpose,
            category, entry.Target, DateTime.Now);
        _fundsTransactions.Add(transaction);
        _fundsTransactionStore.Save(_fundsTransactions);
        if (isSupplier) ApplyManualSupplierPayment(transaction, entry.RecipientOrPurpose, entry.IsOldDebtPayment);
        await LoadAsync(_currentPage);
    }

    private void ApprovePendingSalaryEmployee(Guid id)
    {
        var pending = _pendingSalaryEmployees.FirstOrDefault(x => x.Id == id);
        if (pending is null) return;
        _salaryAccruals.Add(pending.ProposedAccrual);
        _pendingSalaryEmployees.RemoveAll(x => x.Id == id);
        _salaryStore.SaveAccruals(_salaryAccruals);
        _pendingSalaryEmployeeStore.Save(_pendingSalaryEmployees);
        _ = LoadAsync("Salaries");
    }

    private void RejectPendingSalaryEmployee(Guid id)
    {
        _pendingSalaryEmployees.RemoveAll(x => x.Id == id);
        _pendingSalaryEmployeeStore.Save(_pendingSalaryEmployees);
        _ = LoadAsync("Salaries");
    }

    private void OpenSalaryEmployee(string employee)
    {
        var window = new SalaryEmployeeDetailsWindow(employee, SalaryRules.WeekStart(_selectedDate), _salaryAccruals, _salaryPayments, () =>
        {
            try { _salaryStore.SaveCorrection(_salaryAccruals,_salaryPayments); }
            catch(Exception ex) { _salaryAccruals.Clear(); _salaryAccruals.AddRange(_salaryStore.LoadAccruals()); _salaryPayments.Clear(); _salaryPayments.AddRange(_salaryStore.LoadPayments()); MessageBox.Show(ex.Message,"Փոփոխությունը չի պահպանվել"); }
        }) { Owner = this };
        window.ShowDialog();
        _ = LoadAsync("Salaries");
    }

    private void AddSalaryAccrual()
    {
        var employees = _salaryAccruals.Select(x => x.Employee).Concat(_salaryPayments.Select(x => x.Employee));
        var window = new SalaryAccrualWindow(_selectedDate, employees) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;
        _salaryAccruals.Add(window.Result);
        _salaryStore.SaveAccruals(_salaryAccruals);
        _ = LoadAsync("Salaries");
    }

    private void AddSalaryPayment()
    {
        var employees = _salaryAccruals.Select(x => x.Employee).Concat(_salaryPayments.Select(x => x.Employee));
        var window = new SalaryPaymentWindow(_selectedDate, employees) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;
        _salaryPayments.Add(window.Result);
        _salaryStore.SavePayments(_salaryPayments);
        _ = LoadAsync("Salaries");
    }

    private decimal PlannedSundayPayroll(DateOnly date) => SalaryRules.PlannedSundayPayroll(_salaryAccruals, _salaryPayments, date);

    private async void AddRequiredPayment_Click(object sender, RoutedEventArgs e)
    {
        var window = new RequiredPaymentWindow(_selectedDate) { Owner = this };
        window.PaymentSavedAndNew += payment =>
        {
            _requiredPayments.Add(payment);
            _requiredPaymentStore.Save(_requiredPayments);
        };
        var accepted = window.ShowDialog() == true && window.Result is not null;
        if (accepted)
        {
            _requiredPayments.Add(window.Result!);
            _requiredPaymentStore.Save(_requiredPayments);
        }
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
            if (!isXml)
            {
                var batch = ExcelReportReader.Read(picker.FileName);
                var state = _excelImportStore.Load(); ExcelImportStore.Merge(state,batch);
                var conflicts = ExcelReportReader.Reconcile(state);
                if (conflicts.Count > 0) throw new InvalidOperationException(string.Join("\n",conflicts));
                if (!ConfirmImportPreview(batch.FileName + "\nՆույն օրերի նախորդ ներմուծումը կփոխարինվի։\n" + string.Join("\n",batch.Warnings))) return;
                _excelImportStore.Save(state); ConfigureDataProvider(); _snapshot = null;
                _selectedDate = batch.Dates.Max(); ViewDatePicker.SelectedDate = _selectedDate.ToDateTime(TimeOnly.MinValue);
                await LoadAsync("Dashboard"); return;
            }
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

    private void ImportWarehouse_Click(object sender, RoutedEventArgs e) => OpenExcelImports_Click(sender, e);

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
        => await SaveAllSupplierRowsAsync([new SupplierRowEdit(row, order, payment, oldDebtPayment, debt)]);

    private async Task SaveAllSupplierRowsAsync(IReadOnlyList<SupplierRowEdit> edits)
    {
        try
        {
        if (edits.Any(x => x.Order < 0 || x.Payment < 0 || x.OldDebtPayment < 0 || x.Debt < 0))
            throw new InvalidOperationException("Գումարները չեն կարող բացասական լինել։");
        var nextRows = _supplierWeekRows.ToList();
        var history = new List<SupplierDebtChange>();
        foreach (var edit in edits)
        {
        var (row, order, payment, oldDebtPayment, debt) = edit;
        var index = nextRows.FindIndex(x => x.Date == row.Date && string.Equals(x.Supplier, row.Supplier, StringComparison.OrdinalIgnoreCase));
        if (index >= 0 && (nextRows[index].OrderAmount != row.OrderAmount || nextRows[index].PaymentAmount != row.PaymentAmount || nextRows[index].OldDebtPayment != row.OldDebtPayment || nextRows[index].Debt != row.Debt && nextRows[index].HasActualDebt))
            throw new InvalidOperationException($"{row.Supplier}․ տվյալները փոխվել են այլ գործողությամբ։ Թարմացրեք ցանկը և նորից պահպանեք։");
        // The debt field represents the closing balance for the selected day.  Only the change
        // made by the owner is applied, so pressing Save a second time cannot reduce it twice.
        var movementChange = (order - row.OrderAmount) - (payment - row.PaymentAmount) - (oldDebtPayment - row.OldDebtPayment);
        var closingDebt = Math.Max(0m, debt + movementChange);
        var updated = row with { OrderAmount = order, PaymentAmount = payment, OldDebtPayment = oldDebtPayment, Debt = closingDebt, HasActualDebt = true };
        if (index < 0) nextRows.Add(updated);
        else nextRows[index] = updated;
        if (closingDebt != row.Debt || !row.HasActualDebt)
        {
            var reason = debt != row.Debt || order == row.OrderAmount && payment == row.PaymentAmount && oldDebtPayment == row.OldDebtPayment
                ? "Տնօրենի կողմից պարտքի հիմքային մնացորդի ուղղում"
                : "Պատվերի կամ վճարման փոփոխության արդյունքում վերահաշվարկ";
            history.Add(new SupplierDebtChange(Guid.NewGuid(), row.Date, row.Supplier,
                row.Debt, closingDebt, reason, DateTime.Now, "Տնօրեն"));
        }
        }
        _supplierWeekPlanStore.Save(nextRows);
        _supplierWeekRows = nextRows;
        foreach (var edit in edits) _supplierInputDrafts.Remove($"{edit.Row.Date:yyyyMMdd}|{edit.Row.Supplier}");
        _supplierDebtHistory.AddRange(history);
        _supplierDebtHistoryStore.Save(_supplierDebtHistory);
        _supplierSaveStatus = $"✓ Պահպանված է {edits.Count} տող · {DateTime.Now:HH:mm:ss}։ Սա չի նշանակում մատակարարման ստացման հաստատում։";
        await LoadAsync(_currentPage == "PurchasePlan" ? "PurchasePlan" : "Suppliers");
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

    private CashDailySummary? CashSummaryForSelectedDate()
    {
        var source = App.Services.DataProvider is ExcelDataProvider
            ? _excelImportStore.Load().Batches.SelectMany(x => x.Cash).ToList() : _cashDocuments;
        return source.Any(x => x.Date == _selectedDate) ? CashDocumentImportService.Summary(source,_selectedDate) : null;
    }

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

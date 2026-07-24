using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace PatarikAIOS;

/// <summary>Local Telegram configuration. The token is kept only on this Windows account.</summary>
public sealed record TelegramBotSettings(string BotToken, string? ChatId = null, long? LastUpdateId = null, DateOnly? EditingDate = null, DateOnly? LastMorningBriefDate = null, DateOnly? LastEveningDashboardDate = null, DateOnly? LastEveningOperationsDate = null, DateOnly? LastDeliveryConfirmationDate = null)
{
    public static TelegramBotSettings Empty => new("", null);
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BotToken);
}

public sealed class TelegramBotSettingsStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PatarikAIOS", "telegram-settings.json");

    public TelegramBotSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<TelegramBotSettings>(File.ReadAllText(_path)) ?? TelegramBotSettings.Empty;
        }
        catch (JsonException) { }
        return TelegramBotSettings.Empty;
    }

    public void Save(TelegramBotSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}

/// <summary>Separate configuration for the employee-only Telegram bot.</summary>
public sealed record EmployeeTelegramBotSettings(string BotUsername, string BotToken, long? LastUpdateId = null, DateOnly? LastMorningOrdersDate = null, DateOnly? LastMorningTasksDate = null, DateOnly? LastNextDayPlanDate = null, DateOnly? LastEveningReminderDate = null)
{
    public static EmployeeTelegramBotSettings Empty => new("@MPatarik_bot", "");
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BotToken);
}

public sealed class EmployeeTelegramBotSettingsStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PatarikAIOS", "employee-telegram-settings.json");

    public EmployeeTelegramBotSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<EmployeeTelegramBotSettings>(File.ReadAllText(_path)) ?? EmployeeTelegramBotSettings.Empty;
        }
        catch (JsonException) { }
        return EmployeeTelegramBotSettings.Empty;
    }

    public void Save(EmployeeTelegramBotSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed record EmployeeBotUser(string ChatId, string DisplayName, DateTime RegisteredAt, DateTime LastSeenAt);

public sealed class EmployeeBotUserStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PatarikAIOS", "employee-bot-users.json");

    public List<EmployeeBotUser> Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<List<EmployeeBotUser>>(File.ReadAllText(_path)) ?? [];
        }
        catch (JsonException) { }
        return [];
    }

    public void Save(IEnumerable<EmployeeBotUser> users)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed record EmployeeSupplierAction(
    DateOnly Date,
    string Supplier,
    string Status,
    string Description,
    string ReportedByChatId,
    string ReportedByName,
    DateTime ReportedAt);

public sealed class EmployeeSupplierActionStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PatarikAIOS", "employee-supplier-actions.json");

    public List<EmployeeSupplierAction> Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<List<EmployeeSupplierAction>>(File.ReadAllText(_path)) ?? [];
        }
        catch (JsonException) { }
        return [];
    }

    public void Save(IEnumerable<EmployeeSupplierAction> actions)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(actions, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed record EmployeePendingIssue(string ChatId, DateOnly Date, string Supplier, string Workflow = "receipt", string PendingAction = "issue");

public sealed record PendingEmployeeOrderChange(
    Guid Id,
    DateOnly Date,
    string Supplier,
    decimal PlannedOrder,
    decimal PlannedPayment,
    decimal ActualOrder,
    decimal ActualPayment,
    string ReportedByChatId,
    string ReportedByName,
    DateTime CreatedAt,
    decimal PlannedOldDebtPayment = 0m,
    decimal ActualOldDebtPayment = 0m);

public sealed class PendingEmployeeOrderChangeStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PatarikAIOS", "pending-employee-order-changes.json");

    public List<PendingEmployeeOrderChange> Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<List<PendingEmployeeOrderChange>>(File.ReadAllText(_path)) ?? [];
        }
        catch (JsonException) { }
        return [];
    }

    public void Save(IEnumerable<PendingEmployeeOrderChange> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class EmployeePendingIssueStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PatarikAIOS", "employee-pending-issues.json");

    public List<EmployeePendingIssue> Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<List<EmployeePendingIssue>>(File.ReadAllText(_path)) ?? [];
        }
        catch (JsonException) { }
        return [];
    }

    public void Save(IEnumerable<EmployeePendingIssue> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed record EmployeeTask(Guid Id, DateOnly Date, string Description, DateTime CreatedAt);

public sealed class EmployeeTaskStore
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PatarikAIOS", "employee-tasks.json");
    public List<EmployeeTask> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<EmployeeTask>>(File.ReadAllText(_path)) ?? [] : []; }
        catch (JsonException) { return []; }
    }
    public void Save(IEnumerable<EmployeeTask> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed record EmployeeTaskAction(Guid TaskId, string Status, string Description, string ChatId, string EmployeeName, DateTime ReportedAt);

public sealed class EmployeeTaskActionStore
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PatarikAIOS", "employee-task-actions.json");
    public List<EmployeeTaskAction> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<EmployeeTaskAction>>(File.ReadAllText(_path)) ?? [] : []; }
        catch (JsonException) { return []; }
    }
    public void Save(IEnumerable<EmployeeTaskAction> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed record EmployeePendingTaskIssue(string ChatId, Guid TaskId);

public sealed class EmployeePendingTaskIssueStore
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PatarikAIOS", "employee-pending-task-issues.json");
    public List<EmployeePendingTaskIssue> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<EmployeePendingTaskIssue>>(File.ReadAllText(_path)) ?? [] : []; }
        catch (JsonException) { return []; }
    }
    public void Save(IEnumerable<EmployeePendingTaskIssue> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed record EmployeeIssue(Guid Id, DateOnly Date, string Description, string ChatId, string EmployeeName, DateTime ReportedAt, string? TaskDescription = null, string? DirectorResponse = null);

public sealed class EmployeeIssueStore
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PatarikAIOS", "employee-general-issues.json");
    public List<EmployeeIssue> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<EmployeeIssue>>(File.ReadAllText(_path)) ?? [] : []; }
        catch (JsonException) { return []; }
    }
    public void Save(IEnumerable<EmployeeIssue> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed record EmployeePendingGeneralIssue(string ChatId);
public sealed class EmployeePendingGeneralIssueStore
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PatarikAIOS", "employee-pending-general-issues.json");
    public List<EmployeePendingGeneralIssue> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<EmployeePendingGeneralIssue>>(File.ReadAllText(_path)) ?? [] : []; }
        catch (JsonException) { return []; }
    }
    public void Save(IEnumerable<EmployeePendingGeneralIssue> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed record OwnerPendingEmployeeIssueReply(Guid IssueId);
public sealed class OwnerPendingEmployeeIssueReplyStore
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PatarikAIOS", "owner-pending-employee-issue-reply.json");
    public List<OwnerPendingEmployeeIssueReply> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<OwnerPendingEmployeeIssueReply>>(File.ReadAllText(_path)) ?? [] : []; }
        catch (JsonException) { return []; }
    }
    public void Save(IEnumerable<OwnerPendingEmployeeIssueReply> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public static class TelegramBotClient
{
    private static Uri Api(TelegramBotSettings settings, string method) =>
        new($"https://api.telegram.org/bot{Uri.EscapeDataString(settings.BotToken)}/{method}");

    public static async Task<string?> FindChatIdAsync(TelegramBotSettings settings, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var response = await client.GetAsync(Api(settings, "getUpdates"), cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Telegram-ը չընդունեց բոտի բանալին։ Ստուգեք այն և կրկին փորձեք։");

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array) return null;
        foreach (var update in result.EnumerateArray().Reverse())
        {
            if (update.TryGetProperty("message", out var message) && message.TryGetProperty("chat", out var chat) && chat.TryGetProperty("id", out var id))
                return id.ToString();
        }
        return null;
    }

    public static async Task<IReadOnlyList<TelegramIncomingMessage>> GetNewMessagesAsync(TelegramBotSettings settings, CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured || string.IsNullOrWhiteSpace(settings.ChatId)) return [];
        var offset = settings.LastUpdateId is { } last ? $"?offset={last + 1}" : string.Empty;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var response = await client.GetAsync(Api(settings, "getUpdates" + offset), cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) return [];

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array) return [];
        var messages = new List<TelegramIncomingMessage>();
        foreach (var update in result.EnumerateArray())
        {
            if (!update.TryGetProperty("update_id", out var updateId)) continue;
            if (update.TryGetProperty("message", out var message))
            {
                if (!message.TryGetProperty("chat", out var chat) || !chat.TryGetProperty("id", out var chatId)) continue;
                var text = message.TryGetProperty("text", out var textValue) ? textValue.GetString() : null;
                if (string.IsNullOrWhiteSpace(text) || !string.Equals(chatId.ToString(), settings.ChatId, StringComparison.Ordinal)) continue;
                messages.Add(new TelegramIncomingMessage(updateId.GetInt64(), chatId.ToString(), text.Trim()));
            }
            else if (update.TryGetProperty("callback_query", out var callback))
            {
                if (!callback.TryGetProperty("message", out var callbackMessage) || !callbackMessage.TryGetProperty("chat", out var chat) || !chat.TryGetProperty("id", out var chatId)) continue;
                var data = callback.TryGetProperty("data", out var dataValue) ? dataValue.GetString() : null;
                var callbackId = callback.TryGetProperty("id", out var callbackIdValue) ? callbackIdValue.GetString() : null;
                if (string.IsNullOrWhiteSpace(data) || string.IsNullOrWhiteSpace(callbackId) || !string.Equals(chatId.ToString(), settings.ChatId, StringComparison.Ordinal)) continue;
                messages.Add(new TelegramIncomingMessage(updateId.GetInt64(), chatId.ToString(), data, callbackId));
            }
        }
        return messages;
    }

    public static async Task<IReadOnlyList<EmployeeTelegramIncomingMessage>> GetEmployeeMessagesAsync(EmployeeTelegramBotSettings settings, CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured) return [];
        var offset = settings.LastUpdateId is { } last ? $"?offset={last + 1}" : string.Empty;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var apiSettings = new TelegramBotSettings(settings.BotToken);
        using var response = await client.GetAsync(Api(apiSettings, "getUpdates" + offset), cancellationToken);
        if (!response.IsSuccessStatusCode) return [];
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array) return [];
        var messages = new List<EmployeeTelegramIncomingMessage>();
        foreach (var update in result.EnumerateArray())
        {
            if (!update.TryGetProperty("update_id", out var updateId)) continue;
            var isCallback = update.TryGetProperty("callback_query", out var callback);
            var message = isCallback && callback.TryGetProperty("message", out var callbackMessage)
                ? callbackMessage : update.TryGetProperty("message", out var normalMessage) ? normalMessage : default;
            if (message.ValueKind == JsonValueKind.Undefined || !message.TryGetProperty("chat", out var chat) || !chat.TryGetProperty("id", out var chatId)) continue;
            var text = isCallback ? (callback.TryGetProperty("data", out var dataValue) ? dataValue.GetString() : null)
                : (message.TryGetProperty("text", out var textValue) ? textValue.GetString() : null);
            if (string.IsNullOrWhiteSpace(text)) continue;
            var displayName = "Աշխատակից";
            if (message.TryGetProperty("from", out var from))
            {
                var firstName = from.TryGetProperty("first_name", out var first) ? first.GetString() : null;
                var lastName = from.TryGetProperty("last_name", out var lastNameValue) ? lastNameValue.GetString() : null;
                displayName = string.Join(" ", new[] { firstName, lastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (string.IsNullOrWhiteSpace(displayName)) displayName = "Աշխատակից";
            }
            var callbackId = isCallback && callback.TryGetProperty("id", out var callbackIdValue) ? callbackIdValue.GetString() : null;
            messages.Add(new EmployeeTelegramIncomingMessage(updateId.GetInt64(), chatId.ToString(), text.Trim(), displayName, callbackId));
        }
        return messages;
    }

    public static async Task SendMessageAsync(TelegramBotSettings settings, string text, IReadOnlyList<IReadOnlyList<TelegramInlineButton>>? buttons = null, CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured || string.IsNullOrWhiteSpace(settings.ChatId))
            throw new InvalidOperationException("Telegram չաթը դեռ չի հաստատվել։ Բոտին ուղարկեք /start և կրկին փորձեք։");

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var fields = new Dictionary<string, string>
        {
            ["chat_id"] = settings.ChatId,
            ["text"] = text
        };
        if (buttons is not null)
            fields["reply_markup"] = JsonSerializer.Serialize(new { inline_keyboard = buttons.Select(row => row.Select(button => new { text = button.Text, callback_data = button.Data })) });
        using var body = new FormUrlEncodedContent(fields);
        using var response = await client.PostAsync(Api(settings, "sendMessage"), body, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Telegram հաղորդագրությունը չուղարկվեց։ Ստուգեք, որ բոտին /start եք ուղարկել։");
    }

    public static async Task AnswerCallbackAsync(TelegramBotSettings settings, string callbackId, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var body = new FormUrlEncodedContent(new Dictionary<string, string> { ["callback_query_id"] = callbackId });
        await client.PostAsync(Api(settings, "answerCallbackQuery"), body, cancellationToken);
    }
}

public sealed record TelegramInlineButton(string Text, string Data);
public sealed record TelegramIncomingMessage(long UpdateId, string ChatId, string Text, string? CallbackId = null);
public sealed record EmployeeTelegramIncomingMessage(long UpdateId, string ChatId, string Text, string DisplayName, string? CallbackId = null);

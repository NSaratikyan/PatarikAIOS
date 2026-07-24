# Patarik AI OS — MVP

Windows desktop MVP for operational and financial oversight of Patarik.

## Included

- CEO dashboard: available cash, expected payments, alerts and daily actions
- Financial center: 7-day cash-flow forecast and payment recommendations
- Supplier debts: balances, upcoming supply dates and priority scoring
- Payment plan: recommended payment sequence, with cash-reserve check
- AI recommendations: explainable, rule-based proposals pending owner approval
- Owner payment-change window: add an agreed or rescheduled supplier payment to the in-app calendar
- Manually added payment changes appear immediately in both the payment plan and the Suppliers daily table, including after in-app refresh
- Payment changes are automatically stored locally in `data/manual-payment-changes.json`; this is separate from real HԾ records
- The initial supplier-weekday schedule is built automatically from `WarehouseDocument.xlsx` on the Desktop when no saved schedule exists; supplier day changes can be set as "this week only" or "always from now on".
- Replaceable HԾ integration boundary (`IHtsDataProvider`)
- Seed data only — no real HԾ credentials or live payments are used

## Run locally

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.
2. Open PowerShell in this folder.
3. Run `dotnet run --project PatarikAIOS.csproj`.

The project uses no third-party packages. Once the HԾ API specification is available, implement `HtsApiDataProvider` and register it in `App.xaml.cs` instead of `DemoDataProvider`.

## Important safety boundary

This MVP only creates recommendations. Payments, purchase orders and Telegram messages must remain approval-only until real integrations have been reviewed and tested.

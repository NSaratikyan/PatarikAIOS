# HԾ Cloud integration contract

Before enabling live synchronization, obtain the HԾ API documentation and a restricted integration account. Do not place administrator credentials in the application or source code.

## MVP data pulls

| Business data | Minimum fields | Target tables | Refresh |
|---|---|---|---|
| Suppliers/contracts | external ID, name, contract/direction, terms | `supplier`, `supplier_contract` | Daily + on change |
| Debt balances | supplier, contract, balance, date | `supplier_balance_snapshot` | Hourly |
| Purchase receipts | document ID, date, supplier, warehouse, lines | `purchase_receipt`, `purchase_receipt_line` | Hourly |
| Cash/bank | account, balance, timestamp | `cash_balance_snapshot` | Hourly |
| Payments | supplier, amount, date, payment purpose | reconcile `payment_plan` | Hourly |

## Required connector behaviour

1. Call HԾ read-only endpoints only in MVP.
2. Preserve every source document ID in `external_id`; imports must be idempotent.
3. Record source update time and import time.
4. Failed imports must be visible as an alert; never silently replace existing data with zero values.
5. Recommendations and payment plans are not posted back to HԾ in MVP.

## Supplier daily movement mapping

The Suppliers screen needs these fields per supplier and day:

`date, supplier, contract/direction, opening_debt, order_amount, payment_for_order, old_debt_payment, old_debt_due_date`.

The calculation is deterministic:

`debt_change = (order_amount - payment_for_order) - old_debt_payment`

`closing_debt = opening_debt + debt_change`

The daily total is the sum of all supplier `debt_change` values. Positive means total debt increased; negative means it decreased.

## Adapter location

`DataProvider.cs` contains `IHtsDataProvider`. Add `HtsApiDataProvider` that authenticates to HԾ, maps source DTOs into the domain snapshot, and retains `DemoDataProvider` for offline testing.

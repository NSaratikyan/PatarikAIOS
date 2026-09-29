# Excel imports and manual balance updates

## Implemented
- Explicit Excel mode persisted in LocalApplicationData/PatarikAIOS/excel-imports.json; API mode can be restored.
- Three supplied XLSX layouts: CashDocument, WarehouseDocument, SalesAnalysis. Read sheet1, validate required data, retain identifiers and units.
- Preview before saving. Daily full-report replacement rather than append; overlapping batch dates rejected. Source workbooks are never edited.
- Draft/nonregistered documents excluded with warnings; mismatched cash/sales totals or document counts block import.
- Stale grand total produces warning; actual rows are used.
- Cost warnings when cost is negative, at most 1% of positive sales, or greater than positive sales. Original values retained. Unlinked suppliers and identical product rows flagged, not discarded.
- Sales, cost, gross profit, receipt count, average receipt, supplier analysis, receipt/payment activity, product/unit summaries from files.
- Manual daily non-cash sales separate from closing bank balance. Missing split does not fabricate cash-sale inflows. Values exceeding daily sales rejected.
- Cash/bank closing corrections preserve prior entries, notes and timestamps; latest same-day correction wins.
- Dedicated supplier debt correction button. Explicit zero balances persist. Excel debt comparison requires dated manual baseline and complete cash/warehouse date coverage.
- Pending approval name warnings (red), duplicate warnings (orange), probable name suggestions; bulk approval excludes flagged records. Duplicate single approval asks confirmation.

## Important boundaries
- Excel debt comparison is separate, not an automatic overwrite of operational supplier debts. Returns and bank/manual payment reconciliation still need complete source coverage.
- Return formats have not been supplied. Unrecognized return document types are blocked, never silently interpreted as purchases or income.
- Same-amount manual/import payments may represent duplicates or different payments. Warn instead of auto-deleting.
- Non-cash sales are not proof of bank settlement. User must correct actual bank closing balance as needed.
- No background folder watcher, live Telegram numeric-input command, or automatic import has been added.
- File-backed storage updates take effect only after explicit confirmation. Partial daily exports must not be used as full-day replacements.
- Local tests do not start the production UI, send Telegram messages, or change production data.

## Verification
- 19 operations regression checks, 9 Telegram regression checks.
- Additional read-only checks against the three supplied files, with isolated temporary state:
  330 registered cash records (2 drafts excluded), 535 sales lines / 317 sales documents,
  sales 193576.40, cost 96502.02, profit 97074.38,
  14 receipts totaling 182969.74, stale total warning,
  repeated imports do not duplicate, unknown supplier sales preserved,
  manual noncash split and debt-baseline/missing-baseline checks.

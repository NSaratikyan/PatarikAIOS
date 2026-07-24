-- Patarik AI OS MVP data model (PostgreSQL target).
-- HԾ remains the source of truth; external_id maintains traceability.

create table app_user (
  id uuid primary key, display_name text not null, role text not null check (role in ('owner','employee','accountant')),
  telegram_chat_id text, is_active boolean not null default true, created_at timestamptz not null default now()
);

create table supplier (
  id uuid primary key, external_id text unique, name text not null, active boolean not null default true,
  priority_score integer not null default 50 check (priority_score between 0 and 100), payment_mode text,
  credit_limit numeric(14,2), owner_note text, updated_at timestamptz not null default now()
);

create table supplier_contract (
  id uuid primary key, supplier_id uuid not null references supplier(id), external_id text,
  name text not null, payment_terms text, priority_override integer, unique (supplier_id, name)
);

create table product (
  id uuid primary key, external_id text unique, name text not null, unit text not null,
  product_type text not null check (product_type in ('raw_material','resale','finished_good')), active boolean not null default true
);

create table supplier_balance_snapshot (
  id uuid primary key, supplier_id uuid not null references supplier(id), contract_id uuid references supplier_contract(id),
  balance_date date not null, balance numeric(14,2) not null, source_updated_at timestamptz, unique(supplier_id, contract_id, balance_date)
);

create table purchase_receipt (
  id uuid primary key, external_id text unique, supplier_id uuid not null references supplier(id),
  contract_id uuid references supplier_contract(id), received_at timestamptz not null, warehouse_external_id text,
  total_amount numeric(14,2) not null, source_updated_at timestamptz
);

create table purchase_receipt_line (
  id uuid primary key, receipt_id uuid not null references purchase_receipt(id) on delete cascade,
  product_id uuid not null references product(id), quantity numeric(14,3) not null, unit_price numeric(14,2) not null,
  amount numeric(14,2) not null
);

-- Daily financial interpretation of a supplier's receipt/payment movement.
-- It makes "payment of today's order" and "payment of old debt" independently auditable.
create table supplier_daily_movement (
  id uuid primary key, movement_date date not null, supplier_id uuid not null references supplier(id),
  contract_id uuid references supplier_contract(id), opening_debt numeric(14,2) not null,
  order_amount numeric(14,2) not null default 0, payment_for_order numeric(14,2) not null default 0,
  old_debt_payment numeric(14,2) not null default 0, old_debt_due_date date,
  note text, source_document_id text, imported_at timestamptz not null default now()
);
create index ix_supplier_daily_movement_date on supplier_daily_movement(movement_date, supplier_id);

create table payment_plan (
  id uuid primary key, supplier_id uuid references supplier(id), contract_id uuid references supplier_contract(id),
  due_date date not null, suggested_amount numeric(14,2) not null, mandatory boolean not null default false,
  status text not null default 'proposed' check(status in ('proposed','approved','rejected','completed','deferred')),
  reason text not null, approved_by uuid references app_user(id), created_at timestamptz not null default now()
);

-- Owner-entered changes: a rescheduled or newly agreed payment must be audit-friendly.
create table payment_change_note (
  id uuid primary key, payment_plan_id uuid not null references payment_plan(id) on delete cascade,
  change_type text not null check(change_type in ('new_agreement','rescheduled','not_paid_today')),
  instruction text not null, entered_by uuid references app_user(id), entered_at timestamptz not null default now()
);

create table cash_balance_snapshot (
  id uuid primary key, snapshot_at timestamptz not null, cash_amount numeric(14,2) not null,
  bank_amount numeric(14,2) not null, source_updated_at timestamptz
);

create table cash_forecast (
  id uuid primary key, forecast_date date not null, expected_income numeric(14,2) not null,
  expected_outflow numeric(14,2) not null, closing_balance numeric(14,2) not null, calculated_at timestamptz not null default now()
);

create table management_rule (
  id uuid primary key, rule_type text not null, subject_type text, subject_id uuid,
  instruction text not null, priority integer not null default 50, active_from date, active_to date,
  created_by uuid references app_user(id), created_at timestamptz not null default now()
);

create table ai_recommendation (
  id uuid primary key, module text not null, severity text not null check(severity in ('info','warning','critical')),
  title text not null, finding text not null, suggested_action text not null, evidence jsonb not null default '{}'::jsonb,
  status text not null default 'proposed' check(status in ('proposed','approved','rejected','expired','completed')),
  requires_approval boolean not null default true, created_at timestamptz not null default now(), reviewed_by uuid references app_user(id)
);

create table task (
  id uuid primary key, assignee_id uuid references app_user(id), recommendation_id uuid references ai_recommendation(id),
  title text not null, due_at timestamptz, status text not null default 'open' check(status in ('open','in_progress','done','blocked','cancelled')),
  created_at timestamptz not null default now(), completed_at timestamptz
);

create index ix_receipt_received_at on purchase_receipt(received_at);
create index ix_payment_plan_date_status on payment_plan(due_date, status);
create index ix_recommendation_status_created on ai_recommendation(status, created_at desc);

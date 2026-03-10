# Money Flow & Validation Audit

This document summarizes checks and fixes for calculations, duplicates, validation, and receipt/print behaviour.

---

## 1. Receipt calculations (fixed)

| Item | Status | Notes |
|------|--------|--------|
| **Previous Balance** | Fixed | Now uses **CLEARED** payments only (same as customer balance and BalanceService). Before: used all payments, so receipt could show a different "previous balance" than the ledger. |
| **Remaining Balance** | Fixed | When negative (customer credit), receipt now shows **"Credit (Balance in your favour): X.XX AED"** instead of "Remaining Balance: -X.XX AED" to avoid confusion. |
| **Total / Amount Paid** | OK | Always equals sum of **selected** payment amounts from DB. No double-counting. |
| **Exact payment set** | OK | Receipt reuses an existing receipt only when its payments **exactly** match the selected IDs (no "all bills" when you selected two). |

---

## 2. Duplicates & validation

| Item | Status | Notes |
|------|--------|--------|
| **Duplicate payment IDs** | OK | Backend rejects batch with duplicate IDs. Frontend sends distinct IDs. |
| **Payment amount > 0** | OK | PaymentService validates `request.Amount > 0`. |
| **Same customer** | OK | All payments on one receipt must belong to the same customer. |
| **Payment IDs exist** | OK | Service throws if any ID not found or count mismatch. |

---

## 3. Customer balance consistency

| Source | Formula | Notes |
|--------|---------|--------|
| **BalanceService** | TotalSales − TotalPayments (CLEARED only) | Used for stored customer balance. |
| **CustomerService.Recalculate** | Sales − Payments (CLEARED) − SalesReturns | Same idea; includes returns. |
| **Receipt Previous Balance** | Sales (before asOfDate) − Payments (CLEARED, before asOfDate) | Now aligned with above. |
| **Ledger entries** | Running balance from sales (debit) and payments (credit) | Built from actual sales/payments; payment totals per sale include all payments for display. |

---

## 4. Print & display

| Item | Status | Notes |
|------|--------|--------|
| **Receipt Print button** | Fixed | Opens new window, writes receipt HTML, calls `print()`. If pop-ups are blocked, user sees message to allow them. |
| **Wrong numbers on receipt** | Fixed | Caused by reusing a receipt that had more payments than selected; now reuse only when payment set matches exactly. |

---

## 5. What to do (recommendations)

1. **Deploy** the latest backend and frontend so all receipt and print fixes are live.
2. **Run schema script** if you still see "column s.RoundOff does not exist" (or redeploy so startup runs `ApplyMissingSchema.sql`).
3. **Recalculate balances** if you ever suspect stored balances are wrong: use Admin/validation or the "Recalculate balance" action on the customer ledger so stored balance = Sales − CLEARED Payments − Returns.
4. **Check validation API** (if you have one) for balance mismatches; fix any data then recalc.
5. **Test receipt flow**: select 2 payments → Generate Receipt → confirm only those 2 and correct total → Print (allow pop-ups if prompted).

---

## 6. Files changed in this audit

- `backend/FrozenApi/Services/PaymentReceiptService.cs` – CLEARED-only for previous balance; credit label when remaining &lt; 0.
- `backend/FrozenApi/Controllers/PaymentsController.cs` – Duplicate payment ID validation (already in place).
- `frontend/frozen-ui/src/components/ReceiptPreviewModal.jsx` – Print via new window; distinct payment IDs when calling API (already in place).

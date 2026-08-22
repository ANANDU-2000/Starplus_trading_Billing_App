# Starplus Billing — Deferred Audit Items (Client Sign-off Required)

Follow-up to the stale-PDF cache fix and deep-dive edge-case audits (Passes 1–4). These items were confirmed in code but are **not implemented** — they need client or accountant approval before any code change.

---

## 1. Payment records deleted with invoice

**Location:** `backend/FrozenApi/Services/SaleService.cs` — `DeleteSaleAsync` (~lines 1647–1671)

**Current behavior:** When an invoice is deleted, related `Payment` rows are **hard-deleted** (`_context.Payments.Remove(payment)`).

**Impact:**

- If a customer paid against an invoice and received a printed/saved receipt, deleting that invoice removes the payment record permanently.
- Reprinting the receipt (`GET /payments/receipt/{id}/pdf`) returns 404 after invoice delete.
- The "delete and recreate invoice" workaround can silently destroy payment audit history.

**Proposed direction (after sign-off):**

- Soft-delete payments (same `IsDeleted` pattern already used on `Sale`).
- Filter active payment queries/reports with `!IsDeleted`.
- Allow receipt PDF endpoint to resolve soft-deleted payments for reprint/audit.

**Question for client:** Should payment records survive invoice deletion for VAT/audit compliance?

---

## 2. Invoice-level discount applied after VAT

**Location:** `backend/FrozenApi/Services/SaleService.cs` — `CreateSaleAsync` / `UpdateSaleAsync` (~lines 1213–1251)

**Current behavior:**

- Each line's VAT is calculated on the full line total (`rowTotal × vatRate`).
- Invoice-level `Discount` is subtracted **after** VAT is summed: `calcTotal = subtotal + vatTotal - request.Discount`.
- Per-line VAT on the printed tax invoice does not reflect the discount.

**Impact:** Business/compliance question, not a runtime bug. Under UAE FTA rules, trade discounts generally reduce taxable value before VAT; settlement/cash discounts may be treated differently.

**Question for client's accountant:** Are POS invoice discounts **trade discounts** (pre-VAT) or **post-VAT settlement discounts** (current behavior)? Confirm before any code change.

---

## 3. Historical invoice template (handoff note only)

**Location:** `InvoiceTemplateService.RenderActiveTemplateAsync` — no `Sale.InvoiceTemplateId` stored at issue time.

**Current behavior:** Reprinting any historical invoice uses the **currently active** template (letterhead, TRN, footer, layout).

**Impact:** Cosmetic for most businesses; material only if template changes affect mandatory fields and old invoices are reprinted.

**No action needed** unless the client plans active template redesign while reprinting historical invoices.

---

## 4. Receipt shows live outstanding balance, not point-in-time (Pass 4)

**Location:** `backend/FrozenApi/Services/PaymentReceiptService.cs` (~lines 190–200, 275)

**Current behavior:** `CurrentTotalOutstanding` is computed using `DateTime.UtcNow.Date` on every receipt PDF render. `PreviousBalance` is correctly as-of payment date.

**Impact:** Reprinting an old receipt shows **today's** outstanding balance, not what was owed at receipt time. May be intentional ("balance owed right now") — confirm with client.

**Question for client:** Should `CurrentTotalOutstanding` on receipts always reflect today's ledger, or be locked to the original receipt date?

---

## 5. Invoice number fallback not safe across multiple backend instances (Pass 4)

**Location:** `backend/FrozenApi/Services/InvoiceNumberService.cs` (~lines 26, 38)

**Current behavior:** Primary path uses Postgres `nextval('invoice_number_seq')` (safe). Fallback path (`MAX(invoice_no)+1`) is protected only by an in-process `SemaphoreSlim`.

**Impact:** Harmless on single-instance deployment (current Windows/NSSM setup). Becomes a duplicate-invoice-number risk only if the API scales to multiple processes behind a load balancer.

**Action:** Document for future scaling; no change needed today.

---

## 6. Frontend/backend rounding parity at negative `.5` midpoints (Pass 4)

**Location:**

- Frontend: `frontend/frozen-ui/src/utils/invoiceTotals.js` — `Math.round()` in `computeAutoRoundOffFromCalc`
- Backend: `SaleService.cs` — `Math.Round(x, 2, MidpointRounding.AwayFromZero)`

**Impact:** For **positive** amounts both agree. Negative round-off at exact `.5` can differ by 1 cent in POS **preview only** (backend recalculates authoritatively on save). Rare with real price/VAT combinations.

**If closing:** Fix frontend only with away-from-zero helper; do not change backend without explicit decision.

---

## Shipped fixes (no client action)

| Fix | Location |
|-----|----------|
| Stale PDF cache on edit | `clearCachedInvoicePdf(editingSaleId)` before success modal in `PosPage-AnanduPC.jsx` / `PosPage.jsx` |
| AnanduPC PDF deploy wiring | `App.jsx` — `PdfDocumentModal`, tablet POS/ledger variants |
| Admin override invoice date | `SaleService.CreateSaleWithOverrideAsync` — respects `request.InvoiceDate` |
| Activity Log page | `ActivityLogPage.jsx`, `AdminController.GetAuditLogs` filters, `/activity-log` route |

**Manual PDF test after deploy:**

1. Create invoice → wait for inline PDF in success modal → note total.
2. Edit same invoice (remove line) → save without page reload.
3. Success modal re-opens → inline preview must show **updated** total before any button click.
4. Print / View / Save and Customer Ledger PDF icon must all match.

**Note:** `validateHtmlReceiptBlob` in `pdfBlob.js` is **not** dead code — used by `ReceiptPreviewModal.jsx` and `PaymentsPage.jsx` as HTML-receipt fallback.

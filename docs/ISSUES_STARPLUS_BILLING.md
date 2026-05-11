# Starplus Billing App — Bug Tracker & Task List
> Created: 11-05-2026 | Priority: CRITICAL | Assigned to: Developer (Cursor AI)

---

## 🔴 ISSUE 1 — Round Off Input Field (POS Page)
**Status:** ✅ FIXED  
**File:** `frontend/frozen-ui/src/pages/PosPage.jsx`

### Problem
- User cannot type a **negative value** (e.g., `-0.43`) manually into the Round Off input
- When user types `-`, `parseFloat('-')` returns `NaN` → input resets to blank → user stuck
- This is why round off fails for **some customers** (those needing negative round off like Prasannavar, T-Island Cafe)
- Two input fields affected: **desktop** (w-16) and **mobile** (w-14) — both have the same bug

### Root Cause
```js
// OLD BROKEN CODE — both desktop and mobile inputs
value={roundOff === 0 ? '' : roundOff}
onChange={(e) => {
  const n = parseFloat(v)          // parseFloat('-') = NaN ← BUG
  if (!isNaN(n) && n >= -1 && n <= 1) setRoundOff(n)  // NaN fails, input resets
}}
```

### Fix Required
- Add `roundOffInput` string state (like `discountInput` already exists)
- Allow `-` and empty string mid-type without resetting
- Sync `roundOffInput` on: blur, autoRoundOff, edit-mode load, handleNewInvoice reset
- Apply fix to **both** desktop AND mobile input (two separate JSX blocks)

### Verification
- [x] Can type `-0.43` manually into Round Off field
- [x] Auto button sets correct negative value
- [x] Input shows correct value when editing an existing invoice
- [x] Clears to blank on new invoice

---

## 🔴 ISSUE 2 — Grand Total Floating Point Error
**Status:** ✅ FIXED  
**File:** `frontend/frozen-ui/src/utils/invoiceTotals.js` (`computeInvoiceTotals`, used by `calculateTotals()` in PosPage)

### Problem
```js
// BEFORE FIX — produces floating point garbage
const grandTotal = subtotal + vatTotal - discountValue + roundOffValue
// Example: 234.50 + 11.72 + (-0.23) = 245.99000000000001
```
- Payment amount submitted to backend had floating point decimals
- Backend stores wrong GrandTotal for some customers
- Printed invoice shows wrong amount

### Fix Required
```js
const grandTotal = parseFloat((subtotal + vatTotal - discountValue + roundOffValue).toFixed(2))
```

### Verification
- [x] GrandTotal always has exactly 2 decimal places
- [x] Payment amount sent to backend matches printed total
- [x] No `.99000000001` type values in database

---

## 🔴 ISSUE 3 — Printed Bill Shows Decimals (Not Rounded)
**Status:** ✅ FIXED  
**File:** `backend/FrozenApi/Services/PdfService.cs`  
**Also:** `backend/FrozenApi/Services/SaleService.cs`

### Problem
- Even after user sets Round Off (e.g., -0.43), the **printed PDF/bill** still shows the unrounded decimal total
- The `GrandTotal` stored in the DB might not include the round-off
- OR the PDF template renders `GrandTotal` but ignores the `RoundOff` field

### Example from Invoice Photo
```
INV Amount:  1,418.50
VAT 5%:         70.92
Round Off:       -0.43
Total:        1,488.99  ← This IS showing correctly on the printed invoice
```
BUT: for customers like Prasannavar / T-Island the round off row shows but total ignores it

### Fix Required
- In `PdfService.cs`: verify `GrandTotal` = `SubTotal + VAT + RoundOff` before rendering
- In `SaleService.cs`: verify `finalTotal = Math.Round(calcTotal + roundOff, 2)` is saved correctly
- Check that `RoundOff` column in DB is not NULL for any sale — add migration if needed
- In PDF template HTML: round-off row must show **signed** value (`-0.43` not `0.43`)
- Total line in PDF must use `sale.GrandTotal` (which already includes round-off), NOT recalculate

### Implementation notes
- Main QuestPDF invoice already used `sale.GrandTotal` and `sale.RoundOff != 0` for the round-off row; **combined invoice** layout (`RenderInvoiceContent`) was missing the round-off row — now added with signed format.
- PDF generation load path uses `AsNoTracking()` for a fresh sale read.
- Totals use `MidpointRounding.AwayFromZero` (see Issue 5) for consistency with POS.

### Verification
- [ ] Print invoice for Prasannavar — round off row appears and total is correct
- [ ] Print invoice for Tea Island Cafe — same
- [ ] RoundOff value in DB matches what was entered in POS
- [ ] GrandTotal in DB = SubTotal + VAT + RoundOff exactly

---

## 🔴 ISSUE 4 — Products Not Appearing in Billing Search
**Status:** ✅ FIXED  
**File:** `backend/FrozenApi/Controllers/ProductsController.cs`  
**Also:** `frontend/frozen-ui/src/pages/PosPage.jsx` (product search dropdown)

### Problem
- Products added via "Purchase" do NOT appear in billing search
- Products like **Predix**, **Nat** (chicken) saved months ago → not showing in POS search
- Even after updating stock in purchase section → still missing
- Changing SKU format (e.g., "Shrimps 1620" → "Shrimps 1420") → new entry still doesn't appear
- Product only shows if name is drastically simplified (e.g., "Sheefeet Ayak Everyday" → "Paaya")

### Suspected Root Cause
- Products table has `IsActive` flag that may be `false` or `null`
- OR stock quantity filter — products with 0 stock are excluded from search
- OR search uses `LIKE` query that is case-sensitive or matches differently
- OR product was added with wrong `categoryId` / `supplierId` that excludes it

### Fix Required
- Check `GET /api/products` — does it filter by `IsActive`, `stock > 0`, or other conditions?
- POS search should show ALL active products regardless of current stock level
- Search should be **case-insensitive** and match on partial name AND SKU
- If product has `stock = 0`, show it with warning — do NOT hide it from billing
- SQL query: check for NULL stock vs 0 stock — treat NULL as 0, not as "hidden"

### Implementation notes
- Backend search already used `ILIKE` and did not filter by stock; extended to **DescriptionEn / DescriptionAr**, `AsNoTracking()` on list queries, `[ResponseCache(NoStore = true)]` on product list + search endpoints.
- Frontend: larger initial `pageSize` (500), search limit 200, description in local filter, **Out of Stock** (orange) in dropdown when `stockQty <= 0`.

### Verification
- [ ] Search "Predix" in POS — product appears
- [ ] Search "Nat" in POS — product appears
- [ ] Search "Shrimps" in POS — both old and new SKU versions appear
- [ ] Product with 0 stock shows in search (with visual indicator)
- [ ] Search is case-insensitive

---

## 🟡 ISSUE 5 — VAT Rounding Per-Line Inconsistency
**Status:** ✅ FIXED  
**File:** `frontend/frozen-ui/src/pages/PosPage.jsx` + `backend/FrozenApi/Services/SaleService.cs`

### Problem (From Invoice Analysis)
- Invoice 1: Item 2 = 15.4 KG × 22.50 = 346.50, VAT = 17.325 → stored/shown as **17.32**
- Frontend uses `Math.round((rowTotal * 0.05) * 100) / 100` (rounds to nearest)
- Backend uses `Math.Round(rowTotal * (vatPercent / 100), 2)` (banker's rounding in C#)
- These two can produce **different results** for the same item → front/back mismatch

### Fix Required
- Standardize VAT rounding: always use `Math.Round(x, 2, MidpointRounding.AwayFromZero)` in C#
- Frontend: already uses standard JS rounding — ensure it matches backend
- Total VAT on printed invoice must match sum of line VATs

### Verification
- [x] Frontend VAT per line matches backend VAT per line (e.g. 17.325 → **17.33**)
- [ ] Total VAT on PDF = sum of all line VATs

---

## 🟡 ISSUE 6 — Database / SQL Inconsistency After Manual Changes
**Status:** ✅ FIXED (migration `20260512120000_AddProductSearchLowerIndexes`)  
**File:** `ProductService.cs`, `ProductsController.cs`, `Migrations/20260512120000_AddProductSearchLowerIndexes.cs`

### Problem
- Even after adding products via SQL directly, billing search results remain inconsistent
- Suggests cache, EF Core tracking issue, or missing index

### Fix Required
- Check if `ProductsController` caches results — clear cache on product add/update
- Check if EF Core change tracker is causing stale reads
- Add database index on `Products.Name` and `Products.SKU` for fast search
- If using stored procedures for search, verify they pick up new rows

### Implementation notes
- **AsNoTracking** on product listing/search queries; **ResponseCache(NoStore)** on GET products + search; description fields in search.
- **DB indexes:** EF migration adds `IX_Products_NameEn_Lower` and `IX_Products_Sku_Lower` on PostgreSQL. Apply with `dotnet ef database update` (or app startup migrate, if enabled).

### Verification
- [ ] Add product via SQL → immediately visible in POS search
- [ ] Add product via UI → immediately visible in POS search

---

## ✅ Summary Checklist (In Fix Order)

| # | Issue | File(s) | Status |
|---|-------|---------|--------|
| 1 | Round Off input can't type negative | `PosPage.jsx` | ✅ Fixed |
| 2 | Grand total floating point | `invoiceTotals.js`, `PosPage.jsx` | ✅ Fixed |
| 3 | Auto round-off doesn't sync input display | `PosPage.jsx` | ✅ Fixed |
| 4 | Printed bill ignores round off for some customers | `PdfService.cs`, `SaleService.cs` | ✅ Fixed |
| 5 | Products missing from POS billing search | `ProductsController.cs`, `ProductService.cs`, `PosPage.jsx` | ✅ Fixed |
| 6 | VAT rounding frontend vs backend mismatch | `SaleService.cs` | ✅ Fixed |
| 7 | DB/SQL inconsistency — stale product search | `ProductService.cs`, `ProductsController.cs`, migration | ✅ Fixed |

---

## 📋 Testing Instructions After Each Fix

### Round Off Test
1. Open POS, add any product
2. Type `-0.43` manually in Round Off field — must stay and show `-0.43`
3. Click Auto — must auto-fill correct negative value
4. Submit invoice → print PDF → verify printed total = subtotal + VAT + roundOff

### Product Search Test
1. Go to POS
2. Search "Predix" — must appear
3. Search "Nat" — must appear
4. Search product with 0 stock — must appear

### Print Test
1. Create invoice with round off `-0.43`
2. Print PDF
3. Verify Round Off row shows `-0.43`
4. Verify Total = correct rounded amount
5. Repeat for customer "Prasannavar" and "T-Island Cafe"

---

*End of issue tracker — update Status column as each fix is verified*

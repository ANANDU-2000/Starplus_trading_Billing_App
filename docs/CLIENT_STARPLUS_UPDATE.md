# Starplus Billing — Update for client (May 2026)

Use this as an email or WhatsApp summary after deployment.

---

## Short message (copy-paste)

Hello,

We have deployed an update to your **Starplus Foodstuff Trading** billing system. Here is what is improved:

1. **Round off on invoices** — You can now enter **negative** round-off (for example **-0.43**) in the POS screen without the field clearing. **Auto** round-off still works. Printed invoices show the round-off line and the **total matches** what you saved.

2. **Totals** — Invoice totals are rounded cleanly to **two decimals** (no stray long decimals in the system or on PDFs).

3. **Product search (billing)** — Search is **case-insensitive** (e.g. “predix” finds “Predix”). **Zero-stock** products still appear in search with an **“Out of Stock”** label so you can bill or see them; inactive/deleted products stay hidden.

4. **VAT** — Line VAT on screen and in the database now use the **same rounding rule**, so small differences between POS and invoice should not appear.

5. **Database** — After deploy, we run the latest **migration** once on your PostgreSQL server so product search stays fast on large lists.

**What you should do:**  
- Hard refresh the billing page (Ctrl+F5) or clear cache on tablets if something looks old.  
- Try one test invoice with round off **-0.43**, save, then **Print / PDF** and check the total.  
- If anything looks wrong on a **specific old invoice**, tell us the invoice number—we can check the stored total in the database.

Thank you.

---

## How to use (for staff)

| Area | What to do |
|------|------------|
| **Round off** | Type amount between **-1.00** and **+1.00**, or tap **Auto**. Negative values (e.g. -0.43) are allowed. |
| **Grand total** | Shown at bottom of POS; this is what is saved and what appears on the PDF. |
| **Search products** | Type part of **name**, **SKU**, or **description**; capitals do not matter. Items with **no stock** show **Out of Stock** but can still be selected if your process allows. |
| **Print** | After saving, use your usual **Print invoice / PDF** action; total should match the POS grand total including round off. |

---

## For IT / host (deploy)

1. Deploy new **API** and **frontend** builds to your server (e.g. Render + static hosting as you already use).  
2. On the PostgreSQL database used by the API, apply migrations (example):

   ```bash
   dotnet ef database update --project path/to/FrozenApi
   ```

   Or run the API once in an environment where migrations apply on startup, if you have that enabled.

3. Confirm `__EFMigrationsHistory` includes **`20260512120000_AddProductSearchLowerIndexes`**.

---

*Internal reference: see `ISSUES_STARPLUS_BILLING.md` for technical detail.*

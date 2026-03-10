-- ApplyMissingSchema.sql
-- One-time script for production PostgreSQL when migrations did not run (e.g. "column s.RoundOff does not exist").
-- Run once against your Render Postgres (Dashboard -> Postgres -> Connect / PSQL). Idempotent: safe to run multiple times.

-- 1. Sales.RoundOff
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'Sales' AND column_name = 'RoundOff'
  ) THEN
    ALTER TABLE "Sales" ADD COLUMN "RoundOff" numeric(10,4) NOT NULL DEFAULT 0;
  END IF;
END $$;

-- 2. PaymentReceipts table (if not exists)
CREATE TABLE IF NOT EXISTS "PaymentReceipts" (
  "Id" serial PRIMARY KEY,
  "ReceiptNumber" varchar(30) NOT NULL,
  "GeneratedAt" timestamp without time zone NOT NULL,
  "GeneratedByUserId" integer NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
  "PdfStoragePath" varchar(500) NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_PaymentReceipts_ReceiptNumber" ON "PaymentReceipts"("ReceiptNumber");
CREATE INDEX IF NOT EXISTS "IX_PaymentReceipts_GeneratedByUserId" ON "PaymentReceipts"("GeneratedByUserId");

-- 3. PaymentReceiptPayments table (if not exists)
CREATE TABLE IF NOT EXISTS "PaymentReceiptPayments" (
  "Id" serial PRIMARY KEY,
  "PaymentReceiptId" integer NOT NULL REFERENCES "PaymentReceipts"("Id") ON DELETE CASCADE,
  "PaymentId" integer NOT NULL REFERENCES "Payments"("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "IX_PaymentReceiptPayments_PaymentReceiptId" ON "PaymentReceiptPayments"("PaymentReceiptId");
CREATE INDEX IF NOT EXISTS "IX_PaymentReceiptPayments_PaymentId" ON "PaymentReceiptPayments"("PaymentId");

-- 4. ExpenseCategories columns
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'ExpenseCategories' AND column_name = 'DefaultVatRate') THEN
    ALTER TABLE "ExpenseCategories" ADD COLUMN "DefaultVatRate" numeric(5,4) NOT NULL DEFAULT 0;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'ExpenseCategories' AND column_name = 'DefaultTaxType') THEN
    ALTER TABLE "ExpenseCategories" ADD COLUMN "DefaultTaxType" varchar(20) NOT NULL DEFAULT 'Standard';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'ExpenseCategories' AND column_name = 'DefaultIsTaxClaimable') THEN
    ALTER TABLE "ExpenseCategories" ADD COLUMN "DefaultIsTaxClaimable" boolean NOT NULL DEFAULT false;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'ExpenseCategories' AND column_name = 'DefaultIsEntertainment') THEN
    ALTER TABLE "ExpenseCategories" ADD COLUMN "DefaultIsEntertainment" boolean NOT NULL DEFAULT false;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'ExpenseCategories' AND column_name = 'VatDefaultLocked') THEN
    ALTER TABLE "ExpenseCategories" ADD COLUMN "VatDefaultLocked" boolean NOT NULL DEFAULT false;
  END IF;
END $$;

-- 5. Expenses columns
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Expenses' AND column_name = 'VatRate') THEN
    ALTER TABLE "Expenses" ADD COLUMN "VatRate" numeric(5,4) NULL;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Expenses' AND column_name = 'VatAmount') THEN
    ALTER TABLE "Expenses" ADD COLUMN "VatAmount" numeric(18,2) NULL;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Expenses' AND column_name = 'TotalAmount') THEN
    ALTER TABLE "Expenses" ADD COLUMN "TotalAmount" numeric(18,2) NULL;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Expenses' AND column_name = 'TaxType') THEN
    ALTER TABLE "Expenses" ADD COLUMN "TaxType" varchar(20) NULL;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Expenses' AND column_name = 'IsTaxClaimable') THEN
    ALTER TABLE "Expenses" ADD COLUMN "IsTaxClaimable" boolean NULL;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Expenses' AND column_name = 'IsEntertainment') THEN
    ALTER TABLE "Expenses" ADD COLUMN "IsEntertainment" boolean NULL;
  END IF;
END $$;

-- 6. ExpenseCategories VAT defaults (by Id) - only if columns exist
UPDATE "ExpenseCategories" SET "DefaultVatRate" = 0.05, "DefaultTaxType" = 'Standard', "DefaultIsTaxClaimable" = true WHERE "Id" = 1;
UPDATE "ExpenseCategories" SET "DefaultVatRate" = 0.05, "DefaultTaxType" = 'Standard', "DefaultIsTaxClaimable" = true WHERE "Id" = 2;
UPDATE "ExpenseCategories" SET "DefaultTaxType" = 'OutOfScope', "DefaultIsTaxClaimable" = false WHERE "Id" = 3;
UPDATE "ExpenseCategories" SET "DefaultVatRate" = 0.05, "DefaultTaxType" = 'Standard', "DefaultIsTaxClaimable" = true WHERE "Id" = 4;
UPDATE "ExpenseCategories" SET "DefaultTaxType" = 'Petroleum', "DefaultIsTaxClaimable" = false WHERE "Id" = 5;
UPDATE "ExpenseCategories" SET "DefaultVatRate" = 0.05, "DefaultTaxType" = 'Standard', "DefaultIsTaxClaimable" = true WHERE "Id" = 6;
UPDATE "ExpenseCategories" SET "DefaultVatRate" = 0.05, "DefaultIsEntertainment" = true WHERE "Id" = 7;
UPDATE "ExpenseCategories" SET "DefaultVatRate" = 0.05, "DefaultTaxType" = 'Standard', "DefaultIsTaxClaimable" = true WHERE "Id" = 8;
UPDATE "ExpenseCategories" SET "DefaultTaxType" = 'Exempt', "DefaultIsTaxClaimable" = false WHERE "Id" = 9;

-- 7. Record migrations in EF history so future app Migrate() does not re-apply (idempotent)
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES
  ('20260310120000_AddRoundOffToSales', '9.0.0'),
  ('20260310130000_AddPaymentReceipts', '9.0.0'),
  ('20260310140000_AddVatDefaultsToExpenseCategories', '9.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

using FrozenApi.Models;

namespace FrozenApi.Services
{
    public interface IPdfService
    {
        Task<byte[]> GenerateInvoicePdfAsync(SaleDto sale);
        Task<byte[]> GenerateCombinedInvoicePdfAsync(List<SaleDto> sales);
        Task<byte[]> GenerateSalesLedgerPdfAsync(SalesLedgerReportDto ledgerReport, DateTime fromDate, DateTime toDate);
        Task<byte[]> GeneratePendingBillsPdfAsync(List<PendingBillDto> pendingBills, DateTime fromDate, DateTime toDate);
        Task<byte[]> GenerateCustomerPendingBillsPdfAsync(List<OutstandingInvoiceDto> outstandingInvoices, CustomerDto customer, DateTime asOfDate, DateTime fromDate, DateTime toDate);
    }
}

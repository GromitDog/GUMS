using GUMS.Data.Entities;

namespace GUMS.Services;

public interface IReconciliationService
{
    Task<BankReconciliation?> GetCurrentReconciliationAsync();
    Task<(bool Success, string ErrorMessage, BankReconciliation? Reconciliation)> StartReconciliationAsync(
        DateTime statementDate, decimal statementBalance, string? notes);
    Task<List<ReconciliationLineDto>> GetBankLinesForReconciliationAsync(int reconciliationId);
    Task<(bool Success, string ErrorMessage)> ToggleLineClearedAsync(int reconciliationId, int transactionLineId);
    Task<ReconciliationSummary> GetReconciliationSummaryAsync(int reconciliationId);
    Task<(bool Success, string ErrorMessage)> CompleteReconciliationAsync(int reconciliationId);
    Task<(bool Success, string ErrorMessage)> AbandonReconciliationAsync(int reconciliationId);
    Task<List<BankReconciliation>> GetReconciliationHistoryAsync();
    Task<BankReconciliation?> GetLastCompletedReconciliationAsync();
    Task<decimal> GetCurrentBankBookBalanceAsync();
}

public class ReconciliationLineDto
{
    public int TransactionLineId { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool IsCleared { get; set; }
}

public class ReconciliationSummary
{
    public decimal LastReconciledBalance { get; set; }
    public decimal ClearedItemsTotal { get; set; }
    public decimal ClearedBalance => LastReconciledBalance + ClearedItemsTotal;
    public decimal StatementBalance { get; set; }
    public decimal Difference => ClearedBalance - StatementBalance;
}

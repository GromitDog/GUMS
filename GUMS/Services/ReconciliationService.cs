using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using Microsoft.EntityFrameworkCore;

namespace GUMS.Services;

public class ReconciliationService : IReconciliationService
{
    private readonly ApplicationDbContext _context;
    private const string BankAccountCode = "1003";

    public ReconciliationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BankReconciliation?> GetCurrentReconciliationAsync()
    {
        return await _context.BankReconciliations
            .AsNoTracking()
            .FirstOrDefaultAsync(br => br.Status == ReconciliationStatus.InProgress);
    }

    public async Task<(bool Success, string ErrorMessage, BankReconciliation? Reconciliation)> StartReconciliationAsync(
        DateTime statementDate, decimal statementBalance, string? notes)
    {
        var existing = await _context.BankReconciliations
            .AnyAsync(br => br.Status == ReconciliationStatus.InProgress);

        if (existing)
            return (false, "There is already an active reconciliation in progress.", null);

        var reconciliation = new BankReconciliation
        {
            StatementDate = statementDate,
            StatementBalance = statementBalance,
            Status = ReconciliationStatus.InProgress,
            CreatedDate = DateTime.Now,
            Notes = notes
        };

        _context.BankReconciliations.Add(reconciliation);
        await _context.SaveChangesAsync();

        return (true, string.Empty, reconciliation);
    }

    public async Task<List<ReconciliationLineDto>> GetBankLinesForReconciliationAsync(int reconciliationId)
    {
        var bankAccount = await GetBankAccountAsync();
        if (bankAccount == null)
            return new List<ReconciliationLineDto>();

        // Get unreconciled bank lines + lines cleared in this session
        var lines = await _context.TransactionLines
            .AsNoTracking()
            .Include(tl => tl.Transaction)
            .Where(tl => tl.AccountId == bankAccount.Id
                         && !tl.Transaction.IsVoided
                         && (tl.BankReconciliationId == null || tl.BankReconciliationId == reconciliationId))
            .OrderBy(tl => tl.Transaction.Date)
            .ThenBy(tl => tl.Id)
            .Select(tl => new ReconciliationLineDto
            {
                TransactionLineId = tl.Id,
                Date = tl.Transaction.Date,
                Description = tl.Transaction.Description,
                Amount = tl.Debit - tl.Credit,
                IsCleared = tl.BankReconciliationId == reconciliationId
            })
            .ToListAsync();

        return lines;
    }

    public async Task<(bool Success, string ErrorMessage)> ToggleLineClearedAsync(
        int reconciliationId, int transactionLineId)
    {
        var reconciliation = await _context.BankReconciliations
            .FirstOrDefaultAsync(br => br.Id == reconciliationId
                                       && br.Status == ReconciliationStatus.InProgress);

        if (reconciliation == null)
            return (false, "Reconciliation not found or already completed.");

        var line = await _context.TransactionLines
            .FirstOrDefaultAsync(tl => tl.Id == transactionLineId);

        if (line == null)
            return (false, "Transaction line not found.");

        if (line.BankReconciliationId == reconciliationId)
        {
            // Unclear the line
            line.BankReconciliationId = null;
        }
        else if (line.BankReconciliationId == null)
        {
            // Clear the line
            line.BankReconciliationId = reconciliationId;
        }
        else
        {
            return (false, "This line has already been reconciled in another session.");
        }

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<ReconciliationSummary> GetReconciliationSummaryAsync(int reconciliationId)
    {
        var reconciliation = await _context.BankReconciliations
            .AsNoTracking()
            .FirstOrDefaultAsync(br => br.Id == reconciliationId);

        if (reconciliation == null)
            return new ReconciliationSummary();

        var lastCompleted = await GetLastCompletedReconciliationAsync();
        var lastReconciledBalance = lastCompleted?.ReconciledBookBalance ?? 0m;

        // Sum of cleared items in this session (debit - credit for bank account)
        var clearedTotal = await _context.TransactionLines
            .AsNoTracking()
            .Where(tl => tl.BankReconciliationId == reconciliationId)
            .SumAsync(tl => tl.Debit - tl.Credit);

        return new ReconciliationSummary
        {
            LastReconciledBalance = lastReconciledBalance,
            ClearedItemsTotal = clearedTotal,
            StatementBalance = reconciliation.StatementBalance
        };
    }

    public async Task<(bool Success, string ErrorMessage)> CompleteReconciliationAsync(int reconciliationId)
    {
        var reconciliation = await _context.BankReconciliations
            .FirstOrDefaultAsync(br => br.Id == reconciliationId
                                       && br.Status == ReconciliationStatus.InProgress);

        if (reconciliation == null)
            return (false, "Reconciliation not found or already completed.");

        var summary = await GetReconciliationSummaryAsync(reconciliationId);
        if (summary.Difference != 0)
            return (false, $"Cannot complete: difference of {summary.Difference:C} remains.");

        reconciliation.Status = ReconciliationStatus.Completed;
        reconciliation.CompletedDate = DateTime.Now;
        reconciliation.ReconciledBookBalance = summary.ClearedBalance;

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> AbandonReconciliationAsync(int reconciliationId)
    {
        var reconciliation = await _context.BankReconciliations
            .FirstOrDefaultAsync(br => br.Id == reconciliationId
                                       && br.Status == ReconciliationStatus.InProgress);

        if (reconciliation == null)
            return (false, "Reconciliation not found or already completed.");

        // Unclear all lines associated with this reconciliation
        var clearedLines = await _context.TransactionLines
            .Where(tl => tl.BankReconciliationId == reconciliationId)
            .ToListAsync();

        foreach (var line in clearedLines)
        {
            line.BankReconciliationId = null;
        }

        _context.BankReconciliations.Remove(reconciliation);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<List<BankReconciliation>> GetReconciliationHistoryAsync()
    {
        return await _context.BankReconciliations
            .AsNoTracking()
            .Where(br => br.Status == ReconciliationStatus.Completed)
            .OrderByDescending(br => br.StatementDate)
            .ToListAsync();
    }

    public async Task<BankReconciliation?> GetLastCompletedReconciliationAsync()
    {
        return await _context.BankReconciliations
            .AsNoTracking()
            .Where(br => br.Status == ReconciliationStatus.Completed)
            .OrderByDescending(br => br.CompletedDate)
            .FirstOrDefaultAsync();
    }

    public async Task<decimal> GetCurrentBankBookBalanceAsync()
    {
        var bankAccount = await GetBankAccountAsync();
        return bankAccount?.Balance ?? 0m;
    }

    private async Task<Account?> GetBankAccountAsync()
    {
        return await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Code == BankAccountCode);
    }
}

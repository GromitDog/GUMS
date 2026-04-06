using GUMS.Data.Entities;
using GUMS.Data.Enums;

namespace GUMS.Services;

/// <summary>
/// Service for managing member credit balances.
/// Credits are created when paid payments are converted (e.g. cancelled trip deposit),
/// and can be applied to pending payments or refunded as cash.
/// </summary>
public interface ICreditService
{
    /// <summary>
    /// Gets the current credit balance for a member.
    /// </summary>
    Task<decimal> GetCreditBalanceAsync(string membershipNumber);

    /// <summary>
    /// Gets the credit transaction history for a member.
    /// </summary>
    Task<List<CreditTransaction>> GetCreditHistoryAsync(string membershipNumber);

    /// <summary>
    /// Gets all members who have a credit balance greater than zero.
    /// </summary>
    Task<List<MemberCreditSummary>> GetMembersWithCreditAsync();

    /// <summary>
    /// Converts a paid payment (or part of it) into member credit.
    /// Marks the source payment as Refunded and creates a credit balance.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> ConvertPaymentToCreditAsync(
        int paymentId, decimal amount, string reason);

    /// <summary>
    /// Applies credit from a member's balance to a pending payment.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> ApplyCreditToPaymentAsync(
        string membershipNumber, int targetPaymentId, decimal amount);

    /// <summary>
    /// Refunds credit as cash to the parent.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> RefundCreditAsync(
        string membershipNumber, decimal amount, PaymentMethod method, DateTime refundDate, string reason);

    /// <summary>
    /// Converts all paid activity payments for a meeting to credit,
    /// then optionally auto-applies to each member's oldest pending subs.
    /// </summary>
    Task<(bool Success, string ErrorMessage, List<BulkCreditResult> Results)> BulkConvertMeetingToCreditAsync(
        int meetingId, bool autoApplyToSubs, string reason);
}

/// <summary>
/// Summary of a member's credit balance.
/// </summary>
public class MemberCreditSummary
{
    public string MembershipNumber { get; set; } = string.Empty;
    public string? MemberName { get; set; }
    public decimal CreditBalance { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Result of a bulk credit conversion for a single member.
/// </summary>
public class BulkCreditResult
{
    public string MembershipNumber { get; set; } = string.Empty;
    public string? MemberName { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal AppliedToSubs { get; set; }
    public decimal RemainingCredit { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

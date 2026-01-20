using GUMS.Data.Entities;
using GUMS.Data.Enums;

namespace GUMS.Services;

/// <summary>
/// Service for managing payments, termly subscriptions, and activity payments.
/// </summary>
public interface IPaymentService
{
    // ===== CRUD Operations =====

    /// <summary>
    /// Gets a payment by its ID.
    /// </summary>
    Task<Payment?> GetByIdAsync(int id);

    /// <summary>
    /// Gets all payments ordered by due date (most recent first).
    /// </summary>
    Task<List<Payment>> GetAllAsync();

    /// <summary>
    /// Creates a new payment.
    /// </summary>
    Task<(bool Success, string ErrorMessage, Payment? Payment)> CreateAsync(Payment payment);

    /// <summary>
    /// Updates an existing payment.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> UpdateAsync(Payment payment);

    /// <summary>
    /// Deletes a payment.
    /// </summary>
    Task<(bool Success, string ErrorMessage)> DeleteAsync(int id);

    // ===== Query Methods =====

    /// <summary>
    /// Gets payments filtered by status.
    /// </summary>
    Task<List<Payment>> GetByStatusAsync(PaymentStatus status);

    /// <summary>
    /// Gets all payments for a specific member.
    /// </summary>
    Task<List<Payment>> GetByMembershipNumberAsync(string membershipNumber);

    /// <summary>
    /// Gets all payments for a specific term.
    /// </summary>
    Task<List<Payment>> GetByTermAsync(int termId);

    /// <summary>
    /// Gets the payment for a specific meeting (activity payments).
    /// </summary>
    Task<List<Payment>> GetByMeetingAsync(int meetingId);

    /// <summary>
    /// Gets all overdue payments (Pending status with DueDate < today).
    /// </summary>
    Task<List<Payment>> GetOverduePaymentsAsync();

    /// <summary>
    /// Gets pending payments for a specific member.
    /// </summary>
    Task<List<Payment>> GetPendingPaymentsForMemberAsync(string membershipNumber);

    // ===== Termly Subscriptions =====

    /// <summary>
    /// Generates termly subscription payments for all active girls in a term.
    /// Skips members who already have a subs payment for the term.
    /// </summary>
    /// <param name="termId">The term to generate subscriptions for.</param>
    /// <returns>Number of payments created.</returns>
    Task<(bool Success, string ErrorMessage, int PaymentsCreated)> GenerateTermlySubsAsync(int termId);

    /// <summary>
    /// Checks if termly subscriptions have already been generated for a term.
    /// </summary>
    Task<bool> HasTermlySubsBeenGeneratedAsync(int termId);

    /// <summary>
    /// Gets the count of members who would receive subs payments for a term.
    /// Used for preview before generating.
    /// </summary>
    Task<int> GetEligibleMembersCountForTermAsync(int termId);

    // ===== Activity Payments =====

    /// <summary>
    /// Creates an activity payment for a member attending a costed meeting.
    /// </summary>
    /// <param name="meetingId">The meeting ID.</param>
    /// <param name="membershipNumber">The member's membership number.</param>
    /// <returns>The created payment.</returns>
    Task<(bool Success, string ErrorMessage, Payment? Payment)> CreateActivityPaymentAsync(int meetingId, string membershipNumber);

    /// <summary>
    /// Checks if an activity payment exists for a member and meeting.
    /// </summary>
    Task<bool> HasActivityPaymentAsync(int meetingId, string membershipNumber);

    // ===== Payment Recording =====

    /// <summary>
    /// Records a payment (full or partial) against an existing payment record.
    /// Automatically sets status to Paid when fully paid.
    /// </summary>
    /// <param name="paymentId">The payment ID.</param>
    /// <param name="amount">The amount being paid.</param>
    /// <param name="paymentMethod">How the payment was received.</param>
    /// <param name="paymentDate">The date of payment (defaults to today).</param>
    /// <param name="notes">Optional notes about the payment.</param>
    Task<(bool Success, string ErrorMessage)> RecordPaymentAsync(
        int paymentId,
        decimal amount,
        PaymentMethod paymentMethod,
        DateTime? paymentDate = null,
        string? notes = null);

    /// <summary>
    /// Cancels a payment and records the reason.
    /// </summary>
    /// <param name="paymentId">The payment ID.</param>
    /// <param name="reason">The reason for cancellation.</param>
    Task<(bool Success, string ErrorMessage)> CancelPaymentAsync(int paymentId, string reason);

    // ===== Statistics =====

    /// <summary>
    /// Gets payment statistics for a term.
    /// </summary>
    Task<PaymentTermStats> GetTermPaymentStatsAsync(int termId);

    /// <summary>
    /// Gets payment summary for a member.
    /// </summary>
    Task<MemberPaymentSummary> GetMemberPaymentSummaryAsync(string membershipNumber);

    /// <summary>
    /// Gets dashboard statistics for payments.
    /// </summary>
    Task<PaymentDashboardStats> GetDashboardStatsAsync();
}

/// <summary>
/// Payment statistics for a term.
/// </summary>
public class PaymentTermStats
{
    public int TermId { get; set; }
    public string? TermName { get; set; }
    public int TotalPayments { get; set; }
    public int PendingPayments { get; set; }
    public int PaidPayments { get; set; }
    public int CancelledPayments { get; set; }
    public int OverduePayments { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOutstanding { get; set; }

    public double PaidPercent => TotalPayments > 0 ? (double)PaidPayments / TotalPayments * 100 : 0;
}

/// <summary>
/// Payment summary for a member.
/// </summary>
public class MemberPaymentSummary
{
    public string MembershipNumber { get; set; } = string.Empty;
    public string? MemberName { get; set; }
    public int TotalPayments { get; set; }
    public int PendingPayments { get; set; }
    public int OverduePayments { get; set; }
    public decimal TotalOwed { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOutstanding { get; set; }
}

/// <summary>
/// Dashboard statistics for payments overview.
/// </summary>
public class PaymentDashboardStats
{
    public int PendingCount { get; set; }
    public int OverdueCount { get; set; }
    public int PaidThisTermCount { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal TotalPaidThisTerm { get; set; }
}

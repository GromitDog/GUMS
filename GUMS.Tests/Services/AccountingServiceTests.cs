using FluentAssertions;
using GUMS.Data;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GUMS.Tests.Services;

public class AccountingServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ITermService> _mockTermService;
    private readonly AccountingService _sut; // System Under Test

    public AccountingServiceTests()
    {
        // Arrange - Create in-memory database for each test
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        // Mock term service
        _mockTermService = new Mock<ITermService>();

        _sut = new AccountingService(_context, _mockTermService.Object);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    #region Helper Methods

    private async Task EnsureDefaultAccountsAsync()
    {
        await _sut.EnsureDefaultAccountsAsync();
    }

    private async Task<Account> CreateTestAccountAsync(string code, string name, AccountType type, decimal balance = 0)
    {
        var account = new Account
        {
            Code = code,
            Name = name,
            Type = type,
            Balance = balance
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();
        return account;
    }

    private async Task<Term> CreateTestTermAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var term = new Term
        {
            Name = "Spring 2026",
            StartDate = startDate ?? new DateTime(2026, 1, 6),
            EndDate = endDate ?? new DateTime(2026, 4, 3),
            SubsAmount = 25.00m
        };

        _context.Terms.Add(term);
        await _context.SaveChangesAsync();

        _mockTermService.Setup(x => x.GetCurrentTermAsync())
            .ReturnsAsync(term);

        return term;
    }

    #endregion

    #region EnsureDefaultAccountsAsync Tests

    [Fact]
    public async Task EnsureDefaultAccountsAsync_ShouldCreateAllDefaultAccounts()
    {
        // Act
        await _sut.EnsureDefaultAccountsAsync();

        // Assert
        var accounts = await _context.Accounts.ToListAsync();
        accounts.Should().HaveCount(5);

        accounts.Should().Contain(a => a.Code == "1001" && a.Name == "Cash on Hand");
        accounts.Should().Contain(a => a.Code == "1002" && a.Name == "Cheques Pending");
        accounts.Should().Contain(a => a.Code == "1003" && a.Name == "Bank Account");
        accounts.Should().Contain(a => a.Code == "4001" && a.Name == "Subscription Income");
        accounts.Should().Contain(a => a.Code == "4002" && a.Name == "Activity Income");
    }

    [Fact]
    public async Task EnsureDefaultAccountsAsync_ShouldNotDuplicateAccounts()
    {
        // Arrange
        await _sut.EnsureDefaultAccountsAsync();

        // Act - Call again
        await _sut.EnsureDefaultAccountsAsync();

        // Assert
        var accounts = await _context.Accounts.ToListAsync();
        accounts.Should().HaveCount(5);
    }

    [Fact]
    public async Task EnsureDefaultAccountsAsync_ShouldMarkAccountsAsSystem()
    {
        // Act
        await _sut.EnsureDefaultAccountsAsync();

        // Assert
        var accounts = await _context.Accounts.ToListAsync();
        accounts.Should().OnlyContain(a => a.IsSystem == true);
    }

    #endregion

    #region GetAccountsAsync Tests

    [Fact]
    public async Task GetAccountsAsync_ShouldReturnEmptyList_WhenNoAccounts()
    {
        // Act
        var result = await _sut.GetAccountsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAccountsAsync_ShouldReturnAllAccounts_OrderedByCode()
    {
        // Arrange
        await CreateTestAccountAsync("4001", "Income", AccountType.Income);
        await CreateTestAccountAsync("1001", "Cash", AccountType.Asset);

        // Act
        var result = await _sut.GetAccountsAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Code.Should().Be("1001");
        result[1].Code.Should().Be("4001");
    }

    #endregion

    #region GetAccountByIdAsync Tests

    [Fact]
    public async Task GetAccountByIdAsync_ShouldReturnNull_WhenNotFound()
    {
        // Act
        var result = await _sut.GetAccountByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAccountByIdAsync_ShouldReturnAccount_WhenExists()
    {
        // Arrange
        var account = await CreateTestAccountAsync("1001", "Cash", AccountType.Asset);

        // Act
        var result = await _sut.GetAccountByIdAsync(account.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Code.Should().Be("1001");
    }

    #endregion

    #region GetAccountByCodeAsync Tests

    [Fact]
    public async Task GetAccountByCodeAsync_ShouldReturnNull_WhenNotFound()
    {
        // Act
        var result = await _sut.GetAccountByCodeAsync("9999");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAccountByCodeAsync_ShouldReturnAccount_WhenExists()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();

        // Act
        var result = await _sut.GetAccountByCodeAsync("1001");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Cash on Hand");
    }

    #endregion

    #region Balance Accessor Tests

    [Fact]
    public async Task GetCashOnHandAsync_ShouldReturnBalance()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();
        var cashAccount = await _context.Accounts.FirstAsync(a => a.Code == "1001");
        cashAccount.Balance = 150.00m;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetCashOnHandAsync();

        // Assert
        result.Should().Be(150.00m);
    }

    [Fact]
    public async Task GetBankBalanceAsync_ShouldReturnBalance()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();
        var bankAccount = await _context.Accounts.FirstAsync(a => a.Code == "1003");
        bankAccount.Balance = 500.00m;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetBankBalanceAsync();

        // Assert
        result.Should().Be(500.00m);
    }

    [Fact]
    public async Task GetChequesPendingAsync_ShouldReturnBalance()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();
        var chequeAccount = await _context.Accounts.FirstAsync(a => a.Code == "1002");
        chequeAccount.Balance = 75.00m;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetChequesPendingAsync();

        // Assert
        result.Should().Be(75.00m);
    }

    #endregion

    #region CreateTransactionAsync Tests

    [Fact]
    public async Task CreateTransactionAsync_ShouldCreateTransaction()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();
        var cashAccount = await _context.Accounts.FirstAsync(a => a.Code == "1001");
        var incomeAccount = await _context.Accounts.FirstAsync(a => a.Code == "4001");

        var transaction = new Transaction
        {
            Date = DateTime.Today,
            Description = "Test payment",
            Lines = new List<TransactionLine>
            {
                new TransactionLine { AccountId = cashAccount.Id, Debit = 25.00m, Credit = 0 },
                new TransactionLine { AccountId = incomeAccount.Id, Debit = 0, Credit = 25.00m }
            }
        };

        // Act
        var result = await _sut.CreateTransactionAsync(transaction);

        // Assert
        result.Success.Should().BeTrue();
        result.Transaction.Should().NotBeNull();
        result.Transaction!.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateTransactionAsync_ShouldUpdateAccountBalances()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();
        var cashAccount = await _context.Accounts.FirstAsync(a => a.Code == "1001");
        var incomeAccount = await _context.Accounts.FirstAsync(a => a.Code == "4001");

        var transaction = new Transaction
        {
            Date = DateTime.Today,
            Description = "Test payment",
            Lines = new List<TransactionLine>
            {
                new TransactionLine { AccountId = cashAccount.Id, Debit = 25.00m, Credit = 0 },
                new TransactionLine { AccountId = incomeAccount.Id, Debit = 0, Credit = 25.00m }
            }
        };

        // Act
        await _sut.CreateTransactionAsync(transaction);

        // Assert
        var updatedCash = await _context.Accounts.FirstAsync(a => a.Code == "1001");
        var updatedIncome = await _context.Accounts.FirstAsync(a => a.Code == "4001");

        updatedCash.Balance.Should().Be(25.00m); // Asset debit = increase
        updatedIncome.Balance.Should().Be(25.00m); // Income credit = increase
    }

    [Fact]
    public async Task CreateTransactionAsync_ShouldFail_WhenNotBalanced()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();
        var cashAccount = await _context.Accounts.FirstAsync(a => a.Code == "1001");

        var transaction = new Transaction
        {
            Date = DateTime.Today,
            Description = "Unbalanced transaction",
            Lines = new List<TransactionLine>
            {
                new TransactionLine { AccountId = cashAccount.Id, Debit = 25.00m, Credit = 0 }
                // Missing credit side
            }
        };

        // Act
        var result = await _sut.CreateTransactionAsync(transaction);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not balanced");
    }

    [Fact]
    public async Task CreateTransactionAsync_ShouldFail_WhenNoLines()
    {
        // Arrange
        var transaction = new Transaction
        {
            Date = DateTime.Today,
            Description = "Empty transaction",
            Lines = new List<TransactionLine>()
        };

        // Act
        var result = await _sut.CreateTransactionAsync(transaction);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("at least one line");
    }

    [Fact]
    public async Task CreateTransactionAsync_ShouldFail_WhenDescriptionEmpty()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();
        var cashAccount = await _context.Accounts.FirstAsync(a => a.Code == "1001");
        var incomeAccount = await _context.Accounts.FirstAsync(a => a.Code == "4001");

        var transaction = new Transaction
        {
            Date = DateTime.Today,
            Description = "",
            Lines = new List<TransactionLine>
            {
                new TransactionLine { AccountId = cashAccount.Id, Debit = 25.00m, Credit = 0 },
                new TransactionLine { AccountId = incomeAccount.Id, Debit = 0, Credit = 25.00m }
            }
        };

        // Act
        var result = await _sut.CreateTransactionAsync(transaction);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("description");
    }

    [Fact]
    public async Task CreateTransactionAsync_ShouldFail_WhenAccountNotFound()
    {
        // Arrange
        var transaction = new Transaction
        {
            Date = DateTime.Today,
            Description = "Test",
            Lines = new List<TransactionLine>
            {
                new TransactionLine { AccountId = 999, Debit = 25.00m, Credit = 0 },
                new TransactionLine { AccountId = 998, Debit = 0, Credit = 25.00m }
            }
        };

        // Act
        var result = await _sut.CreateTransactionAsync(transaction);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("do not exist");
    }

    #endregion

    #region GetTransactionsAsync Tests

    [Fact]
    public async Task GetTransactionsAsync_ShouldReturnEmptyList_WhenNoTransactions()
    {
        // Act
        var result = await _sut.GetTransactionsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTransactionsAsync_ShouldFilterByDateRange()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();
        var cashAccount = await _context.Accounts.FirstAsync(a => a.Code == "1001");
        var incomeAccount = await _context.Accounts.FirstAsync(a => a.Code == "4001");

        // Create transactions on different dates
        await _sut.CreateTransactionAsync(new Transaction
        {
            Date = new DateTime(2026, 1, 10),
            Description = "Transaction 1",
            Lines = new List<TransactionLine>
            {
                new TransactionLine { AccountId = cashAccount.Id, Debit = 10, Credit = 0 },
                new TransactionLine { AccountId = incomeAccount.Id, Debit = 0, Credit = 10 }
            }
        });

        await _sut.CreateTransactionAsync(new Transaction
        {
            Date = new DateTime(2026, 2, 15),
            Description = "Transaction 2",
            Lines = new List<TransactionLine>
            {
                new TransactionLine { AccountId = cashAccount.Id, Debit = 20, Credit = 0 },
                new TransactionLine { AccountId = incomeAccount.Id, Debit = 0, Credit = 20 }
            }
        });

        await _sut.CreateTransactionAsync(new Transaction
        {
            Date = new DateTime(2026, 3, 20),
            Description = "Transaction 3",
            Lines = new List<TransactionLine>
            {
                new TransactionLine { AccountId = cashAccount.Id, Debit = 30, Credit = 0 },
                new TransactionLine { AccountId = incomeAccount.Id, Debit = 0, Credit = 30 }
            }
        });

        // Act
        var result = await _sut.GetTransactionsAsync(new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));

        // Assert
        result.Should().HaveCount(1);
        result[0].Description.Should().Be("Transaction 2");
    }

    #endregion

    #region RecordPaymentEntryAsync Tests

    [Fact]
    public async Task RecordPaymentEntryAsync_ShouldCreateCorrectEntries_ForCashPayment()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();

        // Act
        var result = await _sut.RecordPaymentEntryAsync(
            paymentId: 1,
            amount: 25.00m,
            paymentMethod: PaymentMethod.Cash,
            paymentType: PaymentType.Subs,
            description: "Test payment",
            date: DateTime.Today);

        // Assert
        result.Success.Should().BeTrue();

        var cashBalance = await _sut.GetCashOnHandAsync();
        cashBalance.Should().Be(25.00m);

        var subsIncome = await _context.Accounts.FirstAsync(a => a.Code == "4001");
        subsIncome.Balance.Should().Be(25.00m);
    }

    [Fact]
    public async Task RecordPaymentEntryAsync_ShouldCreateCorrectEntries_ForChequePayment()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();

        // Act
        var result = await _sut.RecordPaymentEntryAsync(
            paymentId: 1,
            amount: 25.00m,
            paymentMethod: PaymentMethod.Cheque,
            paymentType: PaymentType.Subs,
            description: "Test payment",
            date: DateTime.Today);

        // Assert
        result.Success.Should().BeTrue();

        var chequeBalance = await _sut.GetChequesPendingAsync();
        chequeBalance.Should().Be(25.00m);
    }

    [Fact]
    public async Task RecordPaymentEntryAsync_ShouldCreateCorrectEntries_ForBankTransfer()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();

        // Act
        var result = await _sut.RecordPaymentEntryAsync(
            paymentId: 1,
            amount: 25.00m,
            paymentMethod: PaymentMethod.BankTransfer,
            paymentType: PaymentType.Subs,
            description: "Test payment",
            date: DateTime.Today);

        // Assert
        result.Success.Should().BeTrue();

        var bankBalance = await _sut.GetBankBalanceAsync();
        bankBalance.Should().Be(25.00m);
    }

    [Fact]
    public async Task RecordPaymentEntryAsync_ShouldCreditCorrectIncomeAccount_ForActivityPayment()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();

        // Act
        await _sut.RecordPaymentEntryAsync(
            paymentId: 1,
            amount: 15.00m,
            paymentMethod: PaymentMethod.Cash,
            paymentType: PaymentType.Activity,
            description: "Activity payment",
            date: DateTime.Today);

        // Assert
        var activityIncome = await _context.Accounts.FirstAsync(a => a.Code == "4002");
        activityIncome.Balance.Should().Be(15.00m);

        var subsIncome = await _context.Accounts.FirstAsync(a => a.Code == "4001");
        subsIncome.Balance.Should().Be(0); // Subs income should be unchanged
    }

    #endregion

    #region BankDepositAsync Tests

    [Fact]
    public async Task BankDepositAsync_ShouldMoveCashToBank()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();
        var cashAccount = await _context.Accounts.FirstAsync(a => a.Code == "1001");
        cashAccount.Balance = 100.00m;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.BankDepositAsync(50.00m, 0, DateTime.Today);

        // Assert
        result.Success.Should().BeTrue();

        var cash = await _sut.GetCashOnHandAsync();
        var bank = await _sut.GetBankBalanceAsync();

        cash.Should().Be(50.00m);
        bank.Should().Be(50.00m);
    }

    [Fact]
    public async Task BankDepositAsync_ShouldMoveChequesToBank()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();
        var chequeAccount = await _context.Accounts.FirstAsync(a => a.Code == "1002");
        chequeAccount.Balance = 75.00m;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.BankDepositAsync(0, 75.00m, DateTime.Today);

        // Assert
        result.Success.Should().BeTrue();

        var cheques = await _sut.GetChequesPendingAsync();
        var bank = await _sut.GetBankBalanceAsync();

        cheques.Should().Be(0);
        bank.Should().Be(75.00m);
    }

    [Fact]
    public async Task BankDepositAsync_ShouldMoveBothCashAndCheques()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();
        var cashAccount = await _context.Accounts.FirstAsync(a => a.Code == "1001");
        var chequeAccount = await _context.Accounts.FirstAsync(a => a.Code == "1002");
        cashAccount.Balance = 100.00m;
        chequeAccount.Balance = 50.00m;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.BankDepositAsync(80.00m, 50.00m, DateTime.Today, "Monthly deposit");

        // Assert
        result.Success.Should().BeTrue();

        var cash = await _sut.GetCashOnHandAsync();
        var cheques = await _sut.GetChequesPendingAsync();
        var bank = await _sut.GetBankBalanceAsync();

        cash.Should().Be(20.00m);
        cheques.Should().Be(0);
        bank.Should().Be(130.00m);
    }

    [Fact]
    public async Task BankDepositAsync_ShouldFail_WhenInsufficientCash()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();
        var cashAccount = await _context.Accounts.FirstAsync(a => a.Code == "1001");
        cashAccount.Balance = 50.00m;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.BankDepositAsync(100.00m, 0, DateTime.Today);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Insufficient cash");
    }

    [Fact]
    public async Task BankDepositAsync_ShouldFail_WhenBothAmountsZero()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();

        // Act
        var result = await _sut.BankDepositAsync(0, 0, DateTime.Today);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("At least one amount");
    }

    #endregion

    #region GetIncomeReportAsync Tests

    [Fact]
    public async Task GetIncomeReportAsync_ShouldCalculateIncomeCorrectly()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();

        // Record some payments
        await _sut.RecordPaymentEntryAsync(1, 25.00m, PaymentMethod.Cash, PaymentType.Subs, "Subs 1", new DateTime(2026, 1, 15));
        await _sut.RecordPaymentEntryAsync(2, 25.00m, PaymentMethod.Cash, PaymentType.Subs, "Subs 2", new DateTime(2026, 1, 20));
        await _sut.RecordPaymentEntryAsync(3, 15.00m, PaymentMethod.Cash, PaymentType.Activity, "Activity 1", new DateTime(2026, 1, 25));

        // Act
        var result = await _sut.GetIncomeReportAsync(new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));

        // Assert
        result.SubsIncome.Should().Be(50.00m);
        result.ActivityIncome.Should().Be(15.00m);
        result.TotalIncome.Should().Be(65.00m);
    }

    #endregion

    #region GetDashboardStatsAsync Tests

    [Fact]
    public async Task GetDashboardStatsAsync_ShouldReturnCorrectStats()
    {
        // Arrange
        await EnsureDefaultAccountsAsync();
        await CreateTestTermAsync(DateTime.Today.AddDays(-30), DateTime.Today.AddDays(60));

        // Set some balances
        var cashAccount = await _context.Accounts.FirstAsync(a => a.Code == "1001");
        var chequeAccount = await _context.Accounts.FirstAsync(a => a.Code == "1002");
        var bankAccount = await _context.Accounts.FirstAsync(a => a.Code == "1003");

        cashAccount.Balance = 50.00m;
        chequeAccount.Balance = 25.00m;
        bankAccount.Balance = 200.00m;
        await _context.SaveChangesAsync();

        // Record some payments
        await _sut.RecordPaymentEntryAsync(1, 25.00m, PaymentMethod.Cash, PaymentType.Subs, "Subs", DateTime.Today);
        await _sut.RecordPaymentEntryAsync(2, 15.00m, PaymentMethod.Cash, PaymentType.Activity, "Activity", DateTime.Today);

        // Act
        var result = await _sut.GetDashboardStatsAsync();

        // Assert
        result.CashOnHand.Should().Be(90.00m); // 50 + 25 + 15 from payments
        result.ChequesPending.Should().Be(25.00m);
        result.BankBalance.Should().Be(200.00m);
        result.TotalAssets.Should().Be(315.00m);
        result.SubsIncomeThisTerm.Should().Be(25.00m);
        result.ActivityIncomeThisTerm.Should().Be(15.00m);
    }

    #endregion
}

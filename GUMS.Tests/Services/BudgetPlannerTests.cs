using FluentAssertions;
using GUMS.Data.Entities;
using GUMS.Data.Enums;
using GUMS.Services;

namespace GUMS.Tests.Services;

public class BudgetPlannerTests
{
    // Canonical mix: £5/girl, £3/adult, £2/head, £20 fixed.
    private static List<EventBudgetItem> CanonicalItems() => new()
    {
        new EventBudgetItem { Description = "Activity", CostType = BudgetCostType.PerGirl, Amount = 5m },
        new EventBudgetItem { Description = "Leader place", CostType = BudgetCostType.PerAdult, Amount = 3m },
        new EventBudgetItem { Description = "Entry", CostType = BudgetCostType.PerPerson, Amount = 2m },
        new EventBudgetItem { Description = "Coach", CostType = BudgetCostType.FixedTotal, Amount = 20m }
    };

    private static List<EventBudgetIncome> Grants(params decimal[] amounts) =>
        amounts.Select(a => new EventBudgetIncome { Description = "Grant", Amount = a }).ToList();

    [Fact]
    public void CalculateCost_SumsAllCostTypes()
    {
        // 5*8 + 3*2 + 2*10 + 20 = 86
        BudgetPlanner.CalculateCost(CanonicalItems(), girls: 8, adults: 2).Should().Be(86m);
    }

    [Fact]
    public void CalculatePlan_IsBalanced_WhenChargesExactlyCoverCost()
    {
        var plan = BudgetPlanner.CalculatePlan(
            CanonicalItems(), Grants(), girls: 8, adults: 2, leadersPay: true,
            chargePerGirl: 9m, chargePerAdult: 7m);

        plan.TotalCost.Should().Be(86m);
        plan.ChargeIncome.Should().Be(86m); // 9*8 + 7*2
        plan.Balance.Should().Be(0m);
        plan.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void CalculatePlan_ShowsShortfall_WhenChargesTooLow()
    {
        var plan = BudgetPlanner.CalculatePlan(
            CanonicalItems(), Grants(), girls: 8, adults: 2, leadersPay: false,
            chargePerGirl: 5m, chargePerAdult: 0m);

        // income 5*8 = 40, cost 86 -> shortfall 46
        plan.Balance.Should().Be(-46m);
        plan.IsBalanced.Should().BeFalse();
    }

    [Fact]
    public void CalculatePlan_OtherIncomeOffsetsCost()
    {
        var plan = BudgetPlanner.CalculatePlan(
            CanonicalItems(), Grants(20m), girls: 8, adults: 2, leadersPay: false,
            chargePerGirl: 0m, chargePerAdult: 0m);

        plan.OtherIncome.Should().Be(20m);
        plan.ToCoverFromCharges.Should().Be(66m); // 86 - 20
        // no charge yet -> income 20, shortfall 66
        plan.Balance.Should().Be(-66m);
    }

    [Fact]
    public void CalculatePlan_Recommends_LeadersDontPay_WholeNetOntoGirls()
    {
        var plan = BudgetPlanner.CalculatePlan(
            CanonicalItems(), Grants(), girls: 8, adults: 2, leadersPay: false,
            chargePerGirl: 0m, chargePerAdult: 0m);

        plan.RecommendedPerGirl.Should().Be(10.75m); // 86 / 8
        plan.RecommendedPerAdult.Should().Be(0m);
    }

    [Fact]
    public void CalculatePlan_Recommends_LeadersPay_SplitsNetFairly()
    {
        var plan = BudgetPlanner.CalculatePlan(
            CanonicalItems(), Grants(20m), girls: 8, adults: 2, leadersPay: true,
            chargePerGirl: 0m, chargePerAdult: 0m);

        // Recommended charges should recover exactly the net-of-grant amount (66).
        var recovered = plan.RecommendedPerGirl * 8 + plan.RecommendedPerAdult * 2;
        recovered.Should().BeApproximately(66m, 0.0001m);
    }

    [Fact]
    public void CalculatePlan_ApplyingRecommendation_Balances()
    {
        var initial = BudgetPlanner.CalculatePlan(
            CanonicalItems(), Grants(20m), girls: 8, adults: 2, leadersPay: true,
            chargePerGirl: 0m, chargePerAdult: 0m);

        // Feed the recommended charges back in (unrounded) -> should balance.
        var applied = BudgetPlanner.CalculatePlan(
            CanonicalItems(), Grants(20m), girls: 8, adults: 2, leadersPay: true,
            chargePerGirl: initial.RecommendedPerGirl, chargePerAdult: initial.RecommendedPerAdult);

        applied.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void CalculatePlan_GrantsExceedCost_NoChargeNeeded()
    {
        var plan = BudgetPlanner.CalculatePlan(
            CanonicalItems(), Grants(100m), girls: 8, adults: 2, leadersPay: false,
            chargePerGirl: 0m, chargePerAdult: 0m);

        plan.ToCoverFromCharges.Should().Be(0m);
        plan.RecommendedPerGirl.Should().Be(0m);
        plan.Balance.Should().Be(14m); // 100 - 86 surplus
    }

    [Fact]
    public void CalculateBreakEven_LeadersDontPay_PutsAllCostOnGirls()
    {
        var (perGirl, perAdult) = BudgetPlanner.CalculateBreakEven(CanonicalItems(), girls: 8, adults: 2, leadersPay: false);
        perGirl.Should().Be(10.75m);
        perAdult.Should().Be(0m);
    }
}

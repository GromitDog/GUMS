using GUMS.Data.Entities;
using GUMS.Data.Enums;

namespace GUMS.Services;

/// <summary>
/// Pure, in-memory budget maths shared by the interactive planner UI and the budget service.
/// No database access — safe to call on every slider tick.
/// </summary>
public static class BudgetPlanner
{
    /// <summary>Total cost of the event for a given turnout.</summary>
    public static decimal CalculateCost(IEnumerable<EventBudgetItem> items, int girls, int adults)
    {
        decimal total = 0;
        foreach (var item in items)
        {
            total += item.CostType switch
            {
                BudgetCostType.PerGirl => item.Amount * girls,
                BudgetCostType.PerAdult => item.Amount * adults,
                BudgetCostType.PerPerson => item.Amount * (girls + adults),
                BudgetCostType.FixedTotal => item.Amount,
                _ => 0
            };
        }
        return total;
    }

    /// <summary>
    /// Break-even charge per girl and per adult that exactly recovers the whole cost.
    /// When leaders don't pay, the whole cost falls on the girls and the per-adult charge is zero.
    /// When leaders pay, per-girl/per-adult items are charged directly, per-head items to everyone,
    /// and fixed totals split evenly across every head.
    /// </summary>
    public static (decimal PerGirl, decimal PerAdult) CalculateBreakEven(
        IEnumerable<EventBudgetItem> items, int girls, int adults, bool leadersPay)
    {
        var itemList = items as IList<EventBudgetItem> ?? items.ToList();
        var total = CalculateCost(itemList, girls, adults);

        if (!leadersPay)
        {
            var perGirlOnly = girls > 0 ? total / girls : 0;
            return (perGirlOnly, 0m);
        }

        var perGirlItems = itemList.Where(i => i.CostType == BudgetCostType.PerGirl).Sum(i => i.Amount);
        var perAdultItems = itemList.Where(i => i.CostType == BudgetCostType.PerAdult).Sum(i => i.Amount);
        var perHeadItems = itemList.Where(i => i.CostType == BudgetCostType.PerPerson).Sum(i => i.Amount);
        var fixedTotal = itemList.Where(i => i.CostType == BudgetCostType.FixedTotal).Sum(i => i.Amount);

        var heads = girls + adults;
        var fixedShare = heads > 0 ? fixedTotal / heads : 0;

        var perGirl = perGirlItems + perHeadItems + fixedShare;
        var perAdult = perAdultItems + perHeadItems + fixedShare;
        return (perGirl, perAdult);
    }

    /// <summary>
    /// Builds the live plan: cost, income from grants/charges, the resulting balance, and the
    /// break-even charges needed to cover whatever the grants/fundraising don't.
    /// </summary>
    public static BudgetPlanResult CalculatePlan(
        IEnumerable<EventBudgetItem> items,
        IEnumerable<EventBudgetIncome> incomes,
        int girls,
        int adults,
        bool leadersPay,
        decimal chargePerGirl,
        decimal chargePerAdult)
    {
        var itemList = items as IList<EventBudgetItem> ?? items.ToList();

        var totalCost = CalculateCost(itemList, girls, adults);
        var otherIncome = incomes.Sum(i => i.Amount);
        var chargeIncome = chargePerGirl * girls + (leadersPay ? chargePerAdult * adults : 0m);

        // Amount that still has to come from charges once grants/fundraising are applied.
        var toCover = totalCost - otherIncome;
        if (toCover < 0) toCover = 0;

        decimal recPerGirl;
        decimal recPerAdult;
        if (!leadersPay)
        {
            recPerGirl = girls > 0 ? toCover / girls : 0;
            recPerAdult = 0;
        }
        else
        {
            var (beGirl, beAdult) = CalculateBreakEven(itemList, girls, adults, true);
            // Scale the fair break-even split down so it covers exactly `toCover`.
            var factor = totalCost > 0 ? toCover / totalCost : 0;
            recPerGirl = beGirl * factor;
            recPerAdult = beAdult * factor;
        }

        return new BudgetPlanResult
        {
            Girls = girls,
            Adults = adults,
            LeadersPay = leadersPay,
            TotalCost = totalCost,
            OtherIncome = otherIncome,
            ChargeIncome = chargeIncome,
            RecommendedPerGirl = recPerGirl,
            RecommendedPerAdult = recPerAdult
        };
    }
}

public class BudgetPlanResult
{
    public int Girls { get; set; }
    public int Adults { get; set; }
    public bool LeadersPay { get; set; }

    public decimal TotalCost { get; set; }

    /// <summary>Grants and fundraising — income that isn't charged to attendees.</summary>
    public decimal OtherIncome { get; set; }

    /// <summary>Income from the per-girl / per-adult charges.</summary>
    public decimal ChargeIncome { get; set; }

    public decimal TotalIncome => OtherIncome + ChargeIncome;

    /// <summary>Positive = surplus, negative = shortfall.</summary>
    public decimal Balance => TotalIncome - TotalCost;

    /// <summary>Balanced to within half a penny.</summary>
    public bool IsBalanced => Math.Abs(Balance) < 0.005m;

    /// <summary>Cost left to recover from charges after grants/fundraising.</summary>
    public decimal ToCoverFromCharges => Math.Max(0m, TotalCost - OtherIncome);

    /// <summary>Recommended charge per girl to balance the budget at this turnout.</summary>
    public decimal RecommendedPerGirl { get; set; }

    /// <summary>Recommended charge per adult to balance the budget (zero when leaders don't pay).</summary>
    public decimal RecommendedPerAdult { get; set; }
}

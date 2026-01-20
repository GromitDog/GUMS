using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Payments;

public partial class GenerateSubs
{
    [Inject] private IPaymentService PaymentService { get; set; } = default!;
    [Inject] private ITermService TermService { get; set; } = default!;
    [Inject] private IConfigurationService ConfigurationService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private List<Term> _availableTerms = new();
    private Term? _selectedTerm;
    private int _eligibleCount;
    private bool _hasExistingSubs;
    private DateTime? _dueDate;

    private bool _isLoading = true;
    private bool _isGenerating;
    private bool _showConfirmation;
    private string _successMessage = string.Empty;
    private string _errorMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadTerms();
    }

    private async Task LoadTerms()
    {
        _isLoading = true;

        try
        {
            // Get current and future terms
            var currentTerm = await TermService.GetCurrentTermAsync();
            var futureTerms = await TermService.GetFutureTermsAsync();

            _availableTerms = new List<Term>();

            if (currentTerm != null)
            {
                _availableTerms.Add(currentTerm);
            }

            _availableTerms.AddRange(futureTerms);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading terms: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OnTermSelected(ChangeEventArgs e)
    {
        var termIdStr = e.Value?.ToString();

        if (string.IsNullOrEmpty(termIdStr) || !int.TryParse(termIdStr, out var termId))
        {
            _selectedTerm = null;
            _eligibleCount = 0;
            _hasExistingSubs = false;
            return;
        }

        _selectedTerm = _availableTerms.FirstOrDefault(t => t.Id == termId);

        if (_selectedTerm != null)
        {
            // Check if subs already generated
            _hasExistingSubs = await PaymentService.HasTermlySubsBeenGeneratedAsync(_selectedTerm.Id);

            // Get eligible count
            _eligibleCount = await PaymentService.GetEligibleMembersCountForTermAsync(_selectedTerm.Id);

            // Calculate due date
            var config = await ConfigurationService.GetConfigurationAsync();
            _dueDate = _selectedTerm.StartDate.AddDays(config.PaymentTermDays);
        }
    }

    private void ShowConfirmation()
    {
        _showConfirmation = true;
    }

    private void CancelConfirmation()
    {
        _showConfirmation = false;
    }

    private async Task GenerateSubscriptions()
    {
        if (_selectedTerm == null) return;

        _isGenerating = true;
        _errorMessage = string.Empty;

        try
        {
            var result = await PaymentService.GenerateTermlySubsAsync(_selectedTerm.Id);

            if (result.Success)
            {
                NavigationManager.NavigateTo($"/Payments?success=generated&count={result.PaymentsCreated}");
            }
            else
            {
                _errorMessage = result.ErrorMessage;
                _showConfirmation = false;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"An error occurred: {ex.Message}";
            _showConfirmation = false;
        }
        finally
        {
            _isGenerating = false;
        }
    }

    private void ClearSuccess()
    {
        _successMessage = string.Empty;
    }

    private void ClearError()
    {
        _errorMessage = string.Empty;
    }
}

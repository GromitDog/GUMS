using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace GUMS.Components.Pages.Configuration;

[Authorize]
public partial class TermManagement
{
    
    [Inject] public required ITermService TermService { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }
    
    private List<Term> _allTerms = [];
    private List<Term> _futureTerms = [];
    private List<Term> _pastTerms = [];
    private Term? _currentTermData;

    private Term _currentTerm = new();
    private Term? _editingTerm;
    private Term? _termToDelete;

    private bool _isLoading = true;
    private bool _showForm;
    private bool _isSaving;
    private bool _showDeleteConfirm;
    private bool _isDeleting;

    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadTerms();
    }

    private async Task LoadTerms()
    {
        _isLoading = true;

        try
        {
            _allTerms = await TermService.GetAllAsync();
            _currentTermData = await TermService.GetCurrentTermAsync();
            _futureTerms = await TermService.GetFutureTermsAsync();
            _pastTerms = await TermService.GetPastTermsAsync();
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

    private void ShowAddForm()
    {
        _editingTerm = null;
        _currentTerm = new Term
        {
            Name = string.Empty,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddMonths(3),
            SubsAmount = 20.00m
        };
        _showForm = true;
        ClearMessages();
    }

    private void EditTerm(Term? term)
    {
        if (term == null) return;

        _editingTerm = term;
        _currentTerm = new Term
        {
            Id = term.Id,
            Name = term.Name,
            StartDate = term.StartDate,
            EndDate = term.EndDate,
            SubsAmount = term.SubsAmount
        };
        _showForm = true;
        ClearMessages();
    }

    private async Task SaveTerm()
    {
        _isSaving = true;
        ClearMessages();

        try
        {
            var result = _editingTerm == null
                ? await TermService.CreateAsync(_currentTerm)
                : await TermService.UpdateAsync(_currentTerm);

            if (result.Success)
            {
                _successMessage = _editingTerm == null
                    ? $"Term '{_currentTerm.Name}' has been added successfully!"
                    : $"Term '{_currentTerm.Name}' has been updated successfully!";

                _showForm = false;
                await LoadTerms();
            }
            else
            {
                _errorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"An error occurred: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void CancelForm()
    {
        _showForm = false;
        _currentTerm = new();
        _editingTerm = null;
        ClearMessages();
    }

    private void ConfirmDelete(Term? term)
    {
        if (term == null) return;

        _termToDelete = term;
        _showDeleteConfirm = true;
        ClearMessages();
    }

    private void CancelDelete()
    {
        _showDeleteConfirm = false;
        _termToDelete = null;
    }

    private async Task DeleteTerm()
    {
        if (_termToDelete == null) return;

        _isDeleting = true;
        ClearMessages();

        try
        {
            var result = await TermService.DeleteAsync(_termToDelete.Id);

            if (result.Success)
            {
                _successMessage = $"Term '{_termToDelete.Name}' has been deleted successfully.";
                _showDeleteConfirm = false;
                _termToDelete = null;
                await LoadTerms();
            }
            else
            {
                _errorMessage = result.ErrorMessage;
                _showDeleteConfirm = false;
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"An error occurred: {ex.Message}";
            _showDeleteConfirm = false;
        }
        finally
        {
            _isDeleting = false;
        }
    }

    private void ClearMessages()
    {
        _errorMessage = string.Empty;
        _successMessage = string.Empty;
    }

    private void ClearError()
    {
        _errorMessage = string.Empty;
    }

    private void ClearSuccess()
    {
        _successMessage = string.Empty;
    }
}
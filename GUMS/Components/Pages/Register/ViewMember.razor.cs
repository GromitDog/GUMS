using System.Text;
using GUMS.Data.Entities;
using GUMS.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.JSInterop;

namespace GUMS.Components.Pages.Register;

public partial class ViewMember
{
    [Inject] public required IPersonService PersonService { get; set; }
    [Inject] public required IPaymentService PaymentService { get; set; }
    [Inject] public required UserManager<IdentityUser> UserManager { get; set; }
    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }

    [Parameter]
    public int Id { get; set; }

    private Person? _person;
    private MemberPaymentSummary? _paymentSummary;
    private string? _errorMessage;
    private string? _successMessage;
    private bool _isLoading = true;
    private bool _isProcessing;
    private bool _showMarkAsLeftModal;
    private DateTime _dateLeft = DateTime.Today;
    private bool _exportBeforeRemoval = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _person = await PersonService.GetByIdAsync(Id);

            // Load payment summary if person exists
            if (_person != null)
            {
                _paymentSummary = await PaymentService.GetMemberPaymentSummaryAsync(_person.MembershipNumber);
            }
        }
        catch
        {
            _person = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ShowMarkAsLeftModal()
    {
        _showMarkAsLeftModal = true;
        _dateLeft = DateTime.Today;
        _exportBeforeRemoval = true;
    }

    private void CloseMarkAsLeftModal()
    {
        _showMarkAsLeftModal = false;
        _errorMessage = null;
    }

    private async Task ConfirmMarkAsLeft()
    {
        if (_person == null) return;

        _isProcessing = true;
        _errorMessage = null;

        try
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = await UserManager.GetUserAsync(authState.User);
            var userName = user?.Email ?? "Unknown";

            if (_exportBeforeRemoval)
            {
                var exportData = await PersonService.ExportMemberDataAsync(Id);
                var fileName = $"{_person.MembershipNumber}_{_person.FullName?.Replace(" ", "_")}_Export.json";

                var bytes = Encoding.UTF8.GetBytes(exportData);
                var base64 = Convert.ToBase64String(bytes);
                await JS.InvokeVoidAsync("eval", $"(function(){{var link = document.createElement('a');link.download = '{fileName}';link.href = 'data:application/json;base64,{base64}';link.click();}})()");
            }

            if (_person.DateLeft != _dateLeft)
            {
                _person.DateLeft = _dateLeft;
            }

            await PersonService.RemoveMemberDataAsync(Id, userName, _exportBeforeRemoval);

            _person = await PersonService.GetByIdAsync(Id);

            _showMarkAsLeftModal = false;
            _successMessage = "Member data has been successfully removed. The membership number and historical records have been retained.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error removing member data: {ex.Message}";
        }
        finally
        {
            _isProcessing = false;
        }
    }
}

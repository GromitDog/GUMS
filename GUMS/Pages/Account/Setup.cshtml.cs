using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GUMS.Pages.Account
{
    public class SetupModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public SetupModel(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ErrorMessage { get; set; }
        public bool AlreadySetup { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [StringLength(100, MinimumLength = 8)]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Required]
            [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
            [DataType(DataType.Password)]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Check if any users already exist
            if (_userManager.Users.Any())
            {
                AlreadySetup = true;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Double-check no users exist
            if (_userManager.Users.Any())
            {
                ErrorMessage = "Setup has already been completed.";
                AlreadySetup = true;
                return Page();
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Create the admin user
            var user = new IdentityUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                // Sign in the new user
                await _signInManager.SignInAsync(user, isPersistent: false);

                // Redirect to home page
                return Redirect("/");
            }

            ErrorMessage = string.Join(" ", result.Errors.Select(e => e.Description));
            return Page();
        }
    }
}

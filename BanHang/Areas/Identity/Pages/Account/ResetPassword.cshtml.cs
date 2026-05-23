// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace BanHang.Areas.Identity.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;

        public ResetPasswordModel(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            public string Code { get; set; }

        }

        public IActionResult OnGet(string code = null)
        {
            // Cho phép truy cập trực tiếp từ luồng OTP
            Input = new InputModel
            {
                Email = HttpContext.Session.GetString("ResetEmail") ?? "",
                Code = "" // Người dùng sẽ nhập mã OTP vào trường này
            };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Email không tồn tại trong hệ thống.");
                return Page();
            }

            // Kiểm tra OTP trong Session
            string sessionEmail = HttpContext.Session.GetString("ResetEmail");
            string sessionOtp = HttpContext.Session.GetString("ResetOtp");
            string sessionExpiryStr = HttpContext.Session.GetString("ResetOtpExpiry");

            if (string.IsNullOrEmpty(sessionEmail) || string.IsNullOrEmpty(sessionOtp) || string.IsNullOrEmpty(sessionExpiryStr))
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy phiên yêu cầu OTP hoặc mã OTP chưa được gửi.");
                return Page();
            }

            if (!sessionEmail.Equals(Input.Email, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "Email không khớp với email đã nhận mã OTP.");
                return Page();
            }

            if (sessionOtp != Input.Code.Trim())
            {
                ModelState.AddModelError(string.Empty, "Mã OTP không chính xác.");
                return Page();
            }

            if (DateTime.TryParse(sessionExpiryStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime expiryTime))
            {
                if (DateTime.Now > expiryTime)
                {
                    ModelState.AddModelError(string.Empty, "Mã OTP đã hết hạn (hiệu lực 5 phút). Vui lòng gửi lại yêu cầu khôi phục.");
                    return Page();
                }
            }

            // Nếu mã OTP hợp lệ, tiến hành tạo token đặt lại mật khẩu và cập nhật mật khẩu
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, Input.Password);
            
            if (result.Succeeded)
            {
                // Xoá thông tin OTP trong Session
                HttpContext.Session.Remove("ResetEmail");
                HttpContext.Session.Remove("ResetOtp");
                HttpContext.Session.Remove("ResetOtpExpiry");

                return RedirectToPage("./ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }
    }
}

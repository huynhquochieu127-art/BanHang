// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace BanHang.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;

        public ForgotPasswordModel(UserManager<IdentityUser> userManager, IEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender = emailSender;
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

            // Tạo mã OTP ngẫu nhiên 6 chữ số
            string otp = new Random().Next(100000, 999999).ToString();

            // Lưu thông tin vào Session để kiểm tra sau
            HttpContext.Session.SetString("ResetEmail", Input.Email);
            HttpContext.Session.SetString("ResetOtp", otp);
            HttpContext.Session.SetString("ResetOtpExpiry", DateTime.Now.AddMinutes(5).ToString("o"));

            // Gửi OTP qua email (Gmail)
            string subject = "Mã OTP khôi phục mật khẩu - Shop Bán Hàng";
            string message = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #eee; padding: 20px; border-radius: 8px;'>
                    <h2 style='color: #222; text-align: center; border-bottom: 2px solid #222; padding-bottom: 10px;'>MÃ OTP KHÔI PHỤC MẬT KHẨU</h2>
                    <p>Xin chào,</p>
                    <p>Chúng tôi nhận được yêu cầu khôi phục mật khẩu cho tài khoản liên kết với email này. Vui lòng sử dụng mã OTP dưới đây để tiến hành đặt lại mật khẩu của bạn:</p>
                    <div style='background: #f7f7f7; padding: 15px; border-radius: 6px; text-align: center; margin: 20px 0;'>
                        <span style='font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #d9534f;'>{otp}</span>
                    </div>
                    <p style='color: #e74c3c; font-size: 13px; font-weight: bold;'>Mã OTP này có hiệu lực trong vòng 5 phút. Vui lòng không chia sẻ mã này cho bất kỳ ai.</p>
                    <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                    <p style='font-size: 12px; color: #999; text-align: center;'>Đây là email tự động, vui lòng không phản hồi.<br><strong>Shop Bán Hàng</strong></p>
                </div>";

            await _emailSender.SendEmailAsync(Input.Email, subject, message);

            return RedirectToPage("./ForgotPasswordConfirmation");
        }
    }
}

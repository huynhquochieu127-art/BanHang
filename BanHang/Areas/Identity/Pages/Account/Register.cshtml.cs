using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BanHang.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IUserStore<IdentityUser> _userStore;
        private readonly IUserEmailStore<IdentityUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;

        public RegisterModel(
            UserManager<IdentityUser> userManager,
            IUserStore<IdentityUser> userStore,
            SignInManager<IdentityUser> signInManager,
            ILogger<RegisterModel> _logger,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            this._logger = _logger;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public bool OtpSent { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Email không được để trống")]
            [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Số điện thoại không được để trống")]
            [Phone(ErrorMessage = "Số điện thoại không đúng định dạng")]
            [Display(Name = "Số điện thoại")]
            public string PhoneNumber { get; set; }

            [Required(ErrorMessage = "Mật khẩu không được để trống")]
            [StringLength(100, ErrorMessage = "Mật khẩu phải dài ít nhất {2} ký tự.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Xác nhận mật khẩu")]
            [Compare("Password", ErrorMessage = "Mật khẩu và mật khẩu xác nhận không khớp.")]
            public string ConfirmPassword { get; set; }

            public string OtpCode { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            var sessionEmail = HttpContext.Session.GetString("RegisterEmail");
            if (!string.IsNullOrEmpty(sessionEmail))
            {
                Input = new InputModel
                {
                    Email = sessionEmail,
                    PhoneNumber = HttpContext.Session.GetString("RegisterPhone")
                };
                OtpSent = true;
            }
        }

        public async Task<IActionResult> OnPostSendOtpAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            OtpSent = false;

            if (string.IsNullOrEmpty(Input.Email) || string.IsNullOrEmpty(Input.PhoneNumber))
            {
                ModelState.AddModelError(string.Empty, "Vui lòng điền đầy đủ Email và Số điện thoại.");
                return Page();
            }

            // Kiểm tra trùng Email
            var existingEmailUser = await _userManager.FindByEmailAsync(Input.Email.Trim());
            if (existingEmailUser != null)
            {
                ModelState.AddModelError("Input.Email", "Địa chỉ Email này đã được sử dụng.");
                return Page();
            }

            // Kiểm tra trùng Số điện thoại
            var existingPhoneUser = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == Input.PhoneNumber.Trim());
            if (existingPhoneUser != null)
            {
                ModelState.AddModelError("Input.PhoneNumber", "Số điện thoại này đã được sử dụng.");
                return Page();
            }

            // Tạo mã OTP 6 chữ số ngẫu nhiên
            string otp = new Random().Next(100000, 999999).ToString();

            // Lưu thông tin đăng ký tạm vào Session
            HttpContext.Session.SetString("RegisterEmail", Input.Email.Trim());
            HttpContext.Session.SetString("RegisterPhone", Input.PhoneNumber.Trim());
            HttpContext.Session.SetString("RegisterOtp", otp);
            HttpContext.Session.SetString("RegisterOtpExpiry", DateTime.Now.AddMinutes(5).ToString("o"));

            // 1. Log OTP to debug console for testing
            _logger.LogWarning($"🔥 [MÃ OTP ĐĂNG KÝ THỬ NGHIỆM]: Email: {Input.Email} | Mã OTP: {otp}");

            // 2. Gửi mail xác nhận mã OTP thực tế
            string subject = "Mã OTP xác minh đăng ký tài khoản - Shop Bán Hàng";
            string message = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #eee; padding: 20px; border-radius: 8px;'>
                    <h2 style='color: #222; text-align: center; border-bottom: 2px solid #3498db; padding-bottom: 10px;'>MÃ OTP XÁC MINH ĐĂNG KÝ</h2>
                    <p>Xin chào,</p>
                    <p>Cảm ơn bạn đã đăng ký tài khoản tại Shop Bán Hàng. Mã OTP xác nhận tài khoản của bạn là:</p>
                    <div style='background: #f4f9fc; padding: 15px; border-radius: 6px; text-align: center; margin: 20px 0; border: 1px dashed #3498db;'>
                        <span style='font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #3498db;'>{otp}</span>
                    </div>
                    <p style='color: #e74c3c; font-size: 13px; font-weight: bold;'>Mã OTP này có hiệu lực trong vòng 5 phút. Vui lòng tuyệt đối không chia sẻ mã này cho bất kỳ ai khác.</p>
                    <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                    <p style='font-size: 12px; color: #999; text-align: center;'>Hệ thống Shop Bán Hàng</p>
                </div>";

            await _emailSender.SendEmailAsync(Input.Email.Trim(), subject, message);

            OtpSent = true;
            TempData["SuccessMessage"] = $"Mã OTP xác nhận đã được gửi đến email {Input.Email}!";
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            OtpSent = true;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (string.IsNullOrEmpty(Input.OtpCode))
            {
                ModelState.AddModelError("Input.OtpCode", "Vui lòng nhập mã OTP xác nhận.");
                return Page();
            }

            // Kiểm tra session dữ liệu
            string sessionEmail = HttpContext.Session.GetString("RegisterEmail");
            string sessionPhone = HttpContext.Session.GetString("RegisterPhone");
            string sessionOtp = HttpContext.Session.GetString("RegisterOtp");
            string sessionExpiryStr = HttpContext.Session.GetString("RegisterOtpExpiry");

            if (string.IsNullOrEmpty(sessionEmail) || string.IsNullOrEmpty(sessionOtp) || string.IsNullOrEmpty(sessionExpiryStr))
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy phiên gửi mã xác nhận. Vui lòng gửi lại mã OTP.");
                return Page();
            }

            if (sessionEmail != Input.Email.Trim() || sessionPhone != Input.PhoneNumber.Trim())
            {
                ModelState.AddModelError(string.Empty, "Thông tin Email hoặc Số điện thoại đã thay đổi so với mã OTP yêu cầu.");
                return Page();
            }

            if (sessionOtp != Input.OtpCode.Trim())
            {
                ModelState.AddModelError("Input.OtpCode", "Mã OTP xác nhận không chính xác.");
                return Page();
            }

            if (DateTime.TryParse(sessionExpiryStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime expiryTime))
            {
                if (DateTime.Now > expiryTime)
                {
                    ModelState.AddModelError(string.Empty, "Mã OTP xác nhận đã hết hạn. Vui lòng gửi lại mã.");
                    return Page();
                }
            }

            // Tiến hành tạo user
            var user = CreateUser();

            await _userStore.SetUserNameAsync(user, Input.Email.Trim(), CancellationToken.None);
            await _emailStore.SetEmailAsync(user, Input.Email.Trim(), CancellationToken.None);
            
            // Thiết lập số điện thoại và xác nhận Email
            user.PhoneNumber = Input.PhoneNumber.Trim();
            user.EmailConfirmed = true; 

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("User created a new account with password and verified phone.");

                // Xóa Session đăng ký
                HttpContext.Session.Remove("RegisterEmail");
                HttpContext.Session.Remove("RegisterPhone");
                HttpContext.Session.Remove("RegisterOtp");
                HttpContext.Session.Remove("RegisterOtpExpiry");

                await _signInManager.SignInAsync(user, isPersistent: false);
                return LocalRedirect(returnUrl);
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        private IdentityUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<IdentityUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(IdentityUser)}'. " +
                    $"Ensure that '{nameof(IdentityUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<IdentityUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<IdentityUser>)_userStore;
        }
    }
}

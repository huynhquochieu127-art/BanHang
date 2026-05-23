using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace BanHang.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginWithOtpModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<LoginWithOtpModel> _logger;

        public LoginWithOtpModel(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            IEmailSender emailSender,
            ILogger<LoginWithOtpModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public bool OtpSent { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Email không được để trống")]
            [EmailAddress(ErrorMessage = "Định dạng Email không hợp lệ")]
            public string Email { get; set; }

            public string Otp { get; set; }
        }

        public void OnGet()
        {
            Input = new InputModel();
            // Kiểm tra xem đã có OTP đang chờ trong session không
            var sessionEmail = HttpContext.Session.GetString("LoginOtpEmail");
            if (!string.IsNullOrEmpty(sessionEmail))
            {
                Input.Email = sessionEmail;
                OtpSent = true;
            }
        }

        public async Task<IActionResult> OnPostSendOtpAsync()
        {
            OtpSent = false;

            if (string.IsNullOrEmpty(Input.Email))
            {
                ModelState.AddModelError("Input.Email", "Vui lòng nhập Email.");
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Email không tồn tại trên hệ thống.");
                return Page();
            }

            // Tạo mã OTP 6 chữ số ngẫu nhiên
            string otp = new Random().Next(100000, 999999).ToString();

            // Lưu thông tin vào Session
            HttpContext.Session.SetString("LoginOtpEmail", Input.Email);
            HttpContext.Session.SetString("LoginOtpCode", otp);
            HttpContext.Session.SetString("LoginOtpExpiry", DateTime.Now.AddMinutes(5).ToString("o"));

            // Gửi Gmail
            string subject = "Mã OTP đăng nhập tài khoản - Shop Bán Hàng";
            string message = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #eee; padding: 20px; border-radius: 8px;'>
                    <h2 style='color: #222; text-align: center; border-bottom: 2px solid #2ecc71; padding-bottom: 10px;'>MÃ OTP ĐĂNG NHẬP NHANH</h2>
                    <p>Xin chào,</p>
                    <p>Bạn đã yêu cầu đăng nhập bằng mã OTP qua Gmail. Vui lòng sử dụng mã xác nhận dưới đây để truy cập tài khoản của bạn:</p>
                    <div style='background: #f4fbf7; padding: 15px; border-radius: 6px; text-align: center; margin: 20px 0; border: 1px dashed #2ecc71;'>
                        <span style='font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #2ecc71;'>{otp}</span>
                    </div>
                    <p style='color: #e74c3c; font-size: 13px; font-weight: bold;'>Mã OTP này có hiệu lực trong vòng 5 phút. Vui lòng tuyệt đối không chia sẻ mã này cho bất kỳ ai khác.</p>
                    <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                    <p style='font-size: 12px; color: #999; text-align: center;'>Đây là email tự động từ hệ thống Shop Bán Hàng, vui lòng không trả lời thư này.</p>
                </div>";

            await _emailSender.SendEmailAsync(Input.Email, subject, message);

            OtpSent = true;
            TempData["SuccessMessage"] = "Mã OTP đã được gửi đến Gmail của bạn thành công!";
            return Page();
        }

        public async Task<IActionResult> OnPostVerifyOtpAsync()
        {
            OtpSent = true;

            if (string.IsNullOrEmpty(Input.Email))
            {
                ModelState.AddModelError("Input.Email", "Vui lòng nhập Email.");
                return Page();
            }

            if (string.IsNullOrEmpty(Input.Otp))
            {
                ModelState.AddModelError("Input.Otp", "Vui lòng nhập mã OTP.");
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Email không tồn tại trên hệ thống.");
                return Page();
            }

            // Kiểm tra Session
            string sessionEmail = HttpContext.Session.GetString("LoginOtpEmail");
            string sessionOtp = HttpContext.Session.GetString("LoginOtpCode");
            string sessionExpiryStr = HttpContext.Session.GetString("LoginOtpExpiry");

            if (string.IsNullOrEmpty(sessionEmail) || string.IsNullOrEmpty(sessionOtp) || string.IsNullOrEmpty(sessionExpiryStr))
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy phiên gửi OTP. Vui lòng gửi lại mã OTP.");
                return Page();
            }

            if (!sessionEmail.Equals(Input.Email, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "Email không khớp với email đã nhận mã OTP.");
                return Page();
            }

            if (sessionOtp != Input.Otp.Trim())
            {
                ModelState.AddModelError("Input.Otp", "Mã OTP nhập vào không chính xác.");
                return Page();
            }

            if (DateTime.TryParse(sessionExpiryStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime expiryTime))
            {
                if (DateTime.Now > expiryTime)
                {
                    ModelState.AddModelError(string.Empty, "Mã OTP đã hết hạn (hiệu lực 5 phút). Vui lòng gửi lại mã mới.");
                    return Page();
                }
            }

            // Đăng nhập người dùng vào hệ thống
            await _signInManager.SignInAsync(user, isPersistent: false);

            _logger.LogInformation("User logged in with OTP via Gmail.");

            // Clear Session
            HttpContext.Session.Remove("LoginOtpEmail");
            HttpContext.Session.Remove("LoginOtpCode");
            HttpContext.Session.Remove("LoginOtpExpiry");

            // Kiểm tra Role để chuyển hướng thích hợp
            var userRoles = await _userManager.GetRolesAsync(user);
            if (userRoles.Contains("Admin"))
            {
                return RedirectToAction("Index", "ThongKe", new { area = "Admin" });
            }

            return Redirect("~/");
        }
    }
}

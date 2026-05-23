using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using BanHang.Models;
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
    public class LoginWithPhoneModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<LoginWithPhoneModel> _logger;

        public LoginWithPhoneModel(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            ApplicationDbContext context,
            IEmailSender emailSender,
            ILogger<LoginWithPhoneModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _emailSender = emailSender;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public bool OtpSent { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Số điện thoại không được để trống")]
            [Phone(ErrorMessage = "Số điện thoại không đúng định dạng")]
            public string PhoneNumber { get; set; }

            public string Otp { get; set; }
        }

        public void OnGet()
        {
            Input = new InputModel();
            var sessionPhone = HttpContext.Session.GetString("LoginPhone");
            if (!string.IsNullOrEmpty(sessionPhone))
            {
                Input.PhoneNumber = sessionPhone;
                OtpSent = true;
            }
        }

        public async Task<IActionResult> OnPostSendOtpAsync()
        {
            OtpSent = false;

            if (string.IsNullOrEmpty(Input.PhoneNumber))
            {
                ModelState.AddModelError("Input.PhoneNumber", "Vui lòng nhập Số điện thoại.");
                return Page();
            }

            string phone = Input.PhoneNumber.Trim();

            // 1. Tìm kiếm trong bảng AspNetUsers bằng PhoneNumber
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);

            // 2. Nếu không tìm thấy, tìm trong bảng NguoiDung tùy chỉnh bằng DienThoai
            if (user == null)
            {
                var nguoiDung = await _context.NguoiDungs.FirstOrDefaultAsync(n => n.DienThoai == phone);
                if (nguoiDung != null)
                {
                    user = await _userManager.FindByEmailAsync(nguoiDung.Email);
                }
            }

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Số điện thoại chưa được đăng ký trong hệ thống.");
                return Page();
            }

            // Tạo mã OTP 6 chữ số ngẫu nhiên
            string otp = new Random().Next(100000, 999999).ToString();

            // Lưu thông tin vào Session
            HttpContext.Session.SetString("LoginPhone", phone);
            HttpContext.Session.SetString("LoginPhoneOtp", otp);
            HttpContext.Session.SetString("LoginPhoneOtpExpiry", DateTime.Now.AddMinutes(5).ToString("o"));

            // 1. In ra Console để lập trình viên dễ test thử nghiệm
            _logger.LogWarning($"🔥 [MÃ OTP SMS THỬ NGHIỆM]: Đăng nhập SĐT {phone} - Mã OTP: {otp}");

            // 2. Gửi về Gmail đã đăng ký của tài khoản này
            if (!string.IsNullOrEmpty(user.Email))
            {
                string subject = "Mã OTP đăng nhập qua Số điện thoại - Shop Bán Hàng";
                string message = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #eee; padding: 20px; border-radius: 8px;'>
                        <h2 style='color: #222; text-align: center; border-bottom: 2px solid #2ecc71; padding-bottom: 10px;'>MÃ OTP XÁC MINH SỐ ĐIỆN THOẠI</h2>
                        <p>Xin chào,</p>
                        <p>Bạn đã yêu cầu đăng nhập bằng số điện thoại <strong>{phone}</strong>. Vui lòng nhập mã OTP dưới đây để hoàn tất đăng nhập:</p>
                        <div style='background: #f4fbf7; padding: 15px; border-radius: 6px; text-align: center; margin: 20px 0; border: 1px dashed #2ecc71;'>
                            <span style='font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #2ecc71;'>{otp}</span>
                        </div>
                        <p style='color: #e74c3c; font-size: 13px; font-weight: bold;'>Mã OTP này có hiệu lực trong vòng 5 phút. Vui lòng tuyệt đối không chia sẻ mã này cho bất kỳ ai khác.</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                        <p style='font-size: 12px; color: #999; text-align: center;'>Hệ thống Shop Bán Hàng</p>
                    </div>";

                await _emailSender.SendEmailAsync(user.Email, subject, message);
            }

            OtpSent = true;
            TempData["SuccessMessage"] = $"Mã OTP đã được gửi đến số điện thoại {phone} và Gmail đăng ký!";
            return Page();
        }

        public async Task<IActionResult> OnPostVerifyOtpAsync()
        {
            OtpSent = true;

            if (string.IsNullOrEmpty(Input.PhoneNumber))
            {
                ModelState.AddModelError("Input.PhoneNumber", "Vui lòng nhập Số điện thoại.");
                return Page();
            }

            if (string.IsNullOrEmpty(Input.Otp))
            {
                ModelState.AddModelError("Input.Otp", "Vui lòng nhập mã OTP.");
                return Page();
            }

            string phone = Input.PhoneNumber.Trim();

            // Tìm user
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);
            if (user == null)
            {
                var nguoiDung = await _context.NguoiDungs.FirstOrDefaultAsync(n => n.DienThoai == phone);
                if (nguoiDung != null)
                {
                    user = await _userManager.FindByEmailAsync(nguoiDung.Email);
                }
            }

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Số điện thoại chưa được đăng ký trong hệ thống.");
                return Page();
            }

            // Kiểm tra Session
            string sessionPhone = HttpContext.Session.GetString("LoginPhone");
            string sessionOtp = HttpContext.Session.GetString("LoginPhoneOtp");
            string sessionExpiryStr = HttpContext.Session.GetString("LoginPhoneOtpExpiry");

            if (string.IsNullOrEmpty(sessionPhone) || string.IsNullOrEmpty(sessionOtp) || string.IsNullOrEmpty(sessionExpiryStr))
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy phiên gửi OTP. Vui lòng nhấn gửi lại mã.");
                return Page();
            }

            if (sessionPhone != phone)
            {
                ModelState.AddModelError(string.Empty, "Số điện thoại không khớp với số điện thoại đã yêu cầu mã OTP.");
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

            _logger.LogInformation("User logged in with OTP via Phone Number.");

            // Clear Session
            HttpContext.Session.Remove("LoginPhone");
            HttpContext.Session.Remove("LoginPhoneOtp");
            HttpContext.Session.Remove("LoginPhoneOtpExpiry");

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

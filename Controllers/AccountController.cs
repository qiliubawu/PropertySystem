using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using PropertySystem.Data;
using PropertySystem.Models;
using System.Security.Claims;

namespace PropertySystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context) { _context = context; }

        [HttpGet] public IActionResult Login() => View();

        [HttpPost]
        [HttpPost]
        //登录
        public IActionResult Login(string loginKey, string password) 
        {
            var user = _context.Users.FirstOrDefault(u =>
                (u.Username == loginKey || u.FullName == loginKey) && u.Password == password);

            if (user != null)
            {
                var claims = new List<Claim> {
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim("UserId", user.Id.ToString())
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

                if (user.Role == "Admin") return RedirectToAction("Index", "AdminDashboard");
                if (user.Role == "Owner" || user.Role == "Resident") return RedirectToAction("Index", "Owner");
                if (user.Role == "Security") return RedirectToAction("Index", "Security");
                if (user.Role == "Cleaner") return RedirectToAction("Index", "Cleaner");
                if (user.Role == "Maintenance") return RedirectToAction("Index", "Maintenance");
                if (user.Role == "FireSafety") return RedirectToAction("Index", "FireSafety");
            }
            ViewBag.Error = "账号/姓名或密码错误！"; return View();
        }

        //登出
        public IActionResult Logout()
        {
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        //注册
        [HttpGet] public IActionResult Register() => View();

        [HttpPost]
        public IActionResult Register(string phone, string fullName, string password, string role)
        {
            //防止账号重复注册
            if (_context.Users.Any(u => u.Username == phone))
            {
                ViewBag.Error = "❌ 该账号(手机号)已被注册，请换一个号码测试！";
                return View();
            }

            // 建号
            var newUser = new User
            {
                Username = phone,
                Password = password,
                FullName = fullName,
                Role = role
            };

            _context.Users.Add(newUser);
            _context.SaveChanges();


            if (role == "Owner")
            {
                var dummyOwner = new Owner
                {
                    Name = fullName,
                    Gender = "男", // 给个默认值
                    Age = 30,     // 给个默认值
                    Phone = phone,
                    ResidentCount = 1,
                    UserId = newUser.Id // 绑定刚生成的账号
                };
                _context.Owners.Add(dummyOwner);
                _context.SaveChanges();
            }

            TempData["Success"] = $"🎉 演示账号 [{fullName}] 创建成功！\n已赋予 [{role}] 最高操作权限，请直接登录。";
            return RedirectToAction("Login");
        }

        //找回密码
        [HttpGet] public IActionResult ForgotPassword() => View();

        [HttpPost]
        public IActionResult ForgotPassword(string phone, string newPassword)
        {
            //改密码
            var user = _context.Users.FirstOrDefault(u => u.Username == phone);
            if (user == null)
            {
                ViewBag.Error = "❌ 找不到该账号！";
                return View();
            }

            user.Password = newPassword;
            _context.SaveChanges();
            TempData["Success"] = "✅ 密码重置成功，请使用新密码！";
            return RedirectToAction("Login");
        }
    }
}

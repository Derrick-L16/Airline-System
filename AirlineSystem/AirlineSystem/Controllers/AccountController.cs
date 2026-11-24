using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirlineSystem.Controllers;

public class AccountController(DB db,
                               Helper hp,
                               IConfiguration config) : Controller
{
    // GET: Account/Login
    public IActionResult Login()
    {
        return View();
    }

    // POST: Account/Login
    [HttpPost]
    public async Task<IActionResult> Login(LoginVM vm, string? returnURL)
    {
        // (1) Verify Google reCAPTCHA
        string? captchaResponse = Request.Form["g-recaptcha-response"];

        if (string.IsNullOrEmpty(captchaResponse))
        {
            ModelState.AddModelError("", "Please check the 'I'm not a robot' box.");
            return View(vm);
        }

        var secretKey = config["Recaptcha:SecretKey"];
        using var client = new HttpClient();
        var verifyUrl = $"https://www.google.com/recaptcha/api/siteverify?secret={secretKey}&response={captchaResponse}";

        var response = await client.PostAsync(verifyUrl, null);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<RecaptchaResult>();
            if (result == null || !result.Success)
            {
                ModelState.AddModelError("", "CAPTCHA verification failed. Please try again.");
                return View(vm);
            }
        }
        else
        {
            ModelState.AddModelError("", "Unable to verify CAPTCHA at this time.");
            return View(vm);
        }

        // (2) Get user (admin or member) record based on email (PK)
        var u = db.Users.FirstOrDefault(u => u.Email == vm.Email);

        // (3) Check if the account is currently locked out
        if (u != null && u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTime.Now)
        {
            var remaining = Math.Ceiling((u.LockoutEnd.Value - DateTime.Now).TotalMinutes);
            ModelState.AddModelError("", $"Account temporarily locked. Try again in {remaining} minute(s).");
            return View(vm);
        }

        // (4) Custom validation -> verify password
        if (u == null || !hp.VerifyPassword(u.Hash, vm.Password))
        {
            // User exists but password is incorrect
            if (u != null)
            {
                u.FailedLoginAttempts++;

                // Trigger lockout every 3 failed attempts
                if (u.FailedLoginAttempts >= 3)
                {
                    u.LockoutCount++;
                    // Gradually increase: 5 mins, then 10 mins, 20 mins, etc. (Exponential)
                    double lockoutMinutes = 5 * Math.Pow(2, u.LockoutCount - 1);

                    u.LockoutEnd = DateTime.Now.AddMinutes(lockoutMinutes);
                    u.FailedLoginAttempts = 0; // Reset attempt counter for the next cycle

                    ModelState.AddModelError("", $"Too many failed attempts. Account locked for {lockoutMinutes} minutes.");
                }
                else
                {
                    int attemptsLeft = 3 - u.FailedLoginAttempts;
                    ModelState.AddModelError("", $"Login credentials not matched. {attemptsLeft} attempt(s) remaining.");
                }
                db.SaveChanges();
            }
            else
            {
                // User email doesn't exist
                ModelState.AddModelError("", "Account with the email provided does not exist.");
            }
        }
        else if (u.Status == "Blocked")
        {
            ModelState.AddModelError("", "Your account has been permanently blocked. Please contact the administrator.");
        }

        if (ModelState.IsValid)
        {
            // Reset all lockout tracking upon a successful login
            u!.FailedLoginAttempts = 0;
            u.LockoutCount = 0;
            u.LockoutEnd = null;
            db.SaveChanges();

            TempData["Info"] = "Login successfully.";

            // (3) Sign in
            hp.SignIn(u!.Id, u!.Email, u.Role, vm.RememberMe);

            if (u.Role == "Admin")
            {
                return RedirectToAction("AdminFlightTable", "Admin");
            }

            // (4) Handle return URL
            if (string.IsNullOrEmpty(returnURL))
            {
                return RedirectToAction("Index", "Home");
            }
            return Redirect(returnURL);
        }
        
        return View(vm);
    }

    // GET: Account/Logout
    public IActionResult Logout(string? returnURL)
    {
        TempData["Info"] = "Logout successfully.";

        // Sign out
        hp.SignOut();

        return RedirectToAction("Index", "Home");
    }

    // GET: Account/AccessDenied
    public IActionResult AccessDenied(string? returnURL)
    {
        return View();
    }



    // ------------------------------------------------------------------------
    // Others
    // ------------------------------------------------------------------------

    // GET: Account/CheckEmail
    public bool CheckEmail(string email)
    {
        return !db.Users.Any(u => u.Email == email);
    }

    // GET: Account/Register
    public IActionResult Register()
    {
        return View();
    }

    // POST: Account/Register
    [HttpPost]
    public IActionResult Register(RegisterVM vm)
    {
        if (ModelState.IsValid("Email") &&
            db.Users.Any(u => u.Email == vm.Email))
        {
            ModelState.AddModelError("Email", "Duplicated Email.");
        }

        if (ModelState.IsValid("Photo"))
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }
        
        if (ModelState.IsValid)
        {
            // Insert member
            db.Members.Add(new()
            {
                Email = vm.Email,
                Hash = hp.HashPassword(vm.Password),
                Name = vm.Name,
                PhotoURL = hp.SavePhoto(vm.Photo, "photos"),
                PhoneNumber = vm.PhoneNumber,
                IC = vm.IC,
                PassportNo = vm.PassportNo,
                PassportExpiry = vm.PassportExpiry,
            });
            db.SaveChanges();

            TempData["Info"] = "Register successfully. Please login.";
            return RedirectToAction("Login");
        }

        return View(vm);
    }

    // GET: Account/UpdatePassword
    public IActionResult UpdatePassword()
    {
        return View();
    }

    // POST: Account/UpdatePassword
    [HttpPost]
    public IActionResult UpdatePassword(UpdatePasswordVM vm)
    {
        // Get user (admin or member) record based on email (PK)
        var m = db.Members.FirstOrDefault(m => m.Email == User.Identity!.Name);
        if (m == null) return RedirectToAction("Index", "Home");

        // If current password not matched
        if (!hp.VerifyPassword(m.Hash, vm.Current))
        {
            ModelState.AddModelError("Current", "Current Password not matched.");
        }

        if (ModelState.IsValid)
        {
            // Update user password (hash)
            m.Hash = hp.HashPassword(vm.New);
            db.SaveChanges();

            TempData["Info"] = "Password updated.";
            return RedirectToAction();
        }

        return View();
    }

    // GET: Account/UpdateProfile
    [Authorize(Roles = "Member")]
    public IActionResult UpdateProfile()
    {
        // Get member record based on signed-in user's email
        var m = db.Members.FirstOrDefault(m => m.Email == User.Identity!.Name);
        if (m == null) return RedirectToAction("Index", "Home");

        var vm = new UpdateProfileVM
        {
            Email = m.Email,
            Name = m.Name,
            PhotoURL = m.PhotoURL,
            PhoneNumber = m.PhoneNumber,
            IC = m.IC,
            PassportNo = m.PassportNo,
            PassportExpiry = m.PassportExpiry,
        };

        return View(vm);
    }

    // POST: Account/UpdateProfile
    [Authorize(Roles = "Member")]
    [HttpPost]
    public IActionResult UpdateProfile(UpdateProfileVM vm)
    {
        // Get member record based on email (PK)
        var m = db.Members.FirstOrDefault(m => m.Email == User.Identity!.Name); ;
        if (m == null) return RedirectToAction("Index", "Home");

        if (vm.Photo != null)
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }

        if (ModelState.IsValid)
        {
            m.Name = vm.Name;
            m.PhoneNumber = vm.PhoneNumber;
            m.IC = vm.IC;
            m.PassportNo = vm.PassportNo;
            m.PassportExpiry = vm.PassportExpiry;

            if (vm.Photo != null)
            {
                hp.DeletePhoto(m.PhotoURL, "photos");
                m.PhotoURL = hp.SavePhoto(vm.Photo, "photos");
            }

            db.SaveChanges();

            TempData["Info"] = "Profile updated.";
            return RedirectToAction();
        }

        vm.Email = m.Email;
        vm.PhotoURL = m.PhotoURL;
        return View(vm);
    }

    // GET: Account/ResetPassword
    public IActionResult ResetPassword()
    {
        return View();
    }

    // POST: Account/ResetPassword
    [HttpPost]
    public IActionResult ResetPassword(ResetPasswordVM vm)
    {
        var u = db.Users.FirstOrDefault(u => u.Email == vm.Email);

        if (u == null)
        {
            ModelState.AddModelError("Email", "Email not found.");
        }

        if (ModelState.IsValid)
        {
            // Generate random password
            string password = hp.RandomPassword();

            // Update user (admin or member) record
            u!.Hash = hp.HashPassword(password);

            // TODO: Send reset password email

            // Temporary only
            TempData["Info"] = $"Password reset to <b>{password}</b>.";
            return RedirectToAction();
        }

        return View();
    }

    // GET: Account/Detail
    [Authorize(Roles = "Admin")]
    public IActionResult Detail(int id)
    {
        var model = db.Users.Find(id);

        if (model == null)
        {
            return RedirectToAction("UserManagement", "Admin");
        }

        return View(model);
    }

    // POST: Account/Block
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public IActionResult Block(string email)
    {
        var u = db.Users.FirstOrDefault(u => u.Email == email);
        if (u != null && u.Role != "Admin") // Prevent Blocking another admin
        {
            u.Status = "Blocked";
            db.SaveChanges();
            TempData["Info"] = $"User {u.Name} has been blocked.";
        }
        return RedirectToAction("UserManagement", "Admin");
    }

    // POST: Account/Unblock
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public IActionResult Unblock(string email)
    {
        var u = db.Users.FirstOrDefault(u => u.Email == email);
        if (u != null)
        {
            u.Status = null;
            db.SaveChanges();
            TempData["Info"] = $"User {u.Name} has been unblocked.";
        }

        return RedirectToAction("UserManagement", "Admin");
    }
}

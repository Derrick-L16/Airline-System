using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Rest.Verify.V2.Service;
using Twilio.Types;

namespace AirlineSystem;

// TODO
public class Helper(IWebHostEnvironment en,
                    IHttpContextAccessor ct,
                    IConfiguration config)
{
    // ------------------------------------------------------------------------
    // Photo Upload
    // ------------------------------------------------------------------------

    public string ValidatePhoto(IFormFile f)
    {
        var reType = new Regex(@"^image\/(jpeg|png)$", RegexOptions.IgnoreCase);
        var reName = new Regex(@"^.+\.(jpeg|jpg|png)$", RegexOptions.IgnoreCase);

        if (!reType.IsMatch(f.ContentType) || !reName.IsMatch(f.FileName))
        {
            return "Only JPG and PNG photo is allowed.";
        }
        else if (f.Length > 1 * 1024 * 1024)
        {
            return "Photo size cannot more than 1MB.";
        }

        return "";
    }

    public string SavePhoto(IFormFile f, string folder)
    {
        var file = Guid.NewGuid().ToString("n") + ".jpg";
        var path = Path.Combine(en.WebRootPath, folder, file);

        var options = new ResizeOptions
        {
            Size = new(200, 200),
            Mode = ResizeMode.Crop,
        };

        using var stream = f.OpenReadStream();
        using var img = Image.Load(stream);
        img.Mutate(x => x.Resize(options));
        img.Save(path);

        return file;
    }

    public void DeletePhoto(string file, string folder)
    {
        file = Path.GetFileName(file);
        var path = Path.Combine(en.WebRootPath, folder, file);
        File.Delete(path);
    }



    // ------------------------------------------------------------------------
    // Security Helper Functions
    // ------------------------------------------------------------------------

    private readonly PasswordHasher<object> ph = new();

    public string HashPassword(string password)
    { 
        return ph.HashPassword(0, password);
    }

    public bool VerifyPassword(string hash, string password)
    {
        return ph.VerifyHashedPassword(0, hash, password)
               == PasswordVerificationResult.Success;
    }

    public void SignIn(int id, string email, string role, bool rememberMe)
    {
        // (1) Claim, identity and principal

        List<Claim> claims = 
        [
            new(ClaimTypes.NameIdentifier, id.ToString()),
            new(ClaimTypes.Name, email),
            new(ClaimTypes.Role, role),
        ];

        ClaimsIdentity identity = new(claims, "Cookies");

        ClaimsPrincipal principal = new(identity);

        // (2) Remember me (authentication properties)
        AuthenticationProperties properties = new()
        {
            IsPersistent = rememberMe,
        };

        // (3) Sign in
        ct.HttpContext!.SignInAsync(principal, properties);
    }

    public void SignOut()
    {
        // Sign out
        ct.HttpContext!.SignOutAsync();
    }

    public string RandomPassword()
    {
        string s = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        
        return Random.Shared.GetString(s, 10);
    }
    // ------------------------------------------------------------------------
    // Email Helper Functions
    // ------------------------------------------------------------------------

    public async void SendEmail(MailMessage mail)
    {
        string user = config["Smtp:User"] ?? "";
        string pass = config["Smtp:Pass"] ?? "";
        string name = config["Smtp:Name"] ?? "";
        string host = config["Smtp:Host"] ?? "";
        int port = config.GetValue<int>("Smtp:Port");

        mail.From = new MailAddress(user, name);

        using var smtp = new SmtpClient
        {
            Host = host,
            Port = port,
            EnableSsl = true,
            Credentials = new NetworkCredential(user, pass),
        };

        await smtp.SendMailAsync(mail);
    }

    // ------------------------------------------------------------------------
    // SMS Verification Helper Functions
    // ------------------------------------------------------------------------
    private string FormatPhone(string toPhoneNumber)
    {
        toPhoneNumber = toPhoneNumber.Replace("-", "").Trim();
        if (toPhoneNumber.StartsWith("01"))
        {
            return "+60" + toPhoneNumber.Substring(1);
        }
        if (!toPhoneNumber.StartsWith("+"))
        {
            return "+60" + toPhoneNumber;
        }
        return toPhoneNumber;
    }

    public async Task<bool> SendOTP(string toPhoneNumber)
    {
        try
        {
            TwilioClient.Init(config["Twilio:AccountSid"], config["Twilio:AuthToken"]);

            var verification = await VerificationResource.CreateAsync(
                to: FormatPhone(toPhoneNumber),
                channel: "sms",
                pathServiceSid: config["Twilio:VerifyServiceSid"]
            );

            return verification.Status == "pending";
        }
        catch (Exception ex)
        {
            Console.WriteLine("TWILIO ERROR: " + ex.Message);
            return false;
        }
    }

    public async Task<bool> CheckOTP(string toPhoneNumber, string code)
    {
        try
        {
            TwilioClient.Init(config["Twilio:AccountSid"], config["Twilio:AuthToken"]);

            var check = await VerificationCheckResource.CreateAsync(
                to: FormatPhone(toPhoneNumber),
                code: code.Trim(),
                pathServiceSid: config["Twilio:VerifyServiceSid"]
            );

            return check.Status == "approved";
        }
        catch (Exception ex)
        {
            Console.WriteLine("TWILIO ERROR: " + ex.Message);
            return false;
        }
    }
}

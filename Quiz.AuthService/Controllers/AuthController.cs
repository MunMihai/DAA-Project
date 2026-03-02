using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Quiz.AuthService.DTOs;
using Quiz.AuthService.Models;
using Quiz.AuthService.Services;

namespace Quiz.AuthService.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    RoleManager<ApplicationRole> roleManager,
    IJwtTokenService jwt
) : ControllerBase
{
    [HttpPost("signup")]
    public async Task<ActionResult<AuthResponse>> Signup([FromBody] SignupRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
            return Conflict(new { message = "Email already registered." });

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true 
        };

        var result = await userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(new { message = "Signup failed.", errors = result.Errors.Select(e => e.Description) });

        const string defaultRole = "Student";
        if (!await roleManager.RoleExistsAsync(defaultRole))
            await roleManager.CreateAsync(new ApplicationRole { Name = defaultRole });

        await userManager.AddToRoleAsync(user, defaultRole);

        var roles = await userManager.GetRolesAsync(user);
        var (token, exp) = jwt.CreateToken(user, roles);

        return Ok(new AuthResponse(token, exp, user.Email!));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            return Unauthorized(new { message = "Invalid credentials." });

        var check = await signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
            return Unauthorized(new { message = "Invalid credentials." });

        var roles = await userManager.GetRolesAsync(user);
        var (token, exp) = jwt.CreateToken(user, roles);

        return Ok(new AuthResponse(token, exp, user.Email!));
    }

    [HttpPost("run-seed")]
    public async Task<IActionResult> RunSeed()
    {
        var roles = new[] { "Admin", "Teacher", "Student" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new ApplicationRole { Name = role });
            }
        }

        var demoUsers = new[]
        {
            new { Email = "admin@exemplu.md", Role = "Admin" },
            new { Email = "teacher@exemplu.md", Role = "Teacher" },
            new { Email = "student@exemplu.md", Role = "Student" }
        };

        var createdCount = 0;
        foreach (var u in demoUsers)
        {
            var existing = await userManager.FindByEmailAsync(u.Email);
            if (existing == null)
            {
                var user = new ApplicationUser
                {
                    UserName = u.Email,
                    Email = u.Email,
                    EmailConfirmed = true
                };

                var res = await userManager.CreateAsync(user, "Password123!");
                if (res.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, u.Role);
                    createdCount++;
                }
            }
        }

        return Ok(new
        {
            message = $"Seed completat. Au fost create {createdCount} conturi noi.",
            accounts = demoUsers.Select(d => new { d.Email, Password = "Password123!", d.Role })
        });
    }
}

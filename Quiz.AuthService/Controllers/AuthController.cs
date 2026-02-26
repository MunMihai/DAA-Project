using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Quiz.AuthService.DTOs;
using Quiz.AuthService.Services;

namespace Quiz.AuthService.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    RoleManager<IdentityRole> roleManager,
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

        var user = new IdentityUser
        {
            Email = email,
            UserName = email,
            EmailConfirmed = true 
        };

        var result = await userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(new { message = "Signup failed.", errors = result.Errors.Select(e => e.Description) });

        // rol implicit "Student"
        const string defaultRole = "Student";
        if (!await roleManager.RoleExistsAsync(defaultRole))
            await roleManager.CreateAsync(new IdentityRole(defaultRole));

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
}
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Quiz.CodingService.Services;

namespace Quiz.CodingService.Controllers;

[ApiController]
[Route("api/compile-coding-templates")]
public sealed class CompileCodingTemplatesController(
    CompileCodingTemplateService service,
    IHostEnvironment environment,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTemplates(CancellationToken ct)
    {
        return Ok(await service.GetTemplatesAsync(ct));
    }

    [HttpPost("run-seed")]
    public async Task<IActionResult> RunSeed(CancellationToken ct)
    {
        if (!environment.IsDevelopment())
        {
            if (!configuration.GetValue<bool>("Seed:Enabled"))
                return NotFound();

            var configuredSeedToken = configuration["Seed:Token"];
            if (string.IsNullOrWhiteSpace(configuredSeedToken))
            {
                return Problem(
                    detail: "Seed endpoint is enabled, but Seed:Token is missing.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            if (!Request.Headers.TryGetValue("X-Seed-Token", out var providedSeedToken) ||
                !TokensMatch(providedSeedToken.ToString(), configuredSeedToken))
            {
                return Unauthorized(new { message = "Missing or invalid seed token." });
            }
        }

        var template = await service.RunSeedAsync(ct);

        return Ok(new
        {
            message = "Seed-ul pentru șablonul compile a fost aplicat.",
            template
        });
    }

    private static bool TokensMatch(string providedToken, string configuredToken)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedToken),
            Encoding.UTF8.GetBytes(configuredToken));
    }
}

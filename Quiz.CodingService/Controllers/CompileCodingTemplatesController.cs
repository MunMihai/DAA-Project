using Microsoft.AspNetCore.Mvc;
using Quiz.CodingService.Services;

namespace Quiz.CodingService.Controllers;

[ApiController]
[Route("api/compile-coding-templates")]
public sealed class CompileCodingTemplatesController(CompileCodingTemplateService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTemplates(CancellationToken ct)
    {
        return Ok(await service.GetTemplatesAsync(ct));
    }

    [HttpPost("run-seed")]
    public async Task<IActionResult> RunSeed(CancellationToken ct)
    {
        var template = await service.RunSeedAsync(ct);

        return Ok(new
        {
            message = "Seed-ul pentru șablonul compile a fost aplicat.",
            template
        });
    }
}

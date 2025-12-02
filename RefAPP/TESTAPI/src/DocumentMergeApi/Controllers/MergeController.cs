using DocumentMergeApi.Models;
using DocumentMergeApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocumentMergeApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class MergeController : ControllerBase
{
    private readonly MergeService _service;
    public MergeController(MergeService service) => _service = service;

    [HttpPost("merge")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Merge([FromForm] MergeRequest request)
    {
        if (request.Template is null || request.Raw is null)
            return BadRequest("template and raw files are required.");

        using var templateStream = new MemoryStream();
        using var rawStream = new MemoryStream();
        await request.Template.CopyToAsync(templateStream);
        await request.Raw.CopyToAsync(rawStream);
        templateStream.Position = 0;
        rawStream.Position = 0;

        var result = _service.Merge(templateStream, rawStream, request.Author);
        return File(result.Content,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            result.FileName);
    }
}


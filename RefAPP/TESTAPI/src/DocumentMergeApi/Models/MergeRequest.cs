using Microsoft.AspNetCore.Http;

namespace DocumentMergeApi.Models;

public sealed class MergeRequest
{
    public IFormFile Template { get; set; } = null!;
    public IFormFile Raw { get; set; } = null!;
    public string? Author { get; set; }
}


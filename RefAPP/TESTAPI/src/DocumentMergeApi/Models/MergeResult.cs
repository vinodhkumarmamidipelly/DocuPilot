namespace DocumentMergeApi.Models;

public sealed class MergeResult
{
    public MergeResult(byte[] content, string fileName)
    {
        Content = content;
        FileName = fileName;
    }

    public byte[] Content { get; }
    public string FileName { get; }
}


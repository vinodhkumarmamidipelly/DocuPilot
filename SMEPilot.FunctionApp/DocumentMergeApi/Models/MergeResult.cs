namespace DocumentMergeApi.Models;

public sealed class MergeResult
{
    public MergeResult(byte[] content, string fileName, string version)
    {
        Content = content;
        FileName = fileName;
        Version = version;
    }

    public byte[] Content { get; }
    public string FileName { get; }
    public string Version { get; }
}



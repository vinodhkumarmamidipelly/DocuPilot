using DocumentFormat.OpenXml;

namespace DocumentMergeApi.Models;

public sealed class RawSection
{
    public int Key { get; }
    public string HeadingText { get; }
    public List<OpenXmlElement> Elements { get; }

    public RawSection(int Key, string HeadingText, List<OpenXmlElement> Elements)
    {
        this.Key = Key;
        this.HeadingText = HeadingText;
        this.Elements = Elements;
    }
}



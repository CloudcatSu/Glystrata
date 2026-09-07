using System.Text;

namespace MDeditor.Core.Documents;

public sealed class UnsupportedTextEncodingException : Exception
{
    public UnsupportedTextEncodingException(string message)
        : base(message)
    {
    }
}

public sealed class FileCodec
{
    public DecodedText Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var bytes = File.ReadAllBytes(fullPath);
        return Decode(bytes, FileFingerprint.Read(fullPath));
    }

    public DecodedText Decode(byte[] bytes, FileFingerprint fingerprint)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var offset = 0;
        FileEncodingKind encoding;
        Encoding decoder;

        if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            encoding = FileEncodingKind.Utf8WithBom;
            offset = 3;
            decoder = new UTF8Encoding(false, true);
        }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE }))
        {
            encoding = FileEncodingKind.Utf16LittleEndian;
            offset = 2;
            decoder = new UnicodeEncoding(false, false, true);
        }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF }))
        {
            throw new UnsupportedTextEncodingException("UTF-16 Big Endian 目前不在支援範圍內。");
        }
        else
        {
            encoding = FileEncodingKind.Utf8;
            decoder = new UTF8Encoding(false, true);
        }

        string rawText;
        try
        {
            rawText = decoder.GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException exception)
        {
            throw new UnsupportedTextEncodingException($"無法以 UTF-8 或 UTF-16 LE 讀取檔案：{exception.Message}");
        }

        var lineEnding = DetectLineEnding(rawText);
        return new DecodedText(NormalizeLineEndings(rawText), encoding, lineEnding, fingerprint);
    }

    public byte[] Encode(string text, FileEncodingKind encoding, LineEndingKind lineEnding)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = NormalizeLineEndings(text);
        var serialized = lineEnding switch
        {
            LineEndingKind.CrLf => normalized.Replace("\n", "\r\n", StringComparison.Ordinal),
            LineEndingKind.Cr => normalized.Replace("\n", "\r", StringComparison.Ordinal),
            _ => normalized
        };

        Encoding encoder = encoding switch
        {
            FileEncodingKind.Utf8WithBom => new UTF8Encoding(false),
            FileEncodingKind.Utf16LittleEndian => new UnicodeEncoding(false, false),
            _ => new UTF8Encoding(false)
        };
        var body = encoder.GetBytes(serialized);
        var preamble = encoding switch
        {
            FileEncodingKind.Utf8WithBom => new UTF8Encoding(true).GetPreamble(),
            FileEncodingKind.Utf16LittleEndian => new UnicodeEncoding(false, true).GetPreamble(),
            _ => Array.Empty<byte>()
        };
        return preamble.Concat(body).ToArray();
    }

    public static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    public static LineEndingKind DetectLineEnding(string text)
    {
        var crlf = 0;
        var lf = 0;
        var cr = 0;

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\r')
            {
                if (index + 1 < text.Length && text[index + 1] == '\n')
                {
                    crlf++;
                    index++;
                }
                else
                {
                    cr++;
                }
            }
            else if (text[index] == '\n')
            {
                lf++;
            }
        }

        if (crlf == 0 && lf == 0 && cr == 0)
        {
            return LineEndingKind.Lf;
        }

        if (crlf >= lf && crlf >= cr)
        {
            return LineEndingKind.CrLf;
        }

        return lf >= cr ? LineEndingKind.Lf : LineEndingKind.Cr;
    }
}

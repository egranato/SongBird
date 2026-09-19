namespace SongBird.Tests.TestSupport;

/// <summary>
/// Synthesizes tiny, valid, silent WAV files at test time so scanner/repository tests don't
/// need binary fixture assets checked into the repo. TagLibSharp can open and tag these normally.
/// </summary>
internal static class WavFixture
{
    public static void Create(
        string path,
        string? title = null,
        string? artist = null,
        string? album = null,
        double durationSeconds = 0.1)
    {
        WriteSilentWav(path, durationSeconds);

        if (title is null && artist is null && album is null)
        {
            return;
        }

        using var file = TagLib.File.Create(path);
        if (title is not null) file.Tag.Title = title;
        if (artist is not null) file.Tag.Performers = [artist];
        if (album is not null) file.Tag.Album = album;
        file.Save();
    }

    private static void WriteSilentWav(string path, double seconds, int sampleRate = 8000)
    {
        const int channels = 1;
        const int bitsPerSample = 16;
        var byteRate = sampleRate * channels * bitsPerSample / 8;
        var blockAlign = channels * bitsPerSample / 8;
        var dataSize = (int)(sampleRate * seconds) * blockAlign;

        using var stream = new FileStream(path, FileMode.Create);
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataSize);
        writer.Write(new byte[dataSize]);
    }
}

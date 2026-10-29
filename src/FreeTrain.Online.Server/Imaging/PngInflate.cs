namespace StbImageSharp;

internal static class PngInflate
{
    // Exact PNG scanline/filter byte count, including the seven Adam7 passes.
    internal static int Length(uint width, uint height, int channels, int depth, int interlace)
    {
        if (width is < 1 or > 4096 || height is < 1 or > 4096 || (long)width * height > 4_194_304 ||
            channels is < 1 or > 4 || depth is not (1 or 2 or 4 or 8 or 16) || interlace is not (0 or 1)) throw new InvalidDataException();
        long Rows(long w, long h) => w == 0 || h == 0 ? 0 : h * (1 + (w * channels * depth + 7) / 8);
        if (interlace == 0) return checked((int)Rows(width, height));
        ReadOnlySpan<int> x = [0,4,0,2,0,1,0], y = [0,0,4,0,2,0,1], dx = [8,8,4,4,2,2,1], dy = [8,8,8,4,4,2,2];
        long total = 0;
        for (int i = 0; i < 7; i++)
            total += Rows(Math.Max(0, ((long)width - x[i] + dx[i] - 1) / dx[i]), Math.Max(0, ((long)height - y[i] + dy[i] - 1) / dy[i]));
        return checked((int)total);
    }
}

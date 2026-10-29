// Vendored StbImageSharp 2.30.16. Local: strict, allocation-free stream reads; thread-local errors.
#nullable disable
using StbImageSharp.Hebron.Runtime;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace StbImageSharp
{
#if !STBSHARP_INTERNAL
	public
#else
	internal
#endif
	static unsafe partial class StbImage
	{
		[ThreadStatic] public static string stbi__g_failure_reason;
		[ThreadStatic] private static char[] invalidChunk;
        public static char[] stbi__parse_png_file_invalid_chunk => invalidChunk ??= new char[25];

		public class stbi__context
		{
			private readonly Stream _stream;

			public int img_n = 0;
			public int img_out_n = 0;
			public uint img_x = 0;
			public uint img_y = 0;

			public stbi__context(Stream stream)
			{
				if (stream == null)
					throw new ArgumentNullException("stream");

				_stream = stream;
			}

			public Stream Stream
			{
				get
				{
					return _stream;
				}
			}
		}

		private static int stbi__err(string str)
		{
			stbi__g_failure_reason = str;
			return 0;
		}

		public static byte stbi__get8(stbi__context s)
		{
			DecodeBudget.Check();
            var b = s.Stream.ReadByte();
			if (b == -1) throw new EndOfStreamException();

			return (byte)b;
		}

		public static void stbi__skip(stbi__context s, int skip)
		{
			DecodeBudget.Check();
            if (skip < 0 || skip > s.Stream.Length - s.Stream.Position) throw new EndOfStreamException();
            s.Stream.Seek(skip, SeekOrigin.Current);
		}

		public static void stbi__rewind(stbi__context s)
		{
			s.Stream.Seek(0, SeekOrigin.Begin);
		}

		public static int stbi__at_eof(stbi__context s)
		{
			return s.Stream.Position == s.Stream.Length ? 1 : 0;
		}

        public static int stbi__getn(stbi__context s, byte* buf, int size)
        {
            DecodeBudget.Check();
            if (size < 0 || size > s.Stream.Length - s.Stream.Position) throw new System.IO.EndOfStreamException();
            s.Stream.ReadExactly(new Span<byte>(buf, size));
            return size;
        }
    }
}

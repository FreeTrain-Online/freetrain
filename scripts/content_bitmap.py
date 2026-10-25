"""Portable RGB888 bitmap surfaces. No native decoder or desktop dependency."""
import struct
import zlib


def decode(data):
    if len(data) < 54 or data[:2] != b'BM':
        raise ValueError('Invalid BMP header')
    offset, header = struct.unpack_from('<II', data, 10)
    width, signed_height, planes, bits, compression = struct.unpack_from('<iiHHI', data, 18)
    if (header < 40 or 14 + header > len(data) or planes != 1 or compression != 0
            or bits not in (4, 8, 24) or not 0 < width <= 4096 or not 0 < abs(signed_height) <= 4096
            or width * abs(signed_height) > 4_194_304):
        raise ValueError('Unsupported BMP layout')
    height = abs(signed_height)
    stride = ((width * bits + 31) // 32) * 4
    if offset < 14 + header or offset + stride * height > len(data):
        raise ValueError('Truncated BMP pixels')
    palette = []
    if bits != 24:
        count = struct.unpack_from('<I', data, 46)[0] or 1 << bits
        if not 0 < count <= 1 << bits or 14 + header + count * 4 > offset:
            raise ValueError('Invalid BMP palette')
        palette = [tuple(data[p:p+3][::-1]) for p in range(14 + header, 14 + header + count * 4, 4)]
    pixels = []
    for y in range(height):
        row = offset + (y if signed_height < 0 else height - 1 - y) * stride
        for x in range(width):
            if bits == 24:
                rgb = tuple(data[row+x*3:row+x*3+3][::-1])
            else:
                index = data[row+x] if bits == 8 else (data[row+x//2] >> (4 if x % 2 == 0 else 0)) & 15
                if index >= len(palette):
                    raise ValueError('BMP palette index out of range')
                rgb = palette[index]
            pixels.append(rgb)
    return width, height, pixels


def night_color(rgb):
    return {(8, 0, 0): (255, 8, 8), (0, 8, 0): (252, 243, 148),
            (0, 0, 8): (255, 227, 99)}.get(rgb, tuple(v // 4 for v in rgb))


def surface(pixels, night=False):
    # The source keys the resulting surface AFTER the night transform. Distinct
    # colors which quarter to the transformed key consequently become transparent.
    colors = [night_color(p) for p in pixels] if night else pixels
    key = colors[0]
    return [(*p, 0 if p == key else 255) for p in colors]


def png(width, height, pixels):
    if width <= 0 or height <= 0 or len(pixels) != width * height:
        raise ValueError('Invalid PNG extent')
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))
    raw = b''.join(b'\0' + bytes(v for p in pixels[y*width:(y+1)*width] for v in p) for y in range(height))
    return (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0))
            + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))

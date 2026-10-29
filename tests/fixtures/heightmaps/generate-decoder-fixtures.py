"""Independent stdlib-only PNG fixtures. Original test patterns, no third-party art."""
from pathlib import Path
import struct,zlib,binascii
root=Path(__file__).resolve().parent

def png(name,w,h,depth,color,rows,palette=None):
    def chunk(tag,data):
        return struct.pack('>I',len(data))+tag+data+struct.pack('>I',binascii.crc32(tag+data))
    image=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,depth,color,0,0,1 if 'adam7' in name else 0))
    if palette: image+=chunk(b'PLTE',palette)
    image+=chunk(b'IDAT',zlib.compress(rows))+chunk(b'IEND',b'')
    (root/name).write_bytes(image)

def rgba(x,y):return bytes([x*17,y*17,(x+y)*8,255])
for interlace in [False,True]:
    passes=[(0,0,1,1)] if not interlace else [(0,0,8,8),(4,0,8,8),(0,4,4,8),(2,0,4,4),(0,2,2,4),(1,0,2,2),(0,1,1,2)]
    for depth in [8,16]:
        rows=b''
        for sx,sy,dx,dy in passes:
            for y in range(sy,16,dy):
                row=b''.join(rgba(x,y) if depth==8 else b''.join(bytes([v,v]) for v in rgba(x,y)) for x in range(sx,16,dx))
                rows+=b'\0'+row
        png(f'rgba{depth}{"-adam7" if interlace else ""}.png',16,16,depth,6,rows)
png('palette.png',4,1,2,3,b'\0\x1b',bytes([255,0,0,0,255,0,0,0,255,255,255,255]))
png('gray1.png',8,1,1,0,b'\0\x55')

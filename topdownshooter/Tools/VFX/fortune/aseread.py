# reads an .aseprite file's frames, every layer composited (for preview.py)
import struct, zlib, sys
def read(path):
    b=open(path,'rb').read()
    size,magic,nframes,w,h,depth,flags=struct.unpack('<IHHHHHI',b[:18])
    assert magic==0xA5E0 and size==len(b), (hex(magic),size,len(b))
    pos=128; layers=[]; frames=[]; tags=[]
    for f in range(nframes):
        fsize,fm,old,dur,_,new=struct.unpack('<IHHH2sI',b[pos:pos+16]); assert fm==0xF1FA
        n=new or old; p=pos+16; cels={}
        for c in range(n):
            cs,ct=struct.unpack('<IH',b[p:p+6]); d=b[p+6:p+cs]
            if ct==0x2004:
                nl=struct.unpack('<H',d[16:18])[0]; layers.append(d[18:18+nl].decode())
            elif ct==0x2005:
                li,x,y,op,typ=struct.unpack('<HhhBH',d[:9])
                if typ==2:
                    cw,chh=struct.unpack('<HH',d[16:20]); px=zlib.decompress(d[20:]); assert len(px)==cw*chh*4
                    cels[li]=(x,y,cw,chh,px)
                elif typ==1:
                    link=struct.unpack('<H',d[16:18])[0]; cels[li]=('link',link)
            elif ct==0x2018:
                nt=struct.unpack('<H',d[:2])[0]; q=10
                for t in range(nt):
                    fr,to=struct.unpack('<HH',d[q:q+4]); q+=17; nl=struct.unpack('<H',d[q:q+2])[0]; tags.append((d[q+2:q+2+nl].decode(),fr,to)); q+=2+nl
            p+=cs
        frames.append((dur,cels)); pos+=fsize
    # composite
    out=[]
    for fi,(dur,cels) in enumerate(frames):
        img=[0]*(w*h)
        for li in range(len(layers)):
            c=cels.get(li)
            if c and c[0]=='link': c=frames[c[1]][1].get(li)
            if not c: continue
            x,y,cw,chh,px=c
            for yy in range(chh):
                for xx in range(cw):
                    i=(yy*cw+xx)*4
                    if px[i+3]: img[(y+yy)*w+x+xx]=bytes(px[i:i+4])
        out.append((dur,img))
    return dict(w=w,h=h,layers=layers,tags=tags,frames=out,depth=depth)
if __name__=='__main__':
    a=read(sys.argv[1]); b=read(sys.argv[2])
    print(a['w'],a['h'],b['w'],b['h'],a['layers'],b['layers'],a['tags'],b['tags'],[d for d,_ in a['frames']],[d for d,_ in b['frames']])
    diff=sum(1 for (_,x),(_,y) in zip(a['frames'],b['frames']) for p,q in zip(x,y) if p!=q)
    print('pixel diffs',diff)

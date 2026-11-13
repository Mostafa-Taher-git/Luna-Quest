import struct, json, math, os
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
P = os.path.join(ROOT, 'assets', 'astronaut', 'cute_astronaut.glb')
b=open(P,'rb').read(); length=struct.unpack_from('<I',b,8)[0]
off=12; chunks=[]
while off<length:
    clen,ctype=struct.unpack_from('<II',b,off); off+=8
    chunks.append((ctype,clen,off)); off+=clen
js=json.loads(b[chunks[0][2]:chunks[0][2]+chunks[0][1]]); bin_off=chunks[1][2]
def acc(i):
    a=js['accessors'][i]; bv=js['bufferViews'][a['bufferView']]
    comp={5120:'b',5121:'B',5122:'h',5123:'H',5125:'I',5126:'f'}[a['componentType']]
    ncomp={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
    start=bin_off+bv.get('byteOffset',0)+a.get('byteOffset',0)
    stride=bv.get('byteStride', struct.calcsize(comp)*ncomp)
    out=[]
    for k in range(a['count']):
        vals=struct.unpack_from('<'+comp*ncomp, b, start+k*stride)
        out.append(vals[0] if ncomp==1 else list(vals))
    return out
def mat_identity(): return [1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]
def mat_mul(a,b):
    o=[0.0]*16
    for r in range(4):
        for c in range(4):
            o[r*4+c]=sum(a[r*4+k]*b[k*4+c] for k in range(4))
    return o
def mat_vec(m,v):
    x,y,z=v; w=1.0
    return [m[0]*x+m[1]*y+m[2]*z+m[3]*w, m[4]*x+m[5]*y+m[6]*z+m[7]*w, m[8]*x+m[9]*y+m[10]*z+m[11]*w]
def mat_dir(m,v):
    x,y,z=v
    return [m[0]*x+m[1]*y+m[2]*z, m[4]*x+m[5]*y+m[6]*z, m[8]*x+m[9]*y+m[10]*z]
def mat_inv(m):
    a=[m[r*4:(r+1)*4]+[1.0 if r==c else 0.0 for c in range(4)] for r in range(4)]
    for i in range(4):
        p=max(range(i,4), key=lambda r: abs(a[r][i]))
        if abs(a[p][i])<1e-20: raise ValueError('singular')
        a[i],a[p]=a[p],a[i]
        d=a[i][i]; a[i]=[v/d for v in a[i]]
        for r in range(4):
            if r==i: continue
            f=a[r][i]
            if f: a[r]=[a[r][c]-f*a[i][c] for c in range(8)]
    return [a[r][4+c] for r in range(4) for c in range(4)]
def from_gltf(m): return [m[c*4+r] for r in range(4) for c in range(4)]
def from_trs(t,r,s):
    x,y,z,w=r; xx,yy,zz=x*x,y*y,z*z; xy,xz,yz=x*y,x*z,y*z; wx,wy,wz=w*x,w*y,w*z
    rot=[1-2*(yy+zz),2*(xy-wz),2*(xz+wy),2*(xy+wz),1-2*(xx+zz),2*(yz-wx),2*(xz-wy),2*(yz+wx),1-2*(xx+yy)]
    m=[0.0]*16
    for row in range(3):
        for col in range(3): m[row*4+col]=rot[row*3+col]*s[col]
    m[3]=t[0]; m[7]=t[1]; m[11]=t[2]; m[15]=1.0
    return m
def norm(q):
    l=math.sqrt(sum(v*v for v in q)); return [v/l for v in q]
def slerp(a,b,t):
    d=sum(x*y for x,y in zip(a,b))
    if d<0: b=[-v for v in b]; d=-d
    if d>0.9995: return norm([a[i]+(b[i]-a[i])*t for i in range(4)])
    th=math.acos(max(-1,min(1,d))); st=math.sin(th)
    wa=math.sin((1-t)*th)/st; wb=math.sin(t*th)/st
    return [a[i]*wa+b[i]*wb for i in range(4)]
nodes=js['nodes']; parents={}
for i,n in enumerate(nodes):
    for c in n.get('children',[]): parents[c]=i
local={}
for i,n in enumerate(nodes):
    if 'matrix' in n: local[i]=from_gltf(n['matrix'])
    else: local[i]=from_trs(n.get('translation',[0,0,0]), n.get('rotation',[0,0,0,1]), n.get('scale',[1,1,1]))
def world(i,L=None):
    if L is None: L=local
    m=L[i]; j=i
    while j in parents:
        j=parents[j]; m=mat_mul(L[j],m)
    return m
anim=js['animations'][0]
def anim_value(ch,t):
    s=anim['samplers'][ch['sampler']]; times=acc(s['input']); vals=acc(s['output'])
    if t<=times[0]: return vals[0]
    if t>=times[-1]: return vals[-1]
    lo,hi=0,len(times)-1
    while hi-lo>1:
        mid=(lo+hi)//2
        if times[mid]<=t: lo=mid
        else: hi=mid
    a=vals[lo]; bb=vals[hi]; dt=times[hi]-times[lo]; f=(t-times[lo])/dt if dt else 0
    if ch['target']['path']=='rotation': return slerp(a,bb,f)
    return [a[i]+(bb[i]-a[i])*f for i in range(len(a))]
def local_at(t):
    L={i:list(local[i]) for i in range(len(nodes))}
    for ch in anim['channels']:
        n=nodes[ch['target']['node']]; path=ch['target']['path']; val=anim_value(ch,t)
        L[ch['target']['node']]=from_trs(val if path=='translation' else n.get('translation',[0,0,0]),
                                         val if path=='rotation' else n.get('rotation',[0,0,0,1]),
                                         val if path=='scale' else n.get('scale',[1,1,1]))
    return L
skin=js['skins'][0]; joints=skin['joints']; ibms=acc(skin['inverseBindMatrices'])
def skinned(t):
    L=local_at(t); W=lambda i: world(i,L)
    inv=mat_inv(W(9)); sk=[]
    for ji,jn in enumerate(joints):
        ibm=from_gltf(ibms[ji]); sk.append(mat_mul(mat_mul(inv,W(jn)),ibm))
    out={}
    for mi,mesh in enumerate(js['meshes']):
        pr=mesh['primitives'][0]; pos=acc(pr['attributes']['POSITION']); J=acc(pr['attributes']['JOINTS_0']); Wt=acc(pr['attributes']['WEIGHTS_0'])
        idx=[v[0] if isinstance(v,list) else v for v in acc(pr['indices'])]
        sp=[]
        for vi in range(len(pos)):
            a=[0.0,0.0,0.0]; ws=0.0
            for k in range(4):
                w=Wt[vi][k]
                if w<=0: continue
                p=mat_vec(sk[J[vi][k]],pos[vi])
                a=[a[0]+p[0]*w,a[1]+p[1]*w,a[2]+p[2]*w]; ws+=w
            if ws>1e-9: a=[v/ws for v in a]
            sp.append(a)
        out[mesh['name']]=(sp,idx)
    return out,W
if __name__=='__main__':
    for tnode in ['Object_9','Object_10']:
        pass
    for t in [0.0,0.5,1.0,2.0]:
        out,W=skinned(t); G=W(9)
        print('t',t)
        for name,(sp,idx) in out.items():
            wpos=[mat_vec(G,p) for p in sp]
            for ax in range(3):
                print(' ',name,'xyz'[ax],round(min(p[ax] for p in wpos),3),round(max(p[ax] for p in wpos),3),end='; ')
            print()
        # visor center dir
        sp,_=out['Astronaut_visier_0']; G=W(9)
        wv=[mat_vec(G,p) for p in sp]
        c=[sum(p[i] for p in wv)/len(wv) for i in range(3)]
        print('  visor center world',[round(v,3) for v in c])
        # mesh node 9 global basis
        print('  G basis x',[round(G[i],3) for i in range(4)],'y',[round(G[4+i],3) for i in range(4)],'z',[round(G[8+i],3) for i in range(4)],'t',[round(G[12+i],3) for i in range(4)])

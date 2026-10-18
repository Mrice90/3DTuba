import hashlib,json,pathlib
p=pathlib.Path(__file__).resolve().parent
files=[f for root in ('source','runner') for f in (p/root).rglob('*') if f.is_file() and not {'bin','obj'}.intersection(f.relative_to(p).parts)]
rows=[dict(path=f.relative_to(p).as_posix(),sha256=hashlib.sha256(f.read_bytes()).hexdigest()) for f in sorted(files)]
(p/'native-source-manifest.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
print('Pinned native source files:',len(rows))

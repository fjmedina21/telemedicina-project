"""Export source-only milestone patches against HEAD without touching the index or publishing."""
import difflib
import hashlib
import json
from pathlib import Path
import stat
import subprocess

ROOT=Path(__file__).resolve().parents[3]
SCOPES=["api/src","app/src","portalweb/src",".github/workflows/offline-tests.yml",".gitignore","README.md"]
SENSITIVE={"ConnectionStrings__Telemedicina","Mail__Password","Mail__Username","Mail__Sender",
    "Mail__PayloadKey","Milestone__MailboxKey","Milestone__AdmissionPassword","Milestone__AdmissionTotpSecret"}

def git(*args):
    result=subprocess.run(["git",*args],cwd=ROOT,capture_output=True,check=True)
    return result.stdout

def component(name):
    if name.startswith("app/src/"):return "app"
    if name.startswith("portalweb/src/"):return "portalweb"
    return "api"

def text_at(path):
    if not path.exists():return ""
    if path.is_symlink():raise RuntimeError("Source symlink cannot be exported: "+str(path.relative_to(ROOT)))
    try:return path.read_bytes().decode("utf-8")
    except UnicodeDecodeError:return None

def main():
    base=git("rev-parse","HEAD").decode().strip()
    names=set()
    for flags in [("--cached",),("--others","--exclude-standard")]:
        raw=git("ls-files",*flags,"-z","--",*SCOPES)
        names.update(value.decode() for value in raw.split(b"\0") if value)
    tracked={}
    for entry in git("ls-tree","-r","-z","HEAD","--",*SCOPES).split(b"\0"):
        if not entry:continue
        metadata,name=entry.split(b"\t",1)
        mode,kind,sha=metadata.decode().split()
        if kind=="blob":
            name=name.decode();tracked[name]=(mode,sha);names.add(name)
    private=[]
    env=ROOT/"api/src/.env"
    if env.exists():
        for line in env.read_text().splitlines():
            key,separator,value=line.partition("=")
            if separator and key in SENSITIVE and len(value.strip())>=8:private.append(value.strip())
    patches={name:[] for name in ["api","app","portalweb"]}
    manifests={name:{"baseCommit":base,"scope":"UTF-8 source, configuration and tests; existing binary assets omitted",
        "files":[],"omittedBinaryFiles":[]} for name in patches}
    for name in sorted(names):
        p=ROOT/name
        if p.name.startswith(".env") and p.name!=".env.example":continue
        if p.suffix.lower() in {".pem",".key",".pfx",".p12",".keystore",".jks"}:continue
        group=component(name)
        new=text_at(p)
        try:old=git("show","HEAD:"+name).decode("utf-8") if name in tracked else ""
        except UnicodeDecodeError:old=None
        if new is None or old is None:
            manifests[group]["omittedBinaryFiles"].append(name);continue
        if old==new:continue
        if any(value in old or value in new for value in private):
            raise RuntimeError("Private value detected; export refused: "+name)
        if "AVNS_" in old or "AVNS_" in new:
            raise RuntimeError("Credential pattern detected; export refused: "+name)
        pieces=["diff --git a/"+name+" b/"+name+"\n"]
        if name not in tracked:
            mode="100755" if p.stat().st_mode&stat.S_IXUSR else "100644"
            pieces.append("new file mode "+mode+"\n")
        elif not p.exists():pieces.append("deleted file mode "+tracked[name][0]+"\n")
        lines=difflib.unified_diff(old.splitlines(keepends=True),new.splitlines(keepends=True),
            fromfile="a/"+name if name in tracked else "/dev/null",tofile="b/"+name if p.exists() else "/dev/null")
        for line in lines:
            if not line.endswith("\n"):pieces.extend([line+"\n","\\ No newline at end of file\n"])
            else:pieces.append(line)
        patches[group].extend(pieces)
        manifests[group]["files"].append({"path":name,"sha256":hashlib.sha256(new.encode()).hexdigest(),
            "change":"added" if name not in tracked else "deleted" if not p.exists() else "modified"})
    for group,parts in patches.items():
        patch="".join(parts)
        result=subprocess.run(["git","apply","--reverse","--check","-"],cwd=ROOT,input=patch.encode(),capture_output=True)
        if result.returncode:raise RuntimeError("Reverse patch check failed for "+group+": "+result.stderr.decode())
        folder=ROOT/"hitos"/group/("primer-hito-"+group)/"parches"
        folder.mkdir(parents=True,exist_ok=True)
        (folder/"implementacion-final.patch").write_text(patch)
        manifests[group]["reverseApplyCheck"]="PASS"
        manifests[group]["privateConfigurationIncluded"]=False
        (folder/"manifest.json").write_text(json.dumps(manifests[group],indent=2)+"\n")
        print(group+": "+str(len(manifests[group]["files"]))+" source changes; reverse check PASS; no private configuration.")

if __name__=="__main__":
    main()

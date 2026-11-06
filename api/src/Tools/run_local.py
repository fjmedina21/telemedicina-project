"""Load private local configuration without shell expansion or printing values."""
import argparse
import os
from pathlib import Path
import subprocess

ROOT=Path(__file__).resolve().parent.parent
ALLOWED_PREFIXES=("ConnectionStrings__","Milestone__","Mail__","DemoReset__")
ALLOWED_NAMES={"ASPNETCORE_ENVIRONMENT","ASPNETCORE_URLS","DOTNET_ROLL_FORWARD"}

def environment():
    env=os.environ.copy()
    local=ROOT/".env"
    if local.exists():
        for line in local.read_text().splitlines():
            if not line.strip() or line.lstrip().startswith("#"):
                continue
            key,separator,value=line.partition("=")
            key=key.strip()
            if not separator or not (key.startswith(ALLOWED_PREFIXES) or key in ALLOWED_NAMES):
                raise RuntimeError("Unsupported local configuration key.")
            env.setdefault(key,value.strip())
    return env

if __name__=="__main__":
    parser=argparse.ArgumentParser()
    parser.add_argument("--configuration",choices=["Debug","Release"],default="Debug")
    parser.add_argument("--no-build",action="store_true")
    parser.add_argument("arguments",nargs=argparse.REMAINDER)
    args=parser.parse_args()
    command=["dotnet","run","--no-launch-profile","--project",str(ROOT/"Telemedicina.Api/Telemedicina.Api.csproj"),
        "--configuration",args.configuration]
    if args.no_build:
        command.append("--no-build")
    command+=["--"]+(args.arguments[1:] if args.arguments[:1]==["--"] else args.arguments)
    raise SystemExit(subprocess.call(command,env=environment()))

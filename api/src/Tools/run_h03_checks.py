"""Run H03 checks with private config; synthetic data and development mail only."""
from pathlib import Path
import subprocess
from run_local import environment

ROOT=Path(__file__).resolve().parents[3]
if __name__ == "__main__":
    env=environment()
    if env.get("ASPNETCORE_ENVIRONMENT") != "Development":
        raise SystemExit("H03_REQUIRES_DEVELOPMENT")
    env["Mail__Mode"]="development"
    env["MILESTONE_TEST_SYNTHETIC"]="1"
    command=["dotnet","run","--no-build","--configuration","Release","--project",
        str(ROOT/"api/src/Telemedicina.Tests/Telemedicina.Tests.csproj"),"--","--h03-db-tests"]
    raise SystemExit(subprocess.call(command,cwd=ROOT,env=env))

"""Validate the proposed UI in isolated copies. Never applies it to the working sources."""
import base64
import concurrent.futures
import json
import os
from pathlib import Path
import secrets
import shutil
import signal
import subprocess
import tempfile
import time
import urllib.request

ROOT=Path(__file__).resolve().parents[5]
ART=Path(__file__).resolve().parent

def run(command,cwd,env=None):
    print('CHECK: '+ ' '.join(command[:3]),flush=True)
    subprocess.run(command,cwd=cwd,env=env,check=True)

def copy_project(component,dest):
    source=ROOT/component/'src'
    names=['lib','test','web','assets','pubspec.yaml','pubspec.lock','analysis_options.yaml'] if component=='app' else ['src','public','tests','package.json','package-lock.json','tsconfig.json','next-env.d.ts','next.config.ts','tailwind.config.ts','postcss.config.mjs','playwright.config.ts']
    for name in names:
        original=source/name
        if not original.exists():continue
        target=dest/name
        if original.is_dir():shutil.copytree(original,target)
        else:shutil.copy2(original,target)
    changes=json.loads((source/'Tools/proposals/minimal-brand/changes.json').read_text())
    prefix=component+'/src/'
    for name,text in changes.items():
        target=dest/name.removeprefix(prefix)
        target.parent.mkdir(parents=True,exist_ok=True)
        target.write_text(text)
    if component=='portalweb':
        (dest/'node_modules').symlink_to(source/'node_modules',target_is_directory=True)
        p=dest/'playwright.config.ts'
        p.write_text(p.read_text().replace('path.resolve(__dirname,"../../hitos/portalweb/primer-hito-portalweb/evidencias/playwright")',json.dumps(str(ART/'browser-results'))))

def ready(proc,url):
    for _ in range(120):
        if proc.poll() is not None:raise RuntimeError('Preview service stopped before readiness')
        try:
            with urllib.request.urlopen(url,timeout=2) as response:
                if response.status==200:return
        except OSError:pass
        time.sleep(.25)
    raise RuntimeError('Preview readiness timed out')

def stop(proc):
    if proc is None:return
    if proc.poll() is not None:return
    os.killpg(proc.pid,signal.SIGTERM)
    try:proc.wait(timeout=10)
    except subprocess.TimeoutExpired:os.killpg(proc.pid,signal.SIGKILL);proc.wait()

def app_checks(dest):
    run(['flutter','pub','get','--offline'],dest)
    run(['dart','format','lib','test'],dest)
    changes_path=ROOT/'app/src/Tools/proposals/minimal-brand/changes.json'
    changes=json.loads(changes_path.read_text())
    for name in changes:
        changes[name]=(dest/name.removeprefix('app/src/')).read_text()
    changes_path.write_text(json.dumps(changes,ensure_ascii=False,indent=2))
    run(['flutter','analyze'],dest)
    run(['flutter','test'],dest)
    run(['flutter','build','web','--no-web-resources-cdn'],dest)

def web_checks(dest):
    env=os.environ.copy();env['API_INTERNAL_URL']='http://127.0.0.1:5091/api/v1'
    run(['npm','run','build'],dest,env)

def browser_checks(web,app):
    env=os.environ.copy()
    for key in list(env):
        if key.startswith(('Mail__','Milestone__','ConnectionStrings__')):del env[key]
    name='telemedicina-design-test-'+secrets.token_hex(4)
    password=secrets.token_urlsafe(24)
    api=portal=app_server=None
    try:
        subprocess.run(['docker','run','--rm','-d','--name',name,'-e','POSTGRES_USER=synthetic_test','-e','POSTGRES_DB=telemedicina_test','-e','POSTGRES_PASSWORD='+password,'-p','127.0.0.1::5432','postgres:17'],check=True,stdout=subprocess.DEVNULL)
        for _ in range(100):
            if subprocess.run(['docker','exec',name,'pg_isready','-U','synthetic_test'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL).returncode==0:break
            time.sleep(.25)
        else:raise RuntimeError('Temporary PostgreSQL unavailable')
        port=subprocess.check_output(['docker','port',name,'5432/tcp'],text=True).strip().rsplit(':',1)[1]
        env.update({'ConnectionStrings__Telemedicina':f'Host=127.0.0.1;Port={port};Database=telemedicina_test;Username=synthetic_test;Password={password};SSL Mode=Disable',
            'ASPNETCORE_ENVIRONMENT':'Development','Milestone__Enabled':'true','Milestone__PortalOrigin':'http://localhost:5096',
            'Milestone__MailboxKey':secrets.token_hex(32),'Milestone__AdmissionEmail':'synthetic-design@example.invalid',
            'Milestone__AdmissionPassword':secrets.token_urlsafe(32),'Milestone__AdmissionTotpSecret':base64.b32encode(secrets.token_bytes(20)).decode(),
            'Mail__Mode':'development','Mail__PayloadKey':base64.b64encode(secrets.token_bytes(32)).decode(),
            'MILESTONE_TEST_SYNTHETIC':'1','MILESTONE_PORTAL_TEST_URL':'http://localhost:5096',
            'API_INTERNAL_URL':'http://127.0.0.1:5091/api/v1'})
        base=['dotnet','run','--no-build','--no-launch-profile','--configuration','Release','--project',str(ROOT/'api/src/Telemedicina.Api/Telemedicina.Api.csproj')]
        for action in ['--migrate','--seed-synthetic']:run(base+['--',action],ROOT,env)
        api=subprocess.Popen(base+['--','--urls','http://127.0.0.1:5091'],cwd=ROOT,env=env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,start_new_session=True)
        ready(api,'http://127.0.0.1:5091/api/v1/health')
        # Rewrites are evaluated during next build; the default target remains the port below.
        portal=subprocess.Popen(['node','node_modules/next/dist/bin/next','start','-p','5096'],cwd=web,env=env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,start_new_session=True)
        ready(portal,'http://localhost:5096/login')
        run(['npm','run','test:e2e'],web,env)
        app_server=subprocess.Popen(['python3','-m','http.server','5097','--bind','127.0.0.1'],cwd=app/'build/web',stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,start_new_session=True)
        ready(app_server,'http://127.0.0.1:5097')
        run(['node',str(ART/'capture.cjs')],ROOT,env)
    finally:
        stop(app_server);stop(portal);stop(api)
        subprocess.run(['docker','rm','-f',name],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)

def main():
    with tempfile.TemporaryDirectory(prefix='review-',dir=ROOT/'app/src/Tools/proposals/minimal-brand') as a,tempfile.TemporaryDirectory(prefix='review-',dir=ART) as w:
        app=Path(a);web=Path(w)
        copy_project('app',app);copy_project('portalweb',web)
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            tasks=[pool.submit(app_checks,app),pool.submit(web_checks,web)]
            for task in tasks:task.result()
        browser_checks(web,app)
    print('PROPOSED DESIGN CHECKS: PASS. Working sources unchanged.',flush=True)

if __name__=='__main__':main()

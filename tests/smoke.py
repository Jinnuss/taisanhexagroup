"""No SQL required. Starts an isolated API process and checks every protected route."""
import os, pathlib, subprocess, secrets, time, json, urllib.request, urllib.error, re, socket
root=pathlib.Path(__file__).resolve().parents[1]
dotnet=os.environ.get('DOTNET','dotnet')
with socket.socket() as s:
    s.bind(('127.0.0.1',0)); port=s.getsockname()[1]
base=f'http://127.0.0.1:{port}'
env=dict(os.environ, Jwt__Key=secrets.token_hex(32), ASPNETCORE_ENVIRONMENT='Development',ASPNETCORE_URLS=base,ConnectionStrings__DefaultConnection='')
def request(path,method='GET',body=None,headers=None):
    req=urllib.request.Request(base+path,data=None if body is None else json.dumps(body).encode(),method=method,headers={'Content-Type':'application/json',**(headers or {})})
    try:
        with urllib.request.urlopen(req,timeout=10) as response:return response.status,response.read(),response.headers
    except urllib.error.HTTPError as e:return e.code,e.read(),e.headers
log=open(root/'tests/smoke-server.log','w')
proc=subprocess.Popen([dotnet,str(root/'server/bin/Debug/net10.0/Hexa.Api.dll')],cwd=root/'server',env=env,stdout=log,stderr=log)
try:
    for attempt in range(100):
        try:
            if request('/')[0]==200:break
        except OSError:pass
        if proc.poll() is not None:raise RuntimeError('API stopped; see smoke-server.log')
        time.sleep(.1)
    else:raise RuntimeError('API did not start')
    assert request('/swagger/index.html')[0]==200,'Swagger UI'
    status,body,_=request('/openapi/v1.json'); assert status==200,body
    spec=json.loads(body);count=0
    public={'/','/api/health','/api/auth/login'}
    for path,operations in spec['paths'].items():
        if path in public or path.startswith('/api/public/'):continue
        sample=re.sub(r'\{[^}]+\}','1',path)
        for method in operations:
            if method not in ['get','post','put','delete','patch']:continue
            status,body,_=request(sample,method.upper(),{} if method in ['post','put','patch'] else None)
            assert status==401,(method,path,status,body)
            count+=1
    assert request('/api/auth/login','POST',{})[0]==400,'Login input validation'
    assert request('/api/assets',headers={'Authorization':'Bearer invalid'})[0]==401,'Invalid JWT'
    assert request('/api/health')[0]==503,'Missing SQL configuration'
    assert request('/api/public/assets/00000000-0000-0000-0000-000000000001')[0]==503,'Public QR bypasses login'
    assert request('/api/assets','OPTIONS',headers={'Origin':'http://localhost:5173','Access-Control-Request-Method':'GET'})[2].get('Access-Control-Allow-Origin')=='http://localhost:5173','CORS'
    statuses=[request('/api/auth/login','POST',{})[0] for _ in range(11)]
    assert 429 in statuses,'Login rate limiter'
    report={'protected_routes_checked':count,'swagger':'passed','input_validation':'passed','invalid_jwt':'passed','missing_database':'503 as expected','public_qr_route':'passed without database','cors':'passed','login_rate_limit':'passed','sql_integration':'NOT RUN'}
    (root/'tests/smoke-results.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(report,indent=2))
finally:
    proc.terminate()
    try:proc.wait(timeout=10)
    except subprocess.TimeoutExpired:proc.kill();proc.wait()
    log.close()

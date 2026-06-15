#!/usr/bin/env python3
"""简单测试服务器：模拟 .NET Program.cs 的静态文件路由
- /              → frontend/index.html
- /css/*, /js/*  → frontend/css, frontend/js
- /admin         → admin/index.html
- /admin/css/*, /admin/js/* → admin/css, admin/js
- /api/*         → 返回 mock JSON
"""
import http.server, socketserver, json, os, pathlib, urllib.parse

ROOT = pathlib.Path(__file__).parent.resolve()
FRONTEND = ROOT / "frontend"
ADMIN = ROOT / "admin"

def _serve_file(handler, path: pathlib.Path):
    if not path.is_file():
        handler.send_error(404, "Not Found: " + str(path))
        return
    ext = path.suffix.lower()
    content_type = {
        ".html": "text/html; charset=utf-8",
        ".css": "text/css; charset=utf-8",
        ".js": "application/javascript; charset=utf-8",
        ".json": "application/json; charset=utf-8",
        ".png": "image/png",
        ".jpg": "image/jpeg",
        ".jpeg": "image/jpeg",
        ".svg": "image/svg+xml",
        ".ico": "image/x-icon",
    }.get(ext, "application/octet-stream")
    body = path.read_bytes()
    handler.send_response(200)
    handler.send_header("Content-Type", content_type)
    handler.send_header("Content-Length", str(len(body)))
    handler.send_header("Cache-Control", "no-cache")
    handler.send_header("Access-Control-Allow-Origin", "*")
    handler.send_header("Access-Control-Allow-Headers", "*")
    handler.send_header("Access-Control-Allow-Methods", "*")
    handler.end_headers()
    handler.wfile.write(body)

class Handler(http.server.BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):
        print("[%s] %s" % (self.log_date_time_string(), fmt % args))

    def _json(self, obj, code=200):
        body = json.dumps(obj, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Headers", "*")
        self.send_header("Access-Control-Allow-Methods", "*")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _read_body(self):
        length = int(self.headers.get("Content-Length", "0") or "0")
        if length <= 0: return ""
        return self.rfile.read(length).decode("utf-8")

    def do_OPTIONS(self):
        self.send_response(204)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Headers", "*")
        self.send_header("Access-Control-Allow-Methods", "*")
        self.end_headers()

    def do_GET(self):
        raw = urllib.parse.urlparse(self.path)
        path = raw.path.rstrip("/") or "/"
        if path.startswith("/api"):
            return self._api(path, raw)
        if path == "/admin" or path.startswith("/admin/"):
            sub = path[len("/admin"):].lstrip("/") or "index.html"
            target = ADMIN / sub
            _serve_file(self, target)
            return
        if path == "/" or path == "":
            _serve_file(self, FRONTEND / "index.html")
            return
        target = FRONTEND / path.lstrip("/")
        if target.is_file():
            _serve_file(self, target)
        else:
            _serve_file(self, FRONTEND / "index.html")

    def do_POST(self):
        raw = urllib.parse.urlparse(self.path)
        path = raw.path.rstrip("/") or "/"
        body = self._read_body()
        data = {}
        if body:
            try: data = json.loads(body)
            except: data = {}
        self._api_post(path, data)

    def _api(self, path: str, raw: urllib.parse.ParseResult):
        qs = urllib.parse.parse_qs(raw.query)
        def q(name, default=""): return (qs.get(name, [default])[0])

        if path == "/api/health":
            return self._json({"Status":"OK","Mode":"Mock","WeCom":"Disabled"})
        if path == "/api/config":
            return self._json({"Mode":"Mock","FormId":"BD_MATERIAL",
                "FieldMappings":[{"fieldKey":"materialName","label":"物料名称","kingdeeField":"FName","enabled":True}],"WeComConfigured":False})
        if path == "/api/material":
            materials = [
                {"materialId":"1","number":"P001","materialName":"25% 噻虫嗪悬浮剂","generalName":"噻虫嗪","basicUnit":"瓶","specification":"100ml/瓶","materialLevel":"一级","registrationForm":"悬浮剂","cropPlace":"水稻田","controlTarget":"蚜虫、稻飞虱","useTime":"5-9月","registrationNo":"PD20150001","productAttribute":"杀虫剂","productManager":"李经理"},
                {"materialId":"2","number":"P002","materialName":"40% 戊唑醇水乳剂","generalName":"戊唑醇","basicUnit":"瓶","specification":"500ml/瓶","materialLevel":"二级","registrationForm":"水乳剂","cropPlace":"小麦田","controlTarget":"赤霉病、白粉病","useTime":"4-7月","registrationNo":"PD20140020","productAttribute":"杀菌剂","productManager":"王经理","images":["https://picsum.photos/seed/m2/600/400","https://picsum.photos/seed/m2b/600/400"]},
                {"materialId":"3","number":"P003","materialName":"草甘膦水剂","generalName":"草甘膦","basicUnit":"桶","specification":"5L/桶","materialLevel":"三级","registrationForm":"水剂","cropPlace":"果园","controlTarget":"一年生杂草","useTime":"全年","registrationNo":"PD85155","productAttribute":"除草剂","productManager":"赵经理"}
            ]
            kw = (q("keyword") or "").strip()
            level = (q("level") or "").strip()
            if kw:
                lower = kw.lower()
                materials = [m for m in materials if any(lower in str(v).lower() for v in m.values())]
            if level:
                materials = [m for m in materials if m.get("materialLevel","") == level]
            return self._json({"success": True, "data": materials, "message": "ok"})
        if path.startswith("/api/auth"):
            return self._json({"success": True, "message": "mock-auth", "authenticated": False,
                "UserId": "dev-user", "Name": "开发模式用户", "Mode": "Dev"})
        if path.startswith("/api/admin"):
            return self._api_admin(path)
        self._json({"success":False,"message":"unknown api: "+path}, 404)

    def _api_post(self, path, data):
        if path.startswith("/api/admin"):
            return self._api_admin(path, data)
        self._json({"success": False, "message": "unknown api: " + path}, 404)

    def _api_admin(self, path, data=None):
        if data is None:
            if path == "/api/admin/config":
                return self._json({
                    "kingdee":{"enable":False,"serverUrl":"","dbId":"","userName":"","password":"","lcId":2052,"timeoutSeconds":30,"imageServerUrl":"","materialFormId":"BD_MATERIAL"},
                    "fieldMappings":[
                        {"fieldKey":"materialName","label":"物料名称","kingdeeField":"FName","enabled":True},
                        {"fieldKey":"generalName","label":"通用名","kingdeeField":"FDescription","enabled":True},
                        {"fieldKey":"basicUnit","label":"基本单位","kingdeeField":"FBaseUnitId","enabled":True},
                        {"fieldKey":"specification","label":"规格型号","kingdeeField":"FSpecification","enabled":True},
                        {"fieldKey":"materialLevel","label":"物料等级","kingdeeField":"FLevel","enabled":True},
                        {"fieldKey":"registrationForm","label":"登记剂型","kingdeeField":"FDosageForm","enabled":True},
                        {"fieldKey":"cropPlace","label":"作物场所","kingdeeField":"FCropPlace","enabled":True},
                        {"fieldKey":"controlTarget","label":"防治对象","kingdeeField":"FTarget","enabled":True},
                        {"fieldKey":"useTime","label":"大概使用时间","kingdeeField":"FUsePeriod","enabled":True},
                        {"fieldKey":"registrationNo","label":"登记证号","kingdeeField":"FRegNo","enabled":True},
                        {"fieldKey":"productAttribute","label":"产品属性","kingdeeField":"FProductAttr","enabled":True},
                        {"fieldKey":"cropAttribute","label":"作物属性","kingdeeField":"FCropAttr","enabled":True},
                        {"fieldKey":"productManager","label":"产品经理","kingdeeField":"FProductManager","enabled":True},
                        {"fieldKey":"productInfo","label":"产品信息","kingdeeField":"FProductInfo","enabled":True},
                        {"fieldKey":"images","label":"图片字段","kingdeeField":"FImage","enabled":True}
                    ],
                    "wecom":{"enable":False,"corpId":"","agentId":"","secret":"","callbackUrl":"","jwtSecret":"","jwtExpireHours":24,"forceLogin":True},
                    "adminPassword":""
                })
            return self._json({"success":False,"message":"unknown: "+path}, 404)
        if path == "/api/admin/auth/login":
            pwd = data.get("password") or ""
            if pwd == "admin123":
                return self._json({"success": True, "token": "dev-admin-token-ok", "expireHours": 12})
            return self._json({"success":False,"message":"密码错误"}, 401)
        return self._json({"success": True, "message": "（开发模式）已保存"})

if __name__ == "__main__":
    PORT = 8765
    print(f"\n=== 测试服务器启动 ===")
    print(f"  移动端:  http://localhost:{PORT}/")
    print(f"  管理后台: http://localhost:{PORT}/admin")
    print(f"  API测试: http://localhost:{PORT}/api/health")
    print(f"  管理后台登录密码: admin123")
    print()
    try:
        with socketserver.ThreadingTCPServer(("", PORT), Handler) as httpd:
            httpd.serve_forever()
    except OSError as e:
        print(f"端口 {PORT} 被占用: {e}")
        print("尝试其它端口...")

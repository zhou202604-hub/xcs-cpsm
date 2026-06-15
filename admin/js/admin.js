/* ============================================
 *  管理后台前端脚本
 *  - 登录（/api/admin/auth/login）
 *  - 三个 Tab 的保存 & 测试
 *  - 请求自动携带 JWT
 * ============================================ */

const ADMIN_API_BASE = '/api/admin';

/* ---------- 工具 ---------- */
function httpGet(path) {
    return fetch(ADMIN_API_BASE + path, { headers: authHeaders() })
        .then(r => r.json());
}
function httpPost(path, body) {
    return fetch(ADMIN_API_BASE + path, {
        method: 'POST',
        headers: Object.assign({ 'Content-Type': 'application/json' }, authHeaders()),
        body: JSON.stringify(body)
    }).then(r => r.json());
}
function authHeaders() {
    const t = localStorage.getItem('admin_token');
    return t ? { 'Authorization': 'Bearer ' + t } : {};
}
function setMsg(elId, text, type) {
    const el = document.getElementById(elId);
    if (!el) return;
    el.className = 'form-msg ' + (type || '');
    el.textContent = text || '';
}

/* ---------- 登录 / 退出 ---------- */
function doLogin() {
    const pwd = document.getElementById('adminPwd').value.trim();
    if (!pwd) { setMsg('loginTip', '请输入密码', 'error'); return; }
    fetch(ADMIN_API_BASE + '/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ password: pwd })
    }).then(r => r.json()).then(data => {
        if (data && data.token) {
            localStorage.setItem('admin_token', data.token);
            document.getElementById('loginScreen').style.display = 'none';
            document.getElementById('mainScreen').style.display = 'flex';
            loadConfig();
        } else {
            setMsg('loginTip', (data && (data.msg || '密码错误')), 'error';
        }
    }).catch(() => setMsg('loginTip', '网络异常，请检查后端服务是否运行', 'error'));
}
function logout() {
    localStorage.removeItem('admin_token');
    window.location.reload();
}

/* ---------- Tab 切换 ---------- */
function switchTab(el, tab) {
    document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));
    document.querySelectorAll('.tab-panel').forEach(n => n.classList.remove('active'));
    el.classList.add('active');
    document.getElementById('tab-' + tab).classList.add('active');
}

/* ---------- 加载当前配置 ---------- */
function loadConfig() {
    httpGet('/config').then(cfg => {
        if (!cfg) return;
        // 金蝶
        document.getElementById('kd_enable').checked = !!cfg.kingdee?.enable;
        document.getElementById('kd_server').value = cfg.kingdee?.serverUrl || '';
        document.getElementById('kd_dbId').value = cfg.kingdee?.dbId || '';
        document.getElementById('kd_user').value = cfg.kingdee?.userName || '';
        document.getElementById('kd_pwd').value = cfg.kingdee?.password ? '******' : '';
        document.getElementById('kd_lcid').value = cfg.kingdee?.lcId || 2052;
        document.getElementById('kd_imageServer').value = cfg.kingdee?.imageServerUrl || '';
        document.getElementById('kd_formId').value = cfg.kingdee?.materialFormId || 'BD_MATERIAL';

        // 字段映射
        const rows = cfg.fieldMappings || [];
        const tableBody = document.getElementById('fieldTable');
        // 清空现有行
        tableBody.querySelectorAll('.field-row:not(.field-head)').forEach(n => n.remove());
        rows.forEach(row => addFieldRow(row));

        // 企业微信
        document.getElementById('wx_enable').checked = !!cfg.wecom?.enable;
        document.getElementById('wx_corpId').value = cfg.wecom?.corpId || '';
        document.getElementById('wx_agentId').value = cfg.wecom?.agentId || '';
        document.getElementById('wx_secret').value = cfg.wecom?.secret ? '******' : '';
        document.getElementById('wx_callback').value = cfg.wecom?.callbackUrl || '';
        document.getElementById('wx_jwtSecret').value = cfg.wecom?.jwtSecret ? '******' : '';
        document.getElementById('wx_expireHours').value = cfg.wecom?.jwtExpireHours || 24;
    });
}

/* ---------- 字段映射表格 ---------- */
function addFieldRow(preset) {
    const row = document.createElement('div');
    row.className = 'field-row';
    const p = preset || { fieldKey: '', label: '', kingdeeField: '', enabled: true, showInList: false, order: 0 };
    row.innerHTML = `
        <div style="flex:0 0 60px;">
            <input type="checkbox" ${p.enabled ? 'checked' : ''}>
        </div>
        <div style="flex:0 0 220px;">
            <input type="text" placeholder="例：materialName" value="${escapeAttr(p.fieldKey || '')}">
        </div>
        <div style="flex:0 0 220px;">
            <input type="text" placeholder="例：物料名称" value="${escapeAttr(p.label || '')}">
        </div>
        <div style="flex:1 1 auto;">
            <input type="text" placeholder="例：FName 或 FImg,FAttach" value="${escapeAttr(p.kingdeeField || '')}">
        </div>
        <div style="flex:0 0 80px;text-align:right;">
            <button class="link-btn del-btn" onclick="this.closest('.field-row').remove()">删除</button>
        </div>
    `;
    document.getElementById('fieldTable').appendChild(row);
}
function resetDefaults() {
    if (!confirm('确认重置为默认字段映射？')) return;
    // 请求后端返回的默认值
    httpGet('/config').then(cfg => {
        const rows = [
            { fieldKey: 'materialName',     label: '物料名称',   kingdeeField: 'FName', enabled: true },
            { fieldKey: 'generalName',      label: '通用名',    kingdeeField: 'FDescription', enabled: true },
            { fieldKey: 'basicUnit',        label: '基本单位',   kingdeeField: 'FBaseUnitId', enabled: true },
            { fieldKey: 'specification',    label: '规格型号',   kingdeeField: 'FSpecification', enabled: true },
            { fieldKey: 'materialLevel',    label: '物料等级',   kingdeeField: 'FLevel', enabled: true },
            { fieldKey: 'registrationForm', label: '登记剂型',    kingdeeField: 'FDosageForm', enabled: true },
            { fieldKey: 'cropPlace',        label: '作物场所',   kingdeeField: 'FCropPlace', enabled: true },
            { fieldKey: 'controlTarget',    label: '防治对象',   kingdeeField: 'FTarget', enabled: true },
            { fieldKey: 'useTime',         label: '大概使用时间', kingdeeField: 'FUsePeriod', enabled: true },
            { fieldKey: 'registrationNo',   label: '登记证号',   kingdeeField: 'FRegNo', enabled: true },
            { fieldKey: 'productAttribute', label: '产品属性',   kingdeeField: 'FProductAttr', enabled: true },
            { fieldKey: 'cropAttribute',   label: '作物属性', kingdeeField: 'FCropAttr', enabled: true },
            { fieldKey: 'productManager',   label: '产品经理', kingdeeField: 'FProductManager', enabled: true },
            { fieldKey: 'productInfo',      label: '产品信息',   kingdeeField: 'FProductInfo', enabled: true },
            { fieldKey: 'images',             label: '图片字段',  kingdeeField: 'FImage', enabled: true }
        ];
        const tableBody = document.getElementById('fieldTable');
        tableBody.querySelectorAll('.field-row:not(.field-head)').forEach(n => n.remove());
        rows.forEach(r => addFieldRow(r));
    });
}

/* ---------- 保存金蝶 ---------- */
function saveKingdee() {
    const payload = {
        enable: document.getElementById('kd_enable').checked,
        serverUrl: document.getElementById('kd_server').value.trim(),
        dbId: document.getElementById('kd_dbId').value.trim(),
        userName: document.getElementById('kd_user').value.trim(),
        password: document.getElementById('kd_pwd').value,
        lcId: parseInt(document.getElementById('kd_lcid').value) || 2052,
        timeoutSeconds: 30,
        imageServerUrl: document.getElementById('kd_imageServer').value.trim(),
        materialFormId: document.getElementById('kd_formId').value.trim() || 'BD_MATERIAL'
    };
    httpPost('/config/kingdee', payload).then(data => {
        if (data && data.success) {
            setMsg('kingdeeMsg', '金蝶配置已保存 ✅', 'success');
        } else {
            setMsg('kingdeeMsg', (data && data.msg) || '保存失败', 'error');
        }
    }).catch(e => setMsg('kingdeeMsg', '请求异常: ' + e, 'error'));
}
function testKingdee() {
    setMsg('kingdeeMsg', '正在测试，请稍候…', 'info');
    const payload = {
        serverUrl: document.getElementById('kd_server').value.trim(),
        dbId: document.getElementById('kd_dbId').value.trim(),
        userName: document.getElementById('kd_user').value.trim(),
        password: document.getElementById('kd_pwd').value,
        lcId: parseInt(document.getElementById('kd_lcid').value) || 2052,
        timeoutSeconds: 30,
        imageServerUrl: document.getElementById('kd_imageServer').value.trim(),
        materialFormId: document.getElementById('kd_formId').value.trim() || 'BD_MATERIAL'
    };
    httpPost('/test/kingdee', payload).then(data => {
        if (data && data.success) {
            setMsg('kingdeeMsg', '✅ 测试成功，金蝶登录成功', 'success');
        } else {
            setMsg('kingdeeMsg', '❌ 失败：' + ((data && data.msg) || '未知错误', 'error');
        }
    }).catch(e => setMsg('kingdeeMsg', '请求异常: ' + e, 'error'));
}

/* ---------- 保存字段映射 ---------- */
function saveFieldMappings() {
    const rows = document.querySelectorAll('#fieldTable .field-row:not(.field-head)');
    const list = [];
    let order = 0;
    rows.forEach(row => {
        const inputs = row.querySelectorAll('input[type="text"]');
        const cb = row.querySelector('input[type="checkbox"]');
        const fieldKey = inputs[0]?.value.trim();
        const label = inputs[1]?.value.trim();
        const kingdeeField = inputs[2]?.value.trim();
        if (!fieldKey) return;
        list.push({ fieldKey, label, kingdeeField, enabled: cb?.checked });
    });
    if (list.length === 0) {
        setMsg('fieldsMsg', '请至少配置一条字段映射', 'error');
        return;
    }
    httpPost('/config/field-mappings', list).then(data => {
        if (data && data.success) setMsg('fieldsMsg', '字段映射已保存 ✅', 'success');
        else setMsg('fieldsMsg', (data && data.msg) || '保存失败', 'error');
    }).catch(e => setMsg('fieldsMsg', '请求异常: ' + e, 'error'));
}

/* ---------- 保存企业微信 ---------- */
function saveWeCom() {
    const payload = {
        enable: document.getElementById('wx_enable').checked,
        corpId: document.getElementById('wx_corpId').value.trim(),
        agentId: document.getElementById('wx_agentId').value.trim(),
        secret: document.getElementById('wx_secret').value,
        callbackUrl: document.getElementById('wx_callback').value.trim(),
        jwtSecret: document.getElementById('wx_jwtSecret').value,
        jwtExpireHours: parseInt(document.getElementById('wx_expireHours').value) || 24,
        forceLogin: true
    };
    httpPost('/config/wecom', payload).then(data => {
        if (data && data.success) setMsg('wecomMsg', '企业微信配置已保存 ✅', 'success');
        else setMsg('wecomMsg', (data && data.msg) || '保存失败', 'error');
    }).catch(e => setMsg('wecomMsg', '请求异常: ' + e, 'error'));
}
function testWeCom() {
    setMsg('wecomMsg', '正在测试，请稍候…', 'info');
    const payload = {
        corpId: document.getElementById('wx_corpId').value.trim(),
        agentId: document.getElementById('wx_agentId').value.trim(),
        secret: document.getElementById('wx_secret').value,
        callbackUrl: document.getElementById('wx_callback').value.trim(),
        jwtSecret: document.getElementById('wx_jwtSecret').value,
        jwtExpireHours: parseInt(document.getElementById('wx_expireHours').value) || 24,
        enable: document.getElementById('wx_enable').checked,
        forceLogin: true
    };
    httpPost('/test/wecom', payload).then(data => {
        if (data && data.success) setMsg('wecomMsg', '✅ 测试成功，企业微信 API 可用', 'success');
        else setMsg('wecomMsg', '❌ 失败：' + ((data && data.msg) || '未知错误'), 'error');
    }).catch(e => setMsg('wecomMsg', '请求异常: ' + e, 'error'));
}

/* ---------- 改管理员密码 ---------- */
function changeAdminPwd() {
    const p1 = document.getElementById('sec_newPwd').value;
    const p2 = document.getElementById('sec_newPwd2').value;
    if (!p1 || p1.length < 6) { setMsg('secMsg', '密码至少 6 位', 'error'); return; }
    if (p1 !== p2) { setMsg('secMsg', '两次输入不一致', 'error'); return; }
    httpPost('/config/admin-password', { newPassword: p1 }).then(data => {
        if (data && data.success) {
            setMsg('secMsg', '✅ 管理员密码已修改，请重新登录', 'success');
            setTimeout(() => { localStorage.removeItem('admin_token'); window.location.reload(); }, 1200);
        } else setMsg('secMsg', (data && data.msg) || '保存失败', 'error');
    }).catch(e => setMsg('secMsg', '请求异常: ' + e, 'error'));
}

/* ---------- 启动 ---------- */
document.addEventListener('DOMContentLoaded', () => {
    // 回车登录
    document.getElementById('adminPwd').addEventListener('keydown', (e) => {
        if (e.key === 'Enter') doLogin();
    });

    // 有 token 跳过登录
    const t = localStorage.getItem('admin_token');
    if (t && t.length > 20) {
        httpGet('/config').then(cfg => {
            if (cfg && cfg.kingdee) {
                document.getElementById('loginScreen').style.display = 'none';
                document.getElementById('mainScreen').style.display = 'flex';
                loadConfig();
            }
        }).catch(() => { });
    }
});

/* ------ 帮助方法：转义属性 ------ */
function escapeAttr(s) {
    if (!s) return '';
    return String(s).replace(/"/g, '&quot;');
}

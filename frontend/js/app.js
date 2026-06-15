// ============================================================
// 物料查询（前端）：
//  - 优先请求后端 /api/material（失败回退到 data.js 的 mock）
//  - 企业微信登录后写 wc_token，所有请求自动带上 Authorization
//  - 扫码按钮：调用企业微信 JS-SDK scanQRCode，扫到的内容直接作为关键词搜索
// ============================================================

// 兼容两种相对路径：直接打开文件 / 托管在后端根路径
const API_BASE = (location.protocol === 'file:' || location.hostname === '')
    ? 'http://localhost:5000/api'
    : '/api';

const USE_MOCK_FALLBACK = true;

let allMaterials = [];
let filteredMaterials = [];
let currentKeyword = '';
let currentLevelFilter = '';
let searchTimer = null;

// ---------------- 启动 ----------------
document.addEventListener('DOMContentLoaded', async () => {
    // ① 先看有没有本地 token；没有就去后端触发企业微信登录
    const token = localStorage.getItem('wc_token');
    const name = localStorage.getItem('wc_name') || '';
    if (name) {
        document.getElementById('userName').textContent = name;
        document.getElementById('userInfo').style.display = 'flex';
    }

    try {
        const cfgRes = await apiFetch('/config', { silent: true });
        if (cfgRes && cfgRes.weComConfigured && !token) {
            // 需要企业微信登录：跳转 /api/auth（后端会直接跳企业微信授权页）
            window.location.replace('/api/auth');
            return;
        }
    } catch (e) {
        // 后端不可用，继续使用 mock
    }

    // ② 非企业微信环境或已登录，尝试 wx.config（用于扫码）
    try {
        if (window.wx && typeof wx.config === 'function' && location.protocol !== 'file:') {
            const signRes = await apiFetch('/auth/jssdk?url=' + encodeURIComponent(location.href.split('#')[0]));
            if (signRes && signRes.appId) {
                wx.config({
                    beta: true,
                    debug: false,
                    appId: signRes.appId,
                    timestamp: signRes.timestamp,
                    nonceStr: signRes.nonceStr,
                    signature: signRes.signature,
                    jsApiList: ['scanQRCode']
                });
                wx.ready(() => { /* JS-SDK ready */ });
                wx.error(err => {
                    console.warn('wx.config 失败（仅影响扫码功能）:', err);
                });
            }
        }
    } catch (e) {
        console.warn('JS-SDK 未配置：扫码按钮将仅作为普通搜索入口');
    }

    // ③ 加载物料列表
    loadMaterials();
});

// ---------------- 网络请求 ----------------
async function apiFetch(path, opts = {}) {
    const token = localStorage.getItem('wc_token') || '';
    const headers = Object.assign(
        { 'Content-Type': 'application/json' },
        token ? { 'Authorization': 'Bearer ' + token } : {}
    );

    try {
        const resp = await fetch(API_BASE + path, {
            method: 'GET',
            headers,
            cache: 'no-cache'
        });
        if (!resp.ok) {
            throw new Error('HTTP ' + resp.status);
        }
        return await resp.json();
    } catch (err) {
        if (opts.silent) return null;
        throw err;
    }
}

async function fetchMaterialsFromApi(keyword, level) {
    const url = '/material?keyword=' + encodeURIComponent(keyword || '')
        + '&level=' + encodeURIComponent(level || '')
        + '&pageIndex=1&pageSize=50';
    const data = await apiFetch(url);
    if (data && data.data && Array.isArray(data.data)) {
        return data.data;
    }
    if (Array.isArray(data)) return data;
    return null;
}

// ---------------- 列表加载 ----------------
async function loadMaterials() {
    showLoading();
    try {
        const materials = await fetchMaterialsFromApi(currentKeyword, currentLevelFilter);
        if (materials && materials.length > 0) {
            allMaterials = materials;
            filteredMaterials = [...allMaterials];
        } else if (USE_MOCK_FALLBACK) {
            allMaterials = [...window.mockMaterials || []];
            filteredMaterials = [...allMaterials];
            if (currentKeyword) applyFilters();
        } else {
            allMaterials = [];
            filteredMaterials = [];
        }
        renderMaterialList();
    } catch (err) {
        console.warn('API调用失败，使用Mock数据:', err.message);
        if (USE_MOCK_FALLBACK) {
            allMaterials = [...(window.mockMaterials || [])];
            filteredMaterials = [...allMaterials];
            if (currentKeyword) applyFilters();
            renderMaterialList();
        } else {
            showError(err.message);
        }
    }
}

// ---------------- UI 状态控制 ----------------
function showLoading() {
    document.getElementById('loadingState').style.display = 'flex';
    document.getElementById('materialList').style.display = 'none';
    document.getElementById('emptyState').style.display = 'none';
    document.getElementById('errorState').style.display = 'none';
}

function showError(msg) {
    document.getElementById('errorState').style.display = 'flex';
    document.getElementById('errorMessage').textContent = msg || '请求失败';
    document.getElementById('materialList').style.display = 'none';
    document.getElementById('emptyState').style.display = 'none';
    document.getElementById('loadingState').style.display = 'none';
}

// ---------------- 渲染列表 ----------------
function renderMaterialList() {
    const listEl = document.getElementById('materialList');
    const emptyEl = document.getElementById('emptyState');
    const loadingEl = document.getElementById('loadingState');
    const errorEl = document.getElementById('errorState');

    loadingEl.style.display = 'none';
    errorEl.style.display = 'none';

    if (filteredMaterials.length === 0) {
        listEl.style.display = 'none';
        emptyEl.style.display = 'flex';
        return;
    }

    listEl.style.display = 'block';
    emptyEl.style.display = 'none';

    listEl.innerHTML = filteredMaterials.map((m, idx) => {
        const images = (m.images && Array.isArray(m.images) && m.images.length > 0) ? m.images : [];
        const imageCount = images.length;
        const showImages = imageCount > 0;

        return `
            <div class="material-card" onclick="showDetail(${idx})">
                <div class="card-header">
                    <div class="card-title-area">
                        <h3 class="material-name">${escapeHtml(m.materialName || '(未命名)')}</h3>
                        ${m.generalName ? `<p class="material-sub">${escapeHtml(m.generalName)}</p>` : ''}
                    </div>
                    ${m.materialLevel ? `<span class="level-badge level-${levelClass(m.materialLevel)}">${escapeHtml(m.materialLevel)}</span>` : ''}
                </div>
                <div class="card-info">
                    <div class="info-row">
                        ${m.registrationNo ? `<div class="info-item"><span class="info-label">登记证号</span><span class="info-value">${escapeHtml(m.registrationNo)}</span></div>` : ''}
                        ${m.basicUnit ? `<div class="info-item"><span class="info-label">基本单位</span><span class="info-value">${escapeHtml(m.basicUnit)}</span></div>` : ''}
                    </div>
                    <div class="info-row">
                        ${m.specification ? `<div class="info-item"><span class="info-label">规格型号</span><span class="info-value">${escapeHtml(m.specification)}</span></div>` : ''}
                        ${m.productManager ? `<div class="info-item"><span class="info-label">产品经理</span><span class="info-value">${escapeHtml(m.productManager)}</span></div>` : ''}
                    </div>
                </div>
                ${showImages ? `
                    <div class="card-images">
                        ${images.slice(0, 3).map(src => `<img src="${src}" alt="" loading="lazy" onerror="this.style.display='none'">`).join('')}
                        ${imageCount > 3 ? `<div class="image-more">+${imageCount - 3}</div>` : ''}
                    </div>
                ` : ''}
                <div class="card-footer">
                    <span class="detail-hint">查看详情</span>
                    <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="m9 18 6-6-6-6"></path>
                    </svg>
                </div>
            </div>
        `;
    }).join('');
}

function levelClass(level) {
    if (!level) return 'default';
    if (level.includes('一')) return 'one';
    if (level.includes('二')) return 'two';
    if (level.includes('三')) return 'three';
    return 'default';
}

function escapeHtml(s) {
    if (s == null) return '';
    return String(s)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

// ---------------- 搜索 ----------------
function onSearchInput() {
    const val = document.getElementById('searchInput').value.trim();
    document.getElementById('clearBtn').style.display = val ? 'flex' : 'none';
    if (searchTimer) clearTimeout(searchTimer);
    searchTimer = setTimeout(() => {
        currentKeyword = val;
        applyFilters();
    }, 250);
}

function clearSearch() {
    document.getElementById('searchInput').value = '';
    document.getElementById('clearBtn').style.display = 'none';
    currentKeyword = '';
    applyFilters();
}

function filterByLevel(btn, level) {
    document.querySelectorAll('.filter-tab').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    currentLevelFilter = level;
    applyFilters();
}

function applyFilters() {
    const kw = (currentKeyword || '').toLowerCase();
    const lv = currentLevelFilter || '';
    const src = (allMaterials && allMaterials.length) ? allMaterials : (window.mockMaterials || []);

    filteredMaterials = src.filter(m => {
        if (lv) {
            if ((m.materialLevel || '') !== lv) return false;
        }
        if (kw) {
            const hay = [
                m.materialName, m.generalName, m.specification,
                m.registrationNo, m.productManager, m.productInfo
            ].filter(Boolean).join(' ').toLowerCase();
            if (!hay.includes(kw)) return false;
        }
        return true;
    });

    renderMaterialList();
}

// ---------------- 详情 ----------------
let currentDetailImages = [];
let currentPreviewIndex = 0;

function showDetail(idx) {
    const m = filteredMaterials[idx];
    if (!m) return;

    const images = (m.images && Array.isArray(m.images) && m.images.length > 0) ? m.images : [];
    currentDetailImages = images;
    const imageCount = images.length;

    document.getElementById('detailTitle').textContent = m.materialName || '物料详情';

    const rows = [
        { label: '通用名', value: m.generalName },
        { label: '基本单位', value: m.basicUnit },
        { label: '规格型号', value: m.specification },
        { label: '物料等级', value: m.materialLevel },
        { label: '登记剂型', value: m.registrationForm },
        { label: '作物场所', value: m.cropPlace },
        { label: '防治对象', value: m.controlTarget },
        { label: '大概使用时间', value: m.useTime },
        { label: '登记证号', value: m.registrationNo },
        { label: '产品属性', value: m.productAttribute },
        { label: '作物属性', value: m.cropAttribute },
        { label: '产品经理', value: m.productManager }
    ].filter(r => r.value != null && r.value !== '');

    let html = '';

    if (imageCount > 0) {
        html += `
            <div class="detail-image-section">
                <div class="image-carousel">
                    <div class="image-scroll">
                        ${images.map((src, i) => `
                            <div class="image-scroll-item">
                                <img src="${src}" alt="图片${i + 1}" onclick="openImagePreview(${i})"
                                     onerror="this.style.display='none'">
                            </div>
                        `).join('')}
                    </div>
                    <div class="image-count-badge">1 / ${imageCount}</div>
                </div>
                ${imageCount > 1 ? `
                    <div class="carousel-dots" id="carouselDots">
                        ${images.map((_, i) => `<div class="carousel-dot ${i === 0 ? 'active' : ''}"></div>`).join('')}
                    </div>
                ` : ''}
            </div>
        `;
    }

    html += `
        <div class="detail-info-section">
            <h3 class="detail-section-title">基础信息</h3>
            <div class="detail-info-grid">
                ${rows.map(r => `
                    <div class="detail-info-item">
                        <span class="detail-info-label">${escapeHtml(r.label)}</span>
                        <span class="detail-info-value">${escapeHtml(r.value)}</span>
                    </div>
                `).join('')}
            </div>
            ${m.productInfo ? `
                <h3 class="detail-section-title" style="margin-top:16px;">产品信息</h3>
                <p class="product-info-text">${escapeHtml(m.productInfo)}</p>
            ` : ''}
        </div>
    `;

    document.getElementById('detailBody').innerHTML = html;
    document.getElementById('detailModal').classList.add('show');
    document.body.style.overflow = 'hidden';

    // 图片轮播：监听滚动以更新页码
    if (imageCount > 1) {
        const scroll = document.querySelector('.image-scroll');
        if (scroll) {
            scroll.addEventListener('scroll', onCarouselScroll, { passive: true });
        }
    }
}

function onCarouselScroll() {
    const scroll = document.querySelector('.image-scroll');
    if (!scroll) return;
    const itemWidth = scroll.clientWidth;
    if (!itemWidth) return;
    const idx = Math.round(scroll.scrollLeft / itemWidth);
    const total = currentDetailImages.length;

    const badge = document.querySelector('.image-count-badge');
    if (badge) badge.textContent = (idx + 1) + ' / ' + total;

    const dots = document.querySelectorAll('#carouselDots .carousel-dot');
    dots.forEach((d, i) => d.classList.toggle('active', i === idx));
}

function closeDetail() {
    document.getElementById('detailModal').classList.remove('show');
    document.body.style.overflow = '';
}

// ---------------- 图片全屏预览 ----------------
function openImagePreview(index) {
    if (!currentDetailImages || currentDetailImages.length === 0) return;
    currentPreviewIndex = index || 0;

    const slider = document.getElementById('imagePreviewSlider');
    const dotsWrap = document.getElementById('imagePreviewDots');
    const countWrap = document.getElementById('imagePreviewCount');

    slider.innerHTML = currentDetailImages.map(src => `
        <div class="image-preview-item">
            <img src="${src}" alt="预览图" onerror="this.style.opacity=.2">
        </div>
    `).join('');

    dotsWrap.innerHTML = currentDetailImages.map((_, i) =>
        `<div class="carousel-dot ${i === currentPreviewIndex ? 'active' : ''}"
              onclick="event.stopPropagation();jumpToImage(${i})"></div>`
    ).join('');
    countWrap.textContent = (currentPreviewIndex + 1) + ' / ' + currentDetailImages.length;

    slider.style.transform = 'translateX(' + (-currentPreviewIndex * 100) + '%)';

    const modal = document.getElementById('imagePreviewModal');
    modal.classList.add('show');
    modal.onclick = (e) => {
        if (e.target === modal || e.target === modal.querySelector('.image-preview-content') || e.target === modal.querySelector('.image-preview-slider')) {
            closeImagePreview();
        }
    };
}

function closeImagePreview() {
    document.getElementById('imagePreviewModal').classList.remove('show');
}

function nextImage() {
    if (!currentDetailImages.length) return;
    currentPreviewIndex = (currentPreviewIndex + 1) % currentDetailImages.length;
    refreshPreview();
}

function prevImage() {
    if (!currentDetailImages.length) return;
    currentPreviewIndex = (currentPreviewIndex - 1 + currentDetailImages.length) % currentDetailImages.length;
    refreshPreview();
}

function jumpToImage(i) {
    currentPreviewIndex = i;
    refreshPreview();
}

function refreshPreview() {
    const slider = document.getElementById('imagePreviewSlider');
    slider.style.transform = 'translateX(' + (-currentPreviewIndex * 100) + '%)';
    document.getElementById('imagePreviewCount').textContent = (currentPreviewIndex + 1) + ' / ' + currentDetailImages.length;
    const dots = document.querySelectorAll('#imagePreviewDots .carousel-dot');
    dots.forEach((d, i) => d.classList.toggle('active', i === currentPreviewIndex));
}

// 触摸左右滑动切换预览
(function enableSwipe() {
    let startX = 0, delta = 0;
    const slider = document.getElementById('imagePreviewSlider');
    if (!slider) return;
    slider.addEventListener('touchstart', (e) => {
        startX = e.touches[0].clientX; delta = 0;
    }, { passive: true });
    slider.addEventListener('touchmove', (e) => {
        delta = e.touches[0].clientX - startX;
    }, { passive: true });
    slider.addEventListener('touchend', () => {
        if (Math.abs(delta) > 40) {
            if (delta < 0) nextImage(); else prevImage();
        }
    });
})();

// ---------------- 扫码 ----------------
function startScan() {
    if (window.wx && typeof wx.scanQRCode === 'function') {
        try {
            wx.scanQRCode({
                needResult: 1,               // 1 = 返回结果，0 = 企业微信自己处理
                scanType: ['qrCode', 'barCode'],
                success: (res) => {
                    const result = (res.resultStr || '').trim();
                    if (!result) return;
                    // 有时会是 "CODE_128,物料号" 的格式
                    const code = result.includes(',') ? result.split(',').pop() : result;
                    document.getElementById('searchInput').value = code;
                    document.getElementById('clearBtn').style.display = 'flex';
                    currentKeyword = code;
                    applyFilters();
                },
                error: (err) => {
                    alert('扫码失败，请手动输入（' + (err.errMsg || err) + '）');
                }
            });
        } catch (e) {
            promptManual();
        }
    } else {
        promptManual();
    }
}

function promptManual() {
    const code = prompt('当前环境不支持企业微信扫码。请直接输入物料编号/关键词：');
    if (code) {
        document.getElementById('searchInput').value = code;
        document.getElementById('clearBtn').style.display = 'flex';
        currentKeyword = code;
        applyFilters();
    }
}

// ---------------- 样式补充 ----------------
(function addInlineStyles() {
    const style = document.createElement('style');
    style.textContent = `
        .user-info {
            display: flex; align-items: center; gap: 6px;
            padding: 6px 12px; background: #f0f7ff; color: #1890ff;
            font-size: 12px; border-bottom: 1px solid #d9ecff;
        }
        .user-badge {
            background: #1890ff; color: #fff; padding: 2px 6px;
            border-radius: 4px; font-size: 11px; letter-spacing: .5px;
        }
    `;
    document.head.appendChild(style);
})();

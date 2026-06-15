let allMaterials = [];
let filteredMaterials = [];
let currentLevelFilter = '';

document.addEventListener('DOMContentLoaded', function() {
    loadMaterials();
});

function loadMaterials() {
    const listEl = document.getElementById('materialList');
    const emptyEl = document.getElementById('emptyState');
    const loadingEl = document.getElementById('loadingState');

    listEl.style.display = 'none';
    emptyEl.style.display = 'none';
    loadingEl.style.display = 'flex';

    setTimeout(() => {
        allMaterials = [...mockMaterials];
        filteredMaterials = [...allMaterials];
        renderMaterialList();
    }, 500);
}

function renderMaterialList() {
    const listEl = document.getElementById('materialList');
    const emptyEl = document.getElementById('emptyState');
    const loadingEl = document.getElementById('loadingState');

    loadingEl.style.display = 'none';

    if (filteredMaterials.length === 0) {
        listEl.style.display = 'none';
        emptyEl.style.display = 'flex';
        return;
    }

    listEl.style.display = 'flex';
    emptyEl.style.display = 'none';

    listEl.innerHTML = filteredMaterials.map(m => `
        <div class="material-card" onclick="showDetail('${m.id}')">
            <div class="card-header">
                <div class="material-name">${m.materialName}</div>
                <span class="level-badge level-${m.materialLevel}">${m.materialLevel}</span>
            </div>
            <div class="card-subtitle">通用名：<strong>${m.commonName}</strong></div>
            <div class="card-subtitle">规格：<strong>${m.specModel}</strong></div>
            <div class="card-info">
                <span class="info-tag primary">${m.productAttribute}</span>
                <span class="info-tag">${m.dosageForm}</span>
                <span class="info-tag">${m.baseUnit}</span>
            </div>
            <div class="card-footer">
                <span>编号：${m.id}</span>
                <span class="view-detail">
                    查看详情
                    <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="m9 18 6-6-6-6"></path>
                    </svg>
                </span>
            </div>
        </div>
    `).join('');
}

function searchMaterials() {
    const input = document.getElementById('searchInput');
    const clearBtn = document.getElementById('clearBtn');
    const keyword = input.value.trim().toLowerCase();

    clearBtn.style.display = keyword ? 'flex' : 'none';

    filteredMaterials = allMaterials.filter(m => {
        const matchKeyword = !keyword ||
            m.materialName.toLowerCase().includes(keyword) ||
            m.commonName.toLowerCase().includes(keyword) ||
            m.specModel.toLowerCase().includes(keyword) ||
            m.registrationNo.toLowerCase().includes(keyword) ||
            m.productManager.toLowerCase().includes(keyword);

        const matchLevel = !currentLevelFilter || m.materialLevel === currentLevelFilter;

        return matchKeyword && matchLevel;
    });

    renderMaterialList();
}

function clearSearch() {
    document.getElementById('searchInput').value = '';
    document.getElementById('clearBtn').style.display = 'none';
    currentLevelFilter = '';
    document.querySelectorAll('.filter-tab').forEach(tab => {
        tab.classList.remove('active');
    });
    document.querySelector('.filter-tab[data-level=""]').classList.add('active');
    filteredMaterials = [...allMaterials];
    renderMaterialList();
}

function filterByLevel(btn, level) {
    document.querySelectorAll('.filter-tab').forEach(tab => {
        tab.classList.remove('active');
    });
    btn.classList.add('active');
    currentLevelFilter = level;
    searchMaterials();
}

function showDetail(id) {
    const material = allMaterials.find(m => m.id === id);
    if (!material) return;

    const detailBody = document.getElementById('detailBody');
    const detailTitle = document.getElementById('detailTitle');

    detailTitle.textContent = material.materialName;

    detailBody.innerHTML = `
        <div class="detail-image-section">
            <div class="detail-image-wrapper">
                <svg viewBox="0 0 24 24" width="80" height="80" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M20.59 13.41 13.42 20.58a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z"></path>
                    <line x1="7" y1="7" x2="7.01" y2="7"></line>
                </svg>
                <div class="detail-image-placeholder">物料图片<br/><small>${material.specModel}</small></div>
            </div>
        </div>

        <div class="detail-section">
            <div class="section-title">基本信息</div>
            <div class="detail-grid">
                <div class="detail-row">
                    <span class="detail-label">物料名称</span>
                    <span class="detail-value">${material.materialName}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">通用名</span>
                    <span class="detail-value">${material.commonName}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">基本单位</span>
                    <span class="detail-value">${material.baseUnit}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">规格型号</span>
                    <span class="detail-value">${material.specModel}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">物料等级</span>
                    <span class="detail-value"><span class="detail-badge level-${material.materialLevel}">${material.materialLevel}</span></span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">登记剂型</span>
                    <span class="detail-value">${material.dosageForm}</span>
                </div>
            </div>
        </div>

        <div class="detail-section">
            <div class="section-title">使用信息</div>
            <div class="detail-grid">
                <div class="detail-row">
                    <span class="detail-label">作物场所</span>
                    <span class="detail-value secondary">${material.cropSite}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">防治对象</span>
                    <span class="detail-value secondary">${material.controlTarget}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">大概使用时间</span>
                    <span class="detail-value secondary">${material.usageTime}</span>
                </div>
            </div>
        </div>

        <div class="detail-section">
            <div class="section-title">登记与属性</div>
            <div class="detail-grid">
                <div class="detail-row">
                    <span class="detail-label">登记证号</span>
                    <span class="detail-value">${material.registrationNo}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">产品属性</span>
                    <span class="detail-value">${material.productAttribute}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">作物属性</span>
                    <span class="detail-value">${material.cropAttribute}</span>
                </div>
                <div class="detail-row">
                    <span class="detail-label">产品经理</span>
                    <span class="detail-value">${material.productManager}</span>
                </div>
            </div>
        </div>

        <div class="detail-section">
            <div class="section-title">产品信息</div>
            <div style="font-size: 14px; color: var(--text-secondary); line-height: 1.7;">
                ${material.productInfo}
            </div>
        </div>
    `;

    document.getElementById('detailModal').classList.add('active');
    document.body.style.overflow = 'hidden';
}

function closeDetail() {
    document.getElementById('detailModal').classList.remove('active');
    document.body.style.overflow = '';
}

document.addEventListener('keydown', function(e) {
    if (e.key === 'Escape') {
        closeDetail();
    }
});

document.getElementById('detailModal').addEventListener('click', function(e) {
    if (e.target === this) {
        closeDetail();
    }
});

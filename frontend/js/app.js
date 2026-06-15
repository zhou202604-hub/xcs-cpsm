// ============ 配置项 ============
const API_BASE = '/api';
const USE_MOCK_FALLBACK = true;

// ============ Mock 数据（后端不可用时的后备数据）============
const mockMaterials = [
    {
        id: 'WL-00001',
        materialName: '20%草铵膦水剂',
        commonName: '草铵膦',
        baseUnit: '升',
        specModel: '5L/桶',
        materialLevel: '一级',
        dosageForm: '水剂',
        cropSite: '果园、非耕地',
        controlTarget: '牛筋草、狗尾草、马唐等一年生杂草',
        usageTime: '杂草3-5叶期',
        registrationNo: 'PD20200012',
        productAttribute: '除草剂',
        cropAttribute: '大田作物',
        productManager: '张伟',
        productInfo: '本产品为触杀型除草剂，对多种一年生和多年生杂草有良好防效。施药后6小时遇雨不影响药效。',
        images: [
            'https://images.unsplash.com/photo-1560493676-04071c5f467b?w=800&q=80',
            'https://images.unsplash.com/photo-1625246333195-78d9c38ad449?w=800&q=80',
            'https://images.unsplash.com/photo-1592982537447-74608774e56d?w=800&q=80'
        ]
    },
    {
        id: 'WL-00002',
        materialName: '40%多菌灵悬浮剂',
        commonName: '多菌灵',
        baseUnit: '千克',
        specModel: '1kg/袋',
        materialLevel: '一级',
        dosageForm: '悬浮剂',
        cropSite: '蔬菜、果树、水稻',
        controlTarget: '稻瘟病、纹枯病、白粉病、炭疽病',
        usageTime: '发病初期',
        registrationNo: 'PD20180345',
        productAttribute: '杀菌剂',
        cropAttribute: '经济作物',
        productManager: '李娜',
        productInfo: '广谱性杀菌剂，具有保护和治疗作用。可用于防治多种作物的真菌性病害。',
        images: [
            'https://images.unsplash.com/photo-1416879595882-3373a0480b5b?w=800&q=80',
            'https://images.unsplash.com/photo-1583912267550-df7699274737?w=800&q=80'
        ]
    },
    {
        id: 'WL-00003',
        materialName: '25%吡虫啉可湿性粉剂',
        commonName: '吡虫啉',
        baseUnit: '克',
        specModel: '100g/袋',
        materialLevel: '二级',
        dosageForm: '可湿性粉剂',
        cropSite: '小麦、水稻、蔬菜、果树',
        controlTarget: '蚜虫、飞虱、蓟马、粉虱等刺吸式口器害虫',
        usageTime: '害虫发生初期',
        registrationNo: 'PD20190789',
        productAttribute: '杀虫剂',
        cropAttribute: '大田作物',
        productManager: '王强',
        productInfo: '烟碱类杀虫剂，具有内吸、触杀和胃毒作用。对刺吸式口器害虫有特效，持效期长。',
        images: [
            'https://images.unsplash.com/photo-1574949966261-9562ae68a82c?w=800&q=80'
        ]
    },
    {
        id: 'WL-00004',
        materialName: '5%阿维菌素乳油',
        commonName: '阿维菌素',
        baseUnit: '毫升',
        specModel: '500ml/瓶',
        materialLevel: '二级',
        dosageForm: '乳油',
        cropSite: '蔬菜、果树、棉花',
        controlTarget: '红蜘蛛、斑潜蝇、菜青虫、棉铃虫',
        usageTime: '低龄幼虫期',
        registrationNo: 'PD20170567',
        productAttribute: '杀虫剂',
        cropAttribute: '经济作物',
        productManager: '陈芳',
        productInfo: '生物源杀虫剂，具有触杀和胃毒作用。对螨类和鳞翅目幼虫有良好防效。',
        images: [
            'https://images.unsplash.com/photo-1625246333195-78d9c38ad449?w=800&q=80',
            'https://images.unsplash.com/photo-1592982537447-74608774e56d?w=800&q=80',
            'https://images.unsplash.com/photo-1560493676-04071c5f467b?w=800&q=80',
            'https://images.unsplash.com/photo-1416879595882-3373a0480b5b?w=800&q=80'
        ]
    },
    {
        id: 'WL-00005',
        materialName: '95%草甘膦原药',
        commonName: '草甘膦',
        baseUnit: '千克',
        specModel: '25kg/袋',
        materialLevel: '三级',
        dosageForm: '原药',
        cropSite: '非耕地、果园行间',
        controlTarget: '一年生及多年生杂草',
        usageTime: '杂草生长旺盛期',
        registrationNo: 'PD20150234',
        productAttribute: '除草剂',
        cropAttribute: '非耕地',
        productManager: '刘洋',
        productInfo: '广谱灭生性除草剂，通过植物茎叶吸收后传导至根部。用于非耕地除草效果显著。',
        images: []
    },
    {
        id: 'WL-00006',
        materialName: '15%氟磺胺草醚乳油',
        commonName: '氟磺胺草醚',
        baseUnit: '升',
        specModel: '1L/瓶',
        materialLevel: '二级',
        dosageForm: '乳油',
        cropSite: '大豆田、花生田',
        controlTarget: '反枝苋、马齿苋、藜等阔叶杂草',
        usageTime: '大豆2-4片复叶期',
        registrationNo: 'PD20210456',
        productAttribute: '除草剂',
        cropAttribute: '豆科作物',
        productManager: '赵敏',
        productInfo: '二苯醚类选择性苗后除草剂，用于大豆和花生田防除阔叶杂草。',
        images: [
            'https://images.unsplash.com/photo-1583912267550-df7699274737?w=800&q=80'
        ]
    },
    {
        id: 'WL-00007',
        materialName: '80%代森锰锌可湿性粉剂',
        commonName: '代森锰锌',
        baseUnit: '千克',
        specModel: '2kg/袋',
        materialLevel: '一级',
        dosageForm: '可湿性粉剂',
        cropSite: '果树、蔬菜、大田作物',
        controlTarget: '霜霉病、疫病、炭疽病、叶斑病',
        usageTime: '发病前或发病初期',
        registrationNo: 'PD20160890',
        productAttribute: '杀菌剂',
        cropAttribute: '经济作物',
        productManager: '张伟',
        productInfo: '广谱保护性杀菌剂，含锰、锌微量元素。能有效防治多种真菌性病害。',
        images: [
            'https://images.unsplash.com/photo-1592982537447-74608774e56d?w=800&q=80'
        ]
    },
    {
        id: 'WL-00008',
        materialName: '20%噻虫嗪水分散粒剂',
        commonName: '噻虫嗪',
        baseUnit: '克',
        specModel: '200g/袋',
        materialLevel: '二级',
        dosageForm: '水分散粒剂',
        cropSite: '水稻、小麦、棉花、蔬菜',
        controlTarget: '稻飞虱、蚜虫、蓟马、白粉虱',
        usageTime: '害虫发生初期',
        registrationNo: 'PD20200678',
        productAttribute: '杀虫剂',
        cropAttribute: '大田作物',
        productManager: '李娜',
        productInfo: '新一代烟碱类杀虫剂，具有胃毒、触杀和内吸活性。杀虫谱广、活性高、持效期长。',
        images: [
            'https://images.unsplash.com/photo-1574949966261-9562ae68a82c?w=800&q=80',
            'https://images.unsplash.com/photo-1625246333195-78d9c38ad449?w=800&q=80'
        ]
    },
    {
        id: 'WL-00009',
        materialName: '30%苯醚甲环唑水分散粒剂',
        commonName: '苯醚甲环唑',
        baseUnit: '克',
        specModel: '100g/袋',
        materialLevel: '一级',
        dosageForm: '水分散粒剂',
        cropSite: '果树、蔬菜、禾谷类作物',
        controlTarget: '黑星病、白粉病、叶斑病、锈病',
        usageTime: '发病初期',
        registrationNo: 'PD20190123',
        productAttribute: '杀菌剂',
        cropAttribute: '经济作物',
        productManager: '王强',
        productInfo: '三唑类广谱杀菌剂，具有保护、治疗和铲除作用。对子囊菌、担子菌和半知菌引起的病害有特效。',
        images: [
            'https://images.unsplash.com/photo-1560493676-04071c5f467b?w=800&q=80',
            'https://images.unsplash.com/photo-1416879595882-3373a0480b5b?w=800&q=80',
            'https://images.unsplash.com/photo-1583912267550-df7699274737?w=800&q=80'
        ]
    },
    {
        id: 'WL-00010',
        materialName: '10%氰氟草酯乳油',
        commonName: '氰氟草酯',
        baseUnit: '升',
        specModel: '1L/瓶',
        materialLevel: '三级',
        dosageForm: '乳油',
        cropSite: '水稻田',
        controlTarget: '稗草、千金子等禾本科杂草',
        usageTime: '水稻插秧后5-7天',
        registrationNo: 'PD20180456',
        productAttribute: '除草剂',
        cropAttribute: '水稻',
        productManager: '陈芳',
        productInfo: '芳氧苯氧丙酸酯类除草剂，用于水稻田防除禾本科杂草。对千金子、稗草特效。',
        images: [
            'https://images.unsplash.com/photo-1592982537447-74608774e56d?w=800&q=80'
        ]
    },
    {
        id: 'WL-00011',
        materialName: '45%咪鲜胺水乳剂',
        commonName: '咪鲜胺',
        baseUnit: '毫升',
        specModel: '500ml/瓶',
        materialLevel: '一级',
        dosageForm: '水乳剂',
        cropSite: '果树、蔬菜、食用菌',
        controlTarget: '炭疽病、蒂腐病、青霉病、绿霉病',
        usageTime: '发病初期或采后处理',
        registrationNo: 'PD20210234',
        productAttribute: '杀菌剂',
        cropAttribute: '经济作物',
        productManager: '刘洋',
        productInfo: '咪唑类广谱杀菌剂，对多种作物由子囊菌和半知菌引起的病害有明显防效。也可用于水果采后防腐保鲜。',
        images: []
    },
    {
        id: 'WL-00012',
        materialName: '50%氯氰菊酯乳油',
        commonName: '氯氰菊酯',
        baseUnit: '毫升',
        specModel: '250ml/瓶',
        materialLevel: '二级',
        dosageForm: '乳油',
        cropSite: '蔬菜、果树、棉花、大豆',
        controlTarget: '菜青虫、棉铃虫、食心虫、蚜虫',
        usageTime: '低龄幼虫期',
        registrationNo: 'PD20170890',
        productAttribute: '杀虫剂',
        cropAttribute: '大田作物',
        productManager: '赵敏',
        productInfo: '拟除虫菊酯类杀虫剂，具有触杀和胃毒作用。杀虫谱广，击倒速度快。',
        images: [
            'https://images.unsplash.com/photo-1560493676-04071c5f467b?w=800&q=80',
            'https://images.unsplash.com/photo-1625246333195-78d9c38ad449?w=800&q=80'
        ]
    }
];

// ============ 全局状态 ============
let allMaterials = [];
let filteredMaterials = [];
let currentLevelFilter = '';
let currentKeyword = '';
let currentDetail = null;
let currentImageIndex = 0;
let previewImageIndex = 0;

// ============ 初始化 ============
document.addEventListener('DOMContentLoaded', function () {
    loadMaterials();
});

// ============ API 调用 ============
async function fetchMaterialsFromApi(keyword, level) {
    const params = new URLSearchParams();
    if (keyword) params.append('keyword', keyword);
    if (level) params.append('level', level);
    params.append('pageIndex', '1');
    params.append('pageSize', '100');

    const url = `${API_BASE}/material?${params.toString()}`;
    const response = await fetch(url);
    if (!response.ok) {
        throw new Error(`API请求失败: ${response.status}`);
    }
    const data = await response.json();
    return data.data || [];
}

async function fetchMaterialDetailFromApi(id) {
    const response = await fetch(`${API_BASE}/material/${id}`);
    if (!response.ok) throw new Error('获取详情失败');
    const data = await response.json();
    return data.data;
}

// ============ 数据加载 ============
async function loadMaterials() {
    showLoading();
    try {
        // 优先尝试后端API
        const materials = await fetchMaterialsFromApi(currentKeyword, currentLevelFilter);
        if (materials && materials.length > 0) {
            allMaterials = materials;
            filteredMaterials = [...allMaterials];
        } else if (USE_MOCK_FALLBACK) {
            // 回退到mock数据
            allMaterials = [...mockMaterials];
            filteredMaterials = [...allMaterials];
        } else {
            allMaterials = [];
            filteredMaterials = [];
        }
        renderMaterialList();
    } catch (err) {
        console.warn('API调用失败，使用Mock数据:', err.message);
        if (USE_MOCK_FALLBACK) {
            allMaterials = [...mockMaterials];
            filteredMaterials = [...allMaterials];
            if (currentKeyword) applyFilters();
            renderMaterialList();
        } else {
            showError(err.message);
        }
    }
}

function applyFilters() {
    filteredMaterials = allMaterials.filter(m => {
        const kw = (currentKeyword || '').toLowerCase();
        const matchKeyword = !kw ||
            (m.materialName || '').toLowerCase().includes(kw) ||
            (m.commonName || '').toLowerCase().includes(kw) ||
            (m.specModel || '').toLowerCase().includes(kw) ||
            (m.registrationNo || '').toLowerCase().includes(kw) ||
            (m.productManager || '').toLowerCase().includes(kw);

        const matchLevel = !currentLevelFilter || m.materialLevel === currentLevelFilter;
        return matchKeyword && matchLevel;
    });
}

// ============ 搜索与筛选 ============
let searchTimer = null;
function onSearchInput() {
    const keyword = document.getElementById('searchInput').value;
    document.getElementById('clearBtn').style.display = keyword ? 'flex' : 'none';
    currentKeyword = keyword;
    // 防抖：避免频繁请求
    if (searchTimer) clearTimeout(searchTimer);
    searchTimer = setTimeout(() => {
        loadMaterials();
    }, 400);
}

function clearSearch() {
    document.getElementById('searchInput').value = '';
    document.getElementById('clearBtn').style.display = 'none';
    currentKeyword = '';
    currentLevelFilter = '';
    document.querySelectorAll('.filter-tab').forEach(tab => tab.classList.remove('active'));
    document.querySelector('.filter-tab[data-level=""]').classList.add('active');
    loadMaterials();
}

function filterByLevel(btn, level) {
    document.querySelectorAll('.filter-tab').forEach(tab => tab.classList.remove('active'));
    btn.classList.add('active');
    currentLevelFilter = level;
    loadMaterials();
}

// ============ 渲染列表 ============
function renderMaterialList() {
    const listEl = document.getElementById('materialList');
    const emptyEl = document.getElementById('emptyState');
    const loadingEl = document.getElementById('loadingState');
    const errorEl = document.getElementById('errorState');

    errorEl.style.display = 'none';
    loadingEl.style.display = 'none';

    if (filteredMaterials.length === 0) {
        listEl.style.display = 'none';
        emptyEl.style.display = 'flex';
        return;
    }

    listEl.style.display = 'flex';
    emptyEl.style.display = 'none';

    listEl.innerHTML = filteredMaterials.map(m => renderCard(m)).join('');
}

function renderCard(m) {
    const imageCount = (m.images || []).length;
    const displayImages = (m.images || []).slice(0, 3);
    const hasMoreImages = imageCount > 3;

    let thumbsHtml = '';
    if (imageCount > 0) {
        thumbsHtml = `
            <div class="card-image-section">
                ${displayImages.map((img, idx) => `
                    <div class="card-thumb">
                        <img src="${img}" alt="${m.materialName}" loading="lazy" onerror="this.style.display='none';this.parentNode.innerHTML='<svg viewBox=\\'0 0 24 24\\' width=\\'32\\' height=\\'32\\' fill=\\'none\\' stroke=\\'currentColor\\' stroke-width=\\'1.5\\'><path d=\\'M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6\\'/></svg>'">
                    </div>
                `).join('')}
                ${hasMoreImages ? `<div class="card-thumb card-thumb-more">+${imageCount - 3}</div>` : ''}
            </div>
        `;
    }

    return `
        <div class="material-card" onclick="showDetail('${m.id}')">
            ${thumbsHtml}
            <div class="card-header">
                <div class="material-name">${m.materialName}</div>
                ${m.materialLevel ? `<span class="level-badge level-${m.materialLevel}">${m.materialLevel}</span>` : ''}
            </div>
            ${m.commonName ? `<div class="card-subtitle">通用名：<strong>${m.commonName}</strong></div>` : ''}
            ${m.specModel ? `<div class="card-subtitle">规格：<strong>${m.specModel}</strong></div>` : ''}
            <div class="card-info">
                ${m.productAttribute ? `<span class="info-tag primary">${m.productAttribute}</span>` : ''}
                ${m.dosageForm ? `<span class="info-tag">${m.dosageForm}</span>` : ''}
                ${m.baseUnit ? `<span class="info-tag">${m.baseUnit}</span>` : ''}
            </div>
            <div class="card-footer">
                <span>编号：${m.id}</span>
                <span class="view-detail">
                    查看详情
                    <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="m9 18 6-6-6-6"/>
                    </svg>
                </span>
            </div>
        </div>
    `;
}

// ============ 状态显示 ============
function showLoading() {
    document.getElementById('materialList').style.display = 'none';
    document.getElementById('emptyState').style.display = 'none';
    document.getElementById('errorState').style.display = 'none';
    document.getElementById('loadingState').style.display = 'flex';
}

function showError(msg) {
    document.getElementById('materialList').style.display = 'none';
    document.getElementById('emptyState').style.display = 'none';
    document.getElementById('loadingState').style.display = 'none';
    const errorEl = document.getElementById('errorState');
    errorEl.style.display = 'flex';
    document.getElementById('errorTitle').textContent = '加载失败';
    document.getElementById('errorMessage').textContent = msg || '请稍后重试';
}

// ============ 详情页 ============
async function showDetail(id) {
    // 先尝试从已有的列表数据中找
    let material = allMaterials.find(m => m.id === id);
    // 如果没有或图片字段缺失，尝试从后端获取详情
    if (!material || !material.images) {
        try {
            const detail = await fetchMaterialDetailFromApi(id);
            if (detail) material = detail;
        } catch (e) {
            console.warn('获取详情失败:', e);
        }
    }
    if (!material) return;
    currentDetail = material;
    currentImageIndex = 0;

    const detailBody = document.getElementById('detailBody');
    document.getElementById('detailTitle').textContent = material.materialName;

    detailBody.innerHTML = renderDetail(material);
    document.getElementById('detailModal').classList.add('active');
    document.body.style.overflow = 'hidden';

    // 初始化轮播滚动监听
    const scrollEl = document.querySelector('.image-scroll');
    if (scrollEl) {
        scrollEl.addEventListener('scroll', handleCarouselScroll);
    }
}

function renderDetail(m) {
    const images = m.images || [];
    const imageCount = images.length;

    let imageHtml = '';
    if (imageCount > 0) {
        imageHtml = `
            <div class="detail-image-section">
                <div class="image-carousel">
                    <div class="image-scroll">
                        ${images.map((img, idx) => `
                            <div class="image-scroll-item">
                                <img src="${img}" alt="${m.materialName}-${idx + 1}" onclick="openImagePreview(${idx})">
                            </div>
                        `).join('')}
                    </div>
                    ${imageCount > 1 ? `<div class="image-count-badge">1 / ${imageCount}</div>` : ''}
                </div>
                ${imageCount > 1 ? `<div class="carousel-dots" id="carouselDots">
                    ${images.map((_, idx) => `<div class="carousel-dot ${idx === 0 ? 'active' : ''}"></div>`).join('')}
                </div>` : ''}
            </div>
        `;
    } else {
        imageHtml = `
            <div class="detail-image-section">
                <div class="image-carousel">
                    <div class="image-scroll">
                        <div class="image-scroll-item">
                            <svg viewBox="0 0 24 24" width="80" height="80" fill="none" stroke="currentColor" stroke-width="1.5">
                                <path d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6"></path>
                            </svg>
                            <div class="image-placeholder-text">暂无图片</div>
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    const section1 = [
        ['物料名称', m.materialName],
        ['通用名', m.commonName],
        ['物料编码', m.id],
        ['规格型号', m.specModel],
        ['基本单位', m.baseUnit],
        ['物料等级', m.materialLevel, 'badge'],
        ['登记剂型', m.dosageForm]
    ].filter(row => row[1]);

    const section2 = [
        ['作物场所', m.cropSite],
        ['防治对象', m.controlTarget],
        ['大概使用时间', m.usageTime],
    ].filter(row => row[1]);

    const section3 = [
        ['登记证号', m.registrationNo],
        ['产品属性', m.productAttribute],
        ['作物属性', m.cropAttribute],
        ['产品经理', m.productManager]
    ].filter(row => row[1]);

    const renderRow = (label, value, mode) => {
        if (mode === 'badge') {
            return `<div class="detail-row"><span class="detail-label">${label}</span><span class="detail-value"><span class="detail-badge level-${value}">${value}</span></span></div>`;
        }
        return `<div class="detail-row"><span class="detail-label">${label}</span><span class="detail-value secondary">${value}</span></div>`;
    };

    return `
        ${imageHtml}

        <div class="detail-section">
            <div class="section-title">基本信息</div>
            <div class="detail-grid">
                ${section1.map(row => {
                    if (row[2] === 'badge') return renderRow(row[0], row[1], 'badge');
                    return `<div class="detail-row"><span class="detail-label">${row[0]}</span><span class="detail-value">${row[1]}</span></div>`;
                }).join('')}
            </div>
        </div>

        ${section2.length > 0 ? `
        <div class="detail-section">
            <div class="section-title">使用信息</div>
            <div class="detail-grid">
                ${section2.map(row => renderRow(row[0], row[1])).join('')}
            </div>
        </div>` : ''}

        ${section3.length > 0 ? `
        <div class="detail-section">
            <div class="section-title">登记与属性</div>
            <div class="detail-grid">
                ${section3.map(row => `<div class="detail-row"><span class="detail-label">${row[0]}</span><span class="detail-value">${row[1]}</span></div>`).join('')}
            </div>
        </div>` : ''}

        ${m.productInfo ? `
        <div class="detail-section">
            <div class="section-title">产品信息</div>
            <div style="font-size: 14px; color: var(--text-secondary); line-height: 1.7;">${m.productInfo}</div>
        </div>` : ''}
    `;
}

function handleCarouselScroll(e) {
    const container = e.target;
    if (!container) return;
    const itemWidth = container.clientWidth;
    const scrollX = container.scrollLeft;
    const idx = Math.round(scrollX / itemWidth);
    if (idx !== currentImageIndex) {
        currentImageIndex = idx;
        // 更新计数
        const badge = document.querySelector('.image-count-badge');
        if (badge && currentDetail) {
            const total = (currentDetail.images || []).length;
            badge.textContent = `${idx + 1} / ${total}`;
        }
        // 更新圆点
        const dots = document.querySelectorAll('#carouselDots .carousel-dot');
        dots.forEach((dot, i) => dot.classList.toggle('active', i === idx));
    }
}

function closeDetail() {
    document.getElementById('detailModal').classList.remove('active');
    document.body.style.overflow = '';
    currentDetail = null;
}

// ============ 图片全屏预览 ============
function openImagePreview(startIdx) {
    if (!currentDetail || !currentDetail.images || currentDetail.images.length === 0) return;
    previewImageIndex = startIdx;
    const images = currentDetail.images;
    const modal = document.getElementById('imagePreviewModal');

    const slider = document.getElementById('imagePreviewSlider');
    slider.innerHTML = images.map(img => `
        <div class="image-preview-slide">
            <img src="${img}" alt="预览图">
        </div>
    `).join('');

    const dots = document.getElementById('imagePreviewDots');
    dots.innerHTML = images.map((_, idx) => `<div class="carousel-dot ${idx === startIdx ? 'active' : ''}"></div>`).join('');

    document.getElementById('imagePreviewCount').textContent = `${startIdx + 1} / ${images.length}`;

    modal.classList.add('active');
    document.body.style.overflow = 'hidden';

    // 滚动到起始图片
    setTimeout(() => {
        slider.scrollLeft = startIdx * slider.clientWidth;
    }, 50);

    slider.addEventListener('scroll', handlePreviewScroll, { once: false });
}

function handlePreviewScroll(e) {
    const container = e.target;
    const itemWidth = container.clientWidth;
    const scrollX = container.scrollLeft;
    const idx = Math.round(scrollX / itemWidth);
    if (idx !== previewImageIndex) {
        previewImageIndex = idx;
        const images = currentDetail.images || [];
        document.getElementById('imagePreviewCount').textContent = `${idx + 1} / ${images.length}`;
        const dots = document.querySelectorAll('#imagePreviewDots .carousel-dot');
        dots.forEach((dot, i) => dot.classList.toggle('active', i === idx));
    }
}

function prevImage() {
    const images = currentDetail.images || [];
    if (images.length === 0) return;
    previewImageIndex = (previewImageIndex - 1 + images.length) % images.length;
    scrollToPreview(previewImageIndex);
}

function nextImage() {
    const images = currentDetail.images || [];
    if (images.length === 0) return;
    previewImageIndex = (previewImageIndex + 1) % images.length;
    scrollToPreview(previewImageIndex);
}

function scrollToPreview(idx) {
    const slider = document.getElementById('imagePreviewSlider');
    slider.scrollTo({ left: idx * slider.clientWidth, behavior: 'smooth' });
}

function closeImagePreview(event) {
    // 只有点击背景或关闭按钮才关闭
    if (event && event.target && !event.target.closest('.image-preview-content') && !event.target.closest('.image-preview-close')) return;
    document.getElementById('imagePreviewModal').classList.remove('active');
    if (!document.getElementById('detailModal').classList.contains('active')) {
        document.body.style.overflow = '';
    }
}

// ESC 关闭
document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
        if (document.getElementById('imagePreviewModal').classList.contains('active')) {
            document.getElementById('imagePreviewModal').classList.remove('active');
            document.body.style.overflow = '';
        } else if (document.getElementById('detailModal').classList.contains('active')) {
            closeDetail();
        }
    }
    // 左右方向键切换预览
    if (document.getElementById('imagePreviewModal').classList.contains('active')) {
        if (e.key === 'ArrowLeft') prevImage();
        if (e.key === 'ArrowRight') nextImage();
    }
});

// 详情面板点击背景关闭
document.getElementById('detailModal').addEventListener('click', function (e) {
    if (e.target === this) closeDetail();
});

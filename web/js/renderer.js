/**
 * FolderSnap - 视觉卡片与全景大图渲染引擎
 */

class FolderRenderer {
  constructor() {
    this.currentData = null;
    this.searchQuery = '';
    this.sortBy = 'name'; // 'name', 'files', 'size'
  }

  /**
   * HTML 转义防注入
   */
  escapeHtml(str) {
    if (!str) return '';
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  /**
   * 渲染单个子文件夹快照卡片 DOM
   */
  createFolderCard(folder, options = {}) {
    const isPanorama = !!options.isPanorama;
    const card = document.createElement('div');
    card.className = 'snapshot-card';
    card.id = `folder-card-${this.sanitizeId(folder.path)}`;
    card.dataset.path = folder.path;
    card.dataset.filesCount = folder.directFiles.length;
    card.dataset.totalBytes = folder.totalBytes;
    card.dataset.name = folder.name;

    // 面包屑路径
    const pathParts = folder.path.split('/').filter(Boolean);
    const breadcrumbHtml = pathParts.map((p, idx) => {
      const isLast = idx === pathParts.length - 1;
      return `<span class="path-segment ${isLast ? 'active' : ''}">${this.escapeHtml(p)}</span>`;
    }).join(' <span class="path-sep">/</span> ');

    const filesCount = folder.directFiles.length;
    const totalBytesFormatted = folder.formattedTotalBytes;

    card.innerHTML = `
      <div class="card-header">
        <div class="traffic-lights">
          <span class="dot red"></span>
          <span class="dot yellow"></span>
          <span class="dot green"></span>
        </div>
        <div class="card-title-group" title="${this.escapeHtml(folder.path)}">
          <span class="card-folder-icon">📁</span>
          <div class="card-path-breadcrumb">${breadcrumbHtml || this.escapeHtml(folder.name)}</div>
        </div>
        ${!isPanorama ? `
        <div class="card-actions">
          <button class="btn-card-action btn-copy-card" title="复制卡片截图到剪贴板">
            <span>📋</span> 复制
          </button>
          <button class="btn-card-action btn-download-card" title="下载卡片截图 PNG">
            <span>📸</span> 截图
          </button>
        </div>` : ''}
      </div>

      <div class="card-meta-bar">
        <div class="meta-stats-tags">
          <span class="meta-tag">📄 包含文件: <strong>${filesCount}</strong></span>
          <span class="meta-tag">💾 目录占用: <strong>${totalBytesFormatted}</strong></span>
        </div>
        <span class="meta-tag meta-depth">层级深度: L${folder.depth}</span>
      </div>

      <div class="card-body">
        ${filesCount > 0 ? `
          <table class="file-table">
            <thead>
              <tr>
                <th class="col-idx">#</th>
                <th>文件名</th>
                <th class="col-size">大小</th>
                <th class="col-date">修改时间</th>
              </tr>
            </thead>
            <tbody>
              ${folder.directFiles.map((file, idx) => `
                <tr>
                  <td class="col-idx">${idx + 1}</td>
                  <td>
                    <div class="col-name">
                      <span class="file-badge ${file.badgeClass}">${this.escapeHtml(file.label)}</span>
                      <span class="file-title" title="${this.escapeHtml(file.name)}">${this.escapeHtml(file.name)}</span>
                    </div>
                  </td>
                  <td class="col-size">${file.formattedSize}</td>
                  <td class="col-date">${file.lastModifiedFormatted}</td>
                </tr>
              `).join('')}
            </tbody>
          </table>
        ` : `
          <div class="card-empty-state">
            <span>📭 该子文件夹下无直属文件（可能仅包含更深层子目录）</span>
          </div>
        `}
      </div>
    `;

    return card;
  }

  /**
   * 渲染卡片网格列表 (Folder Cards View)
   */
  renderCardsGrid(scanResult, container, options = {}) {
    this.currentData = scanResult;
    container.innerHTML = '';

    const filteredFolders = this.getFilteredAndSortedFolders(scanResult.folders);

    if (filteredFolders.length === 0) {
      container.innerHTML = `
        <div style="grid-column: 1 / -1; padding: 48px; text-align: center; color: var(--text-muted);">
          <p style="font-size: 1.1rem; margin-bottom: 8px;">🔍 没有匹配的文件夹或文件</p>
          <p style="font-size: 0.85rem; color: var(--text-faint);">请尝试调整搜索关键词或打开“不过滤系统文件”开关。</p>
        </div>
      `;
      return;
    }

    const fragment = document.createDocumentFragment();
    for (const folder of filteredFolders) {
      const card = this.createFolderCard(folder, { isPanorama: false });
      fragment.appendChild(card);
    }

    container.appendChild(fragment);
  }

  /**
   * 渲染全景大图聚合视图 (Panorama View)
   */
  renderPanoramaView(scanResult, container) {
    this.currentData = scanResult;
    container.innerHTML = '';

    const filteredFolders = this.getFilteredAndSortedFolders(scanResult.folders);

    const panoWrapper = document.createElement('div');
    panoWrapper.className = 'panorama-container';
    panoWrapper.id = 'panorama-capture-target';

    panoWrapper.innerHTML = `
      <div class="panorama-banner">
        <div class="panorama-title">
          <h2>🗂 ${this.escapeHtml(scanResult.rootPath)} - 目录全景清单</h2>
          <p>生成时间: ${new Date().toLocaleString()} · 共计 ${scanResult.totalFolders} 个子目录 · ${scanResult.totalFiles} 个文件 · ${scanResult.formattedTotalBytes}</p>
        </div>
        <div class="panorama-actions">
          <button class="btn btn-accent btn-copy-panorama">
            <span>📋</span> 复制全景图
          </button>
          <button class="btn btn-primary btn-download-panorama">
            <span>📸</span> 导出全景长图 (PNG)
          </button>
        </div>
      </div>
      <div class="panorama-cards-flow" id="panorama-cards-flow"></div>
    `;

    const cardsFlow = panoWrapper.querySelector('#panorama-cards-flow');

    for (const folder of filteredFolders) {
      const card = this.createFolderCard(folder, { isPanorama: true });
      cardsFlow.appendChild(card);
    }

    container.appendChild(panoWrapper);
  }

  /**
   * 过滤与排序处理
   */
  getFilteredAndSortedFolders(folders) {
    let result = [...folders];

    // 搜索过滤
    if (this.searchQuery) {
      const q = this.searchQuery.toLowerCase();
      result = result.filter(folder => {
        const matchPath = folder.path.toLowerCase().includes(q);
        const matchFile = folder.directFiles.some(f => f.name.toLowerCase().includes(q));
        return matchPath || matchFile;
      });
    }

    // 排序
    result.sort((a, b) => {
      if (this.sortBy === 'name') {
        return a.path.localeCompare(b.path, undefined, { numeric: true, sensitivity: 'base' });
      }
      if (this.sortBy === 'files') {
        return b.directFiles.length - a.directFiles.length;
      }
      if (this.sortBy === 'size') {
        return b.totalBytes - a.totalBytes;
      }
      return 0;
    });

    return result;
  }

  /**
   * 转换合法 ID
   */
  sanitizeId(path) {
    return String(path).replace(/[^a-zA-Z0-9-_]/g, '_');
  }
}

window.FolderRenderer = FolderRenderer;

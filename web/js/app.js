/**
 * FolderSnap - 主控制器应用脚本
 */

document.addEventListener('DOMContentLoaded', () => {
  const scanner = new FolderScanner();
  const renderer = new FolderRenderer();
  const exporter = new FolderExporter();

  // 状态变量
  let currentScanResult = null;
  let currentView = 'cards'; // 'cards' | 'panorama'
  let filterNoise = true;

  // DOM 元素引用
  const btnSelectFolder = document.getElementById('btn-select-folder');
  const btnSelectFolderSecondary = document.getElementById('btn-select-folder-secondary');
  const inputFolderFallback = document.getElementById('input-folder-fallback');
  const btnExportZip = document.getElementById('btn-export-zip');

  const switchNoise = document.getElementById('switch-noise');
  const inputSearch = document.getElementById('input-search');
  const selectSort = document.getElementById('select-sort');

  const viewBtnCards = document.getElementById('view-btn-cards');
  const viewBtnPanorama = document.getElementById('view-btn-panorama');

  const dropzoneEmpty = document.getElementById('dropzone-empty');
  const statsBanner = document.getElementById('stats-banner');
  const controlBar = document.getElementById('control-bar');
  const contentArea = document.getElementById('content-area');
  const cardsGrid = document.getElementById('cards-grid');

  const statFolders = document.getElementById('stat-folders');
  const statFiles = document.getElementById('stat-files');
  const statSize = document.getElementById('stat-size');
  const statRoot = document.getElementById('stat-root');

  const progressModal = document.getElementById('progress-modal');
  const modalTitle = document.getElementById('modal-title');
  const modalDesc = document.getElementById('modal-desc');
  const modalProgressFill = document.getElementById('modal-progress-fill');
  const toastContainer = document.getElementById('toast-container');

  // ==================== Toast 提示系统 ====================
  function showToast(message, type = 'info', duration = 3000) {
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    const iconMap = {
      success: '✅',
      error: '❌',
      info: '💡'
    };
    toast.innerHTML = `<span>${iconMap[type] || '📌'}</span><span>${message}</span>`;
    toastContainer.appendChild(toast);

    setTimeout(() => {
      toast.style.opacity = '0';
      toast.style.transform = 'translateY(10px)';
      toast.style.transition = 'all 0.25s ease';
      setTimeout(() => toast.remove(), 250);
    }, duration);
  }

  // ==================== 进度模态框 ====================
  function showProgressModal(title, desc = '') {
    modalTitle.textContent = title;
    modalDesc.textContent = desc;
    modalProgressFill.style.width = '0%';
    progressModal.classList.add('active');
  }

  function updateProgressModal(percent, desc) {
    modalProgressFill.style.width = `${Math.min(100, Math.max(0, percent))}%`;
    if (desc) modalDesc.textContent = desc;
  }

  function hideProgressModal() {
    progressModal.classList.remove('active');
  }

  // ==================== 文件夹选取模式与即时反馈 ====================
  const selectPickerMode = document.getElementById('select-picker-mode');
  let currentPickerMode = localStorage.getItem('foldersnap_picker_mode') || 'fast';
  if (selectPickerMode) {
    selectPickerMode.value = currentPickerMode;
    selectPickerMode.addEventListener('change', () => {
      currentPickerMode = selectPickerMode.value;
      localStorage.setItem('foldersnap_picker_mode', currentPickerMode);
      showToast(currentPickerMode === 'fast' ? '已切换至「极速模式」：弹窗秒级打开 ⚡' : '已切换至「原生句柄模式」 🚀', 'info');
    });
  }

  let isOpeningFolder = false;

  function setButtonBusy(btn, isBusy) {
    if (!btn) return;
    if (isBusy) {
      btn.dataset.originalHtml = btn.innerHTML;
      btn.innerHTML = `<span>⏳</span><span>正在呼起弹窗...</span>`;
      btn.style.opacity = '0.85';
    } else {
      if (btn.dataset.originalHtml) {
        btn.innerHTML = btn.dataset.originalHtml;
        delete btn.dataset.originalHtml;
      }
      btn.style.opacity = '1';
    }
  }

  async function handleOpenFolder(e) {
    if (e && typeof e.stopPropagation === 'function') {
      e.stopPropagation();
    }
    if (isOpeningFolder) return;
    isOpeningFolder = true;

    const triggerBtn = (e && e.target && e.target.closest('button')) || btnSelectFolder;
    setButtonBusy(triggerBtn, true);

    try {
      // 模式 1: 极速模式（默认）：调用 input.click()，绕过 Chrome File System 权限校验与 Windows Shell 扫描，秒级弹出
      if (currentPickerMode === 'fast') {
        inputFolderFallback.click();
        return;
      }

      // 模式 2: 原生句柄模式：使用带缓存定位的 showDirectoryPicker，避免扫描慢速网络驱动器
      if (window.showDirectoryPicker) {
        try {
          const dirHandle = await window.showDirectoryPicker({
            mode: 'read',
            id: 'foldersnap_picker',
            startIn: 'desktop'
          });
          if (dirHandle) {
            await processDirectoryHandle(dirHandle);
          }
          return;
        } catch (err) {
          if (err.name === 'AbortError') {
            return;
          }
          console.warn('showDirectoryPicker 遇到问题，回退至极速模式:', err);
          inputFolderFallback.click();
        }
      } else {
        inputFolderFallback.click();
      }
    } finally {
      setButtonBusy(triggerBtn, false);
      setTimeout(() => {
        isOpeningFolder = false;
      }, 400);
    }
  }

  async function processDirectoryHandle(dirHandle) {
    showProgressModal('正在深度遍历文件夹...', '准备开始分析文件系统树');

    try {
      const result = await scanner.scanWithFileSystemApi(dirHandle, {
        filterNoise: filterNoise,
        onProgress: (p) => {
          modalDesc.textContent = `已扫描: ${p.foldersCount} 个目录, ${p.filesCount} 个文件\n当前: ${p.currentPath}`;
        }
      });

      applyScanResult(result);
      showToast(`扫描完成！共遍历 ${result.totalFolders} 个子目录，${result.totalFiles} 个文件`, 'success');
    } catch (err) {
      console.error('遍历目录失败:', err);
      showToast(`读取失败: ${err.message}`, 'error', 4000);
    } finally {
      hideProgressModal();
    }
  }

  function handleFileInputChange(event) {
    const files = event.target.files;
    if (!files || files.length === 0) return;

    showProgressModal('正在解析文件树...', '正在读取选择的文件清单');

    setTimeout(() => {
      try {
        const result = scanner.scanWithFileInput(files, {
          filterNoise: filterNoise,
          onProgress: (p) => {
            modalDesc.textContent = `已处理: ${p.foldersCount} 个目录, ${p.filesCount} 个文件`;
          }
        });
        applyScanResult(result);
        showToast(`读取成功！共 ${result.totalFolders} 个子目录，${result.totalFiles} 个文件`, 'success');
      } catch (err) {
        console.error('处理文件出错:', err);
        showToast(`处理失败: ${err.message}`, 'error');
      } finally {
        hideProgressModal();
        inputFolderFallback.value = '';
      }
    }, 50);
  }

  // ==================== 数据装载与渲染 ====================
  function applyScanResult(result) {
    currentScanResult = result;

    // 显示界面各区域
    dropzoneEmpty.style.display = 'none';
    statsBanner.style.display = 'grid';
    controlBar.style.display = 'flex';
    contentArea.style.display = 'flex';
    btnExportZip.disabled = false;

    // 更新看板
    statFolders.textContent = result.totalFolders;
    statFiles.textContent = result.totalFiles;
    statSize.textContent = result.formattedTotalBytes;
    statRoot.textContent = result.rootPath || '-';

    renderCurrentView();
  }

  function renderCurrentView() {
    if (!currentScanResult) return;

    renderer.searchQuery = inputSearch.value.trim();
    renderer.sortBy = selectSort.value;

    if (currentView === 'cards') {
      cardsGrid.style.display = 'grid';
      renderer.renderCardsGrid(currentScanResult, cardsGrid);
    } else {
      cardsGrid.style.display = 'block';
      renderer.renderPanoramaView(currentScanResult, cardsGrid);
    }
  }

  // ==================== 视图切换 ====================
  viewBtnCards.addEventListener('click', () => {
    currentView = 'cards';
    viewBtnCards.classList.add('active');
    viewBtnPanorama.classList.remove('active');
    renderCurrentView();
  });

  viewBtnPanorama.addEventListener('click', () => {
    currentView = 'panorama';
    viewBtnPanorama.classList.add('active');
    viewBtnCards.classList.remove('active');
    renderCurrentView();
  });

  // ==================== 过滤与排序事件 ====================
  switchNoise.addEventListener('change', (e) => {
    filterNoise = e.target.checked;
    showToast(filterNoise ? '已开启开发与系统文件过滤' : '已关闭过滤，将展示所有子文件', 'info');
    // 如果已经有句柄或数据，可重新提示或重新触发解析
    if (currentScanResult) {
      showToast('提示：如需按新过滤规则刷新，请重新点击“选择文件夹”进行读取', 'info', 3500);
    }
  });

  inputSearch.addEventListener('input', () => {
    renderCurrentView();
  });

  selectSort.addEventListener('change', () => {
    renderCurrentView();
  });

  // ==================== 卡片内操作事件代理 ====================
  contentArea.addEventListener('click', async (e) => {
    // 1. 单张卡片截图下载
    const btnDownload = e.target.closest('.btn-download-card');
    if (btnDownload) {
      const card = btnDownload.closest('.snapshot-card');
      const folderPath = card.dataset.path || 'folder';
      const safeName = folderPath.replace(/[/\\:*?"<>|]/g, '_');
      try {
        btnDownload.disabled = true;
        btnDownload.innerHTML = '<span>⏳</span> 生成中';
        await exporter.exportSingleCard(card, `${safeName}-filelist.png`);
        showToast(`已成功下载卡片截图: ${safeName}.png`, 'success');
      } catch (err) {
        console.error(err);
        showToast(`截图失败: ${err.message}`, 'error');
      } finally {
        btnDownload.disabled = false;
        btnDownload.innerHTML = '<span>📸</span> 截图';
      }
      return;
    }

    // 2. 单张卡片复制到剪贴板
    const btnCopy = e.target.closest('.btn-copy-card');
    if (btnCopy) {
      const card = btnCopy.closest('.snapshot-card');
      try {
        btnCopy.disabled = true;
        btnCopy.innerHTML = '<span>⏳</span> 复制中';
        await exporter.copyCardToClipboard(card);
        showToast('截图已成功复制到剪贴板！可直接在聊天或文档中 Ctrl+V 粘贴', 'success');
      } catch (err) {
        console.error(err);
        showToast(err.message, 'error', 3500);
      } finally {
        btnCopy.disabled = false;
        btnCopy.innerHTML = '<span>📋</span> 复制';
      }
      return;
    }

    // 3. 全景长图下载
    const btnPanoDownload = e.target.closest('.btn-download-panorama');
    if (btnPanoDownload) {
      const panoTarget = document.getElementById('panorama-capture-target');
      if (!panoTarget) return;
      try {
        btnPanoDownload.disabled = true;
        btnPanoDownload.innerHTML = '<span>⏳</span> 渲染长图中...';
        showToast('正在渲染全景大长图，可能需要几秒钟...', 'info', 4000);
        const rootName = (currentScanResult?.rootPath || 'panorama').replace(/[/\\:*?"<>|]/g, '_');
        await exporter.exportPanorama(panoTarget, `${rootName}-panorama.png`);
        showToast('全景长图已成功下载！', 'success');
      } catch (err) {
        console.error(err);
        showToast(`导出全景图失败: ${err.message}`, 'error');
      } finally {
        btnPanoDownload.disabled = false;
        btnPanoDownload.innerHTML = '<span>📸</span> 导出全景长图 (PNG)';
      }
      return;
    }

    // 4. 全景长图复制到剪贴板
    const btnPanoCopy = e.target.closest('.btn-copy-panorama');
    if (btnPanoCopy) {
      const panoTarget = document.getElementById('panorama-capture-target');
      if (!panoTarget) return;
      try {
        btnPanoCopy.disabled = true;
        btnPanoCopy.innerHTML = '<span>⏳</span> 复制中...';
        await exporter.copyPanoramaToClipboard(panoTarget);
        showToast('全景长图已复制到剪贴板！', 'success');
      } catch (err) {
        console.error(err);
        showToast(err.message, 'error');
      } finally {
        btnPanoCopy.disabled = false;
        btnPanoCopy.innerHTML = '<span>📋</span> 复制全景图';
      }
      return;
    }
  });

  // ==================== 批量打包 ZIP 导出 ====================
  btnExportZip.addEventListener('click', async () => {
    if (!currentScanResult || !currentScanResult.folders || currentScanResult.folders.length === 0) {
      showToast('当前没有可导出的文件夹数据', 'error');
      return;
    }

    const filteredFolders = renderer.getFilteredAndSortedFolders(currentScanResult.folders);
    if (filteredFolders.length === 0) {
      showToast('当前筛选条件下无文件夹可导出', 'error');
      return;
    }

    showProgressModal('正在批量截图并打包 ZIP...', '正在初始化打包流水线');
    btnExportZip.disabled = true;

    try {
      await exporter.exportAllFoldersAsZip(filteredFolders, renderer, {
        rootName: currentScanResult.rootPath,
        onProgress: (p) => {
          updateProgressModal(p.percent, p.message);
        }
      });
      showToast(`打包完成！已导出包含 ${filteredFolders.length} 个子文件夹截图的 ZIP 包`, 'success');
    } catch (err) {
      console.error('批量导出失败:', err);
      showToast(`打包导出失败: ${err.message}`, 'error', 4000);
    } finally {
      btnExportZip.disabled = false;
      setTimeout(() => hideProgressModal(), 1200);
    }
  });

  // ==================== 拖拽文件夹支持 ====================
  ['dragenter', 'dragover'].forEach(eventName => {
    window.addEventListener(eventName, (e) => {
      e.preventDefault();
      e.stopPropagation();
      dropzoneEmpty.classList.add('dragover');
    }, false);
  });

  ['dragleave', 'drop'].forEach(eventName => {
    window.addEventListener(eventName, (e) => {
      e.preventDefault();
      e.stopPropagation();
      dropzoneEmpty.classList.remove('dragover');
    }, false);
  });

  window.addEventListener('drop', async (e) => {
    const items = e.dataTransfer.items;
    if (!items || items.length === 0) return;

    // 尝试获取文件夹句柄 (Chromium API)
    for (const item of items) {
      if (item.kind === 'file') {
        if (typeof item.getAsFileSystemHandle === 'function') {
          try {
            const handle = await item.getAsFileSystemHandle();
            if (handle && handle.kind === 'directory') {
              await processDirectoryHandle(handle);
              return;
            }
          } catch (err) {
            console.warn('拖拽句柄读取失败:', err);
          }
        }
      }
    }

    showToast('请直接拖拽文件夹，或者点击“选择文件夹”按钮', 'info');
  });

  // ==================== 绑定按钮与事件 ====================
  btnSelectFolder.addEventListener('click', (e) => {
    e.stopPropagation();
    handleOpenFolder(e);
  });

  if (btnSelectFolderSecondary) {
    btnSelectFolderSecondary.addEventListener('click', (e) => {
      e.stopPropagation();
      handleOpenFolder(e);
    });
  }

  dropzoneEmpty.addEventListener('click', (e) => {
    // 点击了内部的任意按钮时不再触发容器点击
    if (e.target.closest('button')) return;
    handleOpenFolder(e);
  });

  inputFolderFallback.addEventListener('change', handleFileInputChange);

  // ==================== 演示数据加载 ====================
  function loadDemoData() {
    const mockFiles = [
      { webkitRelativePath: 'my-project/src/index.ts', size: 1420, lastModified: Date.now() - 3600000 },
      { webkitRelativePath: 'my-project/src/utils.ts', size: 2840, lastModified: Date.now() - 7200000 },
      { webkitRelativePath: 'my-project/src/components/Button.tsx', size: 3200, lastModified: Date.now() - 10000000 },
      { webkitRelativePath: 'my-project/src/components/Card.tsx', size: 4500, lastModified: Date.now() - 12000000 },
      { webkitRelativePath: 'my-project/src/components/Modal.tsx', size: 6800, lastModified: Date.now() - 15000000 },
      { webkitRelativePath: 'my-project/docs/README.md', size: 1800, lastModified: Date.now() - 86400000 },
      { webkitRelativePath: 'my-project/docs/architecture.pdf', size: 245000, lastModified: Date.now() - 90000000 },
      { webkitRelativePath: 'my-project/docs/guide.docx', size: 78000, lastModified: Date.now() - 95000000 },
      { webkitRelativePath: 'my-project/assets/images/logo.svg', size: 8400, lastModified: Date.now() - 5000000 },
      { webkitRelativePath: 'my-project/assets/images/banner.png', size: 154000, lastModified: Date.now() - 6000000 },
      { webkitRelativePath: 'my-project/assets/icons/check.svg', size: 1200, lastModified: Date.now() - 2400000 },
      { webkitRelativePath: 'my-project/assets/icons/cross.svg', size: 1150, lastModified: Date.now() - 2500000 },
      { webkitRelativePath: 'my-project/data/users.json', size: 12800, lastModified: Date.now() - 2000000 },
      { webkitRelativePath: 'my-project/data/records.csv', size: 45600, lastModified: Date.now() - 1800000 },
      { webkitRelativePath: 'my-project/data/config.yaml', size: 950, lastModified: Date.now() - 1500000 }
    ];

    const result = scanner.scanWithFileInput(mockFiles, { filterNoise: filterNoise });
    applyScanResult(result);
    showToast('已成功加载「my-project」演示数据，欢迎体验卡片与批量截图！', 'success', 3500);
  }

  const btnLoadDemo = document.getElementById('btn-load-demo');
  if (btnLoadDemo) {
    btnLoadDemo.addEventListener('click', loadDemoData);
  }

  // 自动检测 URL 是否包含 demo 参数
  if (window.location.search.includes('demo') || window.location.hash.includes('demo')) {
    loadDemoData();
  }

  // 暴露全局访问接口方便调试或自动化测试
  window.FolderSnapApp = {
    loadDemoData,
    applyScanResult,
    scanner,
    renderer,
    exporter
  };

  // 全局快捷键
  window.addEventListener('keydown', (e) => {
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'o') {
      e.preventDefault();
      handleOpenFolder();
    }
    if (e.key === 'Escape') {
      hideProgressModal();
    }
  });
});

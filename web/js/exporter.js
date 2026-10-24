/**
 * FolderSnap - 高倍率截图捕获与 ZIP 批量打包导出引擎
 */

class FolderExporter {
  constructor() {
    this.offscreenHost = document.getElementById('offscreen-capture-host');
  }

  /**
   * 将任意 DOM 节点克隆至离屏容器并进行高倍率渲染
   */
  async renderElementToCanvas(element, options = {}) {
    const scale = options.scale || 2;
    const hideSelectors = options.hideSelectors || ['.card-actions', '.panorama-actions'];

    // 克隆节点至离屏宿主容器
    const clone = element.cloneNode(true);
    clone.classList.add('capture-mode');

    // 隐藏无需在截图中出现的按钮
    for (const selector of hideSelectors) {
      const el = clone.querySelector(selector);
      if (el) el.remove();
    }

    this.offscreenHost.innerHTML = '';
    this.offscreenHost.appendChild(clone);

    // 等待一帧让样式计算稳定
    await new Promise(r => requestAnimationFrame(() => setTimeout(r, 60)));

    try {
      if (typeof window.html2canvas !== 'function') {
        throw new Error('未加载 html2canvas 渲染引擎，请检查 libs 目录');
      }

      const canvas = await window.html2canvas(clone, {
        scale: scale,
        useCORS: true,
        allowTaint: true,
        backgroundColor: '#0f121a',
        logging: false
      });

      return canvas;
    } finally {
      this.offscreenHost.innerHTML = '';
    }
  }

  /**
   * 将 Canvas 转换为 Blob
   */
  canvasToBlob(canvas, type = 'image/png', quality = 0.95) {
    return new Promise((resolve) => {
      canvas.toBlob((blob) => resolve(blob), type, quality);
    });
  }

  /**
   * 触发浏览器下载 Blob 文件
   */
  downloadBlob(blob, filename) {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  /**
   * 导出单张卡片截图为 PNG 文件
   */
  async exportSingleCard(cardElement, filename) {
    const canvas = await this.renderElementToCanvas(cardElement, { scale: 2 });
    const blob = await this.canvasToBlob(canvas);
    this.downloadBlob(blob, filename || 'folder-filelist.png');
    return blob;
  }

  /**
   * 复制单张卡片截图到剪贴板
   */
  async copyCardToClipboard(cardElement) {
    const canvas = await this.renderElementToCanvas(cardElement, { scale: 2 });
    const blob = await this.canvasToBlob(canvas);

    if (!navigator.clipboard || !window.ClipboardItem) {
      throw new Error('当前浏览器环境不支持直接写入图片至剪贴板，请使用下载截图按钮');
    }

    const item = new ClipboardItem({ 'image/png': blob });
    await navigator.clipboard.write([item]);
    return true;
  }

  /**
   * 导出整幅全景长图
   */
  async exportPanorama(panoramaElement, filename) {
    const canvas = await this.renderElementToCanvas(panoramaElement, { scale: 1.8 });
    const blob = await this.canvasToBlob(canvas);
    this.downloadBlob(blob, filename || 'foldersnap-panorama.png');
    return blob;
  }

  /**
   * 复制全景长图至剪贴板
   */
  async copyPanoramaToClipboard(panoramaElement) {
    const canvas = await this.renderElementToCanvas(panoramaElement, { scale: 1.8 });
    const blob = await this.canvasToBlob(canvas);
    if (!navigator.clipboard || !window.ClipboardItem) {
      throw new Error('当前浏览器不支持直接复制图片至剪贴板');
    }
    const item = new ClipboardItem({ 'image/png': blob });
    await navigator.clipboard.write([item]);
    return true;
  }

  /**
   * 批量将所有子文件夹卡片渲染为图片并打包为 ZIP 压缩包
   */
  async exportAllFoldersAsZip(folders, renderer, options = {}) {
    if (typeof window.JSZip !== 'function') {
      throw new Error('未加载 JSZip 库，请检查 libs 目录');
    }

    const onProgress = options.onProgress || (() => {});
    const zip = new window.JSZip();
    const folderImageGroup = zip.folder('folder-screenshots');

    const total = folders.length;

    for (let i = 0; i < total; i++) {
      const folder = folders[i];
      const percent = Math.round(((i + 0.5) / total) * 100);

      onProgress({
        current: i + 1,
        total: total,
        currentPath: folder.path,
        percent: percent,
        message: `正在截取 (${i + 1}/${total}): ${folder.path}`
      });

      // 生成临时卡片节点
      const card = renderer.createFolderCard(folder, { isPanorama: false });
      const canvas = await this.renderElementToCanvas(card, { scale: 2 });
      const blob = await this.canvasToBlob(canvas);

      // 安全文件名
      const safeFilename = `${folder.path.replace(/[/\\:*?"<>|]/g, '_')}-filelist.png`;
      folderImageGroup.file(safeFilename, blob);
    }

    onProgress({
      current: total,
      total: total,
      currentPath: '',
      percent: 98,
      message: '正在打包压缩成 ZIP 文件...'
    });

    const zipBlob = await zip.generateAsync(
      {
        type: 'blob',
        compression: 'DEFLATE',
        compressionOptions: { level: 6 }
      },
      (metadata) => {
        onProgress({
          current: total,
          total: total,
          currentPath: '',
          percent: 90 + Math.round(metadata.percent * 0.1),
          message: `正在生成 ZIP: ${Math.round(metadata.percent)}%`
        });
      }
    );

    const rootName = options.rootName || 'foldersnap';
    const zipName = `${rootName}-all-filelists.zip`;
    this.downloadBlob(zipBlob, zipName);

    onProgress({
      current: total,
      total: total,
      currentPath: '',
      percent: 100,
      message: '批量打包完成并已触发下载！'
    });

    return zipBlob;
  }
}

window.FolderExporter = FolderExporter;

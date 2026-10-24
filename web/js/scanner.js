/**
 * FolderSnap - 文件夹遍历与元数据解析引擎
 */

class FolderScanner {
  constructor() {
    // 默认忽略的系统与依赖目录/文件黑名单
    this.defaultIgnoreList = [
      '.git',
      '.svn',
      '.hg',
      'node_modules',
      '.vscode',
      '.idea',
      'dist',
      'build',
      '__pycache__',
      '.DS_Store',
      'Thumbs.db'
    ];
  }

  /**
   * 判断目录或文件是否应被过滤
   */
  shouldIgnore(name, filterNoise = true) {
    if (!filterNoise) return false;
    const lowerName = name.toLowerCase();
    return this.defaultIgnoreList.some(item => lowerName === item.toLowerCase());
  }

  /**
   * 格式化文件大小
   */
  formatBytes(bytes) {
    if (bytes === 0 || bytes == null || isNaN(bytes)) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(i === 0 ? 0 : 1)) + ' ' + sizes[i];
  }

  /**
   * 格式化时间
   */
  formatDate(timestamp) {
    if (!timestamp) return '-';
    const date = new Date(timestamp);
    if (isNaN(date.getTime())) return '-';
    const y = date.getFullYear();
    const m = String(date.getMonth() + 1).padStart(2, '0');
    const d = String(date.getDate()).padStart(2, '0');
    const hh = String(date.getHours()).padStart(2, '0');
    const mm = String(date.getMinutes()).padStart(2, '0');
    return `${y}-${m}-${d} ${hh}:${mm}`;
  }

  /**
   * 根据扩展名识别分类与图标徽标
   */
  categorizeFile(filename) {
    const dotIdx = filename.lastIndexOf('.');
    const ext = dotIdx > 0 ? filename.slice(dotIdx + 1).toLowerCase() : '';

    const codeExts = ['js', 'ts', 'jsx', 'tsx', 'py', 'java', 'c', 'cpp', 'h', 'cs', 'go', 'rs', 'php', 'rb', 'html', 'css', 'scss', 'vue', 'sh', 'swift', 'kt', 'dart'];
    const imageExts = ['png', 'jpg', 'jpeg', 'gif', 'svg', 'webp', 'bmp', 'ico', 'tiff'];
    const mediaExts = ['mp4', 'mkv', 'mov', 'avi', 'mp3', 'wav', 'flac', 'aac', 'ogg', 'webm'];
    const docExts = ['pdf', 'doc', 'docx', 'ppt', 'pptx', 'txt', 'md', 'rtf', 'odt', 'epub'];
    const dataExts = ['json', 'csv', 'xlsx', 'xls', 'yaml', 'yml', 'xml', 'sql', 'db', 'toml'];
    const archiveExts = ['zip', 'rar', '7z', 'tar', 'gz', 'bz2', 'dmg', 'iso'];

    if (codeExts.includes(ext)) return { category: 'code', label: ext || 'CODE', badgeClass: 'badge-code' };
    if (imageExts.includes(ext)) return { category: 'image', label: ext || 'IMG', badgeClass: 'badge-image' };
    if (mediaExts.includes(ext)) return { category: 'media', label: ext || 'MEDIA', badgeClass: 'badge-media' };
    if (docExts.includes(ext)) return { category: 'doc', label: ext || 'DOC', badgeClass: 'badge-doc' };
    if (dataExts.includes(ext)) return { category: 'data', label: ext || 'DATA', badgeClass: 'badge-data' };
    if (archiveExts.includes(ext)) return { category: 'archive', label: ext || 'ZIP', badgeClass: 'badge-archive' };

    return { category: 'other', label: ext ? ext.slice(0, 4) : 'FILE', badgeClass: 'badge-other' };
  }

  /**
   * 方式 1: 通过 File System Access API (showDirectoryPicker) 原生递归
   */
  async scanWithFileSystemApi(dirHandle, options = {}) {
    const filterNoise = options.filterNoise ?? true;
    const onProgress = options.onProgress || (() => {});

    const rootPath = dirHandle.name;
    const folderMap = new Map(); // path -> DirectoryNode
    let totalFilesFound = 0;
    let totalFoldersFound = 0;

    // 辅助递归函数
    const traverse = async (currentHandle, currentPath, depth = 0) => {
      totalFoldersFound++;
      onProgress({
        currentPath,
        filesCount: totalFilesFound,
        foldersCount: totalFoldersFound
      });

      const folderNode = {
        name: currentHandle.name,
        path: currentPath,
        depth: depth,
        directFiles: [],
        subFolderPaths: [],
        totalBytes: 0,
        allFilesCount: 0
      };

      folderMap.set(currentPath, folderNode);

      const fileHandles = [];
      const subDirHandles = [];

      for await (const entry of currentHandle.values()) {
        if (this.shouldIgnore(entry.name, filterNoise)) {
          continue;
        }

        if (entry.kind === 'file') {
          fileHandles.push(entry);
        } else if (entry.kind === 'directory') {
          subDirHandles.push(entry);
        }
      }

      // 并发分批读取文件元数据，极大提升读取速度（每批 30 个）
      const BATCH_SIZE = 30;
      for (let i = 0; i < fileHandles.length; i += BATCH_SIZE) {
        const batch = fileHandles.slice(i, i + BATCH_SIZE);
        await Promise.all(batch.map(async (entry) => {
          try {
            const file = await entry.getFile();
            const catInfo = this.categorizeFile(file.name);
            const fileEntry = {
              name: file.name,
              path: currentPath ? `${currentPath}/${file.name}` : file.name,
              size: file.size,
              formattedSize: this.formatBytes(file.size),
              lastModified: file.lastModified,
              lastModifiedFormatted: this.formatDate(file.lastModified),
              ...catInfo
            };
            folderNode.directFiles.push(fileEntry);
            folderNode.totalBytes += file.size;
            totalFilesFound++;
          } catch (err) {
            console.warn(`无法读取文件元数据: ${entry.name}`, err);
          }
        }));

        onProgress({
          currentPath,
          filesCount: totalFilesFound,
          foldersCount: totalFoldersFound
        });
      }

      // 递归子目录
      for (const subHandle of subDirHandles) {
        const subPath = currentPath ? `${currentPath}/${subHandle.name}` : subHandle.name;
        folderNode.subFolderPaths.push(subPath);
        await traverse(subHandle, subPath, depth + 1);
      }
    };

    await traverse(dirHandle, rootPath, 0);

    // 汇总计算每个节点包含所有子孙后代的总文件数与总字节
    return this.postProcessResult(folderMap, rootPath);
  }

  /**
   * 方式 2: 通过 <input type="file" webkitdirectory> 兼容回退
   */
  scanWithFileInput(fileList, options = {}) {
    const filterNoise = options.filterNoise ?? true;
    const onProgress = options.onProgress || (() => {});

    const folderMap = new Map();
    let rootPath = '';

    const files = Array.from(fileList);
    let totalFilesFound = 0;

    for (let i = 0; i < files.length; i++) {
      const file = files[i];
      const relPath = file.webkitRelativePath || file.name;
      const parts = relPath.split('/');

      if (!rootPath && parts.length > 1) {
        rootPath = parts[0];
      }

      // 检查路径任一部分是否包含忽略黑名单
      if (filterNoise) {
        const isIgnored = parts.some(p => this.shouldIgnore(p, true));
        if (isIgnored) continue;
      }

      const fileName = parts.pop();
      const folderPath = parts.join('/') || rootPath || 'root';

      // 确保从根到当前目录的各级节点都被创建
      let currentBuildPath = '';
      for (let d = 0; d < parts.length; d++) {
        const partName = parts[d];
        const prevPath = currentBuildPath;
        currentBuildPath = currentBuildPath ? `${currentBuildPath}/${partName}` : partName;

        if (!folderMap.has(currentBuildPath)) {
          folderMap.set(currentBuildPath, {
            name: partName,
            path: currentBuildPath,
            depth: d,
            directFiles: [],
            subFolderPaths: [],
            totalBytes: 0,
            allFilesCount: 0
          });

          if (prevPath && folderMap.has(prevPath)) {
            const parent = folderMap.get(prevPath);
            if (!parent.subFolderPaths.includes(currentBuildPath)) {
              parent.subFolderPaths.push(currentBuildPath);
            }
          }
        }
      }

      const targetFolder = folderMap.get(folderPath);
      if (targetFolder) {
        const catInfo = this.categorizeFile(fileName);
        const fileEntry = {
          name: fileName,
          path: relPath,
          size: file.size,
          formattedSize: this.formatBytes(file.size),
          lastModified: file.lastModified,
          lastModifiedFormatted: this.formatDate(file.lastModified),
          ...catInfo
        };
        targetFolder.directFiles.push(fileEntry);
        targetFolder.totalBytes += file.size;
        totalFilesFound++;
      }

      if (i % 100 === 0) {
        onProgress({
          currentPath: folderPath,
          filesCount: totalFilesFound,
          foldersCount: folderMap.size
        });
      }
    }

    if (!rootPath && folderMap.size > 0) {
      rootPath = folderMap.keys().next().value;
    }

    return this.postProcessResult(folderMap, rootPath);
  }

  /**
   * 后处理计算总统计信息并转为数组列表
   */
  postProcessResult(folderMap, rootPath) {
    const folders = Array.from(folderMap.values());

    // 排序各文件夹内部的文件列表（默认按文件名升序）
    for (const folder of folders) {
      folder.directFiles.sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true, sensitivity: 'base' }));
      folder.formattedTotalBytes = this.formatBytes(folder.totalBytes);
      folder.directFilesCount = folder.directFiles.length;
    }

    // 全局汇总指标
    let totalGlobalFiles = 0;
    let totalGlobalBytes = 0;
    for (const f of folders) {
      totalGlobalFiles += f.directFiles.length;
      totalGlobalBytes += f.totalBytes;
    }

    return {
      rootPath,
      folders,
      totalFolders: folders.length,
      totalFiles: totalGlobalFiles,
      totalBytes: totalGlobalBytes,
      formattedTotalBytes: this.formatBytes(totalGlobalBytes),
      scannedAt: new Date()
    };
  }
}

window.FolderScanner = FolderScanner;

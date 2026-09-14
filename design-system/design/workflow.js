(function (root) {
  'use strict';
  root.designWorkflow = function ({ connected, lost, conflict, saving, dirty, failed, applied }) {
    if (saving) return { text: '正在保存…', label: '正在保存…', action: 'none', disabled: true };
    if (lost) return { text: '暂时无法保存', label: '恢复编辑', action: 'setup' };
    if (!connected) return { text: '尚未开始', label: '开始使用', action: 'setup' };
    if (conflict) return { text: '修改有冲突', label: '查看处理方法', action: 'conflict' };
    if (failed) return { text: '保存未完成', label: '重试保存', action: 'save' };
    if (dirty) return { text: '有修改未保存', label: '保存修改', action: 'save' };
    return { text: applied ? '设计与项目已同步' : '可以交给 AI', label: '复制给 AI', action: 'apply' };
  };
})(globalThis);

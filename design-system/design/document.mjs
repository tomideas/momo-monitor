import { writeFile, rename, unlink, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { randomUUID } from 'node:crypto';

const text = value => String(value ?? '').replaceAll('|', '\\|').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replace(/\r?\n/g, '<br>');
export function renderDesign(data, revision) {
  const lines = ['# ' + text(data.project.projectName) + ' — Design System', '',
    '> 自动生成；请通过编辑器或修改 design-system.json 更新，不要独立编辑本文件。',
    '> Design revision: ' + revision, '', '## 项目与提取范围', ''];
  for (const [key, value] of Object.entries(data.project)) lines.push('- **' + text(key) + '**: ' + text(value));
  lines.push('', '## 设计参数', '', '| 参数 | 值 | 用途 | 来源与代码映射 |', '|---|---|---|---|');
  for (const [key, value] of Object.entries(data.tokens)) {
    const def = data.definitions[key];
    lines.push(`| ${text(key)} | ${text(value)} | ${text(def.label)} — ${text(def.description)} | ${text(def.source)}; ${text(def.mapping || '起步参考，未映射；不能直接套用')} |`);
  }
  for (const [name, values] of Object.entries(data.themes)) {
    lines.push('', '## 主题：' + text(name), '', '未列出的参数继承基础主题。', '');
    for (const [key, value] of Object.entries(values)) lines.push('- ' + text(key) + ': ' + text(value));
  }
  for (const category of ['guidelines', 'components']) {
    lines.push('', category === 'guidelines' ? '## 设计原则与使用边界' : '## 组件与状态', '');
    if (!data[category].length) lines.push('尚未记录；不要把缺失规则当成已确认设计。');
    for (const item of data[category]) {
      lines.push('', '### ' + text(item.name));
      for (const [key, value] of Object.entries(item)) if (key !== 'name') lines.push('- **' + text(key) + '**: ' + text(value));
    }
  }
  lines.push('', '## AI 工作方式', '', 'UI 工作先读 AI.md 与最新 design-system.json。修改软件后，同一任务回写实际变更的共用设计；遇用户未应用的设计差异先比较，不整份覆盖。DESIGN.md 是派生快照，版本不同时以 JSON 为准。', '');
  return lines.join('\n');
}
export async function writeDesign(directory, data, revision) {
  await writeGenerated(directory, 'DESIGN.md', renderDesign(data, revision));
  // Classic local script works for file:// without fetching JSON across origins.
  const snapshot = JSON.stringify({ data, designRevision:revision }).replaceAll('<', '\\u003c').replaceAll('\u2028','\\u2028').replaceAll('\u2029','\\u2029');
  await writeGenerated(directory, 'preview-data.js', '/* Generated from design-system.json. Do not edit. */\nwindow.designSystemSnapshot?.(' + snapshot + ');\n');
}
async function writeGenerated(directory, name, content) {
  const file = join(directory, name);
  if (await readFile(file, 'utf8').catch(() => null) === content) return;
  const temporary = join(directory, '.design-doc-' + randomUUID() + '.tmp');
  try { await writeFile(temporary, content, { flag: 'wx' }); await rename(temporary, file); }
  finally { await unlink(temporary).catch(() => {}); }
}

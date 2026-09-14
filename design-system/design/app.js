(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const clone = value => JSON.parse(JSON.stringify(value));
  // Works at /, /design-system/, and the existing site's extensionless URL.
  const base = new URL(location.href);
  base.hash = ''; base.search = '';
  if (base.pathname.endsWith('/index.html')) base.pathname = base.pathname.slice(0, -10);
  else if (!base.pathname.endsWith('/')) base.pathname += '/';
  const api = new URL('api/design-system', base).href;
  const storageKey = 'project-design-system-v5:' + base.href;
  const tutorialStorageKey = 'momo-design-system:show-tutorial-v1';
  let serviceLost = false;
  const editorTokens = {"primary":["--ds-primary"],"primaryHover":["--ds-primary-hover"],"primarySoft":["--ds-primary-soft"],"workspace":["--ds-workspace"],"background":["--ds-background"],"surface":["--ds-surface"],"borderSoft":["--ds-border-soft"],"border":["--ds-border"],"borderStrong":["--ds-border-strong"],"heading":["--ds-heading"],"text":["--ds-text"],"muted":["--ds-muted"],"tertiary":["--ds-tertiary"],"processing":["--ds-processing"],"processingBorder":["--ds-processing-border"],"processingSoft":["--ds-processing-soft"],"success":["--ds-success"],"warning":["--ds-warning"],"danger":["--ds-danger"],"semantic":["--ds-processing","--ds-success","--ds-warning","--ds-danger"],"displayType":["--ds-font-ui","--ds-display-size","--ds-display-weight"],"titleType":["--ds-font-ui","--ds-title-size","--ds-title-weight"],"bodyType":["--ds-font-ui","--ds-font-size","--ds-line-height"],"labelType":["--ds-label-size","--ds-micro-size","--ds-font-weight"],"fontFamily":["--ds-font-ui","--ds-font-mono"],"spacing":["--ds-space-unit"],"radius":["--ds-radius-control","--ds-radius-card","--ds-radius-modal"],"shadow":["--ds-shadow-dropdown","--ds-shadow-modal"],"controls":["--ds-control-height","--ds-radius-control","--ds-border"],"layout":["--ds-content-width","--ds-column-width"],"buttons":["--ds-primary","--ds-control-height","--ds-radius-control","--ds-font-weight"],"forms":["--ds-control-height","--ds-border","--ds-radius-control","--ds-primary"]};
  let data = null, saved = null, revision = null, designRevision = null, projectFile = null;
  let connected = false, conflict = false, saving = false, timer, theme = '';
  let activeEditor = null, returnFocus = null;
  const undo = [];
  let lastSave = null, saveFailed = false, documentError = null;
  let showTutorialOnStart = true, startupReady = false, tutorialPreferenceReady = !window.designDesktop, startupTutorialHandled = false;
  try { if (!window.designDesktop) showTutorialOnStart = localStorage.getItem(tutorialStorageKey) !== 'false'; } catch {}
  if (window.designDesktop) window.designDesktop.info().then(info => {
    showTutorialOnStart = info.showTutorial !== false;
    tutorialPreferenceReady = true;
    maybeOpenStartupTutorial();
  }).catch(error => { tutorialPreferenceReady = true; notice(error.message); maybeOpenStartupTutorial(); });
  // A local script can supply a navigation link without granting file:// cross-origin writes.
  window.designSystemEditorLink = entry => {
    if (location.protocol !== 'file:' || entry?.source !== new URL('index.html', base).href) return;
    let url;
    try { url = new URL(entry.url); } catch { return; }
    if (url.protocol !== 'http:' || url.hostname !== '127.0.0.1' || !url.searchParams.has('workspace')) return;
    $('localEditorLink').href = url.href;
    $('localEditorEntry').hidden = false;
    const drawerLink = $('drawerLocalLink');
    if (drawerLink) { drawerLink.href = url.href; drawerLink.hidden = false; }
  };
  function checkLocalEntry() {
    if (location.protocol !== 'file:') return;
    const script = document.createElement('script');
    script.src = new URL('editor-link.js?t=' + Date.now(), base).href;
    script.onload = script.onerror = () => script.remove();
    document.head.append(script);
    const snapshot = document.createElement('script');
    snapshot.src = new URL('preview-data.js?t=' + Date.now(), base).href;
    snapshot.onload = snapshot.onerror = () => snapshot.remove();
    document.head.append(snapshot);
  }
  window.designSystemSnapshot = result => {
    if (location.protocol !== 'file:' || !hasShape(result?.data) || !result.designRevision) return;
    if (designRevision === result.designRevision) return;
    data = clone(result.data); saved = clone(data); designRevision = result.designRevision;
    if (!(theme in data.themes)) theme = '';
    render();
  };
  try { lastSave = JSON.parse(sessionStorage.getItem(storageKey + ':saved') || 'null'); } catch {}
  function summarize(before, after) {
    const entries = [];
    for (const section of ['project', 'tokens', 'definitions', 'themes']) {
      for (const key of new Set([...Object.keys(before?.[section] || {}), ...Object.keys(after[section])])) {
        const from = before?.[section]?.[key], to = after[section][key];
        if (JSON.stringify(from) !== JSON.stringify(to)) entries.push({ field: section + '.' + key, before: from, after: to });
      }
    }
    for (const key of ['components', 'guidelines']) if (JSON.stringify(before?.[key]) !== JSON.stringify(after[key])) entries.push({ field: key, before: before?.[key], after: after[key] });
    return entries;
  }
  const content = value => JSON.stringify(value && { project: value.project, tokens: value.tokens, definitions: value.definitions, themes: value.themes, components: value.components, guidelines: value.guidelines });
  const dirty = () => data && content(data) !== content(saved);
  const el = (tag, text, cls) => { const node = document.createElement(tag); if (text !== undefined) node.textContent = text; if (cls) node.className = cls; return node; };
  function notice(message) { $('sessionNotice').textContent = message; $('sessionPanel').hidden = false; }
  function toast(message) { $('toast').textContent = message; $('toast').classList.add('show'); clearTimeout(timer); timer = setTimeout(() => $('toast').classList.remove('show'), 2500); }
  function draft() {
    try { if (dirty()) localStorage.setItem(storageKey, JSON.stringify({ baseRevision: revision, baseline: saved, data })); else localStorage.removeItem(storageKey); }
    catch { notice('暂时无法保留调整，请保持页面打开并点击保存。'); }
    if (window.designDesktop?.setDirty) window.designDesktop.setDirty(dirty()).catch(() => {});
  }
  function checkpoint() { undo.push(clone(data)); if (undo.length > 50) undo.shift(); }
  function changed() { draft(); render(); }
  function edit(mutator) { checkpoint(); mutator(data); changed(); }
  function value(name) { return theme ? data.themes[theme]?.[name] ?? data.tokens[name] : data.tokens[name]; }
  function setValue(name, next) { edit(d => { if (theme) d.themes[theme][name] = next; else d.tokens[name] = next; }); }
  function hasShape(d) {
    const record = value => value && typeof value === 'object' && !Array.isArray(value);
    const strings = value => record(value) && Object.values(value).every(item => typeof item === 'string');
    return d?.schemaVersion === 5 && strings(d.project) && d.project.projectName?.trim() && strings(d.tokens) && Object.keys(d.tokens).length > 0
      && Object.keys(d.tokens).every(name => /^--[a-zA-Z][a-zA-Z0-9_-]*$/.test(name))
      && record(d.definitions) && Object.keys(d.tokens).every(name => strings(d.definitions[name]) && d.definitions[name].label && d.definitions[name].group && ['color', 'text'].includes(d.definitions[name].type))
      && Object.keys(d.definitions).every(name => name in d.tokens)
      && record(d.themes) && Object.entries(d.themes).every(([name, values]) => name.trim() && strings(values) && Object.keys(values).every(key => key in d.tokens))
      && ['components', 'guidelines'].every(key => Array.isArray(d[key]) && d[key].every(item => strings(item) && item.name && item.description));
  }
  async function request(url, options) {
    const response = await fetch(url, { cache: 'no-store', ...options });
    const result = await response.json();
    if (!response.ok) throw Object.assign(new Error(result.error || '请求失败'), { status: response.status });
    return result;
  }
  function accept(result) {
    if (!hasShape(result.data) || typeof result.revision !== 'string') throw new Error('项目数据格式不正确');
    data = clone(result.data); saved = clone(data); revision = result.revision; designRevision = result.designRevision;
    projectFile = result.file;
    documentError = result.documentError;
    connected = true; conflict = false; saveFailed = false;
    serviceLost = false;
    if (!(theme in data.themes)) theme = '';
    draft(); render();
  }
  async function initialize() {
    let cached;
    try { cached = JSON.parse(localStorage.getItem(storageKey) || 'null'); } catch {}
    if (location.protocol !== 'file:') {
      try {
        const result = await request(api); accept(result);
        if (hasShape(cached?.data)) {
          data = cached.data; conflict = cached.baseRevision !== revision;
          if (conflict && hasShape(cached.baseline)) saved = cached.baseline;
          draft(); render();
        }
        return;
      } catch (error) {
        // Static hosting remains useful for preview, but does not imply file-write access.
        try {
          const source = await request(new URL('design-system.json', base));
          if (!hasShape(source)) throw new Error('无效规范');
          data = source; saved = clone(source);
        } catch {}
      }
    }
    if (location.protocol !== 'file:' && hasShape(cached?.data)) { data = cached.data; revision = cached.baseRevision; }
    render();
  }
  async function save() {
    if (saving) return false;
    if (!data) { openAI(); return false; }
    if (conflict) { notice('AI 同时更新了设计，你的调整已保留。点击顶部「查看处理方法」，保留调整后再处理。'); return false; }
    if (!connected) {
      try {
        const latest = await request(api);
        if (content(saved) !== content(latest.data)) { conflict = true; renderStatus(); return false; }
        revision = latest.revision; designRevision = latest.designRevision; projectFile = latest.file; connected = true;
      } catch { notice('暂时无法保存。请重新打开 Momo Design System；你的调整仍保留在这里。'); return false; }
    }
    if (!dirty()) return true;
    const invalid = $('editorFields').querySelector('input:invalid');
    if (invalid) { invalid.reportValidity(); return false; }
    saving = true; saveFailed = false; renderStatus();
    const sent = clone(data), previous = clone(saved);
    try {
      const result = await request(api, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ baseRevision: revision, file: projectFile, data: sent, actor: 'visual-editor' }) });
      documentError = result.documentError;
      revision = result.revision; designRevision = result.designRevision; saved = clone(result.data);
      serviceLost = false;
      data.updatedAt = saved.updatedAt; data.updatedBy = saved.updatedBy; data.application = saved.application;
      lastSave = { designRevision, revision, changes: summarize(previous, saved) };
      try { sessionStorage.setItem(storageKey + ':saved', JSON.stringify(lastSave)); } catch {}
      draft();
    } catch (error) {
      if (error.status === 409) conflict = true;
      saving = false; draft(); renderStatus('保存未完成，你的调整仍在。请重试保存；如有同步冲突，可让 AI 协助处理。'); return false;
    }
    saving = false; renderStatus(); toast(dirty() ? '已保存刚才的调整；新调整请再次保存。' : '已保存');
    if (!dirty()) { closeDrawer(); openAI('update'); }
    return !dirty();
  }
  function mergeRemote(result) {
    let collision = false;
    const equal = (a, b) => JSON.stringify(a) === JSON.stringify(b);
    const object = item => item && typeof item === 'object' && !Array.isArray(item);
    function merge(before, local, remote) {
      if (equal(local, before)) return remote;
      if (equal(remote, before) || equal(local, remote)) return local;
      if ([before, local, remote].every(object)) {
        const out = {};
        for (const key of new Set([...Object.keys(before), ...Object.keys(local), ...Object.keys(remote)])) {
          const value = merge(before[key], local[key], remote[key]);
          if (value !== undefined) out[key] = value;
        }
        return out;
      }
      collision = true; return local;
    }
    const merged = merge(saved, data, result.data);
    if (collision) { conflict = true; draft(); renderStatus(); return; }
    accept(result); data = merged; undo.length = 0; draft(); render();
  }
  async function checkExternal() {
    if (location.protocol === 'file:' || saving || document.hidden) return;
    try {
      const result = await request(api);
      if (!hasShape(result.data)) throw new Error('服务未就绪');
      documentError = result.documentError;
      if (!connected) { if (dirty()) { connected = true; mergeRemote(result); } else accept(result); }
      serviceLost = false; renderStatus();
      if (result.revision === revision) return;
      if (dirty()) { mergeRemote(result); }
      else {
        accept(result); undo.length = 0;
        if (activeEditor && activeEditor !== 'ai') closeDrawer();
        toast('已更新设计');
      }
    } catch (error) {
      if (connected) serviceLost = true;
      renderStatus();
    }
  }
  const needsSetup = () => connected && data && !data.project.sourceEntry && data.project.designStatus !== 'imported' && !Object.values(data.definitions).some(def => def.mapping);
  function renderStatus(error) {
    if (error) saveFailed = true;
    const state = designWorkflow({ connected, lost: serviceLost, conflict, saving, dirty: Boolean(dirty()), failed: saveFailed, applied: Boolean(designRevision && saved?.application?.designRevision === designRevision) });
    if (location.protocol === 'file:' && data) Object.assign(state, { text:'已保存设计的预览', label:'如何编辑', action:'setup', disabled:false });
    if (window.designDesktop && needsSetup() && !dirty() && !saving && !serviceLost && !conflict && !saveFailed) Object.assign(state, { text:'待整理项目设计', label:'开始使用', action:'setup', disabled:false });
    $('primaryAction').textContent = state.label;
    $('primaryAction').disabled = Boolean(state.disabled);
    $('primaryAction').dataset.action = state.action;
    $('refreshAI').disabled = !connected || serviceLost || dirty() || conflict || saving;
    $('exportDesign').disabled = !connected || serviceLost || dirty() || conflict || saving;
    $('refreshHint').textContent = !connected || serviceLost ? '先恢复编辑服务，再使用此功能。' : dirty() ? '先保存当前调整，再从软件更新。' : conflict ? '先处理当前冲突。' : '仅在软件已于其他任务改变外观时使用。';
    if (conflict) notice('AI 同时更新了设计，你的调整已保留。点击顶部「查看处理方法」，先保留你尚未保存的调整。');
    else if (error) notice(error);
    else if (documentError) notice('设计数据已保存，但 DESIGN.md 或 HTML 预览快照更新失败：' + documentError + '。修复文件写入权限后会重新生成。');
    else $('sessionPanel').hidden = true;
  }
  function card(name) {
    const def = data.definitions[name], button = el('button', undefined, 'extra-card');
    button.type = 'button'; button.setAttribute('aria-label', '编辑 ' + def.label);
    if (def.type === 'color') { const sample = el('span', undefined, 'color-preview'); sample.style.background = value(name); button.append(sample); }
    button.append(el('strong', def.label), el('code', value(name)), el('small', def.description || name), el('small', def.mapping ? '已对应软件样式' : '起步参考 · 尚未对应软件'));
    button.addEventListener('click', () => openTokenEditor([name], def.label));
    return button;
  }
  function render() {
    if (data) {
      for (const name of Object.keys(data.tokens)) document.documentElement.style.setProperty(name, value(name));
      document.querySelectorAll('[data-token-value]').forEach(node => node.textContent = value(node.dataset.tokenValue) || '未定义');
      const colorGroups = ['colors-brand', 'colors-surfaces', 'colors-borders', 'colors-text', 'colors-semantic'];
      for (const group of colorGroups) {
        const host = document.querySelector('#' + group + ' .palette');
        // Keep the HTML swatches and their edit handlers; data changes values, not layout.
        host.querySelectorAll('[data-added-swatch]').forEach(node => node.remove());
        const names = Object.keys(data.tokens).filter(name => data.definitions[name]?.group === group);
        const existing = new Set();
        for (const node of host.querySelectorAll('.swatch')) {
          const name = node.querySelector('[data-token-value]')?.dataset.tokenValue;
          node.hidden = !names.includes(name); existing.add(name);
          if (!node.hidden) node.title = data.definitions[name].description || '';
        }
        for (const name of names.filter(name => !existing.has(name))) {
          const button = el('button', undefined, 'swatch editable'); button.type = 'button'; button.dataset.addedSwatch = '';
          button.style.setProperty('--swatch', value(name));
          button.setAttribute('aria-label', '编辑 ' + data.definitions[name].label);
          button.append(el('span', '编辑 Edit', 'edit-hint'), el('strong', data.definitions[name].label), el('code', value(name)));
          button.addEventListener('click', () => openTokenEditor([name], data.definitions[name].label)); host.append(button);
        }
        host.style.setProperty('--palette-count', Math.max(1, names.length));
      }
      const host = $('extensionsHost'); host.replaceChildren();
      const groups = [...new Set(Object.values(data.definitions).map(def => def.group))].filter(group => !colorGroups.includes(group));
      for (const group of groups) {
        host.append(el('h3', group));
        const grid = el('div', undefined, 'extra-grid');
        grid.append(...Object.keys(data.tokens).filter(name => data.definitions[name]?.group === group).map(card)); host.append(grid);
      }
      for (const category of ['components', 'guidelines']) {
        host.append(el('h3', category === 'components' ? '项目组件' : '项目规范'));
        if (!data[category].length) host.append(el('p', '尚未录入。让 AI 根据实际项目增加，避免把示例当成真实组件。'));
        const grid = el('div', undefined, 'extra-grid');
        data[category].forEach((item, index) => {
          const button = el('button', undefined, 'extra-card'); button.type = 'button';
          button.append(el('strong', item.name), el('span', item.description), el('small', '来源：' + (item.source || '待确认')));
          button.addEventListener('click', () => openRecordEditor(category, index));
          const wrapper = el('div'); wrapper.append(button);
          if (/^(components|guidelines)\/[\w\u0080-\uFFFF./ -]+\.(html|md)$/.test(item.preview || '') && !item.preview.split('/').includes('..')) {
            const link = el('a', '打开真实样本'); link.href = new URL(item.preview, base).href; link.target = '_blank'; link.rel = 'noopener'; wrapper.append(link);
          }
          grid.append(wrapper);
        }); host.append(grid);
      }
    } else {
      const defaults = getComputedStyle(document.documentElement);
      document.querySelectorAll('[data-token-value]').forEach(node => node.textContent = defaults.getPropertyValue(node.dataset.tokenValue).trim());
    }
    renderStatus();
  }
  function showDrawer(title, intro, key) {
    $('editorDrawer').classList.remove('tutorial-drawer');
    $('moreActions').open = false;
    document.querySelectorAll('.ai-copy-action,.drawer-support-link').forEach(node => node.remove());
    if (!activeEditor) returnFocus = document.activeElement;
    activeEditor = key;
    $('drawerTitle').textContent = title; $('drawerSubtitle').textContent = 'Project Design System'; $('drawerIntro').textContent = intro;
    $('editorFields').hidden = false; $('editorFields').replaceChildren();
    $('undoButton').hidden = key === 'ai'; $('drawerDone').hidden = key === 'ai'; $('drawerDone').textContent = '保存';
    $('editorDrawer').classList.add('open'); $('drawerBackdrop').classList.add('open'); document.body.classList.add('modal-open');
    $('editorDrawer').setAttribute('aria-hidden', 'false'); $('drawerClose').focus();
  }
  function closeDrawer() {
    $('editorDrawer').classList.remove('open'); $('drawerBackdrop').classList.remove('open'); document.body.classList.remove('modal-open');
    $('editorDrawer').setAttribute('aria-hidden', 'true'); activeEditor = null; returnFocus?.focus();
  }
  async function setTutorialPreference(show) {
    showTutorialOnStart = Boolean(show);
    try { localStorage.setItem(tutorialStorageKey, String(showTutorialOnStart)); } catch {}
    if (window.designDesktop?.setShowTutorial) await window.designDesktop.setShowTutorial(showTutorialOnStart);
  }
  function maybeOpenStartupTutorial() {
    if (startupTutorialHandled || !startupReady || !tutorialPreferenceReady) return;
    startupTutorialHandled = true;
    if (showTutorialOnStart && window.designDesktop) openTutorial(true);
  }
  async function openTutorial(startup = false) {
    showDrawer('新手教程', '复制提示词，贴给 AI；需要时再回来调整设计。', 'ai');
    $('editorDrawer').classList.add('tutorial-drawer');
    $('editorFields').textContent = '正在打开教程…';
    try {
      const response = await fetch(new URL('tutorial.html', base), {cache:'no-store'});
      if (!response.ok) throw new Error('无法读取教程');
      const parsed = new DOMParser().parseFromString(await response.text(), 'text/html');
      const main = parsed.querySelector('main');
      if (!main?.querySelector('#help')) throw new Error('教程内容不完整');
      if (!$('editorDrawer').classList.contains('tutorial-drawer')) return;
      const content = el('div', undefined, 'tutorial-content');
      for (const node of main.querySelectorAll('script,iframe,object,embed')) node.remove();
      for (const node of main.querySelectorAll('*')) for (const attr of [...node.attributes]) if (attr.name.startsWith('on')) node.removeAttribute(attr.name);
      content.append(...main.childNodes);
      content.addEventListener('click', event => {
        const link = event.target.closest('a'); if (!link) return;
        event.preventDefault();
        const href = link.getAttribute('href');
        if (href?.startsWith('#')) [...content.querySelectorAll('[id]')].find(node => node.id === href.slice(1))?.scrollIntoView({block:'start'});
        else if (href === './index.html') closeDrawer();
      });
      const preference = el('label', undefined, 'tutorial-preference');
      const checkbox = el('input'); checkbox.type = 'checkbox'; checkbox.checked = !showTutorialOnStart;
      const preferenceText = el('span', '不要再提示');
      checkbox.addEventListener('change', async () => {
        checkbox.disabled = true;
        try { await setTutorialPreference(!checkbox.checked); toast(checkbox.checked ? '下次打开不再自动显示教程' : '下次打开会显示教程'); }
        catch (error) { checkbox.checked = !checkbox.checked; notice('无法保存教程设置：' + error.message); }
        checkbox.disabled = false;
      });
      preference.append(checkbox, preferenceText);
      $('editorFields').replaceChildren(preference, content); $('drawerDone').hidden = false; $('drawerDone').textContent = startup ? '开始使用' : '返回设计页';
    } catch (error) { $('editorFields').textContent = '教程暂时无法打开，请重试：' + error.message; }
  }
  function inputField(label, current, onChange, color = false) {
    const wrap = el('div', undefined, 'editor-field'), caption = el('label', label), input = el('input', undefined, 'text-input');
    let picker;
    input.type = 'text'; input.value = current || ''; input.setAttribute('aria-label', label);
    // Commit each valid input so closing the tab immediately does not lose a live preview.
    input.addEventListener('input', () => {
      if (color && !CSS.supports('color', input.value)) { input.setCustomValidity('请输入有效 CSS 颜色'); return; }
      input.setCustomValidity('');
      if (picker && /^#[\da-f]{6}$/i.test(input.value)) picker.value = input.value;
      onChange(input.value);
    });
    caption.append(input); wrap.append(caption);
    if (color) {
      picker = el('input', undefined, 'color-input'); picker.type = 'color'; picker.value = /^#[\da-f]{6}$/i.test(current) ? current : '#000000'; picker.setAttribute('aria-label', label + '拾色器');
      picker.addEventListener('input', () => { input.value = picker.value; input.setCustomValidity(''); onChange(picker.value); }); wrap.append(picker);
    }
    return wrap;
  }
  function openTokenEditor(names, title) {
    if (!data || !connected) return openAI();
    names = names.filter(name => data.definitions[name]);
    showDrawer(title, (theme ? '正在编辑主题「' + theme + '」。未覆盖的值继承基础主题。' : '正在编辑基础主题。') + ' 调整后点击保存。', { kind: 'tokens', names, title });
    for (const name of names) {
      const def = data.definitions[name];
      $('editorFields').append(inputField(def.label, value(name), next => setValue(name, next), def.type === 'color'));
      if (!def.mapping) $('editorFields').append(el('p', '这个参数还没有对应到软件，修改后需要 AI 先确认用途。', 'field-hint'));
      const details = el('details', undefined, 'project-details');
      details.append(el('summary', '用途与技术详情'), el('p', name + ' · ' + (def.description || '') + '\n来源：' + (def.source || '待确认') + '\n代码映射：' + (def.mapping || '尚未建立'), 'field-hint'));
      $('editorFields').append(details);
    }
  }
  function openRecordEditor(category, index) {
    showDrawer(data[category][index].name, '项目资料按数据展示。复杂组件的真实视觉样本由 AI 接入项目后补充。', { kind: 'record', category, index });
    for (const [key, current] of Object.entries(data[category][index])) $('editorFields').append(inputField(key, current, next => edit(d => { d[category][index][key] = next; })));
  }
  function openConflictHelp() {
    showDrawer('你的调整已保留', '你和 AI 改到了同一处。先保留这个页面，不要清除草稿。', 'ai');
    $('editorFields').append(el('p', '回到原来的 AI 对话，说明你修改了哪些设置，以及这里提示“修改有冲突”。浏览器里尚未保存的内容，AI 不能仅靠读取项目文件看到。'));
    const link = el('a', '查看冲突处理教程 ↗', 'tutorial-link');
    link.href = new URL('tutorial.html#help', base).href; link.target = '_blank'; link.rel = 'noopener';
    $('editorFields').append(link); $('drawerDone').hidden = false; $('drawerDone').textContent = '返回我的调整';
  }
  function openAI(mode = 'start') {
    $('moreActions').open = false;
    const isUpdate = connected && !serviceLost && !dirty() && mode === 'update';
    const isRefresh = mode === 'refresh';
    showDrawer(isUpdate ? '让 AI 应用已保存的设计' : isRefresh ? '从项目更新设计系统' : '复制给 AI',
      '复制下面的提示词，贴到正在开发这个项目的 Claude 或 Codex。', 'ai');
    const prompt = [
      '项目设计规范 design-system/ 文件夹已放到项目 root 中。',
      '请先读取 design-system/AI.md 和最新的 design-system/design/design-system.json。',
      isUpdate
        ? '我已在设计系统中保存修改。请按最新规范更新项目代码；保留未映射参数的限制，完成源码修改、构建及真实界面验证后，同步回写实际改变的共享设计。不要用旧代码覆盖我刚保存的设计。'
        : isRefresh
        ? '请按当前项目的真实 UI 和源码更新 design-system/design/design-system.json。先比较现有规范，保留尚未应用的用户修改；有冲突时先问我，不要整份覆盖。此次不要修改产品 UI。'
        : '如果本项目已有 UI 设计或界面，请先问我选择：使用 design-system 更新项目 UI，还是按当前项目更新 design-system。确认方向后再修改，避免覆盖错误的一方。',
      '规范文件固定为项目相对路径 design-system/design/design-system.json。不要依赖、记录或回传 localhost 地址与端口。',
      '在当前项目查找已有的 design-system 文件夹，不要新建、搬迁或复制。找不到或有多份无法确定时，先问我。',
      isUpdate && lastSave?.designRevision === designRevision ? '本次保存摘要：\n' + JSON.stringify(lastSave) : ''
    ].filter(Boolean).join('\n\n');
    const shell = el('section', undefined, 'prompt-shell');
    const toolbar = el('header', undefined, 'prompt-toolbar');
    toolbar.append(el('strong', isUpdate ? '应用设计提示词' : isRefresh ? '更新设计系统提示词' : '项目设计提示词'));
    const copy = el('button', undefined, 'prompt-copy'); copy.type = 'button'; copy.setAttribute('aria-label', '复制提示词');
    copy.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true"><rect x="8" y="8" width="11" height="11" rx="2"></rect><path d="M16 8V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h2"></path></svg><span>复制</span>';
    toolbar.append(copy);
    const area = el('textarea', undefined, 'ai-copy'); area.readOnly = true; area.value = prompt; area.setAttribute('aria-label', '给 AI 的提示词');
    shell.append(toolbar, area);
    const next = el('p', undefined, 'copy-next'); next.id = 'copyNext'; next.hidden = true; next.setAttribute('role', 'status');
    copy.addEventListener('click', async () => {
      try {
        await (window.designDesktop ? window.designDesktop.copyText(area.value) : navigator.clipboard.writeText(area.value));
        copy.querySelector('span').textContent = '已复制'; next.textContent = '已复制。回到 Claude 或 Codex，粘贴并发送。'; next.hidden = false;
      } catch {
        area.focus(); area.select(); next.textContent = '请按 Ctrl+C（Mac 用 ⌘C）复制已选中的提示词。'; next.hidden = false;
      }
    });
    $('editorFields').append(shell, next);
    const help = el('a', '完整教程 ↗', 'tutorial-link drawer-support-link');
    help.href = new URL('tutorial.html', base).href; help.target = '_blank'; help.rel = 'noopener';
    document.querySelector('.drawer-foot').insertBefore(help, $('drawerDone'));
    $('drawerDone').hidden = false; $('drawerDone').textContent = '完成';
  }
  document.querySelectorAll('.editable').forEach(node => {
    const open = () => openTokenEditor(editorTokens[node.dataset.editor] || [], node.getAttribute('aria-label') || '设计参数');
    node.addEventListener('click', open); node.addEventListener('keydown', event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); open(); } });
  });
  async function openSettings() {
    $('moreActions').open = false;
    showDrawer('设置', '管理新手提示和本项目 App 的端口。', 'ai');
    const tutorialGroup = el('section', undefined, 'settings-group');
    tutorialGroup.append(el('h3', '新手教程'));
    const tutorialButton = el('button', '打开新手教程', 'action-button'); tutorialButton.type = 'button';
    tutorialButton.addEventListener('click', () => openTutorial());
    const showLabel = el('label', undefined, 'settings-check');
    const showCheckbox = el('input'); showCheckbox.type = 'checkbox'; showCheckbox.checked = showTutorialOnStart;
    showCheckbox.addEventListener('change', async () => {
      showCheckbox.disabled = true;
      try { await setTutorialPreference(showCheckbox.checked); toast('教程提示设置已保存'); }
      catch (error) { showCheckbox.checked = !showCheckbox.checked; notice('无法保存教程设置：' + error.message); }
      showCheckbox.disabled = false;
    });
    showLabel.append(showCheckbox, el('span', '打开 App 时显示新手教程'));
    tutorialGroup.append(tutorialButton, showLabel);
    $('editorFields').append(tutorialGroup);
    if (window.designDesktop) {
      const info = await window.designDesktop.info();
      const connectionGroup = el('section', undefined, 'settings-group');
      connectionGroup.append(el('h3', 'App 端口'));
      const field = el('input', undefined, 'text-input'); field.type = 'number'; field.min = '0'; field.max = '65535'; field.step = '1'; field.value = info.preferredPort; field.setAttribute('aria-label', '端口，0 表示自动');
      const label = el('label', '端口（0 = 自动）'); label.append(field);
      const message = el('p', '当前使用端口：' + info.port); message.setAttribute('role','status');
      const button = el('button', '保存端口', 'action-button'); button.type = 'button';
      button.addEventListener('click', async () => { if (!field.reportValidity() || field.value === '') return; try { message.textContent = await window.designDesktop.setPort(Number(field.value)); } catch (error) { message.textContent = error.message; } });
      connectionGroup.append(label, message, button); $('editorFields').append(connectionGroup);
    }
    $('drawerDone').hidden = false; $('drawerDone').textContent = '完成';
  }
  $('exportDesign').addEventListener('click', async () => {
    try {
      const response = await fetch(new URL('DESIGN.md', base)); if (!response.ok) throw new Error('无法导出');
      const url = URL.createObjectURL(await response.blob()); const link = el('a'); link.href = url; link.download = 'DESIGN.md'; document.body.append(link); link.click(); link.remove(); setTimeout(() => URL.revokeObjectURL(url), 10000);
      $('moreActions').open = false;
    } catch (error) { notice(error.message); }
  });
  $('appSettings').addEventListener('click', openSettings);
  $('primaryAction').addEventListener('click', () => {
    const action = $('primaryAction').dataset.action;
    if (action === 'save') save();
    else if (action === 'apply') openAI();
    else if (action === 'setup') openAI();
    else if (action === 'conflict') openConflictHelp();
  });
  $('refreshAI').addEventListener('click', () => openAI('refresh'));

  $('drawerClose').addEventListener('click', closeDrawer);
  $('drawerDone').addEventListener('click', async () => { if (activeEditor === 'ai') { closeDrawer(); return; } const hadChanges = dirty(); if (await save() && !hadChanges) closeDrawer(); });
  $('drawerBackdrop').addEventListener('click', closeDrawer);
  const sectionLinks = [...$('nav').querySelectorAll('.nav-item[href^="#"]')];
  const sectionTargets = sectionLinks.map(link => ({ link, section: document.getElementById(link.getAttribute('href').slice(1)) })).filter(item => item.section);
  let navUpdateFrame = 0;
  function showActiveSection(item) {
    for (const entry of sectionTargets) {
      const active = entry === item;
      entry.link.classList.toggle('active', active);
      if (active) entry.link.setAttribute('aria-current', 'location'); else entry.link.removeAttribute('aria-current');
    }
    if (!item) return;
    const nav = $('nav');
    if (getComputedStyle(nav).display === 'flex') {
      if (item.link.offsetLeft < nav.scrollLeft) nav.scrollTo({left:item.link.offsetLeft - 10, behavior:'smooth'});
      else if (item.link.offsetLeft + item.link.offsetWidth > nav.scrollLeft + nav.clientWidth) nav.scrollTo({left:item.link.offsetLeft + item.link.offsetWidth - nav.clientWidth + 10, behavior:'smooth'});
    } else {
      if (item.link.offsetTop < nav.scrollTop + 20) nav.scrollTo({top:item.link.offsetTop - 20, behavior:'smooth'});
      else if (item.link.offsetTop + item.link.offsetHeight > nav.scrollTop + nav.clientHeight - 20) nav.scrollTo({top:item.link.offsetTop + item.link.offsetHeight - nav.clientHeight + 20, behavior:'smooth'});
    }
  }
  function updateActiveSection() {
    navUpdateFrame = 0;
    if (!sectionTargets.length) return;
    const activationLine = Math.min(innerHeight * .42, 360);
    let current = sectionTargets[0];
    for (const item of sectionTargets) {
      if (item.section.getBoundingClientRect().top <= activationLine) current = item;
      else break;
    }
    if (innerHeight + scrollY >= document.documentElement.scrollHeight - 4) current = sectionTargets.at(-1);
    showActiveSection(current);
  }
  function scheduleActiveSection() {
    if (!navUpdateFrame) navUpdateFrame = requestAnimationFrame(updateActiveSection);
  }
  for (const item of sectionTargets) item.link.addEventListener('click', () => showActiveSection(item));
  addEventListener('scroll', scheduleActiveSection, {passive:true});
  addEventListener('resize', scheduleActiveSection);
  addEventListener('hashchange', scheduleActiveSection);
  addEventListener('load', () => {
    const requested = sectionTargets.find(item => '#' + item.section.id === location.hash);
    if (requested) { scrollTo({top:Math.max(0, requested.section.offsetTop - 30), behavior:'instant'}); showActiveSection(requested); }
    else scheduleActiveSection();
  });
  document.addEventListener('click', event => {
    const link = event.target.closest('a');
    if (!link || location.protocol === 'file:') return;
    if (new URL(link.href).pathname.endsWith('/tutorial.html')) { event.preventDefault(); openTutorial(); }
  });
  $('quickSetup').addEventListener('click', () => openAI());
  $('undoButton').addEventListener('click', () => {
    const previous = undo.pop(); if (!previous) return toast('没有可撤销的调整');
    const active = activeEditor; data = previous; changed();
    if (active?.kind === 'tokens') openTokenEditor(active.names, active.title);
    else if (active?.kind === 'record') openRecordEditor(active.category, active.index);

  });
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape') { $('moreActions').open = false; closeDrawer(); }
    if (event.key === 'Tab' && activeEditor) {
      const nodes = [...$('editorDrawer').querySelectorAll('button,input,select,textarea,a')].filter(node => !node.hidden && node.getClientRects().length);
      const first = nodes[0], last = nodes.at(-1);
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    }
  });
  document.addEventListener('click', event => { if (!$('moreActions').contains(event.target)) $('moreActions').open = false; });
  window.addEventListener('beforeunload', event => { if (dirty()) { draft(); event.preventDefault(); event.returnValue = ''; } });
  initialize().finally(() => { startupReady = true; maybeOpenStartupTutorial(); }); setInterval(checkExternal, 3000);
  checkLocalEntry();
  window.addEventListener('focus', checkLocalEntry);
  if (location.protocol === 'file:') setInterval(checkLocalEntry, 3000);
  scheduleActiveSection();
})();

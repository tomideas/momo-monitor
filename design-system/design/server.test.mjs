import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { mkdtemp, readFile, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { createDesignSystemHandler, validate } from './server.mjs';
import { createHash } from 'node:crypto';
import { pathToFileURL } from 'node:url';
import { spawn } from 'node:child_process';

test('versioned design saves, extension data, concurrent conflicts and application evidence', async () => {
  const directory = await mkdtemp(join(tmpdir(), 'design-system-test-'));
  const file = join(directory, 'design-system.json');
  await writeFile(file, await readFile(new URL('./design-system.json', import.meta.url)));
  const server = createServer(createDesignSystemHandler({ directory }));
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const api = 'http://127.0.0.1:' + server.address().port + '/api/design-system';
  const get = async () => (await fetch(api)).json();
  const put = (body, path = '', method = 'PUT', headers = {}) => fetch(api + path, { method, headers: { 'content-type': 'application/json', ...headers }, body: JSON.stringify(body) });
  try {
    const start = await get();
    const workspace = createHash('sha256').update(pathToFileURL(join(directory, 'index.html')).href).digest('hex');
    assert.equal((await fetch(api + '?workspace=' + workspace)).status, 200);
    assert.equal((await fetch(api + '?workspace=other-project')).status, 409);
    const d = structuredClone(start.data);
    d.tokens['--brand-link'] = '#2563eb';
    d.definitions['--brand-link'] = { label: '链接色', type: 'color', group: 'colors-brand', source: 'test.css' };
    d.themes.Dark = { '--brand-link': '#99aaff' };
    d.components.push({ name: 'Card', description: '真实项目卡片', source: 'Card.tsx', states: 'focus, hover' });
    d.guidelines.push({ name: 'Grid', description: '12 columns' });
    d.customMetadata = { preserved: true };
    const first = await put({ baseRevision: start.revision, data: d });
    assert.equal(first.status, 200);
    const updated = await first.json();
    assert.equal(updated.data.tokens['--brand-link'], '#2563eb');
    assert.equal(updated.data.customMetadata.preserved, true);
    assert.equal((await put({ baseRevision: start.revision, data: start.data })).status, 409);
    const concurrent = await Promise.all([put({ baseRevision: updated.revision, data: d }), put({ baseRevision: updated.revision, data: d })]);
    assert.deepEqual(concurrent.map(r => r.status).sort(), [200, 409]);
    const current = await get();
    assert.equal((await put({ baseRevision: current.revision, designRevision: current.designRevision }, '/applied', 'POST')).status, 400);
    assert.equal((await put({ baseRevision: current.revision, designRevision: current.designRevision, files: ['theme.css'], verification: 'browser checked' }, '/applied', 'POST')).status, 200);
    const applied = await get();
    assert.equal(applied.data.application.designRevision, applied.designRevision);
    const changed = structuredClone(applied.data);
    changed.tokens['--brand-link'] = '#ff0000';
    changed.application = { designRevision: 'fake' };
    const changedResponse = await put({ baseRevision: applied.revision, data: changed });
    const changedResult = await changedResponse.json();
    assert.notEqual(changedResult.data.application.designRevision, changedResult.designRevision);
    assert.notEqual(changedResult.data.application.designRevision, 'fake');
    const invalid = structuredClone(changedResult.data);
    invalid.themes.Dark['--missing'] = '#000000';
    assert.equal((await put({ baseRevision: changedResult.revision, data: invalid })).status, 400);
    assert.equal((await put({ baseRevision: changedResult.revision, data: changed }, '', 'PUT', { origin: 'https://unrelated.example' })).status, 403);
    // Offline edits are detected by the exact file revision, including formatting changes.
    await writeFile(file, JSON.stringify(changedResult.data, null, 4));
    assert.equal((await put({ baseRevision: changedResult.revision, data: changed })).status, 409);
    const after = await get();
    assert.equal(after.designRevision, changedResult.designRevision);
  } finally {
    await new Promise(resolve => server.close(resolve));
    await rm(directory, { recursive: true, force: true });
  }
});

test('portable template and current data retain explicit source boundaries', async () => {
  const template = validate(JSON.parse(await readFile(new URL('./template.json', import.meta.url), 'utf8')));
  const current = validate(JSON.parse(await readFile(new URL('./design-system.json', import.meta.url), 'utf8')));
  assert.equal(template.application, null);
  assert.equal(template.project.projectName, '你的项目');
  assert.ok(Object.values(template.definitions).every(def => !def.mapping));
  if (current.project.designStatus === 'imported') {
    assert.ok(current.project.sourceEntry);
    assert.ok(Object.values(current.definitions).some(def => def.mapping));
  }
});

test('standalone service publishes a directory-bound local entry using its assigned port', async () => {
  const directory = await mkdtemp(join(tmpdir(), 'design-system-launch-'));
  for (const file of ['server.mjs', 'document.mjs', 'design-system.json', 'tutorial.html']) await writeFile(join(directory, file), await readFile(new URL('./' + file, import.meta.url)));
  const child = spawn(process.execPath, [join(directory, 'server.mjs')], { env: { ...process.env, DESIGN_SYSTEM_PORT: '0' }, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  try {
    const url = await new Promise((resolve, reject) => {
      let output = '';
      const timeout = setTimeout(() => reject(new Error('server startup timed out')), 10000);
      child.on('error', error => { clearTimeout(timeout); reject(error); });
      child.stdout.on('data', chunk => {
        output += chunk;
        const match = output.match(/Design System: (http:\/\/[^\s]+)/);
        if (match) { clearTimeout(timeout); resolve(match[1]); }
      });
      child.on('exit', code => { clearTimeout(timeout); reject(new Error('early exit: ' + code)); });
    });
    const script = await readFile(join(directory, 'editor-link.js'), 'utf8');
    const entry = JSON.parse(script.slice(script.indexOf('(') + 1, script.lastIndexOf(')')));
    assert.equal(entry.url, url);
    assert.equal(entry.source, pathToFileURL(join(directory, 'index.html')).href);
    assert.notEqual(new URL(url).port, '0');
    const result = await (await fetch(new URL('api/design-system', url))).json();
    assert.equal(result.file, join(directory, 'design-system.json'));
    const tutorial = await fetch(new URL('tutorial.html', url));
    assert.equal(tutorial.status, 200);
    assert.match(tutorial.headers.get('content-type'), /text\/html/);
    const tutorialHtml = await tutorial.text();
    assert.match(tutorialHtml, /开始使用的两个步骤/);
    assert.match(tutorialHtml, /点击「复制给 AI」/);
    assert.match(tutorialHtml, /贴到 Claude 或 Codex/);
    assert.match(tutorialHtml, /id="help"/);
    assert.equal((await fetch(new URL('?workspace=old-project', url))).status, 409);
  } finally {
    if (child.exitCode === null) { const exit = new Promise(resolve => child.once('exit', resolve)); child.kill(); await exit; }
    await rm(directory, { recursive: true, force: true });
  }
});

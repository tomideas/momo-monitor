import { createServer } from 'node:http';
import { readFile, rename, writeFile, unlink, realpath } from 'node:fs/promises';
import { dirname, basename, join, resolve, extname, sep } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createHash, randomUUID } from 'node:crypto';
import { writeDesign, renderDesign } from './document.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const hash = value => createHash('sha256').update(value).digest('hex');
const stable = value => JSON.stringify(value, function (key, item) {
  return item && typeof item === 'object' && !Array.isArray(item)
    ? Object.fromEntries(Object.keys(item).sort().map(k => [k, item[k]])) : item;
});
export const designHash = data => hash(stable({ project: data.project, tokens: data.tokens, definitions: data.definitions, themes: data.themes, components: data.components, guidelines: data.guidelines }));
const fail = (message, status = 400) => Object.assign(new Error(message), { status });
const record = item => item && typeof item === 'object' && !Array.isArray(item);
const strings = object => record(object) && Object.values(object).every(v => typeof v === 'string');

export function validate(data) {
  if (!record(data) || data.schemaVersion !== 5 || !strings(data.project) || !data.project.projectName?.trim()) throw fail('需要 schemaVersion: 5 和项目名称');
  if (!strings(data.tokens) || !Object.keys(data.tokens).length) throw fail('tokens 必须包含字符串值');
  for (const name of Object.keys(data.tokens)) if (!/^--[a-zA-Z][a-zA-Z0-9_-]*$/.test(name)) throw fail('无效 token 名称：' + name);
  if (!record(data.definitions)) throw fail('缺少 definitions');
  for (const [name, def] of Object.entries(data.definitions)) {
    if (!(name in data.tokens) || !strings(def) || !def.label || !def.group || !['color', 'text'].includes(def.type)) throw fail('无效定义：' + name);
  }
  if (Object.keys(data.tokens).some(name => !data.definitions[name])) throw fail('每个 token 都需要 definition');
  if (!record(data.themes)) throw fail('themes 必须为对象');
  for (const [name, values] of Object.entries(data.themes)) {
    if (!name.trim() || !strings(values) || Object.keys(values).some(key => !(key in data.tokens))) throw fail('无效主题：' + name);
  }
  for (const key of ['components', 'guidelines']) {
    if (!Array.isArray(data[key]) || data[key].some(item => !strings(item) || !item.name || !item.description)) throw fail(key + ' 每项需要 name、description 和字符串字段');
  }
  return data;
}

// Mount in an existing development server, or run this file standalone.
export function createDesignSystemHandler({ directory = here, assetDirectory = directory, desktop = false } = {}) {
  directory = resolve(directory);
  const file = join(directory, 'design-system.json');
  const workspace = hash(pathToFileURL(join(directory, 'index.html')).href);
  let queue = Promise.resolve();
  let documentQueue = Promise.resolve();
  const read = () => {
    const operation = documentQueue.then(async () => {
    const source = await readFile(file, 'utf8');
    const data = validate(JSON.parse(source));
    const designRevision = designHash(data);
    let documentError = null;
    try { await writeDesign(directory, data, designRevision); } catch (error) { documentError = error.message; }
    return { data, revision: hash(source), designRevision, file, projectRoot: basename(directory) === 'design' ? dirname(dirname(directory)) : dirname(directory), desktop, documentError };
    });
    documentQueue = operation.catch(() => {});
    return operation;
  };
  const write = async data => {
    const temporary = join(directory, '.design-system-' + randomUUID() + '.tmp');
    try {
      await writeFile(temporary, JSON.stringify(data, null, 2) + '\n', { flag: 'wx' });
      await rename(temporary, file);
    } finally { await unlink(temporary).catch(() => {}); }
    return read();
  };
  const json = (response, status, data) => {
    response.writeHead(status, { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' });
    response.end(JSON.stringify(data));
  };
  return async function handler(request, response) {
    try {
      const url = new URL(request.url, 'http://localhost');
      if (!/^(127\.0\.0\.1|localhost|\[::1\])(:\d+)?$/.test(request.headers.host || '')) throw fail('仅接受本机服务地址', 403);
      const route = url.pathname.replace(/\/$/, '') || '/';
      if (url.searchParams.has('workspace') && url.searchParams.get('workspace') !== workspace) throw fail('此编辑窗口属于另一个项目。请重新打开当前项目的 Momo Design System。', 409);
      if (request.method === 'GET' && route === '/api/design-system/health') return json(response, 200, { ok: true, kind: 'project-design-system', schemaVersion: 5 });
      if (request.method === 'GET' && route === '/api/design-system') return json(response, 200, await read());
      if (request.method === 'GET' && route === '/DESIGN.md') {
        const current = await read();
        response.writeHead(200, { 'content-type': 'text/markdown; charset=utf-8', 'cache-control': 'no-store' });
        return response.end(renderDesign(current.data, current.designRevision));
      }
      if (['PUT', 'POST'].includes(request.method) && ['/api/design-system', '/api/design-system/applied'].includes(route)) {
        const remote = request.socket.remoteAddress;
        if (!['127.0.0.1', '::1', '::ffff:127.0.0.1'].includes(remote)) throw fail('写入仅允许本机连接', 403);
        if (request.headers.origin && request.headers.origin !== 'http://' + request.headers.host && request.headers.origin !== 'https://' + request.headers.host) throw fail('拒绝跨站写入', 403);
        if (!(request.headers['content-type'] || '').startsWith('application/json')) throw fail('需要 application/json', 415);
        let source = '';
        for await (const chunk of request) { source += chunk; if (Buffer.byteLength(source) > 1000000) throw fail('请求超过 1MB', 413); }
        const payload = JSON.parse(source);
        const operation = queue.then(async () => {
          const current = await read();
          if ((desktop || payload.file !== undefined) && payload.file !== current.file) throw fail('请携带 GET 返回的 file，避免更新错误项目。', 409);
          if (!payload.baseRevision || payload.baseRevision !== current.revision) throw fail('规范已由其他窗口或 AI 更新，请先读取最新版本。当前草稿未覆盖项目文件。', 409);
          if (route.endsWith('/applied')) {
            if (request.method !== 'POST') throw fail('请使用 POST', 405);
            if (payload.designRevision !== current.designRevision || !Array.isArray(payload.files) || !payload.files.length || payload.files.some(f => typeof f !== 'string' || !f.trim()) || typeof payload.verification !== 'string' || !payload.verification.trim()) throw fail('需提供当前 designRevision、修改文件和验证说明');
            return write({ ...current.data, application: { designRevision: current.designRevision, files: payload.files, verification: payload.verification, reportedBy: typeof payload.actor === 'string' ? payload.actor : 'AI', appliedAt: new Date().toISOString() } });
          }
          if (request.method !== 'PUT') throw fail('请使用 PUT', 405);
          const data = validate(payload.data);
          return write({ ...data, application: current.data.application || null, updatedAt: new Date().toISOString(), updatedBy: typeof payload.actor === 'string' ? payload.actor : 'editor' });
        });
        queue = operation.catch(() => {});
        return json(response, 200, await operation);
      }
      const assets = { '/': ['index.html', 'text/html'], '/index.html': ['index.html', 'text/html'], '/app.js': ['app.js', 'text/javascript'], '/AI.md': ['AI.md', 'text/plain'], '/README.md': ['README.md', 'text/plain'], '/manifest.json': ['manifest.json', 'application/json'], '/design-system.json': ['design-system.json', 'application/json'] };
      assets['/CONNECT.md'] = ['CONNECT.md', 'text/plain'];
      assets['/tutorial.html'] = ['tutorial.html', 'text/html'];
      assets['/workflow.js'] = ['workflow.js', 'text/javascript'];
      if (request.method === 'GET' && assets[route]) {
        const [name, type] = assets[route];
        const source = await readFile(join(name === 'design-system.json' ? directory : assetDirectory, name));
        response.writeHead(200, { 'content-type': type + '; charset=utf-8', 'cache-control': 'no-store' });
        return response.end(source);
      }
      if (request.method === 'GET' && /^\/(components|guidelines)\//.test(route)) {
        const root = await realpath(directory);
        const target = await realpath(join(directory, decodeURIComponent(route)));
        const types = { '.html': 'text/html', '.md': 'text/plain', '.css': 'text/css', '.js': 'text/javascript', '.svg': 'image/svg+xml', '.png': 'image/png', '.jpg': 'image/jpeg', '.webp': 'image/webp' };
        if (!target.startsWith(root + sep) || !types[extname(target)]) throw fail('无效样本路径', 404);
        const source = await readFile(target);
        response.writeHead(200, { 'content-type': types[extname(target)], 'cache-control': 'no-store' });
        return response.end(source);
      }
      json(response, 404, { error: 'Not found' });
    } catch (error) { json(response, error.status || 400, { error: error.message }); }
  };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const server = createServer(createDesignSystemHandler());
  server.on('error', error => { console.error('无法启动设计系统：' + error.message + '。端口占用时可设置 DESIGN_SYSTEM_PORT。'); process.exitCode = 1; });
  server.listen(Number(process.env.DESIGN_SYSTEM_PORT || 4173), '127.0.0.1', async () => {
    const source = pathToFileURL(join(here, 'index.html')).href;
    const url = 'http://127.0.0.1:' + server.address().port + '/?workspace=' + hash(source);
    try {
      await writeFile(join(here, 'editor-link.js'), 'window.designSystemEditorLink?.(' + JSON.stringify({ source, url }) + ');\n');
    } catch (error) { console.error('浏览器预览入口无法写入：' + error.message); }
    console.log('Design System: ' + url);
    console.log('保存到 ' + join(here, 'design-system.json'));
  });
}

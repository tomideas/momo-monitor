import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { mkdtemp, mkdir, readFile, writeFile, rm, access } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { createDesignSystemHandler, designHash } from './server.mjs';
import { runInNewContext } from 'node:vm';
async function snapshot(directory) {
  let result;
  runInNewContext(await readFile(join(directory,'preview-data.js'),'utf8'), {window:{designSystemSnapshot:value=>{result=value;}}});
  return JSON.parse(JSON.stringify(result));
}

test('nested design folder keeps product root and saves only the nested canonical file', async () => {
  const root = await mkdtemp(join(tmpdir(), 'ds-layout-test-'));
  const directory = join(root, 'design-system', 'design');
  await mkdir(directory, { recursive:true });
  for (const name of ['template.json','index.html','app.js','workflow.js']) await writeFile(join(directory,name),await readFile(new URL('./'+name,import.meta.url)));
  await writeFile(join(directory,'design-system.json'),await readFile(join(directory,'template.json')));
  const server = createServer(createDesignSystemHandler({directory,desktop:true}));
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  const origin = `http://127.0.0.1:${server.address().port}`;
  try {
    const initial = await (await fetch(origin+'/api/design-system')).json();
    assert.equal(initial.projectRoot,root);
    assert.equal(initial.file,join(directory,'design-system.json'));
    assert.equal((await fetch(origin+'/')).status,200);
    initial.data.tokens['--ds-primary']='#123456';
    const response = await fetch(origin+'/api/design-system',{method:'PUT',headers:{'content-type':'application/json'},body:JSON.stringify({file:initial.file,baseRevision:initial.revision,data:initial.data})});
    assert.equal(response.status,200);
    assert.match(await readFile(join(directory,'DESIGN.md'),'utf8'),/#123456/);
    const preview=await snapshot(directory);
    const canonical=JSON.parse(await readFile(initial.file,'utf8'));
    assert.deepEqual(preview.data,canonical);
    assert.equal(preview.designRevision,designHash(canonical));
    await assert.rejects(access(join(root,'design-system','design-system.json')));
  } finally {
    await new Promise(resolve=>server.close(resolve));
    assert.ok(root.startsWith(join(tmpdir(),'ds-layout-test-')));
    await rm(root,{recursive:true,force:true});
  }
});

test('two desktop project services isolate writes and regenerate matching Markdown', async () => {
  const directories = [], servers = [];
  try {
    for (const name of ['First', 'Second']) {
      const directory = await mkdtemp(join(tmpdir(), 'ds-desktop-test-')); directories.push(directory);
      const data = JSON.parse(await readFile(new URL('./template.json', import.meta.url), 'utf8')); data.project.projectName = name;
      await writeFile(join(directory, 'design-system.json'), JSON.stringify(data));
      const server = createServer(createDesignSystemHandler({ directory, desktop:true })); servers.push(server);
      await new Promise(resolve => server.listen(0,'127.0.0.1',resolve));
    }
    const apis = servers.map(s => `http://127.0.0.1:${s.address().port}/api/design-system`);
    assert.notEqual(apis[0], apis[1]);
    const first = await (await fetch(apis[0])).json(), second = await (await fetch(apis[1])).json();
    assert.match(await readFile(join(directories[0], 'DESIGN.md'), 'utf8'), new RegExp(first.designRevision));
    const send = (api, body) => fetch(api, {method:'PUT',headers:{'content-type':'application/json'},body:JSON.stringify(body)});
    assert.equal((await send(apis[1], {file:first.file,baseRevision:second.revision,data:first.data})).status,409);
    first.data.tokens['--ds-primary'] = '#112233';
    const saved = await (await send(apis[0], {file:first.file,baseRevision:first.revision,data:first.data})).json();
    assert.equal(saved.documentError, null);
    const md = await readFile(join(directories[0], 'DESIGN.md'), 'utf8');
    assert.ok(md.includes('#112233') && md.includes(designHash(first.data)));
    assert.equal((await (await fetch(apis[1])).json()).data.project.projectName,'Second');
    const external = saved.data; external.guidelines.push({name:'Density',description:'Compact rows'});
    await writeFile(first.file,JSON.stringify(external));
    await fetch(apis[0]);
    assert.ok((await readFile(join(directories[0], 'DESIGN.md'),'utf8')).includes('Compact rows'));
    assert.deepEqual((await snapshot(directories[0])).data,external);
    await new Promise(resolve => servers[0].close(resolve));
    assert.equal((await fetch(apis[1])).status,200);
  } finally {
    for (const server of servers) if (server.listening) await new Promise(resolve => server.close(resolve));
    for (const directory of directories) await rm(directory,{recursive:true,force:true});
  }
});

test('desktop UI shows a portable AI prompt and persistent tutorial controls', async () => {
  const html = await readFile(new URL('./index.html', import.meta.url), 'utf8');
  const app = await readFile(new URL('./app.js', import.meta.url), 'utf8');
  const host = await readFile(new URL('../node_modules/native-source/windows/MomoDesignSystemHost.cs', import.meta.url), 'utf8');
  assert.match(html, /id="appSettings"[^>]*>设置</);
  assert.doesNotMatch(html, /workflowStatus|AI 未连接|重新让 AI 准备/);
  assert.match(app, /design-system\/design\/design-system\.json/);
  assert.match(app, /aria-label', '复制提示词'/);
  assert.match(app, /完整教程/);
  assert.doesNotMatch(app, /当前编辑入口|desktopInfo\?\.editorUrl|requestId|AI 已连接/);
  assert.match(app, /不要再提示/);
  assert.match(app, /maybeOpenStartupTutorial/);
  assert.match(host, /setShowTutorial/);
  assert.match(host, /ReadShowTutorial/);
});

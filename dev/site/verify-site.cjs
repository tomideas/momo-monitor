/* Run with Node and the bundled Playwright module path in NODE_PATH. */
const { chromium } = require('playwright');
const { pathToFileURL } = require('node:url');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../..');
const out = path.join(__dirname, 'verification');
fs.mkdirSync(out, {recursive:true});

(async () => {
  const browser = await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe', headless:true});
  const context = await browser.newContext({viewport:{width:1440,height:1000}, reducedMotion:'reduce'});
  // The guide must remain usable without network access.
  await context.route(/^https?:/, route => route.abort());
  const page = await context.newPage();
  const errors = [];
  const checks = [];
  page.on('pageerror', e => errors.push(e.message));
  const url = slug => pathToFileURL(path.join(root,'site',slug+'.html')).href;
  const pages = fs.readdirSync(path.join(root,'site')).filter(f => f.endsWith('.html') && f !== 'zh.html');
  for (const file of pages) {
    await page.goto(url(file.slice(0,-5)));
    await page.evaluate(() => document.fonts.ready);
    await page.evaluate(async () => {
      const images = Array.from(document.images);
      for (const img of images) img.loading = 'eager';
      await Promise.all(images.map(img => img.decode().catch(() => {})));
    });
    await page.locator('h1').waitFor();
    const facts = await page.evaluate(() => ({
      language:document.documentElement.lang,
      headings:document.querySelectorAll('h1').length,
      overflow:document.documentElement.scrollWidth > innerWidth,
      badImages:[...document.images].filter(i=>!i.complete||!i.naturalWidth).map(i=>i.src),
      current:document.querySelectorAll('.sidebar [aria-current="page"]').length,
      sections:document.querySelectorAll('main > h2[id]').length,
      toc:document.querySelectorAll('.toc a').length
    }));
    assert.equal(facts.language,'en',file);
    assert.equal(facts.headings,1,file);
    assert.equal(facts.current,1,file);
    assert.equal(facts.sections,facts.toc,file);
    assert.equal(facts.overflow,false,file);
    assert.deepEqual(facts.badImages,[],file);
    checks.push(`PASS desktop offline: ${file}`);
  }
  await page.goto(url('index'));
  await page.screenshot({path:path.join(out,'desktop-home.png'),fullPage:true});
  await page.locator('#guide-search').fill('gpu');
  assert(await page.locator('.nav-group a:visible').count()>0);
  await page.locator('#guide-search').fill('zzzzunfindable');
  assert.equal(await page.locator('.nav-group a:visible').count(),0);
  assert.match(await page.locator('#search-status').textContent(),/No topics found/);
  await page.getByRole('button',{name:'Clear search'}).click();
  assert.equal(await page.locator('#guide-search').inputValue(),'');
  assert.equal(await page.locator('.nav-group a:visible').count(),15);
  assert.equal(await page.evaluate(()=>document.activeElement.id),'guide-search');
  checks.push('PASS topic search, no-results state, clear and focus restoration');
  await page.goto(url('getting-started'));
  assert.equal(await page.locator('.walkthrough-step:visible').count(),1);
  await page.getByRole('button',{name:'Next step',exact:true}).click();
  assert.match(await page.locator('[data-step-status]').textContent(),/Step 2 of 5/);
  await page.getByRole('button',{name:'Next step',exact:true}).focus();
  await page.keyboard.press('Enter');
  assert.match(await page.locator('[data-step-status]').textContent(),/Step 3 of 5/);
  await page.screenshot({path:path.join(out,'walkthrough.png'),fullPage:true});
  await page.getByRole('button',{name:'Next step',exact:true}).click();
  await page.getByRole('button',{name:'Next step',exact:true}).click();
  assert(await page.getByRole('button',{name:'Next step',exact:true}).isDisabled());
  for(let i=0;i<4;i++)await page.getByRole('button',{name:'Previous step',exact:true}).click();
  assert(await page.getByRole('button',{name:'Previous step',exact:true}).isDisabled());
  checks.push('PASS five-step walkthrough, keyboard activation and endpoint states');
  await page.goto(url('tools'));
  await page.evaluate(()=>Object.defineProperty(navigator,'clipboard',{value:{writeText:()=>Promise.reject(new Error('Unavailable'))}}));
  await page.getByRole('button',{name:'Copy commands',exact:true}).first().click();
  assert.match(await page.locator('.copy-status').first().textContent(),/Press Ctrl\+C/);
  assert.match(await page.evaluate(()=>getSelection().toString()),/--dump/);
  checks.push('PASS clipboard-denied fallback selects the actual commands');
  await page.setViewportSize({width:390,height:844});
  for(const file of pages){
    await page.goto(url(file.slice(0,-5)));
    assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false,file);
  }
  await page.goto(url('index'));
  assert.equal(await page.locator('#guide-nav').evaluate(e=>e.inert),true);
  const menu=page.getByRole('button',{name:'Open guide navigation',exact:true});
  await menu.click();
  assert.equal(await page.locator('.menu-toggle').getAttribute('aria-expanded'),'true');
  assert.equal(await page.locator('#guide-nav').evaluate(e=>e.inert),false);
  assert.equal(await page.evaluate(()=>document.activeElement.id),'guide-search');
  await page.screenshot({path:path.join(out,'mobile-menu.png'),fullPage:true});
  await page.keyboard.press('Escape');
  assert.equal(await page.locator('.menu-toggle').getAttribute('aria-expanded'),'false');
  assert.equal(await page.evaluate(()=>document.activeElement.className),'menu-toggle');
  await menu.click();
  await page.locator('.sidebar a[href="fans.html"]').click();
  await page.waitForURL('**/fans.html');
  await page.screenshot({path:path.join(out,'mobile-fans.png'),fullPage:true});
  checks.push('PASS all 15 narrow pages, drawer, inert hidden nav, Escape, focus and topic navigation');
  await page.setViewportSize({width:1440,height:1000});
  await page.goto(url('index')+'#alerts');
  await page.waitForURL('**/alerts.html');
  checks.push('PASS legacy single-page anchor redirects');
  await page.emulateMedia({media:'print'});
  await page.goto(url('getting-started'));
  assert.equal(await page.locator('.walkthrough-step:visible').count(),5);
  checks.push('PASS print shows every walkthrough step');
  const nojs = await browser.newContext({javaScriptEnabled:false,viewport:{width:390,height:844}});
  const plain = await nojs.newPage();
  await plain.goto(url('getting-started'));
  assert.equal(await plain.locator('.walkthrough-step:visible').count(),5);
  assert(await plain.locator('.sidebar').isVisible());
  checks.push('PASS no-JavaScript mobile navigation and full walkthrough');
  assert.deepEqual(errors,[]);
  const report={pages:pages.length,network:'blocked during checks',checks,errors};
  fs.writeFileSync(path.join(out,'browser-checks.json'),JSON.stringify(report,null,2)+'\n');
  console.log(JSON.stringify(report,null,2));
  await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});

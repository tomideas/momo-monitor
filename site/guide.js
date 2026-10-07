/* Offline navigation and user-paced visual guides. No external dependencies. */
(() => {
  'use strict';
  const sidebar = document.querySelector('.sidebar');
  const menu = document.querySelector('.menu-toggle');
  const backdrop = document.querySelector('.backdrop');
  const mobile = window.matchMedia('(max-width: 859px)');
  menu.disabled = false;
  function setMenu(open, restore = false) {
    sidebar.classList.toggle('open', open);
    menu.setAttribute('aria-expanded', String(open));
    menu.setAttribute('aria-label', open ? 'Close guide navigation' : 'Open guide navigation');
    backdrop.hidden = !open;
    sidebar.inert = mobile.matches && !open;
    if (open) sidebar.querySelector('input').focus();
    else if (restore) menu.focus();
  }
  menu.addEventListener('click', () => setMenu(menu.getAttribute('aria-expanded') !== 'true', true));
  backdrop.addEventListener('click', () => setMenu(false, true));
  document.addEventListener('keydown', e => {
    if (e.key === 'Escape' && menu.getAttribute('aria-expanded') === 'true') setMenu(false, true);
  });
  mobile.addEventListener('change', () => setMenu(false));
  setMenu(false);

  const search = document.querySelector('#guide-search');
  const clear = document.querySelector('.search-clear');
  const status = document.querySelector('#search-status');
  clear.disabled = false;
  function filter() {
    const query = search.value.trim().toLowerCase();
    let found = 0;
    document.querySelectorAll('.nav-group a').forEach(link => {
      link.hidden = !(`${link.textContent} ${link.dataset.keywords}`.toLowerCase().includes(query));
      if (!link.hidden) found++;
    });
    document.querySelectorAll('.nav-group').forEach(group => {
      group.hidden = !Array.from(group.querySelectorAll('a')).some(a => !a.hidden);
    });
    clear.hidden = !search.value;
    status.textContent = query ? (found ? `${found} topics found` : 'No topics found. Try “GPU” or “data”.') : '';
  }
  search.addEventListener('input', e => { if (!e.isComposing) filter(); });
  search.addEventListener('compositionend', filter);
  clear.addEventListener('click', () => { search.value = ''; filter(); search.focus(); });

  const toc = document.querySelector('.toc nav');
  const headings = Array.from(document.querySelectorAll('main > h2[id]'));
  headings.forEach(heading => {
    const a = document.createElement('a');
    a.href = `#${heading.id}`;
    a.textContent = heading.textContent;
    toc.append(a);
  });
  if ('IntersectionObserver' in window) {
    const observer = new IntersectionObserver(entries => {
      for (const entry of entries) if (entry.isIntersecting) {
        toc.querySelectorAll('a').forEach(a => a.classList.toggle('active', a.hash === `#${entry.target.id}`));
      }
    }, {rootMargin: '-90px 0px -55% 0px'});
    headings.forEach(h => observer.observe(h));
  }

  document.querySelectorAll('.walkthrough').forEach(viewer => {
    const slides = Array.from(viewer.querySelectorAll('.walkthrough-step'));
    const controls = viewer.querySelector('.walkthrough-controls');
    const prev = viewer.querySelector('[data-prev]');
    const next = viewer.querySelector('[data-next]');
    let index = 0;
    function render() {
      slides.forEach((slide, i) => { slide.hidden = i !== index; });
      prev.disabled = index === 0;
      next.disabled = index === slides.length - 1;
      viewer.querySelector('[data-step-status]').textContent = `Step ${index + 1} of ${slides.length}`;
    }
    prev.addEventListener('click', () => { if (index > 0) { index--; render(); } });
    next.addEventListener('click', () => { if (index < slides.length - 1) { index++; render(); } });
    controls.hidden = false;
    render();
  });

  document.querySelectorAll('.copy-code').forEach(button => {
    button.disabled = false;
    button.addEventListener('click', async () => {
      const block = button.closest('.code-block');
      try {
        await navigator.clipboard.writeText(block.querySelector('code').textContent);
        block.querySelector('.copy-status').textContent = 'Commands copied.';
      } catch {
        const selection = window.getSelection();
        const range = document.createRange();
        range.selectNodeContents(block.querySelector('code'));
        selection.removeAllRanges(); selection.addRange(range);
        block.querySelector('.copy-status').textContent = 'Commands selected. Press Ctrl+C to copy.';
      }
    });
  });

  // Preserve links from the previous single-page guide.
  if (location.pathname.endsWith('/index.html') || location.pathname.endsWith('/')) {
    const legacy = {intro:'index.html#basics',install:'installation.html',quick:'getting-started.html',dashboard:'dashboard.html',skins:'dashboard.html#skins',trends:'process.html',mini:'mini.html',alerts:'alerts.html',info:'info.html',settings:'settings.html',portable:'portable.html',headless:'tools.html',trouble:'troubleshooting.html'};
    const destination = legacy[location.hash.slice(1)];
    if (destination) location.replace(destination);
  }
})();

/* Momo Tools Docs — sidebar nav, search, scroll spy */
try {
(function () {
  var sidebar = document.querySelector('.sidebar');
  if (!sidebar) return;

  setupMenuToggle();
  setupSearch();
  setupScrollSpy();

  function setupMenuToggle() {
    var toggle = document.querySelector('.menu-toggle');
    var backdrop = document.querySelector('.backdrop');
    if (!toggle || !sidebar) return;
    function close() { sidebar.classList.remove('open'); if (backdrop) backdrop.classList.remove('show'); }
    toggle.addEventListener('click', function () {
      sidebar.classList.toggle('open');
      if (backdrop) backdrop.classList.toggle('show');
    });
    if (backdrop) backdrop.addEventListener('click', close);
    sidebar.querySelectorAll('a').forEach(function (a) { a.addEventListener('click', close); });
  }

  function setupSearch() {
    var input = document.querySelector('.sidebar-search input');
    if (!input) return;
    var links = document.querySelectorAll('.sidebar a[href^="#"]');
    var groups = document.querySelectorAll('.sidebar .group-title');
    input.addEventListener('input', function () {
      var q = input.value.trim().toLowerCase();
      links.forEach(function (a) {
        a.classList.toggle('hidden', q !== '' && a.textContent.toLowerCase().indexOf(q) === -1);
      });
      groups.forEach(function (g) {
        var next = g.nextElementSibling;
        var found = false;
        while (next && next.classList && !next.classList.contains('group-title')) {
          if (next.tagName === 'A' && !next.classList.contains('hidden')) { found = true; break; }
          next = next.nextElementSibling;
        }
        g.style.display = found ? '' : 'none';
      });
    });
  }

  function setupScrollSpy() {
    var navLinks = document.querySelectorAll('.sidebar a[href^="#"]');
    var sections = [];
    navLinks.forEach(function (a) {
      var el = document.querySelector(a.getAttribute('href'));
      if (el) sections.push(el);
    });

    function setActive(id) {
      navLinks.forEach(function (a) {
        a.classList.toggle('active', a.getAttribute('href') === '#' + id);
      });
    }

    setActive(location.hash.replace('#', ''));

    if (sections.length && 'IntersectionObserver' in window) {
      var obs = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
          if (entry.isIntersecting) setActive(entry.target.id);
        });
      }, { rootMargin: '-20% 0px -60% 0px' });
      sections.forEach(function (s) { obs.observe(s); });
    }
  }
})();
} catch (e) { console.error('[momo-docs]', e.message, e.stack); }

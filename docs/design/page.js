// 目次の現在位置ハイライト
(function () {
  var links = Array.prototype.slice.call(document.querySelectorAll('nav.toc a[href^="#"]'));
  var map = {};
  links.forEach(function (a) { map[a.getAttribute('href').slice(1)] = a; });
  if (!('IntersectionObserver' in window)) return;
  var io = new IntersectionObserver(function (entries) {
    entries.forEach(function (e) {
      if (e.isIntersecting) {
        links.forEach(function (a) { a.classList.remove('active'); });
        var a = map[e.target.id];
        if (a) a.classList.add('active');
      }
    });
  }, { rootMargin: '-20% 0px -70% 0px' });
  document.querySelectorAll('main section[id]').forEach(function (s) { io.observe(s); });
})();

// 広い画面では目次を常時開く
(function () {
  var d = document.querySelector('nav.toc details');
  if (!d) return;
  var mq = window.matchMedia('(min-width: 960px)');
  function sync() { d.open = mq.matches ? true : false; }
  sync();
  if (mq.addEventListener) mq.addEventListener('change', sync);
})();

// テーマ切替（保存できない環境でも動くようにする）
(function () {
  var root = document.documentElement;
  var btn = document.getElementById('themeBtn');
  try {
    var saved = localStorage.getItem('vision-theme');
    if (saved) root.setAttribute('data-theme', saved);
  } catch (e) {}
  if (!btn) return;
  btn.addEventListener('click', function () {
    var dark = root.getAttribute('data-theme') === 'dark' ||
      (!root.getAttribute('data-theme') && window.matchMedia('(prefers-color-scheme: dark)').matches);
    var next = dark ? 'light' : 'dark';
    root.setAttribute('data-theme', next);
    try { localStorage.setItem('vision-theme', next); } catch (e) {}
  });
})();

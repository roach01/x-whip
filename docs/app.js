(() => {
  const pet = document.getElementById('pet');
  const bubble = document.getElementById('pet-bubble');
  const levels = [...document.querySelectorAll('[data-level]')];
  const countOutput = document.getElementById('demo-count');
  const log = document.getElementById('demo-log');
  const playground = document.getElementById('playground');
  const bossButton = document.getElementById('boss-button');
  const bossView = document.getElementById('boss-view');
  const returnButton = document.getElementById('return-button');
  const originalTitle = document.title;
  const beijingTime = new Intl.DateTimeFormat('zh-CN', {
    timeZone: 'Asia/Shanghai', hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23'
  });
  const faces = [0, 3, 5, 6, 7, 14, 15];
  const lines = [
    '记上了。下一轮，15 分钟后见。',
    '关注、发帖、回复，别光看热闹。',
    '关注完，记得回来点我。',
    '这条能回一句吧？',
    '刷都刷了，发一条吧。',
    '刚才是谁说再刷五分钟？',
    '人在上班。心已经上 X 了。'
  ];
  let bubbleTimer;
  let count = 0;
  let previousScroll = 0;
  function setMood(level) {
    const face = faces[level];
    pet.dataset.mood = String(level);
    pet.style.setProperty('--head-x', `${(face % 4) * 100 / 3}%`);
    pet.style.setProperty('--head-y', `${Math.floor(face / 4) * 100 / 3}%`);
    levels.forEach(button => button.setAttribute('aria-pressed', String(Number(button.dataset.level) === level)));
    bubble.textContent = lines[level];
    bubble.hidden = false;
    clearTimeout(bubbleTimer);
    bubbleTimer = setTimeout(() => { bubble.hidden = true; }, 2800);
  }
  function record(label) {
    count += 1;
    countOutput.textContent = String(count).padStart(2, '0');
    const empty = log.querySelector('.empty-log');
    if (empty) empty.remove();
    const row = document.createElement('li');
    const text = document.createElement('span');
    const timestamp = document.createElement('time');
    text.textContent = `${label} +1`;
    timestamp.dateTime = new Date().toISOString();
    timestamp.textContent = beijingTime.format(new Date());
    row.append(text, timestamp);
    log.prepend(row);
    while (log.children.length > 3) log.lastElementChild.remove();
    setMood(0);
  }
  function openReport() {
    previousScroll = window.scrollY;
    document.querySelectorAll('video').forEach(video => video.pause());
    document.getElementById('report-count').textContent = String(count).padStart(2, '0');
    document.getElementById('report-inline-count').textContent = String(count);
    document.getElementById('report-time').textContent = beijingTime.format(new Date());
    playground.hidden = true;
    bossView.hidden = false;
    document.title = '本周互联网关系维护简报 | 工作台';
    returnButton.focus({ preventScroll: true });
  }
  function closeReport() {
    bossView.hidden = true;
    playground.hidden = false;
    document.title = originalTitle;
    document.documentElement.style.scrollBehavior = 'auto';
    window.scrollTo(0, previousScroll);
    document.documentElement.style.scrollBehavior = '';
    bossButton.focus({ preventScroll: true });
  }
  levels.forEach(button => button.addEventListener('click', () => setMood(Number(button.dataset.level))));
  pet.addEventListener('click', () => record('摸了一笔'));
  document.querySelectorAll('[data-record]').forEach(button => {
    button.addEventListener('click', () => record(`手动记${button.dataset.record}`));
  });
  bossButton.addEventListener('click', openReport);
  returnButton.addEventListener('click', closeReport);
  document.addEventListener('keydown', event => {
    if (bossView.hidden) return;
    if (event.key === 'Escape') { event.preventDefault(); closeReport(); }
    if (event.key === 'Tab') { event.preventDefault(); returnButton.focus(); }
  });
})();

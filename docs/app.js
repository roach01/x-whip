(() => {
  const pet = document.getElementById('pet');
  const bubble = document.getElementById('pet-bubble');
  const levels = [...document.querySelectorAll('[data-level]')];
  const faces = [0, 3, 5, 6, 7, 14, 15];
  const lines = [
    '好的老板。再给你 15 分钟。',
    '今天追热点了吗？',
    '今天交到好朋友了吗？',
    '今天发帖到 200 了吗？',
    '今天关注到 400 了吗？',
    '今天互动到 1500 了吗？',
    '我都上头了！你还没上 X！'
  ];
  let bubbleTimer;
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
  levels.forEach(button => button.addEventListener('click', () => setMood(Number(button.dataset.level))));
  pet.addEventListener('click', () => setMood(0));
})();

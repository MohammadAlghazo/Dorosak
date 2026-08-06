(() => {
  const preference = document.cookie
    .split('; ')
    .find((entry) => entry.startsWith('drs-theme='))
    ?.split('=')[1];
  const theme =
    preference === 'light' || preference === 'dark'
      ? preference
      : matchMedia('(prefers-color-scheme: dark)').matches
        ? 'dark'
        : 'light';
  document.documentElement.dataset.bsTheme = theme;
})();

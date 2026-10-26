'use strict';

// Funções pequenas chamadas pelo C# (IJSRuntime)
window.crankohub = {
  // Aplica o tema salvo ("light" | "dark" | null = segue o Windows)
  aplicarTema(tema) {
    if (tema) document.documentElement.dataset.theme = tema;
    else delete document.documentElement.dataset.theme;
  },

  // Alterna claro/escuro e devolve o novo tema para ser salvo
  alternarTema() {
    const root = document.documentElement;
    const atual = root.dataset.theme || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
    const novo = atual === 'dark' ? 'light' : 'dark';
    root.dataset.theme = novo;
    return novo;
  },

  rolarParaTopo() { window.scrollTo(0, 0); },

  rolarAte(el) { el?.scrollIntoView({ behavior: 'smooth', block: 'center' }); },

  focar(el) { el?.focus(); el?.select?.(); },
};

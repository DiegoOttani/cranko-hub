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

  // Cor de destaque escolhida em Configurações (versões clara/escura e cor do texto sobre ela)
  aplicarDestaque(claro, escuro, texto) {
    const s = document.documentElement.style;
    s.setProperty('--destaque-claro', claro);
    s.setProperty('--destaque-escuro', escuro);
    s.setProperty('--destaque-texto', texto);
  },

  rolarParaTopo() { window.scrollTo(0, 0); },

  rolarAte(el) { el?.scrollIntoView({ behavior: 'smooth', block: 'center' }); },

  focar(el) { el?.focus(); el?.select?.(); },
};

// Enter em campos marcados com data-sem-enviar (ex.: novo item do checklist) não envia o formulário
document.addEventListener('keydown', e => {
  if (e.key === 'Enter' && e.target.closest?.('[data-sem-enviar]')) e.preventDefault();
}, true);

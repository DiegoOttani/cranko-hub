'use strict';

// Utilitários mínimos usados pelos gráficos (o resto da formatação é feito no C#)
const U = {
  DAY: 864e5,
  MESES: ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'],
  esc(s) {
    return String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  },
  num(v, d = 1) {
    if (v == null || !isFinite(v)) return '—';
    v = +v.toFixed(d) || 0; // evita "-0,0"
    return v.toLocaleString('pt-BR', { minimumFractionDigits: d, maximumFractionDigits: d });
  },
  money(v, d = 2) {
    if (v == null || !isFinite(v)) return '—';
    return v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL', minimumFractionDigits: d, maximumFractionDigits: d });
  },
  // "AAAA-MM-DD" -> timestamp (horário local)
  ts(s) {
    const [y, m, d] = String(s).slice(0, 10).split('-').map(Number);
    return new Date(y, m - 1, d).getTime();
  },
  dateBR(v) { return new Date(v).toLocaleDateString('pt-BR'); },
  dateShort(v) { const d = new Date(v); return `${String(d.getDate()).padStart(2, '0')} ${U.MESES[d.getMonth()]}`; },
  // Converte a "receita" de formatação vinda do C# (FormatoNumero) em função
  fmt(spec, fallback) {
    if (!spec) return fallback;
    const { casas = 1, sufixo = '', prefixo = '', moeda = false, sinal = false } = spec;
    return v => (sinal && v > 0 ? '+' : '') + prefixo + (moeda ? U.money(v, casas) : U.num(v, casas)) + sufixo;
  },
};

// Mini biblioteca de gráficos em SVG (sem dependências, funciona offline)
const Charts = (() => {
  const NS = 'http://www.w3.org/2000/svg';

  const el = (tag, attrs = {}, parent) => {
    const e = document.createElementNS(NS, tag);
    for (const k in attrs) e.setAttribute(k, attrs[k]);
    if (parent) parent.appendChild(e);
    return e;
  };
  const text = (parent, x, y, str, cls, anchor = 'middle') => {
    const t = el('text', { x, y, class: cls, 'text-anchor': anchor }, parent);
    t.textContent = str;
    return t;
  };

  // Escala "bonita" para o eixo Y
  function nice(min, max, count = 5) {
    if (min === max) { min -= 1; max += 1; }
    const raw = (max - min) / count;
    const mag = 10 ** Math.floor(Math.log10(raw));
    const err = raw / mag;
    const step = (err >= 7.5 ? 10 : err >= 3.5 ? 5 : err >= 1.5 ? 2 : 1) * mag;
    const lo = Math.floor(min / step) * step, hi = Math.ceil(max / step) * step;
    const ticks = [];
    for (let v = lo; v <= hi + step / 2; v += step) ticks.push(+v.toFixed(10));
    return { lo, hi, ticks };
  }

  // Desenha e redesenha quando o container muda de largura
  function mount(container, draw) {
    container.classList.add('chart');
    container._render = () => {
      container.innerHTML = '';
      container.classList.remove('hovering');
      draw(container, Math.max(container.clientWidth, 240));
    };
    container._render();
    if (!container._ro && 'ResizeObserver' in window) {
      let w = container.clientWidth;
      container._ro = new ResizeObserver(() => {
        const nw = container.clientWidth;
        if (nw && Math.abs(nw - w) > 4) { w = nw; container._render(); }
      });
      container._ro.observe(container);
    }
  }

  function tooltip(root) {
    const t = document.createElement('div');
    t.className = 'chart-tip';
    root.appendChild(t);
    return {
      show(html, x, y, W) {
        t.innerHTML = html;
        t.classList.add('show');
        const tw = t.offsetWidth;
        let left = x + 14;
        if (left + tw > W) left = x - tw - 14;
        t.style.left = Math.max(0, left) + 'px';
        t.style.top = y + 'px';
      },
      hide() { t.classList.remove('show'); },
    };
  }
  const tipRow = (color, name, val) =>
    `<div class="tip-row"><i style="background:${color}"></i><span>${U.esc(name)}</span><b>${val}</b></div>`;

  const empty = (root, msg) => { root.innerHTML = `<div class="chart-empty">${U.esc(msg)}</div>`; };

  /**
   * Gráfico de linhas no tempo.
   * series: [{ name, color, points: [{x: timestamp, y, note?}], markers?, area? }]
   * refs:   [{ y, label }]  (linhas de referência tracejadas, ex.: meta / média)
   */
  function line(container, opts) {
    const {
      series, refs = [], height = 260,
      yFmt = v => U.num(v, 0), fmt = v => U.num(v, 1), xFmt = U.dateShort,
      emptyMsg = 'Sem dados ainda.',
    } = opts;

    mount(container, (root, W) => {
      const all = series.flatMap(s => s.points);
      if (!all.length) return empty(root, emptyMsg);

      const H = height, m = { t: 16, r: 16, b: 28, l: 50 };
      const iw = W - m.l - m.r, ih = H - m.t - m.b;
      const xs = all.map(p => p.x);
      let x0 = Math.min(...xs), x1 = Math.max(...xs);
      if (x0 === x1) { x0 -= 3 * U.DAY; x1 += 3 * U.DAY; }
      const ys = all.map(p => p.y).concat(refs.map(r => r.y));
      const y0 = Math.min(...ys), y1 = Math.max(...ys);
      const pad = (y1 - y0) * 0.1 || Math.max(Math.abs(y1) * 0.05, 1);
      const n = nice(y0 - pad, y1 + pad, 5);

      const X = v => m.l + (v - x0) / (x1 - x0) * iw;
      const Y = v => m.t + (1 - (v - n.lo) / (n.hi - n.lo)) * ih;

      const svg = el('svg', { width: W, height: H, viewBox: `0 0 ${W} ${H}`, role: 'img', 'aria-label': opts.label || 'Gráfico de linhas' }, root);

      n.ticks.forEach(t => {
        el('line', { x1: m.l, x2: W - m.r, y1: Y(t), y2: Y(t), class: 'grid' }, svg);
        text(svg, m.l - 8, Y(t) + 4, yFmt(t), 'axis', 'end');
      });
      const nx = Math.max(2, Math.min(6, Math.floor(iw / 90)));
      for (let i = 0; i < nx; i++) {
        const v = x0 + (x1 - x0) * i / (nx - 1);
        text(svg, X(v), H - 8, xFmt(v), 'axis', i === 0 ? 'start' : i === nx - 1 ? 'end' : 'middle');
      }

      const sorted = series.map(s => ({ ...s, points: [...s.points].sort((a, b) => a.x - b.x) }));
      sorted.forEach(s => {
        if (!s.points.length) return;
        const d = s.points.map((p, i) => `${i ? 'L' : 'M'}${X(p.x).toFixed(1)},${Y(p.y).toFixed(1)}`).join('');
        if (s.area) {
          const base = Y(n.lo).toFixed(1);
          el('path', { d: `${d}L${X(s.points.at(-1).x).toFixed(1)},${base}L${X(s.points[0].x).toFixed(1)},${base}Z`, class: 'series-area', style: `fill:${s.color}` }, svg);
        }
        el('path', { d, class: 'series-line', style: `stroke:${s.color}` }, svg);
        if (s.markers !== false && s.points.length <= 80) {
          s.points.forEach(p => el('circle', { cx: X(p.x), cy: Y(p.y), r: 4, class: 'mark', style: `fill:${s.color}` }, svg));
        }
      });

      refs.forEach(r => {
        el('line', { x1: m.l, x2: W - m.r, y1: Y(r.y), y2: Y(r.y), class: 'ref' }, svg);
        text(svg, W - m.r - 4, Y(r.y) - 6, r.label, 'ref-label', 'end');
      });

      // Camada de hover: linha vertical + tooltip com todos os valores daquela data
      const ux = [...new Set(xs)].sort((a, b) => a - b);
      const cross = el('line', { y1: m.t, y2: H - m.b, class: 'crosshair', visibility: 'hidden' }, svg);
      const dots = sorted.map(s => el('circle', { r: 5, class: 'hover-dot', style: `fill:${s.color}`, visibility: 'hidden' }, svg));
      const tip = tooltip(root);
      const hit = el('rect', { x: m.l - 10, y: 0, width: iw + 20, height: H, fill: 'transparent' }, svg);

      hit.addEventListener('pointermove', ev => {
        const rect = svg.getBoundingClientRect();
        const xv = x0 + (ev.clientX - rect.left - m.l) / iw * (x1 - x0);
        let xn = ux[0];
        for (const v of ux) if (Math.abs(v - xv) < Math.abs(xn - xv)) xn = v;
        const cx = X(xn);
        cross.setAttribute('x1', cx); cross.setAttribute('x2', cx); cross.setAttribute('visibility', 'visible');
        let html = `<div class="tip-title">${U.dateBR(xn)}</div>`;
        const notes = [];
        sorted.forEach((s, i) => {
          const p = s.points.find(p => p.x === xn);
          if (!p) return dots[i].setAttribute('visibility', 'hidden');
          dots[i].setAttribute('cx', cx); dots[i].setAttribute('cy', Y(p.y)); dots[i].setAttribute('visibility', 'visible');
          html += tipRow(s.color, s.name, fmt(p.y));
          if (p.note) notes.push(p.note);
        });
        notes.forEach(t => { html += `<div class="tip-note">${U.esc(t)}</div>`; });
        tip.show(html, cx, m.t, W);
      });
      hit.addEventListener('pointerleave', () => {
        cross.setAttribute('visibility', 'hidden');
        dots.forEach(d => d.setAttribute('visibility', 'hidden'));
        tip.hide();
      });
    });
  }

  // Barra com cantos arredondados só na ponta (longe da linha do zero)
  function barPath(x, y, w, h, r, up) {
    if (h <= 0.5) return '';
    r = Math.min(r, w / 2, h);
    return up
      ? `M${x},${y + h}V${y + r}Q${x},${y} ${x + r},${y}H${x + w - r}Q${x + w},${y} ${x + w},${y + r}V${y + h}Z`
      : `M${x},${y}V${y + h - r}Q${x},${y + h} ${x + r},${y + h}H${x + w - r}Q${x + w},${y + h} ${x + w},${y + h - r}V${y}Z`;
  }

  /**
   * Gráfico de barras.
   * data: [{ label, value, tip? }]
   */
  function bar(container, opts) {
    const {
      data, name = '', height = 240, color = 'var(--series-1)',
      yFmt = v => U.num(v, 0), fmt = v => U.num(v, 1), emptyMsg = 'Sem dados ainda.',
    } = opts;

    mount(container, (root, W) => {
      if (!data.length || data.every(d => !d.value)) return empty(root, emptyMsg);

      const H = height, m = { t: 16, r: 8, b: 28, l: 50 };
      const iw = W - m.l - m.r, ih = H - m.t - m.b;
      const vals = data.map(d => d.value);
      const n = nice(Math.min(0, ...vals), Math.max(0, ...vals), 4);
      const Y = v => m.t + (1 - (v - n.lo) / (n.hi - n.lo)) * ih;

      const svg = el('svg', { width: W, height: H, viewBox: `0 0 ${W} ${H}`, role: 'img', 'aria-label': opts.label || 'Gráfico de barras' }, root);
      n.ticks.forEach(t => {
        el('line', { x1: m.l, x2: W - m.r, y1: Y(t), y2: Y(t), class: 'grid' }, svg);
        text(svg, m.l - 8, Y(t) + 4, yFmt(t), 'axis', 'end');
      });

      const bw = iw / data.length;
      const barW = Math.max(3, Math.min(44, bw * 0.66));
      const every = Math.max(1, Math.ceil(data.length / Math.max(1, Math.floor(iw / 54))));
      const bars = data.map((d, i) => {
        const x = m.l + i * bw + (bw - barW) / 2;
        const yTop = Y(Math.max(d.value, 0)), yBot = Y(Math.min(d.value, 0));
        if ((data.length - 1 - i) % every === 0) text(svg, m.l + i * bw + bw / 2, H - 8, d.label, 'axis');
        return el('path', { d: barPath(x, yTop, barW, yBot - yTop, 4, d.value >= 0), class: 'bar', style: `fill:${color}` }, svg);
      });
      el('line', { x1: m.l, x2: W - m.r, y1: Y(0), y2: Y(0), class: 'baseline' }, svg);

      const tip = tooltip(root);
      data.forEach((d, i) => {
        const hit = el('rect', { x: m.l + i * bw, y: m.t, width: bw, height: ih, fill: 'transparent' }, svg);
        hit.addEventListener('pointerenter', () => {
          root.classList.add('hovering');
          bars.forEach((b, j) => b.classList.toggle('on', j === i));
          tip.show(`<div class="tip-title">${U.esc(d.tip || d.label)}</div>${tipRow(color, name, fmt(d.value))}`, m.l + i * bw + bw / 2, m.t, W);
        });
        hit.addEventListener('pointerleave', () => {
          root.classList.remove('hovering');
          bars.forEach(b => b.classList.remove('on'));
          tip.hide();
        });
      });
    });
  }

  // Legenda (só aparece com 2+ séries)
  function legend(target, items) {
    target.innerHTML = items.length < 2 ? '' :
      items.map(i => `<span class="legend-item"><i style="background:${i.color}"></i>${U.esc(i.name)}</span>`).join('');
  }

  // Desliga o observador de redimensionamento quando o componente Blazor é descartado
  function dispose(container) {
    if (container && container._ro) { container._ro.disconnect(); container._ro = null; }
  }

  return { line, bar, legend, dispose };
})();

// Ponte chamada pelos componentes GraficoLinha / GraficoBarras (C#)
window.crankoCharts = {
  line(container, o) {
    Charts.line(container, {
      series: (o.series || []).map(s => ({
        name: s.name, color: s.color, markers: s.markers, area: s.area,
        points: (s.points || []).map(p => ({ x: U.ts(p.x), y: p.y, note: p.note || undefined })),
      })),
      refs: o.refs || [],
      height: o.height || 260,
      fmt: U.fmt(o.fmt, v => U.num(v, 1)),
      yFmt: U.fmt(o.yFmt, v => U.num(v, 0)),
      emptyMsg: o.emptyMsg || 'Sem dados ainda.',
      label: o.label || undefined,
    });
  },
  bar(container, o) {
    Charts.bar(container, {
      data: (o.data || []).map(d => ({ label: d.label, value: d.value, tip: d.tip || undefined })),
      name: o.name || '',
      height: o.height || 240,
      color: o.color || 'var(--series-1)',
      fmt: U.fmt(o.fmt, v => U.num(v, 1)),
      yFmt: U.fmt(o.yFmt, v => U.num(v, 0)),
      emptyMsg: o.emptyMsg || 'Sem dados ainda.',
      label: o.label || undefined,
    });
  },
  dispose(container) { Charts.dispose(container); },
};

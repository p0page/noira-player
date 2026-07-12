import { describe, expect, it, vi } from 'vitest';

let stylesSource = '';

describe('TV visual contracts', () => {
  it('hides page scrollbars without disabling focus-driven document scrolling', async () => {
    await loadStyles();
    expect(stylesSource).toMatch(
      /html,\s*body\s*{[^}]*scrollbar-width:\s*none/s,
    );
    expect(stylesSource).toMatch(
      /html::-webkit-scrollbar,\s*body::-webkit-scrollbar\s*{[^}]*display:\s*none/s,
    );
    expect(stylesSource).toMatch(/body\s*{[^}]*overflow-x:\s*hidden/s);
    expect(stylesSource).not.toMatch(/body\s*{[^}]*overflow-y:\s*hidden/s);
  });

  it('keeps login text selectable while preventing button-label selection', async () => {
    await loadStyles();
    expect(stylesSource).toContain(
      'button {\n  -webkit-user-select: none;\n  user-select: none;',
    );
    expect(stylesSource).not.toContain(
      '[data-focus-key] {\n  -webkit-user-select: none;',
    );
    expect(stylesSource).not.toMatch(/input\s*{[^}]*user-select:\s*none/s);
  });

  it('recedes Home chrome while retaining the 56px TV safe-area contract', async () => {
    await loadStyles();
    expect(stylesSource).toContain('--tv-safe: 56px;');
    expect(stylesSource).toMatch(
      /\.home-page__title\s*{[^}]*position:\s*absolute[^}]*clip-path:\s*inset\(50%\)/s,
    );
    expect(stylesSource).toMatch(
      /\.home-page__rows\s*{[^}]*gap:\s*var\(--space-sm\)/s,
    );
    expect(stylesSource).toMatch(
      /\.home-page\s*{[^}]*margin-left:\s*var\(--guide-collapsed\)[^}]*padding:\s*var\(--tv-safe\) 0/s,
    );
  });

  it('keeps the collapsed Guide no wider than its icon rail', async () => {
    await loadStyles();
    expect(stylesSource).toContain('--guide-collapsed: 56px;');
    expect(stylesSource).toMatch(
      /\.guide\s*{[^}]*left:\s*0[^}]*width:\s*var\(--guide-collapsed\)[^}]*background:\s*var\(--shell-rail\)/s,
    );
    expect(stylesSource).not.toMatch(/\.guide::before\s*{/s);
    expect(stylesSource).toMatch(
      /\.guide--expanded\s*{[^}]*width:\s*var\(--guide-expanded\)[^}]*background:\s*var\(--surface\)/s,
    );
    expect(stylesSource).toMatch(
      /\.library-page\s*{[^}]*margin-left:\s*var\(--guide-collapsed\)/s,
    );
  });

  it('uses matte fill and luminance transforms for focus without frames or reflow', async () => {
    await loadStyles();
    const cardFocus = readRule('.media-card:focus-visible .media-card__visual');
    expect(cardFocus).toContain('background: var(--card-focus-fill);');
    expect(cardFocus).toContain('transform: scale(1.03);');
    expect(cardFocus).not.toMatch(/(?:border|outline|box-shadow)\s*:/);

    const artworkFocus = readRule(
      '.media-card:focus-visible .media-card__artwork img',
    );
    expect(artworkFocus).toContain('filter: brightness(1.1);');

    const detailsFocus = readRule('.details-page__play:focus-visible');
    expect(detailsFocus).toContain(
      'background: var(--details-decision-tile-focused);',
    );
    expect(detailsFocus).toContain('transform: scale(1.03);');
    expect(detailsFocus).not.toMatch(/(?:border|outline|box-shadow)\s*:/);
  });
});

async function loadStyles(): Promise<void> {
  if (stylesSource) {
    return;
  }

  const { readFileSync } = await vi.importActual<{
    readFileSync(path: string, encoding: 'utf8'): string;
  }>('node:fs');
  const { resolve } = await vi.importActual<{
    resolve(...paths: string[]): string;
  }>('node:path');
  const { cwd } = await vi.importActual<{ cwd(): string }>('node:process');
  stylesSource = readFileSync(resolve(cwd(), 'src/styles.css'), 'utf8').replace(
    /\r\n/g,
    '\n',
  );
}

function readRule(selector: string): string {
  const escapedSelector = selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const match = stylesSource.match(new RegExp(`${escapedSelector}\\s*{([^}]*)}`, 's'));
  if (!match) {
    throw new Error(`Missing CSS rule: ${selector}`);
  }

  return match[1];
}

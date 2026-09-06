import React from 'react';

// ── Parser leve de markdown (sem dependências externas) ─────────────────────
// Suporta: **negrito**, *itálico*, `código inline`, blocos ```código```,
// listas (- / * / 1.), títulos (#, ##, ###), links [texto](url), tabelas e parágrafos.

function parseInline(text, keyPrefix) {
  const nodes = [];
  const regex = /\*\*(.+?)\*\*|`([^`]+?)`|\*(.+?)\*|\[([^\]]+)\]\(([^)]+)\)/g;
  let lastIndex = 0;
  let match;
  let i = 0;

  while ((match = regex.exec(text)) !== null) {
    if (match.index > lastIndex) {
      nodes.push(text.slice(lastIndex, match.index));
    }
    const key = `${keyPrefix}-${i++}`;
    if (match[1] !== undefined) {
      nodes.push(<strong key={key} style={mdStyles.strong}>{match[1]}</strong>);
    } else if (match[2] !== undefined) {
      nodes.push(<code key={key} style={mdStyles.codeInline}>{match[2]}</code>);
    } else if (match[3] !== undefined) {
      nodes.push(<em key={key}>{match[3]}</em>);
    } else if (match[4] !== undefined) {
      nodes.push(
        <a key={key} href={match[5]} target="_blank" rel="noopener noreferrer" style={mdStyles.link}>
          {match[4]}
        </a>
      );
    }
    lastIndex = regex.lastIndex;
  }
  if (lastIndex < text.length) nodes.push(text.slice(lastIndex));
  return nodes;
}

// Alguns modelos "achatam" tabelas inteiras numa única linha, usando "| |"
// como separador entre o fim de uma linha e o começo da próxima. Aqui a
// gente reconstrói as quebras de linha antes do parser de blocos rodar.
function unflattenTables(content) {
  return content.replace(/\|\s*\|/g, '|\n|');
}

function isTableRow(line) {
  const trimmed = line.trim();
  return trimmed.startsWith('|') || (trimmed.includes('|') && trimmed.endsWith('|'));
}

function isSeparatorRow(line) {
  const trimmed = line.trim();
  if (!trimmed.includes('-') || !trimmed.includes('|')) return false;
  const cells = trimmed.replace(/^\||\|$/g, '').split('|').map(c => c.trim());
  return cells.length > 0 && cells.every(c => /^:?-+:?$/.test(c));
}

function parseRowCells(line) {
  let trimmed = line.trim();
  if (trimmed.startsWith('|')) trimmed = trimmed.slice(1);
  if (trimmed.endsWith('|')) trimmed = trimmed.slice(0, -1);
  return trimmed.split('|').map(c => c.trim());
}

function parseBlocks(content) {
  const lines = unflattenTables(content).replace(/\r\n/g, '\n').split('\n');
  const blocks = [];
  let i = 0;

  while (i < lines.length) {
    const line = lines[i];

    if (line.trim() === '') { i++; continue; }

    // Bloco de código ```
    if (line.trim().startsWith('```')) {
      const codeLines = [];
      i++;
      while (i < lines.length && !lines[i].trim().startsWith('```')) {
        codeLines.push(lines[i]);
        i++;
      }
      i++; // pula a cerca de fechamento
      blocks.push({ type: 'code', content: codeLines.join('\n') });
      continue;
    }

    // Títulos
    const headingMatch = line.match(/^(#{1,3})\s+(.*)/);
    if (headingMatch) {
      blocks.push({ type: 'heading', level: headingMatch[1].length, content: headingMatch[2] });
      i++;
      continue;
    }

    // Tabelas: linha "| a | b |" seguida de separador "|---|---|"
    if (isTableRow(line) && i + 1 < lines.length && isSeparatorRow(lines[i + 1])) {
      const header = parseRowCells(line);
      i += 2;
      const rows = [];
      while (i < lines.length && lines[i].trim() !== '' && isTableRow(lines[i])) {
        rows.push(parseRowCells(lines[i]));
        i++;
      }
      blocks.push({ type: 'table', header, rows });
      continue;
    }

    // Listas (com ou sem numeração)
    if (/^\s*([-*]|\d+\.)\s+/.test(line)) {
      const items = [];
      const ordered = /^\s*\d+\.\s+/.test(line);
      while (i < lines.length && /^\s*([-*]|\d+\.)\s+/.test(lines[i])) {
        items.push(lines[i].replace(/^\s*([-*]|\d+\.)\s+/, ''));
        i++;
      }
      blocks.push({ type: ordered ? 'ol' : 'ul', items });
      continue;
    }

    // Parágrafo — junta linhas consecutivas
    const paraLines = [];
    while (
      i < lines.length && lines[i].trim() !== '' &&
      !/^\s*([-*]|\d+\.)\s+/.test(lines[i]) &&
      !lines[i].trim().startsWith('```') &&
      !/^#{1,3}\s+/.test(lines[i]) &&
      !(isTableRow(lines[i]) && i + 1 < lines.length && isSeparatorRow(lines[i + 1]))
    ) {
      paraLines.push(lines[i]);
      i++;
    }
    blocks.push({ type: 'p', content: paraLines.join(' ') });
  }

  return blocks;
}

export default function Markdown({ content }) {
  if (!content) return null;
  const blocks = parseBlocks(content);

  return (
    <div style={mdStyles.root}>
      {blocks.map((block, idx) => {
        const key = `b-${idx}`;
        if (block.type === 'code') {
          return (
            <pre key={key} style={mdStyles.pre}>
              <code>{block.content}</code>
            </pre>
          );
        }
        if (block.type === 'heading') {
          const size = block.level === 1 ? '1rem' : block.level === 2 ? '0.92rem' : '0.86rem';
          return (
            <p key={key} style={{ ...mdStyles.heading, fontSize: size }}>
              {parseInline(block.content, key)}
            </p>
          );
        }
        if (block.type === 'table') {
          return (
            <div key={key} style={mdStyles.tableWrap}>
              <table style={mdStyles.table}>
                <thead>
                  <tr>
                    {block.header.map((cell, c) => (
                      <th key={`${key}-h${c}`} style={mdStyles.th}>{parseInline(cell, `${key}-h${c}`)}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {block.rows.map((row, r) => (
                    <tr key={`${key}-r${r}`} style={r % 2 === 1 ? mdStyles.trAlt : undefined}>
                      {row.map((cell, c) => (
                        <td
                          key={`${key}-r${r}c${c}`}
                          style={r === block.rows.length - 1 ? mdStyles.tdLast : mdStyles.td}
                        >
                          {parseInline(cell, `${key}-r${r}c${c}`)}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          );
        }
        if (block.type === 'ul' || block.type === 'ol') {
          const Tag = block.type === 'ul' ? 'ul' : 'ol';
          return (
            <Tag key={key} style={mdStyles.list}>
              {block.items.map((item, j) => (
                <li key={`${key}-${j}`} style={mdStyles.listItem}>{parseInline(item, `${key}-${j}`)}</li>
              ))}
            </Tag>
          );
        }
        return (
          <p key={key} style={mdStyles.p}>
            {parseInline(block.content, key)}
          </p>
        );
      })}
    </div>
  );
}

const mdStyles = {
  root: { display: 'flex', flexDirection: 'column' },
  p: { margin: '0 0 8px', lineHeight: 1.6 },
  heading: { margin: '2px 0 8px', fontWeight: 700, lineHeight: 1.4 },
  strong: { fontWeight: 700, color: 'inherit' },
  link: { color: '#a5b4fc', textDecoration: 'underline' },
  codeInline: {
    background: 'rgba(255,255,255,0.1)',
    border: '1px solid rgba(255,255,255,0.08)',
    borderRadius: 4,
    padding: '1px 5px',
    fontFamily: "'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace",
    fontSize: '0.85em',
  },
  pre: {
    background: 'rgba(0,0,0,0.3)',
    border: '1px solid rgba(255,255,255,0.08)',
    borderRadius: 8,
    padding: '10px 12px',
    margin: '4px 0 8px',
    overflowX: 'auto',
    fontFamily: "'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace",
    fontSize: '0.78rem',
    lineHeight: 1.5,
  },
  list: { margin: '0 0 8px', paddingLeft: 18 },
  listItem: { marginBottom: 4, lineHeight: 1.55 },
  tableWrap: {
    overflowX: 'auto',
    margin: '4px 0 10px',
    border: '1px solid rgba(255,255,255,0.1)',
    borderRadius: 8,
  },
  table: {
    width: '100%',
    borderCollapse: 'collapse',
    fontSize: '0.78rem',
  },
  th: {
    textAlign: 'left',
    padding: '7px 11px',
    background: 'rgba(255,255,255,0.08)',
    borderBottom: '1px solid rgba(255,255,255,0.15)',
    fontWeight: 700,
    whiteSpace: 'nowrap',
  },
  td: {
    padding: '6px 11px',
    borderBottom: '1px solid rgba(255,255,255,0.06)',
    whiteSpace: 'nowrap',
  },
  tdLast: {
    padding: '6px 11px',
    whiteSpace: 'nowrap',
  },
  trAlt: {
    background: 'rgba(255,255,255,0.03)',
  },
};

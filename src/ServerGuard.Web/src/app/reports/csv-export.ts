/**
 * Tablo satırlarını CSV dosyası olarak indirir.
 */

/** Excel'in Türkçe yerel ayarında sütunları doğru ayırması için noktalı virgül kullanılır. */
const FIELD_SEPARATOR = ';';

/**
 * UTF-8 BOM. Excel bu işaret olmadan dosyayı yerel kod sayfasıyla açar ve
 * Türkçe karakterler bozulur.
 */
const UTF8_BOM = '﻿';

export function downloadCsv(fileName: string, rows: readonly (readonly string[])[]): void {
  const content = UTF8_BOM + rows.map(toCsvLine).join('\r\n');
  const blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);

  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();

  // Blob'un tuttuğu bellek serbest bırakılmazsa sekme kapanana kadar birikir.
  URL.revokeObjectURL(url);
}

/**
 * Ayırıcı, tırnak veya satır sonu içeren alanlar tırnak içine alınır ve
 * içteki tırnaklar ikilenir (RFC 4180).
 */
function toCsvLine(fields: readonly string[]): string {
  return fields.map(escapeField).join(FIELD_SEPARATOR);
}

function escapeField(value: string): string {
  const needsQuoting = value.includes(FIELD_SEPARATOR) || value.includes('"') || /[\r\n]/.test(value);

  return needsQuoting ? `"${value.replace(/"/g, '""')}"` : value;
}

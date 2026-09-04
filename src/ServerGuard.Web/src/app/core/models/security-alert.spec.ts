import { describe, expect, it } from 'vitest';

import { toSecurityAlert } from './security-alert';

const validAlert = {
  id: 42,
  serverName: 'web-01',
  alertType: 'BruteForceAttempt',
  severity: 'High',
  sourceIp: '203.0.113.10',
  observedCount: 5,
  description: 'Beş başarısız giriş denemesi.',
  timestamp: '2026-09-04T10:00:00+00:00'
};

describe('toSecurityAlert', () => {
  it('geçerli alarmı olduğu gibi döner', () => {
    expect(toSecurityAlert(validAlert)).toEqual(validAlert);
  });

  it.each([
    ['null', null],
    ['undefined', undefined],
    ['metin', 'bozuk'],
    ['sayı', 7],
    ['dizi', []],
    ['boş nesne', {}]
  ])('%s girdisini reddeder', (_name, input) => {
    expect(toSecurityAlert(input)).toBeNull();
  });

  it.each([
    ['id eksik', { ...validAlert, id: undefined }],
    ['id metin', { ...validAlert, id: '42' }],
    ['serverName eksik', { ...validAlert, serverName: undefined }],
    ['severity eksik', { ...validAlert, severity: undefined }],
    ['timestamp eksik', { ...validAlert, timestamp: undefined }],
    ['timestamp geçersiz', { ...validAlert, timestamp: 'tarih-degil' }]
  ])('zorunlu alan bozuksa reddeder: %s', (_name, input) => {
    expect(toSecurityAlert(input)).toBeNull();
  });

  it('zorunlu olmayan alanlar eksikse varsayılanlarla doldurur', () => {
    const partial = {
      id: 1,
      serverName: 'db-01',
      severity: 'Medium',
      timestamp: '2026-09-04T10:00:00+00:00'
    };

    const result = toSecurityAlert(partial);

    expect(result).not.toBeNull();
    expect(result?.alertType).toBe('Bilinmiyor');
    expect(result?.sourceIp).toBe('-');
    expect(result?.observedCount).toBe(0);
    expect(result?.description).toBe('');
  });
});

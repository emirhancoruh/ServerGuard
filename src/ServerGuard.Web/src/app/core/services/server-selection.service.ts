import { Injectable, signal } from '@angular/core';

/** Seçim yapılmadığında tüm sunucular gösterilir. */
export const ALL_SERVERS = null;

/**
 * Panelin tamamının hangi sunucuyu gösterdiğini tutar.
 * Bileşenler bu sinyali okuyup kendi verilerini buna göre filtreler.
 */
@Injectable({ providedIn: 'root' })
export class ServerSelectionService {
  private readonly selectedSignal = signal<string | null>(ALL_SERVERS);

  /** Seçili sunucu adı; <c>null</c> ise tüm sunucular. */
  readonly selected = this.selectedSignal.asReadonly();

  select(serverName: string | null): void {
    this.selectedSignal.set(serverName === '' ? ALL_SERVERS : serverName);
  }

  /** Canlı akıştan gelen bir kaydın gösterilip gösterilmeyeceğini söyler. */
  matches(serverName: string): boolean {
    const selected = this.selectedSignal();
    return selected === ALL_SERVERS || selected === serverName;
  }
}

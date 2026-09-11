import { InjectionToken } from '@angular/core';

import { environment } from '../../environments/environment';

/**
 * API'nin kök adresi. Uygulama açılmadan önce <c>config.json</c>'dan okunur ve
 * enjeksiyonla dağıtılır; hiçbir servis adresi kendi başına çözmez.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');

/** Panelin kökünden servis edilen yapılandırma dosyası. */
const CONFIG_PATH = 'config.json';

/**
 * API adresini çalışma zamanında okur.
 *
 * Adres derlemeye gömülmez: panel ile API ayrı sitelerde yayınlandığında adres,
 * HTTPS'e geçişte ve sunucu adı değiştiğinde birkaç kez değişir. Gömülü olsaydı
 * her değişiklik paneli yeniden derlemeyi gerektirirdi; böylece sunucuda tek bir
 * dosyayı düzenlemek yeterli olur.
 *
 * Dosya okunamazsa veya değer boşsa derleme zamanı varsayılanına düşülür. Bu,
 * panelin API ile aynı kaynaktan servis edildiği kurulumda dosyayı hiç
 * doldurmamaya izin verir.
 */
export async function loadApiBaseUrl(): Promise<string> {
  const fallback = normalize(environment.apiBaseUrl);

  try {
    // Ayar dosyası tarayıcıda önbelleğe alınırsa sunucuda yapılan düzenleme
    // kullanıcıya günler sonra ulaşabilir; her açılışta doğrulatılır.
    const response = await fetch(CONFIG_PATH, { cache: 'no-cache' });

    if (!response.ok) {
      console.warn(`${CONFIG_PATH} okunamadı (HTTP ${response.status}); derleme varsayılanı kullanılıyor.`);
      return fallback;
    }

    const value = (await response.json())?.apiBaseUrl;

    if (typeof value !== 'string' || value.trim().length === 0) {
      return fallback;
    }

    return normalize(value);
  } catch (error) {
    console.warn(`${CONFIG_PATH} okunamadı; derleme varsayılanı kullanılıyor.`, error);
    return fallback;
  }
}

/**
 * Sondaki eğik çizgileri atar. Yollar zaten "/api/..." ile başladığından, adres
 * "https://api.ornek.local/" biçiminde yazılırsa istek "//api/..." olur ve tarayıcı
 * bunu başka bir sunucu adı sayar.
 */
function normalize(baseUrl: string): string {
  return baseUrl.trim().replace(/\/+$/, '');
}

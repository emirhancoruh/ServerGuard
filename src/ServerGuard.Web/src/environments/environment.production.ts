export const environment = {
  production: true,
  /**
   * Yalnızca yedek değerdir. Gerçek adres çalışma zamanında panelin kökündeki
   * <c>config.json</c> dosyasından okunur; böylece adres değiştiğinde panel yeniden
   * derlenmez. Dosya yoksa veya değeri boşsa buradaki değer kullanılır.
   *
   * Boş dize "aynı kaynak" demektir. Buraya "/" yazılmamalıdır: yollar zaten
   * "/api/..." ile başladığından sonuç "//api/..." olur ve tarayıcı bunu başka bir
   * sunucu adı sanar.
   */
  apiBaseUrl: ''
};

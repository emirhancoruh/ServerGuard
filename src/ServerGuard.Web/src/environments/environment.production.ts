export const environment = {
  production: true,
  /**
   * Panel, API ile aynı kaynaktan (API'nin wwwroot'u) servis edilir; bu yüzden adres
   * göreli bırakılır. Değer boş dize olmalıdır: yollar zaten "/api/..." ile başladığından
   * buraya "/" yazmak "//api/..." üretir ve tarayıcı bunu başka bir sunucu adı sanar.
   */
  apiBaseUrl: ''
};

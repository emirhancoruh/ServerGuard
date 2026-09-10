/** Tarayıcıda saklanan oturum bilgisi. */
export interface AuthSession {
  accessToken: string;
  userName: string;
  /** Token'ın geçerlilik sonu; süresi dolmuş oturum hiç kullanılmaz. */
  expiresAt: Date;
}

/**
 * API'nin döndürdüğü oturum yanıtını doğrular.
 * Beklenmeyen bir gövde geldiğinde <c>null</c> döner; bozuk bir oturum saklanmaz.
 */
export function toAuthSession(raw: unknown): AuthSession | null {
  if (typeof raw !== 'object' || raw === null) {
    return null;
  }

  const candidate = raw as Record<string, unknown>;
  const accessToken = candidate['accessToken'];
  const userName = candidate['userName'];
  const expiresAtRaw = candidate['expiresAt'];

  if (typeof accessToken !== 'string' || accessToken.length === 0) {
    return null;
  }

  if (typeof userName !== 'string' || userName.length === 0) {
    return null;
  }

  if (typeof expiresAtRaw !== 'string' || Number.isNaN(Date.parse(expiresAtRaw))) {
    return null;
  }

  return { accessToken, userName, expiresAt: new Date(expiresAtRaw) };
}

/** Oturumun hâlâ geçerli olup olmadığını söyler. */
export function isSessionValid(session: AuthSession | null, now: Date = new Date()): boolean {
  return session !== null && session.expiresAt.getTime() > now.getTime();
}

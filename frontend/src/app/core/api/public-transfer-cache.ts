const anonymousPublicReadPattern =
  /^\/api\/v1\/(?:system\/status|catalog\/(?:courses(?:\/[^/?]+)?|categories|tags|featured|popular)|search(?:\/suggestions)?|pages(?:\/[^/?]+)?|faqs|portfolio-settings)(?:\?|$)/u;

export const isAnonymousPublicReadUrl = (url: string): boolean =>
  anonymousPublicReadPattern.test(url);

export type ThemeName = 'dark' | 'light'

export const THEME_STORAGE_KEY = 'teampulse-theme'

export const layoutTokens = {
  sidebarWidth: 256,
  compactSidebarWidth: 88,
  topbarHeight: 72,
  contentPadding: 32,
} as const

export const breakpointTokens = {
  compactSidebar: 1024,
  mobileNavigation: 768,
} as const

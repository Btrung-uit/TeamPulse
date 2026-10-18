import type { ResourceType } from '@/features/resources/types'

export const resourceTypes: ResourceType[] = [
  'GoogleDocs',
  'GoogleDrive',
  'Canva',
  'GitHub',
  'Demo',
  'Other',
]

export const resourceTypeLabels: Record<ResourceType, string> = {
  GoogleDocs: 'Google Docs',
  GoogleDrive: 'Google Drive',
  Canva: 'Canva',
  GitHub: 'GitHub',
  Demo: 'Demo',
  Other: 'Other',
}

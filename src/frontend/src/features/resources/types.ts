export type ResourceType =
  'GoogleDocs' | 'GoogleDrive' | 'Canva' | 'GitHub' | 'Demo' | 'Other'

export interface ResourceOwner {
  userId: string
  displayName: string
  email: string
}

export interface LinkedResourceTask {
  id: string
  code: string
  title: string
}

export interface ProjectResource {
  id: string
  workspaceId: string
  projectId: string
  title: string
  type: ResourceType
  url: string
  externalId: string | null
  owner: ResourceOwner
  linkedTask: LinkedResourceTask | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface ResourceFilters {
  search?: string
  type?: ResourceType
}

export interface SaveResourceInput {
  title: string
  type: ResourceType
  url: string
  externalId?: string
  ownerUserId: string
  linkedTaskId?: string
}

export interface ResourceErrorBody {
  code: string
  message: string
  errors?: Record<string, string[]>
}

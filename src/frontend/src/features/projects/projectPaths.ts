export function projectListPath(workspaceId: string) {
  return `/workspaces/${workspaceId}/projects`
}

export function projectPath(workspaceId: string, projectId: string) {
  return `${projectListPath(workspaceId)}/${projectId}`
}

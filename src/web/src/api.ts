export type Profile = { name: string; engine: string; model?: string }
export type RunEvent = { t: string; kind: string; data: Record<string, unknown> }
export type RunSummary = {
  id: string; profile: string; engine: string; prompt: string; tools: number
  end?: { ms: number; inputTokens?: number; outputTokens?: number; cost?: number }
}
export type Pending = { id: string; profile: string; runId: string; tool: string; args?: string; at: string }

async function json<T>(res: Response): Promise<T> {
  if (!res.ok) throw new Error(`${res.status} ${await res.text()}`)
  return res.status === 204 ? (undefined as T) : res.json()
}

export const api = {
  profiles: () => fetch('/api/profiles').then(json<{ defaultProfile: string; profiles: Profile[]; configError?: string }>),
  runs: () => fetch('/api/runs').then(json<RunSummary[]>),
  run: (id: string) => fetch(`/api/runs/${id}`).then(json<RunEvent[]>),
  startRun: (profile: string, prompt: string) =>
    fetch('/api/runs', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ profile, prompt }) })
      .then(json<{ runId: string; text: string }>),
  approvals: () => fetch('/api/approvals').then(json<Pending[]>),
  answer: (id: string, approve: boolean) =>
    fetch(`/api/approvals/${id}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ approve }) }).then(json<void>),
  config: () => fetch('/api/config').then(r => r.text()),
  saveConfig: async (yaml: string): Promise<string[]> => {
    const res = await fetch('/api/config', { method: 'PUT', body: yaml })
    return res.ok ? [] : (await res.json()).errors
  },
  models: () => fetch('/api/models').then(json<{ id: string }[]>),
}

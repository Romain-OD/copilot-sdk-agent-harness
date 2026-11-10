import { useState, type FormEvent } from 'react'
import { api, type Profile, type RunEvent } from './api'
import { RunView } from './Runs'

/** Same prompt, two profiles, side by side: engine vs engine, or policy vs policy. */
export function Compare({ profiles }: { profiles: Profile[] }) {
  const [a, setA] = useState(profiles[0]?.name ?? '')
  const [b, setB] = useState(profiles[1]?.name ?? '')
  const [prompt, setPrompt] = useState('')
  const [busy, setBusy] = useState(false)
  const [results, setResults] = useState<(RunEvent[] | string)[]>([])

  async function run(e: FormEvent) {
    e.preventDefault()
    if (!prompt.trim() || busy) return
    setBusy(true)
    setResults([])
    setResults(await Promise.all([a, b].map(p =>
      api.startRun(p, prompt).then(r => api.run(r.runId)).catch(err => String(err)))))
    setBusy(false)
  }

  const pick = (value: string, set: (v: string) => void, label: string) => (
    <select aria-label={label} value={value} onChange={e => set(e.target.value)}>
      {profiles.map(p => <option key={p.name}>{p.name}</option>)}
    </select>
  )

  return (
    <div>
      <form onSubmit={run} className="row">
        {pick(a, setA, 'Profile A')} vs {pick(b, setB, 'Profile B')}
        <input aria-label="Prompt" value={prompt} onChange={e => setPrompt(e.target.value)} placeholder="Prompt to run on both…" />
        <button disabled={busy}>{busy ? 'Running…' : 'Run both'}</button>
      </form>
      <div className="columns">
        {results.map((r, i) => (
          <div key={i}>{typeof r === 'string' ? <p className="error">{r}</p> : <RunView events={r} />}</div>
        ))}
      </div>
    </div>
  )
}

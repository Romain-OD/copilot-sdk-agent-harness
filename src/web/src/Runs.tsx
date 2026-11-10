import { useEffect, useState } from 'react'
import { api, type Profile, type RunEvent, type RunSummary } from './api'

/** The flight recorder: every run, every tool decision, replayable step by step. */
export function Runs({ profiles }: { profiles: Profile[] }) {
  const [runs, setRuns] = useState<RunSummary[]>([])
  const [selected, setSelected] = useState<string>()
  const [events, setEvents] = useState<RunEvent[]>([])
  const [shown, setShown] = useState(Infinity)
  const [rerunOn, setRerunOn] = useState('')
  const [busy, setBusy] = useState(false)

  const refresh = () => api.runs().then(setRuns)
  useEffect(() => { refresh() }, [])
  useEffect(() => { if (selected) api.run(selected).then(e => { setEvents(e); setShown(Infinity) }) }, [selected])

  useEffect(() => {
    if (shown >= events.length) return
    const id = setTimeout(() => setShown(s => s + 1), 600)
    return () => clearTimeout(id)
  }, [shown, events.length])

  async function rerun() {
    const prompt = events[0]?.data.prompt as string
    setBusy(true)
    try {
      const r = await api.startRun(rerunOn || (events[0]?.data.profile as string), prompt)
      await refresh()
      setSelected(r.runId)
    } finally { setBusy(false) }
  }

  return (
    <div className="runs">
      <aside>
        <button onClick={refresh}>Refresh</button>
        <ul>
          {runs.map(r => (
            <li key={r.id} className={r.id === selected ? 'active' : ''} onClick={() => setSelected(r.id)}>
              <b>{r.profile}</b> <span className="muted">{r.id.slice(9, 15)} · {r.tools} tools{r.end ? ` · ${(r.end.ms / 1000).toFixed(1)}s` : ''}</span>
              <div className="prompt">{r.prompt}</div>
            </li>
          ))}
        </ul>
      </aside>
      <section>
        {selected && (
          <div className="row">
            <button onClick={() => setShown(1)}>▶ Replay</button>
            <select aria-label="Re-run on profile" value={rerunOn} onChange={e => setRerunOn(e.target.value)}>
              <option value="">same profile</option>
              {profiles.map(p => <option key={p.name}>{p.name}</option>)}
            </select>
            <button onClick={rerun} disabled={busy}>{busy ? 'Running…' : 'Re-run'}</button>
          </div>
        )}
        <RunView events={events.slice(0, shown)} />
      </section>
    </div>
  )
}

export function RunView({ events }: { events: RunEvent[] }) {
  const start = events[0]?.data
  const end = events.find(e => e.kind === 'end')?.data as RunSummary['end']
  return (
    <div className="run">
      {start && <h3>{String(start.profile)} <span className="muted">({String(start.engine)})</span></h3>}
      {end && (
        <p className="stats">
          {(end.ms / 1000).toFixed(1)}s · {end.inputTokens ?? '?'} in / {end.outputTokens ?? '?'} out tokens
          {end.cost != null && ` · $${end.cost.toFixed(4)}`}
        </p>
      )}
      <ol>
        {events.map((e, i) => (
          <li key={i} className={`event ${e.kind} ${String(e.data.decision ?? '')}`}>
            <span className="kind">{e.kind}{e.data.decision ? `: ${e.data.decision}` : ''}</span>
            <Body e={e} />
          </li>
        ))}
      </ol>
    </div>
  )
}

function Body({ e }: { e: RunEvent }) {
  const d = e.data
  switch (e.kind) {
    case 'start': return <div>{String(d.prompt)}</div>
    case 'tool': return <div><code>{String(d.name)}</code> <span className="muted">{String(d.reason)}</span>{d.args ? <pre>{String(d.args)}</pre> : null}</div>
    case 'tool_result': return <details><summary><code>{String(d.name)}</code> result</summary><pre>{String(d.result ?? '')}</pre></details>
    case 'message': return <div className="text">{String(d.text)}</div>
    case 'error': return <div className="error">{String(d.message)}</div>
    default: return null
  }
}

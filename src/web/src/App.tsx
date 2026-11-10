import { useEffect, useState } from 'react'
import { api, type Pending, type Profile } from './api'
import { Chat } from './Chat'
import { Compare } from './Compare'
import { Runs } from './Runs'
import { Config } from './Config'

const tabs = ['Chat', 'Compare', 'Runs', 'Config'] as const
type Tab = (typeof tabs)[number]

export default function App() {
  const [tab, setTab] = useState<Tab>('Chat')
  const [profiles, setProfiles] = useState<Profile[]>([])
  const [profile, setProfile] = useState('')
  const [configError, setConfigError] = useState<string>()

  const loadProfiles = () =>
    api.profiles().then(p => {
      setProfiles(p.profiles)
      setConfigError(p.configError)
      setProfile(current => p.profiles.some(x => x.name === current) ? current : p.defaultProfile)
    })
  useEffect(() => { loadProfiles() }, [])

  return (
    <div className="app">
      <header>
        <h1>Agent Harness</h1>
        <nav>{tabs.map(t => <button key={t} className={t === tab ? 'active' : ''} onClick={() => setTab(t)}>{t}</button>)}</nav>
        <select aria-label="Profile" value={profile} onChange={e => setProfile(e.target.value)}>
          {profiles.map(p => <option key={p.name} value={p.name}>{p.name} ({p.engine}{p.model ? ` · ${p.model}` : ''})</option>)}
        </select>
      </header>
      {configError && <p className="error">harness.yaml has errors, running the last valid version:<br />{configError}</p>}
      <Approvals />
      <main>
        {tab === 'Chat' && profile && <Chat key={profile} profile={profile} />}
        {tab === 'Compare' && <Compare profiles={profiles} />}
        {tab === 'Runs' && <Runs profiles={profiles} />}
        {tab === 'Config' && <Config onSaved={loadProfiles} />}
      </main>
    </div>
  )
}

/** Tool calls the policy marked "ask" wait here until a human decides. */
function Approvals() {
  const [pending, setPending] = useState<Pending[]>([])
  useEffect(() => {
    const id = setInterval(() => api.approvals().then(setPending).catch(() => {}), 1000)
    return () => clearInterval(id)
  }, [])
  if (pending.length === 0) return null
  return (
    <section className="approvals" aria-live="polite">
      {pending.map(p => (
        <div key={p.id} className="approval">
          <strong>{p.profile}</strong> wants to run <code>{p.tool}</code>
          {p.args && <pre>{p.args}</pre>}
          <button onClick={() => api.answer(p.id, true)}>Approve</button>
          <button className="danger" onClick={() => api.answer(p.id, false)}>Deny</button>
        </div>
      ))}
    </section>
  )
}
